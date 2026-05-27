using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using System.Xml.Linq;

public class FirestoreController : MonoBehaviour
{
    #region Vars
    public UserData myUserData = new UserData();
    public Dictionary<string, object> blockList = new Dictionary<string, object>();

    private FirebaseFirestore database;

    private Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;
    #endregion

    #region UNITY_CALLBACKS
    public void Awake()
    {
        //base.Awake();

        InitializeFirestore();
    }
    #endregion

    #region INIT
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
                    myUserData = new UserData();

                    //GetUser(SystemInfo.deviceUniqueIdentifier);
                    GetUser(SystemInfo.deviceUniqueIdentifier);
                }
                catch (Exception e)
                {
                    Debug.Log("FirestoreController:InitializeFirestore::" + e.ToString());
                }
            }
            else
            {
                Debug.Log("Could not resolve all Firebase dependencies: " + dependencyStatus);
            }
        });
    }
    #endregion

    #region CREATE NEW USER
    public void CreateUser()
    {
        try
        {
            //var user = firebaseAuthManager.user;

            var userData = new Dictionary<string, object>
            {
                //["created_date"] = FieldValue.ServerTimestamp,
                ["name"] = PlayerPrefsHolder.UserName,
                ["provider"] = "NavigationShare",
                //["uid"] = SystemInfo.deviceUniqueIdentifier,
                ["uid"] = SystemInfo.deviceUniqueIdentifier,
            };

            var docRef = database.Collection("users").Document(SystemInfo.deviceUniqueIdentifier);

            _ = docRef.SetAsync(userData).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    foreach (var error in task.Exception.Flatten().InnerExceptions)
                    {
                        Debug.LogError($"Firestore error: {error.Message}");
                    }
                }
                else if (task.IsCompleted)
                {
                    GetUser(SystemInfo.deviceUniqueIdentifier);
                    Debug.Log("User added successfully.");
                }
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:CreateUser::" + e.ToString());
        }

    }
    #endregion

    #region GET USER DATA
    public void GetUser(string userId)
    {
        try
        {
            //userId = "MFzEwt5apoUhgSLZPhGPo1nUObC3";
            var docRef = database.Collection("users").Document(userId);

            var docRef_game_stats = database.Collection("game_stats").Document(userId);

            _ = docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                var snapshot = task.Result;
                //Debug.Log(snapshot.Exists);

                if (snapshot.Exists)
                {
                    myUserData = snapshot.ConvertTo<UserData>();

                    Action loadGameStats = () =>
                    {
                        _ = docRef_game_stats.GetSnapshotAsync().ContinueWithOnMainThread(gameStatsTask =>
                        {
                            var gameStatsSnapshot = gameStatsTask.Result;

                            if (gameStatsSnapshot.Exists)
                                myUserData.game_stats = gameStatsSnapshot.ConvertTo<Game_Stats>();
                        });
                    };

                    loadGameStats();
                }
                else
                {
                    CreateUser();
                }
            });

        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:GetUser::" + e.ToString());
        }
    }
    #endregion

    #region GET COURSE DATA

    public List<CourseData> walknRuns = new List<CourseData>();
    public List<CourseData> watchTvandWalk = new List<CourseData>();
    public List<CourseData> hiitWorkouts = new List<CourseData>();
    //public List<CourseData> obstaclesCourses = new List<CourseData>();
    public Dictionary<string, L_data> bestPlayerLevels = new Dictionary<string, L_data>();

    public L_data GetBestPlayerLevelData(int level)
    {
        if (level <= PlayerPrefsHolder.TestLevel || bestPlayerLevels == null)
            return null;

        return bestPlayerLevels.TryGetValue($"LEVEL {PlayerPrefsHolder.ClampLevel(level)}", out L_data data)
            ? data
            : null;
    }
    public List<S_data> averagePlayer = new List<S_data>();
    public List<double> bestPlayerProgress = new List<double>();
    public List<double> averagePlayerProgress = new List<double>();
    private L_data bestLevelProfile;

    public L_data GetBestLevelProfile()
    {
        DataManage.TryNormalizeProfile(bestLevelProfile);
        return bestLevelProfile;
    }

    public void GetCourseData()
    {
        try
        {
            var docRef = database.Collection("courses").Document("liveCourse");

            //_ = docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            //{
            //    var snapshot = task.Result;
            //    //Debug.Log(snapshot.Exists);

            //    if (snapshot.Exists)
            //    {
            //        cdata = snapshot.ConvertTo<C_data>();

            //        obstaclesCourses.Clear();

            //        walknRuns.Clear();

            //        hiitWorkouts.Clear();

            //        watchTvandWalk.Clear();

            //        //============================================================
            //        foreach (var course in cdata.walknRuns.Values.Cast<Dictionary<string, object>>())
            //        {
            //            var c = new CourseData
            //            {
            //                index = Convert.ToInt32(course["index"]),
            //                displayname = (string)course["displayname"],
            //                difficulty = (string)course["difficulty"],
            //                scenename = (string)course["scenename"],
            //                description = (string)course["description"],
            //                imgUrl = (string)course["imgUrl"],
            //                FreeRunMode = (bool)course["FreeRunMode"],
            //                ProgressBarValue = Convert.ToSingle(course["ProgressBarValue"]),
            //                ShowStats = (bool)course["ShowStats"],
            //                isPrivate = (bool)course["isPrivate"],
            //                isGlobal = (bool)course["isGlobal"],
            //                isSolo = (bool)course["isSolo"],
            //                simulation = (string)course["simulation"],
            //                stoneMoveStyle = (string)course["stoneMoveStyle"],
            //                stoneMoveSpeed = Convert.ToSingle(course["stoneMoveSpeed"]),
            //                opt1Dur = Convert.ToInt32(course["opt1Dur"]),
            //                opt2Dur = Convert.ToInt32(course["opt2Dur"]),
            //                opt3Dur = Convert.ToInt32(course["opt3Dur"]),
            //                isTrial = (bool)course["isTrial"],
            //                isComingSoon = (bool)course["isComingSoon"],
            //                isForBackSideTracking = (bool)course["isForBackSideTracking"],
            //                isResetMicNSpeaker = (bool)course["isResetMicNSpeaker"],
            //            };

            //            walknRuns.Add(c);
            //        }

            //        //walknRuns.Sort((a, b) => a.index.CompareTo(b.index));
            //    }
            //});
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:GetCourseData::" + e.ToString());
        }
    }

    public void GetBestPlayerData(string uid, Action<string> callback)
    {
        bestPlayerLevels = new Dictionary<string, L_data>();
        try
        {
            var docRef = database.Collection("game_stats").Document(uid);

            _ = docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                var snapshot = task.Result;
                Game_Stats best_Player = new Game_Stats();

                if (snapshot.Exists)
                {
                    best_Player = snapshot.ConvertTo<Game_Stats>();
                }
                try
                {
                    if (best_Player?.stats == null)
                    {
                        callback?.Invoke("[GetBestPlayerData] - ServerData (Fail)");
                        return;
                    }

                    foreach (var levelEntry in best_Player.stats)
                    {
                        var levels_Data = levelEntry.Value as Dictionary<string, object>;
                        if (levels_Data == null)
                            continue;

                        try
                        {
                            L_data l_Data = new L_data();
                            l_Data.altitude = ((List<object>)levels_Data["altitude"]).Select(x => Convert.ToDouble(x)).ToList();
                            l_Data.speed = ((List<object>)levels_Data["speed"]).Select(x => Convert.ToDouble(x)).ToList();
                            l_Data.flap = ((List<object>)levels_Data["flap"]).Select(x => Convert.ToDouble(x)).ToList();
                            l_Data.landingGear = ((List<object>)levels_Data["landingGear"]).Select(x => Convert.ToDouble(x)).ToList();
                            l_Data.speedBrake = ((List<object>)levels_Data["speedBrake"]).Select(x => Convert.ToDouble(x)).ToList();
                            l_Data.verticalMode = ((List<object>)levels_Data["verticalMode"]).Select(x => Convert.ToDouble(x)).ToList();
                            l_Data.fuelFlow = ((List<object>)levels_Data["fuelFlow"]).Select(x => Convert.ToDouble(x)).ToList();
                            l_Data.remainingFuel = Convert.ToDouble(levels_Data["remainingFuel"]);
                            l_Data.time = levels_Data.ContainsKey("time")
                                ? ((List<object>)levels_Data["time"]).Select(x => Convert.ToString(x)).ToList()
                                : new List<string>();
                            if (levels_Data.ContainsKey("distance"))
                                l_Data.distance = ((List<object>)levels_Data["distance"]).Select(x => Convert.ToDouble(x)).ToList();

                            DataManage.TryNormalizeProfile(l_Data);

                            if (!DataManage.HasValidDistanceData(l_Data))
                                continue;

                            bestPlayerLevels[levelEntry.Key] = l_Data;
                        }
                        catch (Exception e)
                        {
                            Debug.Log(e.ToString());
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e.ToString());
                }

                if (task.IsCompleted)
                {
                    callback("[GetBestPlayerData] - ServerData (sucess)");
                }
                else
                {
                    callback("[GetBestPlayerData] - ServerData (Fail)");
                }
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:GetBestPlayerData::" + e.ToString());
            callback?.Invoke("[GetBestPlayerData] - ServerData (Fail)");
        }
    }

    public void GetBestPlayerProgressData(string uid, Action<string> callback1, Action<string> callback2)
    {
        // Note: use this after GetBestPlayerData() beacause if called beafore best_stats.uid null issue
        bestPlayerProgress = new List<double>();

        if (uid == myUserData.uid) { callback1?.Invoke("[GetBestPlayerProgressData] - LocalData (sucess)"); }
        else
        {
            try
            {
                var docRef = database.Collection("progress_stats").Document(uid);

                _ = docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
                {
                    var snapshot = task.Result;
                    Dictionary<string, object> best_Player = new Dictionary<string, object>();

                    if (snapshot.Exists)
                    {
                        best_Player = snapshot.ConvertTo<Dictionary<string, object>>();
                    }

                    if (best_Player != null)
                    {
                        try
                        {
                            for (int i = 0; i < best_Player.Count; i++)
                            {
                                if (best_Player.ContainsKey($"LEVEL {i}"))
                                {
                                    bestPlayerProgress.Add(Convert.ToDouble(best_Player[$"LEVEL {i}"]));
                                }
                                //try
                                //{

                                //}
                                //catch (Exception e)
                                //{
                                //    Debug.Log(e.ToString());
                                //}
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(e.ToString());
                        }
                    }

                    if (task.IsCompleted)
                    {
                        callback2?.Invoke("[GetBestPlayerProgressData] - ServerData (sucess)");
                    }
                    else
                    {
                        callback2?.Invoke("[GetBestPlayerProgressData] - ServerData (Fail)");
                    }
                });
            }
            catch (Exception e)
            {
                Debug.Log("FirestoreController:UpdateStats::" + e.ToString());
            }
        }

    }

    #endregion

    #region UPDATE USER DATA

    #region UPDATE STATS

    public void UpdateStats(object newStats, Action<string> callback)
    {
        if (!PlayerPrefsHolder.TryGetFirestoreLevelKey(out string levelKey))
        {
            callback?.Invoke("[UpdateStats] - Skipped (test level)");
            return;
        }

        try
        {
            var docRef_game_stats = database.Collection("game_stats").Document(myUserData.uid);

            _ = docRef_game_stats.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                var snapshot = task.Result;
                if (snapshot.Exists)
                {
                    myUserData.game_stats = snapshot.ConvertTo<Game_Stats>();
                }

                var cStats = new Dictionary<string, object>();

                if (myUserData.game_stats == null)
                {
                    myUserData.game_stats = new Game_Stats();
                    myUserData.game_stats.stats = new Dictionary<string, object>();
                }

                string levelKey = PlayerPrefsHolder.FirestoreLevelKey;
                if (!myUserData.game_stats.stats.ContainsKey(levelKey))
                {
                    cStats.Add(levelKey, newStats);
                }

                myUserData.game_stats.stats[levelKey] = newStats;

                AddStatsField(myUserData.game_stats.stats, (res) =>
                {
                    callback($"{res} | [UpdateStats] - ServerData (sucess)");
                });
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:UpdateStats::" + e.ToString());
        }
    }

    bool n_lg = false;
    int n_flap = 0;
    public void UpdateAverageStats(DDL_data newStats, Action<string> callback)
    {
        if (!PlayerPrefsHolder.TryGetFirestoreLevelKey(out _))
        {
            callback?.Invoke("[UpdateAverageStats] - Skipped (test level)");
            return;
        }

        Another local_another = new Another();
        averagePlayer = new List<S_data>();

        try
        {
            var docRef_game_stats = database.Collection("average_stats").Document(PlayerPrefsHolder.FirestoreLevelKey);

            _ = docRef_game_stats.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                var snapshot = task.Result;

                if (snapshot.Exists)
                {
                    myUserData.average_stats = snapshot.ConvertTo<Average_Stats>();
                    Debug.Log($"another.count :: {myUserData.average_stats.another.count}");
                    //Debug.Log($"another.count :: {myUserData.average_stats.another.flap}");
                    //for (int i = 0; i < myUserData.average_stats.another.flap.Count; i++)
                    //{
                    //    Debug.Log($"flap :: {myUserData.average_stats.another.flap[i]}");
                    //}

                    List<double> flapList = myUserData.average_stats.another.flap;
                    if (flapList != null)
                    {
                        for (int i = 0; i < flapList.Count; i++)
                        {
                            Debug.Log($"flap[{i}] :: {flapList[i]}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Flap list is null.");
                    }
                }

                if (myUserData.average_stats == null)
                {
                    myUserData.average_stats = new Average_Stats();
                    myUserData.average_stats.stats = new Dictionary<string, object>();

                    myUserData.average_stats.another = new Another();

                    myUserData.average_stats.another.count = 0;
                    myUserData.average_stats.another.landingGear = 0;
                    myUserData.average_stats.another.flap = new List<double>() { 0, 0, 0, 0, 0, 0, 0, 0 };
                }

                local_another = myUserData.average_stats.another;

                List<S_data> sStats = new List<S_data>() { };

                for (int i = 0; i < myUserData.average_stats.stats.Count; i++)
                {
                    Dictionary<string, object> m_stats = myUserData.average_stats.stats[$"{i}"] as Dictionary<string, object>;

                    int _count = Convert.ToInt32(m_stats["count"]);
                    S_data s_Data = new S_data()
                    {
                        count = _count,
                        altitude = Convert.ToDouble(m_stats["altitude"]) * _count,
                        speed = Convert.ToDouble(m_stats["speed"]) * _count,
                        flap = Convert.ToDouble(m_stats["flap"]) * _count,
                        speedBrake = Convert.ToDouble(m_stats["speedBrake"]) * _count,
                        landingGear = Convert.ToDouble(m_stats["landingGear"]) * _count,
                        distance = m_stats.ContainsKey("distance")
                            ? Convert.ToDouble(m_stats["distance"])
                            : Math.Round(i * 0.1, 1),
                    };
                    sStats.Add(s_Data);
                    //Debug.Log($"{i} --> {sStats[i].altitude} --> {sStats[i].speed} --> {sStats[i].flap} --> {sStats[i].speedBrake} --> {sStats[i].landingGear} --> {sStats[i].count}");
                }


                for (int i = 0; i < myUserData.average_stats.another.flap.Count; i++)
                {
                    myUserData.average_stats.another.flap[i] *= myUserData.average_stats.another.count;
                }
                myUserData.average_stats.another.landingGear *= myUserData.average_stats.another.count;
                myUserData.average_stats.another.count++;


                int loopCount = Math.Max(sStats.Count, newStats.altitude.Count);
                for (int i = 0; i < loopCount; i++)
                {
                    S_data s_Data = new S_data { altitude = 0, speed = 0, flap = 0, speedBrake = 0, landingGear = 0, remainingFuel = 0, count = 0 };

                    if (i < newStats.altitude.Count)
                    {
                        s_Data.count = 1;
                        s_Data.altitude = newStats.altitude[i];
                        s_Data.speed = newStats.speed[i];
                        s_Data.flap = newStats.flap[i];
                        s_Data.speedBrake = newStats.speedBrake[i];
                        s_Data.landingGear = newStats.landingGear[i];
                        s_Data.distance = i < newStats.distance.Count
                            ? newStats.distance[i]
                            : Math.Round(i * 0.1, 1);



                        try
                        {
                            if (newStats.landingGear[i] == 1 && n_lg == false)
                            {
                                myUserData.average_stats.another.landingGear = (myUserData.average_stats.another.landingGear + i) / myUserData.average_stats.another.count;
                                n_lg = true;
                            }
                            if (n_flap <= newStats.flap[i])
                            {
                                //myUserData.average_stats.another.flap[n_flap] *= (myUserData.average_stats.another.count - 1);

                                myUserData.average_stats.another.flap[n_flap] = (myUserData.average_stats.another.flap[n_flap] + i) / myUserData.average_stats.another.count;
                                n_flap++;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.Log(e);
                        }
                    }

                    if (myUserData.average_stats.stats.ContainsKey($"{i}"))
                    {
                        s_Data.count += sStats[i].count;
                        s_Data.altitude = (s_Data.altitude + sStats[i].altitude) / s_Data.count;
                        s_Data.speed = (s_Data.speed + sStats[i].speed) / s_Data.count;
                        s_Data.flap = (s_Data.flap + sStats[i].flap) / s_Data.count;
                        s_Data.speedBrake = (s_Data.speedBrake + sStats[i].speedBrake) / s_Data.count;
                        s_Data.landingGear = (s_Data.landingGear + sStats[i].landingGear) / s_Data.count;
                        if (s_Data.distance <= 0)
                            s_Data.distance = sStats[i].distance > 0 ? sStats[i].distance : Math.Round(i * 0.1, 1);

                        myUserData.average_stats.stats[$"{i}"] = s_Data;
                    }
                    else
                    {
                        myUserData.average_stats.stats.Add($"{i}", s_Data);
                    }
                    averagePlayer.Add(s_Data);
                }


                try
                {
                    //if (n_lg == false) myUserData.average_stats.another.landingGear /= myUserData.average_stats.another.count;
                    if (n_lg == false) myUserData.average_stats.another.landingGear = (myUserData.average_stats.another.landingGear + local_another.landingGear) / myUserData.average_stats.another.count;
                    //if (n_lg == false) myUserData.average_stats.another.landingGear = (myUserData.average_stats.another.landingGear * 2) / myUserData.average_stats.another.count;

                    for (int i = 0; i < myUserData.average_stats.another.flap.Count; i++)
                    {
                        //if (n_flap < i) myUserData.average_stats.another.flap[i] /= myUserData.average_stats.another.count;
                        if (n_flap < i) myUserData.average_stats.another.flap[i] = (myUserData.average_stats.another.flap[i] + local_another.flap[i]) / myUserData.average_stats.another.count;
                        //if (n_flap < i) myUserData.average_stats.another.flap[i] = (myUserData.average_stats.another.flap[i] * 2) / myUserData.average_stats.another.count;
                    }
                }
                catch (Exception e) 
                { 
                Debug.Log(e);
                }


                AddAverageStatsField(myUserData.average_stats, (res) =>
                {
                    callback($"{res} | [UpdateAverageStats] - ServerData (sucess)");
                });
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:UpdateStats::" + e.ToString());
        }
    }

    public void UpdateBestStats(DDL_data newStats, Action<string> callback)
    {
        if (!PlayerPrefsHolder.TryGetFirestoreLevelKey(out _))
        {
            FinishBestStatsCallback(newStats, "[UpdateBestStats] - Skipped (test level)", callback);
            return;
        }

        try
        {
            double remainingFuel = newStats.remainingFuel;
            int level = PlayerPrefsHolder.DisplayLevel;
            var docRef = database.Collection("best_stats").Document($"LEVEL {level}");

            _ = docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                var snapshot = task.Result;

                if (snapshot.Exists)
                    myUserData.best_stats = snapshot.ConvertTo<Best_Stats>();

                if (myUserData.best_stats == null)
                    myUserData.best_stats = new Best_Stats();

                bool hasValidProfile = DataManage.HasValidDistanceData(ToLData(myUserData.best_stats));
                bool isNewRecord = myUserData.best_stats.remainingFuel < remainingFuel;
                bool shouldBackfillProfile = !hasValidProfile && remainingFuel >= myUserData.best_stats.remainingFuel;

                if (isNewRecord || shouldBackfillProfile)
                {
                    if (isNewRecord)
                    {
                        myUserData.best_stats.uid = myUserData.uid;
                        myUserData.best_stats.remainingFuel = remainingFuel;
                    }
                    else if (string.IsNullOrEmpty(myUserData.best_stats.uid))
                    {
                        myUserData.best_stats.uid = myUserData.uid;
                    }

                    CopyProfile(newStats, myUserData.best_stats);
                    bestLevelProfile = NormalizeBestStats(myUserData.best_stats);

                    AddBestStatsField(myUserData.best_stats, (res) =>
                    {
                        FinishBestStatsCallback(newStats, $"{res} | [UpdateBestStats] - LocalData (sucess)", callback);
                    });
                }
                else
                {
                    ResolveBestLevelProfile(newStats, level, hasValidProfile, callback);
                }
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:UpdateStats::" + e.ToString());
        }
    }

    void ResolveBestLevelProfile(DDL_data currentFlight, int level, bool hasValidProfile, Action<string> callback)
    {
        if (hasValidProfile)
        {
            bestLevelProfile = NormalizeBestStats(myUserData.best_stats);
            FinishBestStatsCallback(currentFlight, "[UpdateBestStats] - ServerData (sucess)", callback);
            return;
        }

        string uid = myUserData.best_stats?.uid;
        if (string.IsNullOrEmpty(uid))
            uid = myUserData.uid;

        GetBestPlayerData(uid, _ =>
        {
            L_data fromServer = GetBestPlayerLevelData(level);
            if (DataManage.HasValidDistanceData(fromServer))
                bestLevelProfile = fromServer;
            else
                bestLevelProfile = NormalizeBestStats(myUserData.best_stats);

            FinishBestStatsCallback(currentFlight, "[UpdateBestStats] - ServerData (sucess)", callback);
        });
    }

    void FinishBestStatsCallback(DDL_data currentFlight, string message, Action<string> callback)
    {
        int level = PlayerPrefsHolder.ActiveLevel;

        bestLevelProfile = NormalizeBestStats(myUserData.best_stats) ?? bestLevelProfile;

        if (!DataManage.HasValidDistanceData(bestLevelProfile))
        {
            L_data fromServer = GetBestPlayerLevelData(level);
            if (DataManage.HasValidDistanceData(fromServer))
                bestLevelProfile = fromServer;
        }

        if (!DataManage.HasValidDistanceData(bestLevelProfile) && currentFlight != null)
        {
            double bestFuel = myUserData.best_stats?.remainingFuel ?? 0;
            if (bestFuel > 0 && currentFlight.remainingFuel >= bestFuel - 0.01)
                bestLevelProfile = DataManage.ToLData(currentFlight);
        }

        DataManage.TryNormalizeProfile(bestLevelProfile);
        callback?.Invoke(message);
    }

    static L_data NormalizeBestStats(Best_Stats best)
    {
        L_data profile = ToLData(best);
        return DataManage.NormalizeProfile(profile);
    }

    static void CopyProfile(DDL_data from, Best_Stats to)
    {
        to.altitude = from.altitude?.ToList();
        to.speed = from.speed?.ToList();
        to.flap = from.flap?.ToList();
        to.speedBrake = from.speedBrake?.ToList();
        to.landingGear = from.landingGear?.ToList();
        to.verticalMode = from.verticalMode?.ToList();
        to.fuelFlow = from.fuelFlow?.ToList();
        to.distance = from.distance?.ToList();
    }

    static L_data ToLData(Best_Stats best)
    {
        if (best?.altitude == null)
            return null;

        return new L_data
        {
            altitude = best.altitude,
            speed = best.speed,
            flap = best.flap,
            speedBrake = best.speedBrake,
            landingGear = best.landingGear,
            verticalMode = best.verticalMode,
            fuelFlow = best.fuelFlow,
            distance = best.distance,
            remainingFuel = best.remainingFuel,
        };
    }

    public void UpdateLevelProgressStats(double remainingFuel, Action<string> callback)
    {
        if (!PlayerPrefsHolder.TryGetFirestoreLevelKey(out string levelKey))
        {
            callback?.Invoke("[UpdateLevelProgressStats] - Skipped (test level)");
            return;
        }

        try
        {
            string uid = ResolveUserId();
            if (string.IsNullOrEmpty(uid))
            {
                callback?.Invoke("[UpdateLevelProgressStats] - LocalData (missing uid)");
                return;
            }

            if (myUserData.progress_stats == null)
                myUserData.progress_stats = new Dictionary<string, object>();

            for (int displayLevel = 1; displayLevel <= LevelsLayoutSpec.LevelCount; displayLevel++)
            {
                string key = $"LEVEL {displayLevel}";
                if (!myUserData.progress_stats.ContainsKey(key))
                    myUserData.progress_stats.Add(key, 0);
            }

            myUserData.progress_stats[levelKey] = remainingFuel;

            if (database == null)
            {
                callback?.Invoke("[UpdateLevelProgressStats] - LocalData (offline)");
                return;
            }

            var docRef = database.Collection("progress_stats").Document(uid);

            _ = docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                {
                    var snapshot = task.Result;
                    if (snapshot.Exists)
                    {
                        Dictionary<string, object> remote = ParseProgressStatsDocument(snapshot);
                        if (remote != null)
                        {
                            foreach (var entry in remote)
                                myUserData.progress_stats[entry.Key] = entry.Value;
                        }
                    }
                }

                myUserData.progress_stats[levelKey] = remainingFuel;

                try
                {
                    _ = docRef.SetAsync(myUserData.progress_stats).ContinueWithOnMainThread(writeTask =>
                    {
                        if (writeTask.IsCompleted && !writeTask.IsFaulted && !writeTask.IsCanceled)
                            callback?.Invoke("[UpdateLevelProgressStats] - ServerData (sucess)");
                        else
                            callback?.Invoke("[UpdateLevelProgressStats] - ServerData (Fail)");
                    });
                }
                catch (Exception e)
                {
                    Debug.Log("FirestoreController:AddStatsField::" + e.ToString());
                    callback?.Invoke("[UpdateLevelProgressStats] - ServerData (Fail)");
                }
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:UpdateStats::" + e.ToString());
            callback?.Invoke("[UpdateLevelProgressStats] - ServerData (Fail)");
        }
    }

    public void AddStatsField(Dictionary<string, object> stats, Action<string> callback)
    {
        try
        {
            var docRef = database.Collection("game_stats").Document(myUserData.uid);

            var statsData = new Dictionary<string, object>();
            statsData["stats"] = stats;

            _ = docRef.SetAsync(statsData).ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                {
                    callback("[AddStatsField] - ServerData (sucess)");
                }
                else
                {
                    callback("[AddStatsField] - ServerData (Fail)");
                }
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:AddStatsField::" + e.ToString());
        }
    }

    public void AddAverageStatsField(object stats, Action<string> callback)
    {
        try
        {
            var docRef = database.Collection("average_stats").Document(PlayerPrefsHolder.FirestoreLevelKey);

            _ = docRef.SetAsync(stats).ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                {
                    callback?.Invoke("[AddAverageStatsField] - ServerData (sucess)");
                }
                else
                {
                    callback?.Invoke("[AddAverageStatsField] - ServerData (Fail)");
                }
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:AddStatsField::" + e.ToString());
        }
    }

    public void AddBestStatsField(object stats, Action<string> callback)
    {
        try
        {
            var docRef = database.Collection("best_stats").Document(PlayerPrefsHolder.FirestoreLevelKey);

            // Update the document
            _ = docRef.SetAsync(stats).ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                {
                    callback("[AddBestStatsField] - ServerData (sucess)");
                }
                else
                {
                    callback("[AddBestStatsField] - ServerData (Fail)");
                }
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:AddStatsField::" + e.ToString());
        }
    }
    public void FetchAllLevelRanks(int currentLevelIndex, double currentLevelFuel, Action<int[]> callback)
    {
        int[] localRanks = BuildLevelRanks(currentLevelIndex, currentLevelFuel, null);
        callback?.Invoke(localRanks);

        string myUid = ResolveUserId();
        if (database == null || string.IsNullOrEmpty(myUid))
            return;

        try
        {
            _ = database.Collection("progress_stats").GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (!task.IsCompleted || task.IsFaulted || task.IsCanceled)
                    return;

                int[] cloudRanks = BuildLevelRanks(currentLevelIndex, currentLevelFuel, task.Result);
                callback?.Invoke(cloudRanks);
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Levels] FetchAllLevelRanks failed: {e.Message}");
        }
    }

    int[] BuildLevelRanks(int currentLevelIndex, double currentLevelFuel, QuerySnapshot snapshot)
    {
        int levelCount = LevelsLayoutSpec.LevelCount;
        int[] ranks = new int[levelCount];
        var fuelsByLevel = new List<double>[levelCount];
        for (int i = 0; i < levelCount; i++)
            fuelsByLevel[i] = new List<double>();

        double[] myFuels = new double[levelCount];
        ApplyLocalProgressFuels(myFuels, currentLevelIndex, currentLevelFuel);

        if (snapshot != null)
        {
            string myUid = ResolveUserId();
            foreach (DocumentSnapshot doc in snapshot.Documents)
            {
                if (!doc.Exists)
                    continue;

                Dictionary<string, object> data = ParseProgressStatsDocument(doc);
                if (data == null)
                    continue;

                MergeProgressStatsIntoRankData(
                    data,
                    fuelsByLevel,
                    myFuels,
                    doc.Id == myUid,
                    levelCount);
            }
        }

        ApplyLocalProgressFuels(myFuels, currentLevelIndex, currentLevelFuel);
        ComputeLevelRanks(ranks, myFuels, fuelsByLevel);
        return ranks;
    }

    string ResolveUserId()
    {
        if (!string.IsNullOrEmpty(myUserData?.uid))
            return myUserData.uid;

        return SystemInfo.deviceUniqueIdentifier;
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

    static Dictionary<string, object> ParseProgressStatsDocument(DocumentSnapshot doc)
    {
        if (doc == null || !doc.Exists)
            return null;

        try
        {
            Dictionary<string, object> parsed = doc.ToDictionary();
            if (parsed != null && parsed.Count > 0)
                return parsed;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Levels] progress_stats ToDictionary failed for {doc.Id}: {e.Message}");
        }

        var fallback = new Dictionary<string, object>();
        for (int i = 0; i <= 40; i++)
        {
            string key = $"LEVEL {i}";
            if (!doc.ContainsField(key))
                continue;

            try
            {
                fallback[key] = doc.GetValue<object>(key);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Levels] progress_stats field read failed for {doc.Id}/{key}: {e.Message}");
            }
        }

        return fallback.Count > 0 ? fallback : null;
    }

    static void MergeProgressStatsIntoRankData(
        Dictionary<string, object> data,
        List<double>[] fuelsByLevel,
        double[] myFuels,
        bool isMe,
        int levelCount)
    {
        if (data == null)
            return;

        foreach (var entry in data)
        {
            if (!TryParseLevelKey(entry.Key, out int parsedLevel))
                continue;

            int level = ResolveUiLevelIndex(parsedLevel);
            if (level < 0 || level >= levelCount)
                continue;

            double fuel = ToFuel(entry.Value);
            if (fuel <= 0)
                continue;

            fuelsByLevel[level].Add(fuel);
            if (isMe)
                myFuels[level] = Math.Max(myFuels[level], fuel);
        }
    }

    static int ResolveUiLevelIndex(int parsedLevel)
    {
        // LEVEL 1..39 match UI cells 1..39. LEVEL 0 is test — no panel cell.
        if (parsedLevel >= PlayerPrefsHolder.FirstRankedLevel
            && parsedLevel <= LevelsLayoutSpec.LevelCount)
            return parsedLevel - 1;

        return -1;
    }

    static bool TryParseLevelKey(string key, out int levelIndex)
    {
        levelIndex = -1;
        if (string.IsNullOrEmpty(key))
            return false;

        const string prefix = "LEVEL ";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(key.Substring(prefix.Length), out levelIndex);
    }

    static void ComputeLevelRanks(int[] ranks, double[] myFuels, List<double>[] fuelsByLevel)
    {
        for (int level = 0; level < ranks.Length; level++)
        {
            double myFuel = myFuels[level];
            if (myFuel <= 0)
            {
                ranks[level] = 0;
                continue;
            }

            EnsureFuelListed(fuelsByLevel[level], myFuel);

            int better = 0;
            for (int i = 0; i < fuelsByLevel[level].Count; i++)
            {
                if (fuelsByLevel[level][i] > myFuel + 0.001)
                    better++;
            }

            ranks[level] = better + 1;
        }
    }

    void ApplyLocalProgressFuels(double[] myFuels, int currentLevelIndex, double currentLevelFuel)
    {
        if (myUserData?.progress_stats != null)
        {
            for (int level = 0; level < myFuels.Length; level++)
            {
                double fuel = ReadStoredLevelFuel(myUserData.progress_stats, level);
                if (fuel > 0)
                    myFuels[level] = Math.Max(myFuels[level], fuel);
            }
        }

        if (currentLevelIndex >= 0
            && currentLevelIndex < myFuels.Length
            && currentLevelFuel > 0)
        {
            myFuels[currentLevelIndex] = Math.Max(myFuels[currentLevelIndex], currentLevelFuel);
        }
    }

    static double ReadStoredLevelFuel(Dictionary<string, object> stats, int uiLevelIndex)
    {
        if (stats == null || uiLevelIndex < 0)
            return 0;

        if (stats.TryGetValue($"LEVEL {uiLevelIndex + 1}", out object oneBased))
        {
            double fuel = ToFuel(oneBased);
            if (fuel > 0)
                return fuel;
        }

        return 0;
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

    static double ToFuel(object raw)
    {
        if (raw == null)
            return 0;

        switch (raw)
        {
            case double d:
                return d;
            case float f:
                return f;
            case int i:
                return i;
            case long l:
                return l;
            case decimal m:
                return (double)m;
            case string s when double.TryParse(s, out double parsed):
                return parsed;
        }

        try
        {
            return Convert.ToDouble(raw);
        }
        catch
        {
            return 0;
        }
    }

    #endregion

    #region GET OTHER USER
    public void GetOtherUser(string dbId, Action<UserData> callback)
    {
        var docRef = database.Collection("users").Document(dbId);

        _ = docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            var snapshot = task.Result;

            if (snapshot.Exists)
            {
                callback(snapshot.ConvertTo<UserData>());
            }
            else
            {
                callback(null);
            }
        });
    }

    #endregion


    #endregion

    public void DeleteDocuments(Action<bool> success)
    {
        try
        {
            var docRef_users = database.Collection("users").Document("abcXYZ");

            _ = docRef_users.DeleteAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                {
                    Debug.Log($"Document {"abcXYZ"} successfully deleted from users.");

                    var docRef_gamestats = database.Collection("game_stats").Document("abcXYZ");

                    _ = docRef_gamestats.DeleteAsync().ContinueWithOnMainThread(task =>
              {
                  if (task.IsCompleted)
                  {
                      Debug.Log($"Document {"abcXYZ"} successfully deleted from game_stats.");

                      var docRef_blocklist = database.Collection("block_list").Document("abcXYZ");

                      _ = docRef_blocklist.DeleteAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsCompleted)
                    {
                        Debug.Log($"Document {"abcXYZ"} successfully deleted from block_list.");
                        blockList = null;
                        myUserData = null;
                        success(true);
                    }
                    else if (task.IsFaulted)
                    {
                        success(false);
                        Debug.Log("Failed to delete the document from block_list: " + task.Exception);
                    }
                });
                  }
                  else if (task.IsFaulted)
                  {
                      success(false);
                      Debug.Log("Failed to delete the document from game_stats: " + task.Exception);
                  }
              });

                }
                else if (task.IsFaulted)
                {
                    success(false);
                    Debug.Log("Failed to delete the document from users : " + task.Exception);
                }
            });
        }
        catch (Exception e)
        {
            success(false);
            Debug.Log("FirestoreController:DeleteDocument::" + e.ToString());
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
        DumpCollection("users", FormatUserDocument);
        DumpCollection("progress_stats", FormatProgressStatsDocument);
        DumpCollection("game_stats", FormatGenericDocument);
        DumpCollection("average_stats", FormatGenericDocument);
        DumpCollection("best_stats", FormatGenericDocument);
        DumpCollection("block_list", FormatGenericDocument);
        DumpCollection("courses", FormatGenericDocument);
    }

    void DumpCollection(string collectionName, System.Func<DocumentSnapshot, string> formatter)
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

                if (snapshot.Count == 0)
                {
                    Debug.Log($"[FirestoreDump] {collectionName}: (empty)");
                    return;
                }

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (!doc.Exists)
                        continue;

                    string body = formatter != null ? formatter(doc) : doc.Id;
                    Debug.Log($"[FirestoreDump] {collectionName}/{doc.Id}\n{body}");
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FirestoreDump] {collectionName}: {e.Message}");
        }
    }

    static string FormatUserDocument(DocumentSnapshot doc)
    {
        var lines = new System.Text.StringBuilder();
        AppendField(lines, doc, "name");
        AppendField(lines, doc, "uid");
        AppendField(lines, doc, "provider");
        return lines.Length > 0 ? lines.ToString() : "(no fields)";
    }

    static string FormatProgressStatsDocument(DocumentSnapshot doc)
    {
        Dictionary<string, object> data = ParseProgressStatsDocument(doc);
        if (data == null || data.Count == 0)
            return "(empty)";

        var levelFuels = new System.Collections.Generic.List<string>();
        foreach (var entry in data)
        {
            if (!TryParseLevelKey(entry.Key, out int level))
                continue;

            double fuel = ToFuel(entry.Value);
            if (fuel <= 0)
                continue;

            levelFuels.Add($"LEVEL {level} = {fuel:0.##} T");
        }

        levelFuels.Sort(StringComparer.Ordinal);
        if (levelFuels.Count == 0)
            return "(no level fuels > 0)";

        return string.Join("\n", levelFuels);
    }

    static string FormatGenericDocument(DocumentSnapshot doc)
    {
        try
        {
            Dictionary<string, object> data = doc.ToDictionary();
            if (data == null || data.Count == 0)
                return "(empty)";

            var lines = new System.Text.StringBuilder();
            foreach (var entry in data)
                lines.AppendLine($"{entry.Key}: {SummarizeValue(entry.Value)}");

            return lines.ToString().TrimEnd();
        }
        catch (Exception e)
        {
            return $"(parse error: {e.Message})";
        }
    }

    static void AppendField(System.Text.StringBuilder lines, DocumentSnapshot doc, string field)
    {
        if (!doc.ContainsField(field))
            return;

        try
        {
            lines.AppendLine($"{field}: {doc.GetValue<object>(field)}");
        }
        catch
        {
            lines.AppendLine($"{field}: (unreadable)");
        }
    }

    static string SummarizeValue(object value)
    {
        if (value == null)
            return "null";

        if (value is string s)
            return s;

        if (value is System.Collections.IDictionary dict)
            return $"{{object, {dict.Count} keys}}";

        if (value is System.Collections.IList list)
            return $"[list, {list.Count} items]";

        return value.ToString();
    }
}

