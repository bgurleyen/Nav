using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

public class FirestoreController : MonoBehaviour
{
    const string Collection = "game_stats";

    // Special aggregate "users" stored alongside player documents.
    const string AverageDocId = "average";
    const string BestDocId = "best";

    public string userId;
    private string _composedUserId;

    private FirebaseFirestore database;
    private Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;

    public void Awake()
    {
        InitializeFirestore();
    }

    public void InitializeFirestore()
    {
        _ = Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                try
                {
                    Debug.Log("Firestore Init");
                    database = FirebaseFirestore.DefaultInstance;
                    userId = SystemInfo.deviceUniqueIdentifier;
                }
                catch (Exception e)
                {
                    Debug.Log("FirestoreController:InitializeFirestore::" + e);
                }
            }
            else
            {
                Debug.Log("Could not resolve all Firebase dependencies: " + dependencyStatus);
            }
        });
    }

    // Called by FirebaseAuthManager once an authenticated user id is available.
    public void GetUser(string uid)
    {
        if (!string.IsNullOrEmpty(uid))
            userId = uid;
    }

    // Once composed at save time (FMC scene live), reuse the same id for later rank reads.
    string ResolveUserId()
    {
        return string.IsNullOrEmpty(_composedUserId) ? ComposeUserId() : _composedUserId;
    }

    // Document id = "{scratchpad} {playerName} {uid}", skipping empty parts.
    // When scratchpad is empty and no name is saved, the id is just the uid.
    string ComposeUserId()
    {
        string uid = string.IsNullOrEmpty(userId) ? SystemInfo.deviceUniqueIdentifier : userId;

        string scratchpad = SanitizeForDocumentId(ReadScratchpadText());
        string name = SanitizeForDocumentId(PlayerPrefsHolder.UserName);

        var parts = new List<string>(3);
        if (!string.IsNullOrEmpty(scratchpad))
            parts.Add(scratchpad);
        if (!string.IsNullOrEmpty(name))
            parts.Add(name);
        parts.Add(uid);

        return string.Join(" ", parts);
    }

    static string ReadScratchpadText()
    {
        MainScreen main = FindObjectOfType<MainScreen>();
        if (main == null || main.scratchPadText == null)
            return null;

        return main.scratchPadText.GetCurrentText();
    }

    // Firestore document ids cannot contain '/', so strip path separators and trim.
    static string SanitizeForDocumentId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Replace('/', '-').Replace('\\', '-').Trim();
    }

    public void SaveLevelStat(LevelStat stat, Action<bool> callback)
    {
        if (database == null || stat == null)
        {
            callback?.Invoke(false);
            return;
        }

        string levelKey = PlayerPrefsHolder.FirestoreLevelKey;
        if (string.IsNullOrEmpty(levelKey))
        {
            callback?.Invoke(false);
            return;
        }

        try
        {
            // Compose the id now (FMC scene is live) and cache it for later rank reads.
            _composedUserId = ComposeUserId();

            var docRef = database.Collection(Collection).Document(_composedUserId);
            var payload = new Dictionary<string, object> { [levelKey] = stat };

            _ = docRef.SetAsync(payload, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
            {
                bool ok = task.IsCompleted && !task.IsFaulted && !task.IsCanceled;
                callback?.Invoke(ok);
            });

            UpdateAverageDoc(levelKey, stat);
            UpdateBestDoc(levelKey, stat);
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:SaveLevelStat::" + e);
            callback?.Invoke(false);
        }
    }

    // average user: merge stored value with the played value (sum / 2), or write it directly when empty.
    void UpdateAverageDoc(string levelKey, LevelStat played)
    {
        var docRef = database.Collection(Collection).Document(AverageDocId);

        _ = docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            LevelStat existing = (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                ? ReadLevelStat(task.Result, levelKey)
                : null;

            LevelStat merged = existing == null
                ? played
                : new LevelStat
                {
                    averageAltitude = (existing.averageAltitude + played.averageAltitude) / 2.0,
                    lgAltitude = (existing.lgAltitude + played.lgAltitude) / 2.0,
                    averageFlapAltitude = (existing.averageFlapAltitude + played.averageFlapAltitude) / 2.0,
                    speedBrakeSeconds = (int)Math.Round((existing.speedBrakeSeconds + played.speedBrakeSeconds) / 2.0),
                    remainingFuel = (existing.remainingFuel + played.remainingFuel) / 2.0,
                };

            _ = docRef.SetAsync(new Dictionary<string, object> { [levelKey] = merged }, SetOptions.MergeAll);
        });
    }

    // best user: overwrite when the played remaining fuel beats the stored value, or write it directly when empty.
    void UpdateBestDoc(string levelKey, LevelStat played)
    {
        var docRef = database.Collection(Collection).Document(BestDocId);

        _ = docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            LevelStat existing = (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                ? ReadLevelStat(task.Result, levelKey)
                : null;

            if (existing != null && existing.remainingFuel >= played.remainingFuel)
                return;

            _ = docRef.SetAsync(new Dictionary<string, object> { [levelKey] = played }, SetOptions.MergeAll);
        });
    }

    public void FetchLevelAggregate(string levelKey, double myFuel, Action<LevelAggregate> callback)
    {
        var aggregate = new LevelAggregate();

        if (database == null || string.IsNullOrEmpty(levelKey))
        {
            callback?.Invoke(aggregate);
            return;
        }

        try
        {
            _ = database.Collection(Collection).GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompleted || task.IsFaulted || task.IsCanceled)
                {
                    callback?.Invoke(aggregate);
                    return;
                }

                int count = 0;
                int better = 0;

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    if (doc.Id == AverageDocId)
                    {
                        aggregate.average = ReadLevelStat(doc, levelKey);
                        continue;
                    }

                    if (doc.Id == BestDocId)
                    {
                        aggregate.best = ReadLevelStat(doc, levelKey);
                        continue;
                    }

                    LevelStat s = ReadLevelStat(doc, levelKey);
                    if (s == null)
                        continue;

                    count++;
                    if (s.remainingFuel > myFuel + 0.001)
                        better++;
                }

                aggregate.totalPlayers = count;
                aggregate.rank = myFuel > 0 ? better + 1 : 0;

                callback?.Invoke(aggregate);
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:FetchLevelAggregate::" + e);
            callback?.Invoke(aggregate);
        }
    }

    public void FetchAllLevelRanks(int currentLevelIndex, double currentLevelFuel, Action<int[]> callback)
    {
        int levelCount = LevelsLayoutSpec.LevelCount;
        callback?.Invoke(new int[levelCount]);

        if (database == null)
            return;

        try
        {
            _ = database.Collection(Collection).GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompleted || task.IsFaulted || task.IsCanceled)
                    return;

                var fuelsByLevel = new List<double>[levelCount];
                for (int i = 0; i < levelCount; i++)
                    fuelsByLevel[i] = new List<double>();

                double[] myFuels = new double[levelCount];
                string myUid = ResolveUserId();

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    if (doc.Id == AverageDocId || doc.Id == BestDocId)
                        continue;

                    bool isMe = doc.Id == myUid;
                    for (int level = PlayerPrefsHolder.FirstRankedLevel; level <= levelCount; level++)
                    {
                        LevelStat s = ReadLevelStat(doc, $"LEVEL {level}");
                        if (s == null || s.remainingFuel <= 0)
                            continue;

                        int idx = level - 1;
                        fuelsByLevel[idx].Add(s.remainingFuel);
                        if (isMe)
                            myFuels[idx] = Math.Max(myFuels[idx], s.remainingFuel);
                    }
                }

                if (currentLevelIndex >= 0 && currentLevelIndex < levelCount && currentLevelFuel > 0)
                    myFuels[currentLevelIndex] = Math.Max(myFuels[currentLevelIndex], currentLevelFuel);

                int[] ranks = new int[levelCount];
                for (int level = 0; level < levelCount; level++)
                {
                    double mine = myFuels[level];
                    if (mine <= 0)
                    {
                        ranks[level] = 0;
                        continue;
                    }

                    EnsureFuelListed(fuelsByLevel[level], mine);

                    int better = 0;
                    for (int i = 0; i < fuelsByLevel[level].Count; i++)
                    {
                        if (fuelsByLevel[level][i] > mine + 0.001)
                            better++;
                    }

                    ranks[level] = better + 1;
                }

                callback?.Invoke(ranks);
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Levels] FetchAllLevelRanks failed: {e.Message}");
        }
    }

    static void EnsureFuelListed(List<double> fuels, double fuel)
    {
        for (int i = 0; i < fuels.Count; i++)
        {
            if (Math.Abs(fuels[i] - fuel) < 0.001)
                return;
        }

        fuels.Add(fuel);
    }

    static LevelStat ReadLevelStat(DocumentSnapshot doc, string levelKey)
    {
        if (doc == null || !doc.Exists || !doc.ContainsField(levelKey))
            return null;

        try
        {
            return doc.GetValue<LevelStat>(levelKey);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Levels] {doc.Id}/{levelKey} read failed: {e.Message}");
            return null;
        }
    }

    public void DebugListAllRecords()
    {
        if (database == null)
        {
            Debug.LogWarning("[FirestoreDump] Database not initialized. Enter Play mode and wait for Firebase init.");
            return;
        }

        Debug.Log($"[FirestoreDump] === START === project: {ResolveFirebaseProjectId()}");

        try
        {
            _ = database.Collection(Collection).GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompleted || task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[FirestoreDump] {Collection}: query failed - {task.Exception?.GetBaseException().Message}");
                    return;
                }

                QuerySnapshot snapshot = task.Result;
                Debug.Log($"[FirestoreDump] --- {Collection} ({snapshot.Count} docs) ---");

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (!doc.Exists)
                        continue;

                    Debug.Log($"[FirestoreDump] {Collection}/{doc.Id}\n{FormatGameStatsDocument(doc)}");
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FirestoreDump] {Collection}: {e.Message}");
        }
    }

    static string FormatGameStatsDocument(DocumentSnapshot doc)
    {
        try
        {
            Dictionary<string, object> data = doc.ToDictionary();
            if (data == null || data.Count == 0)
                return "(empty)";

            var lines = new System.Text.StringBuilder();
            foreach (var entry in data)
            {
                LevelStat s = ReadLevelStat(doc, entry.Key);
                if (s == null)
                    continue;

                lines.AppendLine(
                    $"{entry.Key}: fuel={s.remainingFuel:0.##} avgAlt={s.averageAltitude:0} " +
                    $"lgAlt={s.lgAltitude:0} flapAlt={s.averageFlapAltitude:0} sb={s.speedBrakeSeconds}s");
            }

            return lines.Length > 0 ? lines.ToString().TrimEnd() : "(no level stats)";
        }
        catch (Exception e)
        {
            return $"(parse error: {e.Message})";
        }
    }

    static string ResolveFirebaseProjectId()
    {
        try
        {
            string desktopPath = Path.Combine(Application.streamingAssetsPath, "google-services-desktop.json");
            string androidPath = Path.Combine(Application.streamingAssetsPath, "google-services.json");
            string path = Application.isEditor && File.Exists(desktopPath)
                ? desktopPath
                : androidPath;

            if (!File.Exists(path))
                return "(unknown)";

            string json = File.ReadAllText(path);
            const string marker = "\"project_id\"";
            int index = json.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
                return "(unknown)";

            int colon = json.IndexOf(':', index);
            int q1 = json.IndexOf('"', colon + 1);
            int q2 = json.IndexOf('"', q1 + 1);
            if (q1 < 0 || q2 < 0)
                return "(unknown)";

            return json.Substring(q1 + 1, q2 - q1 - 1);
        }
        catch
        {
            return "(unknown)";
        }
    }
}

[FirestoreData]
public class LevelStat
{
    [FirestoreProperty] public double averageAltitude { get; set; }
    [FirestoreProperty] public double lgAltitude { get; set; }
    [FirestoreProperty] public double averageFlapAltitude { get; set; }
    [FirestoreProperty] public int speedBrakeSeconds { get; set; }
    [FirestoreProperty] public double remainingFuel { get; set; }
}

public class LevelAggregate
{
    public LevelStat best;
    public LevelStat average;
    public int rank;
    public int totalPlayers;
}
