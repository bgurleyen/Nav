using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

public class FirestoreController : MonoBehaviour
{
    // Aggregates (average / best) stay here.
    const string AggregatesCollection = "game_stats";

    // Per-player level stats: one doc per player, fields LEVEL 1 … LEVEL 39.
    const string LevelsCollection = "level";

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

            SavePlayerLevelBest(levelKey, stat, callback);
            UpdateAverageDoc(levelKey, stat);
            UpdateBestDoc(levelKey, stat);
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:SaveLevelStat::" + e);
            callback?.Invoke(false);
        }
    }

    // level/{playerId}: store only remainingFuel kg (int) per LEVEL field; keep the higher value.
    void SavePlayerLevelBest(string levelKey, LevelStat played, Action<bool> callback)
    {
        var docRef = database.Collection(LevelsCollection).Document(_composedUserId);

        _ = docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (!task.IsCompleted || task.IsFaulted || task.IsCanceled)
            {
                Debug.LogWarning($"[Firestore] level read failed: {task.Exception?.GetBaseException().Message}");
                callback?.Invoke(false);
                return;
            }

            DocumentSnapshot snap = task.Result;
            var payload = new Dictionary<string, object>();

            // Rewrite every LEVEL field as a plain fuel kg int (strips legacy LevelStat maps / tons).
            if (snap.Exists)
            {
                foreach (var entry in snap.ToDictionary())
                {
                    if (entry.Key == null || !entry.Key.StartsWith("LEVEL ", StringComparison.Ordinal))
                        continue;

                    int fuel = ReadRemainingFuel(snap, entry.Key);
                    if (fuel > 0)
                        payload[entry.Key] = fuel;
                }
            }

            int existingFuel = payload.TryGetValue(levelKey, out object stored)
                ? Convert.ToInt32(stored)
                : 0;
            int bestFuel = Math.Max(existingFuel, played.remainingFuel);
            payload[levelKey] = bestFuel;

            _ = docRef.SetAsync(payload, SetOptions.MergeAll).ContinueWithOnMainThread(writeTask =>
            {
                bool ok = writeTask.IsCompleted && !writeTask.IsFaulted && !writeTask.IsCanceled;
                if (!ok)
                    Debug.LogWarning($"[Firestore] level/{_composedUserId} write failed: {writeTask.Exception?.GetBaseException().Message}");
                else
                    Debug.Log($"[Firestore] level/{_composedUserId} {levelKey} saved (fuel={bestFuel} kg)");
                callback?.Invoke(ok);
            });
        });
    }

    // average user: merge stored value with the played value (sum / 2), or write it directly when empty.
    void UpdateAverageDoc(string levelKey, LevelStat played)
    {
        var docRef = database.Collection(AggregatesCollection).Document(AverageDocId);

        _ = docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            LevelStat existing = (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                ? ReadLevelStat(task.Result, levelKey)
                : null;

            LevelStat merged = existing == null
                ? played
                : new LevelStat
                {
                    averageAltitude = AvgInt(existing.averageAltitude, played.averageAltitude),
                    lgAltitude = AvgInt(existing.lgAltitude, played.lgAltitude),
                    averageFlapAltitude = AvgInt(existing.averageFlapAltitude, played.averageFlapAltitude),
                    speedBrakeSeconds = AvgInt(existing.speedBrakeSeconds, played.speedBrakeSeconds),
                    remainingFuel = AvgInt(existing.remainingFuel, played.remainingFuel),
                };

            _ = docRef.SetAsync(new Dictionary<string, object> { [levelKey] = merged }, SetOptions.MergeAll);
        });
    }

    // best user: overwrite when the played remaining fuel beats the stored value, or write it directly when empty.
    void UpdateBestDoc(string levelKey, LevelStat played)
    {
        var docRef = database.Collection(AggregatesCollection).Document(BestDocId);

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

    public void FetchLevelAggregate(string levelKey, int myFuel, Action<LevelAggregate> callback)
    {
        var aggregate = new LevelAggregate();

        if (database == null || string.IsNullOrEmpty(levelKey))
        {
            callback?.Invoke(aggregate);
            return;
        }

        try
        {
            // average + best from game_stats, ranks from level players.
            _ = database.Collection(AggregatesCollection).GetSnapshotAsync().ContinueWithOnMainThread(aggTask =>
            {
                if (aggTask.IsCompleted && !aggTask.IsFaulted && !aggTask.IsCanceled)
                {
                    foreach (DocumentSnapshot doc in aggTask.Result.Documents)
                    {
                        if (doc.Id == AverageDocId)
                            aggregate.average = ReadLevelStat(doc, levelKey);
                        else if (doc.Id == BestDocId)
                            aggregate.best = ReadLevelStat(doc, levelKey);
                    }
                }

                _ = database.Collection(LevelsCollection).GetSnapshotAsync().ContinueWithOnMainThread(levelTask =>
                {
                    if (!levelTask.IsCompleted || levelTask.IsFaulted || levelTask.IsCanceled)
                    {
                        callback?.Invoke(aggregate);
                        return;
                    }

                    int count = 0;
                    int better = 0;

                    foreach (DocumentSnapshot doc in levelTask.Result.Documents)
                    {
                        int fuel = ReadRemainingFuel(doc, levelKey);
                        if (fuel <= 0)
                            continue;

                        count++;
                        if (fuel > myFuel)
                            better++;
                    }

                    aggregate.totalPlayers = count;
                    aggregate.rank = myFuel > 0 ? better + 1 : 0;

                    callback?.Invoke(aggregate);
                });
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:FetchLevelAggregate::" + e);
            callback?.Invoke(aggregate);
        }
    }

    public void FetchAllLevelRanks(int currentLevelIndex, int currentLevelFuel, Action<int[]> callback)
    {
        int levelCount = LevelsLayoutSpec.LevelCount;
        callback?.Invoke(new int[levelCount]);

        if (database == null)
            return;

        try
        {
            _ = database.Collection(LevelsCollection).GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompleted || task.IsFaulted || task.IsCanceled)
                    return;

                var fuelsByLevel = new List<int>[levelCount];
                for (int i = 0; i < levelCount; i++)
                    fuelsByLevel[i] = new List<int>();

                int[] myFuels = new int[levelCount];
                string myUid = ResolveUserId();

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    bool isMe = doc.Id == myUid;
                    for (int level = PlayerPrefsHolder.FirstRankedLevel; level <= levelCount; level++)
                    {
                        int fuel = ReadRemainingFuel(doc, $"LEVEL {level}");
                        if (fuel <= 0)
                            continue;

                        int idx = level - 1;
                        fuelsByLevel[idx].Add(fuel);
                        if (isMe)
                            myFuels[idx] = Math.Max(myFuels[idx], fuel);
                    }
                }

                if (currentLevelIndex >= 0 && currentLevelIndex < levelCount && currentLevelFuel > 0)
                    myFuels[currentLevelIndex] = Math.Max(myFuels[currentLevelIndex], currentLevelFuel);

                int[] ranks = new int[levelCount];
                for (int level = 0; level < levelCount; level++)
                {
                    int mine = myFuels[level];
                    if (mine <= 0)
                    {
                        ranks[level] = 0;
                        continue;
                    }

                    EnsureFuelListed(fuelsByLevel[level], mine);

                    int better = 0;
                    for (int i = 0; i < fuelsByLevel[level].Count; i++)
                    {
                        if (fuelsByLevel[level][i] > mine)
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

    static void EnsureFuelListed(List<int> fuels, int fuel)
    {
        for (int i = 0; i < fuels.Count; i++)
        {
            if (fuels[i] == fuel)
                return;
        }

        fuels.Add(fuel);
    }

    static int AvgInt(int a, int b) => (int)Math.Round((a + b) / 2.0);

    static LevelStat ReadLevelStat(DocumentSnapshot doc, string levelKey)
    {
        if (doc == null || !doc.Exists || !doc.ContainsField(levelKey))
            return null;

        try
        {
            object raw = doc.GetValue<object>(levelKey);
            if (raw is Dictionary<string, object> map)
                return ParseLevelStatMap(map);

            LevelStat stat = doc.GetValue<LevelStat>(levelKey);
            if (stat != null)
                stat.remainingFuel = FuelToKg(stat.remainingFuel);
            return stat;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Levels] {doc.Id}/{levelKey} read failed: {e.Message}");
            return null;
        }
    }

    static LevelStat ParseLevelStatMap(Dictionary<string, object> map)
    {
        return new LevelStat
        {
            averageAltitude = ToInt(map, "averageAltitude"),
            lgAltitude = ToInt(map, "lgAltitude"),
            averageFlapAltitude = ToInt(map, "averageFlapAltitude"),
            speedBrakeSeconds = ToInt(map, "speedBrakeSeconds"),
            remainingFuel = FuelToKg(ToDouble(map, "remainingFuel")),
        };
    }

    // level collection stores a plain number (kg); still accepts legacy LevelStat maps and tons.
    static int ReadRemainingFuel(DocumentSnapshot doc, string levelKey)
    {
        if (doc == null || !doc.Exists || !doc.ContainsField(levelKey))
            return 0;

        try
        {
            object raw = doc.GetValue<object>(levelKey);
            switch (raw)
            {
                case double d:
                    return FuelToKg(d);
                case float f:
                    return FuelToKg(f);
                case long l:
                    return FuelToKg(l);
                case int i:
                    return FuelToKg(i);
                case Dictionary<string, object> map when map.TryGetValue("remainingFuel", out object nested):
                    return FuelToKg(Convert.ToDouble(nested));
            }

            LevelStat legacy = doc.GetValue<LevelStat>(levelKey);
            return legacy != null ? FuelToKg(legacy.remainingFuel) : 0;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Levels] {doc.Id}/{levelKey} fuel read failed: {e.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Legacy remainingFuel was tons (typically &lt; 100). New values are kilograms.
    /// </summary>
    static int FuelToKg(double raw)
    {
        if (raw <= 0)
            return 0;

        if (raw < 100.0)
            return (int)Math.Round(raw * 1000.0);

        return (int)Math.Round(raw);
    }

    static int ToInt(Dictionary<string, object> map, string key)
    {
        if (map == null || !map.TryGetValue(key, out object value) || value == null)
            return 0;

        return (int)Math.Round(Convert.ToDouble(value));
    }

    static double ToDouble(Dictionary<string, object> map, string key)
    {
        if (map == null || !map.TryGetValue(key, out object value) || value == null)
            return 0;

        return Convert.ToDouble(value);
    }

    public void DebugListAllRecords()
    {
        if (database == null)
        {
            Debug.LogWarning("[FirestoreDump] Database not initialized. Enter Play mode and wait for Firebase init.");
            return;
        }

        Debug.Log($"[FirestoreDump] === START === project: {ResolveFirebaseProjectId()}");
        DumpCollection(AggregatesCollection, fuelOnly: false);
        DumpCollection(LevelsCollection, fuelOnly: true);
    }

    void DumpCollection(string collectionName, bool fuelOnly)
    {
        try
        {
            _ = database.Collection(collectionName).GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompleted || task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[FirestoreDump] {collectionName}: query failed - {task.Exception?.GetBaseException().Message}");
                    return;
                }

                QuerySnapshot snapshot = task.Result;
                Debug.Log($"[FirestoreDump] --- {collectionName} ({snapshot.Count} docs) ---");

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (!doc.Exists)
                        continue;

                    string body = fuelOnly ? FormatLevelFuelDocument(doc) : FormatGameStatsDocument(doc);
                    Debug.Log($"[FirestoreDump] {collectionName}/{doc.Id}\n{body}");
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FirestoreDump] {collectionName}: {e.Message}");
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
                    $"{entry.Key}: fuel={s.remainingFuel} kg avgAlt={s.averageAltitude} " +
                    $"lgAlt={s.lgAltitude} flapAlt={s.averageFlapAltitude} sb={s.speedBrakeSeconds}s");
            }

            return lines.Length > 0 ? lines.ToString().TrimEnd() : "(no level stats)";
        }
        catch (Exception e)
        {
            return $"(parse error: {e.Message})";
        }
    }

    static string FormatLevelFuelDocument(DocumentSnapshot doc)
    {
        try
        {
            Dictionary<string, object> data = doc.ToDictionary();
            if (data == null || data.Count == 0)
                return "(empty)";

            var lines = new System.Text.StringBuilder();
            foreach (var entry in data)
            {
                int fuel = ReadRemainingFuel(doc, entry.Key);
                if (fuel <= 0)
                    continue;

                lines.AppendLine($"{entry.Key}: {fuel} kg");
            }

            return lines.Length > 0 ? lines.ToString().TrimEnd() : "(no fuels)";
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
    [FirestoreProperty] public int averageAltitude { get; set; }
    [FirestoreProperty] public int lgAltitude { get; set; }
    [FirestoreProperty] public int averageFlapAltitude { get; set; }
    [FirestoreProperty] public int speedBrakeSeconds { get; set; }
    /// <summary>Remaining fuel in kilograms.</summary>
    [FirestoreProperty] public int remainingFuel { get; set; }
}

public class LevelAggregate
{
    public LevelStat best;
    public LevelStat average;
    public int rank;
    public int totalPlayers;
}