public class CourseData
{

}

[FirestoreData]
public class Game_Stats
{
    [FirestoreProperty] public Dictionary<string, object> stats { get; set; }
}

[FirestoreData]
public class Average_Stats
{
    [FirestoreProperty] public Dictionary<string, object> stats { get; set; }
    [FirestoreProperty] public Another another { get; set; }
}

[FirestoreData]
public class Best_Stats
{
    [FirestoreProperty] public string uid { get; set; }
    [FirestoreProperty] public double remainingFuel { get; set; }
    [FirestoreProperty] public List<double> altitude { get; set; }
    [FirestoreProperty] public List<double> speed { get; set; }
    [FirestoreProperty] public List<double> flap { get; set; }
    [FirestoreProperty] public List<double> speedBrake { get; set; }
    [FirestoreProperty] public List<double> landingGear { get; set; }
    [FirestoreProperty] public List<double> verticalMode { get; set; }
    [FirestoreProperty] public List<double> fuelFlow { get; set; }
    [FirestoreProperty] public List<double> distance { get; set; }
}

[FirestoreData]
public class UserData
{
    [FirestoreProperty] public Game_Stats game_stats { get; set; }
    [FirestoreProperty] public Average_Stats average_stats { get; set; }
    [FirestoreProperty] public Best_Stats best_stats { get; set; }
    [FirestoreProperty] public Dictionary<string, object> progress_stats { get; set; }
    [FirestoreProperty] public Dictionary<string, object> average_progress_stats { get; set; }
    [FirestoreProperty] public string name { get; set; }
    [FirestoreProperty] public string provider { get; set; }
    [FirestoreProperty] public string uid { get; set; }
}

//[FirestoreData]
//public class Avg
//{
//    [FirestoreProperty] public double altitude { get; set; }
//    [FirestoreProperty] public double speed { get; set; }
//    [FirestoreProperty] public double flap { get; set; }
//    [FirestoreProperty] public double speedBrake { get; set; }
//    [FirestoreProperty] public double landingGear { get; set; }
//    [FirestoreProperty] public int count { get; set; }
//}

[FirestoreData]
public class S_data
{
    [FirestoreProperty] public int count { get; set; }
    [FirestoreProperty] public double altitude { get; set; }
    [FirestoreProperty] public double speed { get; set; }
    [FirestoreProperty] public double flap { get; set; }
    [FirestoreProperty] public double speedBrake { get; set; }
    [FirestoreProperty] public double landingGear { get; set; }
    [FirestoreProperty] public double distance { get; set; }
    //[FirestoreProperty] public double verticalMode { get; set; }
    //[FirestoreProperty] public double fuelFlow { get; set; }
    [FirestoreProperty] public double remainingFuel { get; set; }
    //[FirestoreProperty] public string time { get; set; }
}

[FirestoreData]
public class L_data
{
    [FirestoreProperty] public List<double> altitude { get; set; }
    [FirestoreProperty] public List<double> speed { get; set; }
    [FirestoreProperty] public List<double> flap { get; set; }
    [FirestoreProperty] public List<double> speedBrake { get; set; }
    [FirestoreProperty] public List<double> landingGear { get; set; }
    [FirestoreProperty] public List<double> verticalMode { get; set; }
    [FirestoreProperty] public List<double> fuelFlow { get; set; }
    [FirestoreProperty] public List<double> distance { get; set; }
    [FirestoreProperty] public double remainingFuel { get; set; }
    [FirestoreProperty] public List<string> time { get; set; }
}

[FirestoreData]
public class A_data
{
    [FirestoreProperty] public int count { get; set; }
    [FirestoreProperty] public double remainingFuel { get; set; }
}

[FirestoreData]
public class Another
{
    [FirestoreProperty] public int count { get; set; }
    [FirestoreProperty] public double landingGear { get; set; }
    [FirestoreProperty] public List<double> flap { get; set; }
}