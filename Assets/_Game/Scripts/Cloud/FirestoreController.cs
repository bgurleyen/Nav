using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;
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

                    _ = docRef_game_stats.GetSnapshotAsync().ContinueWithOnMainThread(task =>
              {
                  var snapshot = task.Result;

                  if (snapshot.Exists)
                  {
                      myUserData.game_stats = snapshot.ConvertTo<Game_Stats>();
                  }

                  //Get Course Data from firestore
                  //GetCourseData();

                  //RetrieveBlockList();
              });
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
    public List<L_data> bestPlayerLevels = new List<L_data>();
    public List<S_data> averagePlayer = new List<S_data>();
    public List<double> bestPlayerProgress = new List<double>();
    public List<double> averagePlayerProgress = new List<double>();

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
        bestPlayerLevels = new List<L_data>();
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
                    foreach (var levels_Data in best_Player.stats.Values.Cast<Dictionary<string, object>>())
                    {
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
                            l_Data.time = ((List<object>)levels_Data["time"]).Select(x => Convert.ToString(x)).ToList();

                            try
                            {
                                bestPlayerLevels.Add(l_Data);
                            }
                            catch (Exception e)
                            {
                                Debug.Log(e.ToString());
                            }
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
            Debug.Log("FirestoreController:UpdateStats::" + e.ToString());
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

                if (!myUserData.game_stats.stats.ContainsKey($"LEVEL {PlayerPrefsHolder.Level}"))
                {
                    cStats.Add($"LEVEL {PlayerPrefsHolder.Level}", newStats);
                }

                myUserData.game_stats.stats[$"LEVEL {PlayerPrefsHolder.Level}"] = newStats;

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

    public void UpdateAverageStats(DDL_data newStats, Action<string> callback)
    {
        averagePlayer = new List<S_data>();

        try
        {
            var docRef_game_stats = database.Collection("average_stats").Document($"LEVEL {PlayerPrefsHolder.Level}");

            _ = docRef_game_stats.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                var snapshot = task.Result;

                if (snapshot.Exists)
                {
                    myUserData.average_stats = snapshot.ConvertTo<Average_Stats>();
                }

                if (myUserData.average_stats == null)
                {
                    myUserData.average_stats = new Average_Stats();
                    myUserData.average_stats.stats = new Dictionary<string, object>();
                }

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
                    };
                    sStats.Add(s_Data);
                    //Debug.Log($"{i} --> {sStats[i].altitude} --> {sStats[i].speed} --> {sStats[i].flap} --> {sStats[i].speedBrake} --> {sStats[i].landingGear} --> {sStats[i].count}");
                }

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
                    }

                    if (myUserData.average_stats.stats.ContainsKey($"{i}"))
                    {
                        s_Data.count += sStats[i].count;
                        s_Data.altitude = (s_Data.altitude + sStats[i].altitude) / s_Data.count;
                        s_Data.speed = (s_Data.speed + sStats[i].speed) / s_Data.count;
                        s_Data.flap = (s_Data.flap + sStats[i].flap) / s_Data.count;
                        s_Data.speedBrake = (s_Data.speedBrake + sStats[i].speedBrake) / s_Data.count;
                        s_Data.landingGear = (s_Data.landingGear + sStats[i].landingGear) / s_Data.count;

                        myUserData.average_stats.stats[$"{i}"] = s_Data;
                    }
                    else
                    {
                        myUserData.average_stats.stats.Add($"{i}", s_Data);
                    }
                    averagePlayer.Add(s_Data);
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

    public void UpdateBestStats(double remainingFuel, Action<string> callback1, Action<string> callback2)
    {
        try
        {
            var docRef_game_stats = database.Collection("best_stats").Document($"LEVEL {PlayerPrefsHolder.Level}");

            _ = docRef_game_stats.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                var snapshot = task.Result;

                if (snapshot.Exists)
                {
                    myUserData.best_stats = snapshot.ConvertTo<Best_Stats>();
                }

                if (myUserData.best_stats == null)
                {
                    myUserData.best_stats = new Best_Stats();
                }

                if (myUserData.best_stats.remainingFuel < remainingFuel)
                {
                    myUserData.best_stats.uid = myUserData.uid;
                    myUserData.best_stats.remainingFuel = remainingFuel;

                    AddBestStatsField(myUserData.best_stats, (res) =>
                    {
                        callback1($"{res} | [UpdateBestStats] - LocalData (sucess)");
                    });
                }
                else
                {
                    GetBestPlayerData(myUserData.best_stats.uid, (res) =>
                    {
                        callback2($"{res} | [UpdateBestStats] - ServerData (sucess)");
                    });
                }
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:UpdateStats::" + e.ToString());
        }
    }

    public void UpdateLevelProgressStats(double remainingFuel, Action<string> callback)
    {
        try
        {
            var docRef = database.Collection("progress_stats").Document(myUserData.uid);

            _ = docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                var snapshot = task.Result;
                if (snapshot.Exists)
                {
                    myUserData.progress_stats = snapshot.ConvertTo<Dictionary<string, object>>();
                }

                if (myUserData.progress_stats == null)
                {
                    myUserData.progress_stats = new Dictionary<string, object>();
                }

                for (int i = 0; i < 40; i++)
                {
                    if (!myUserData.progress_stats.ContainsKey($"LEVEL {i}"))
                    {
                        myUserData.progress_stats.Add($"LEVEL {i}", 0);
                    }

                    if (i == PlayerPrefsHolder.Level)
                    {
                        myUserData.progress_stats[$"LEVEL {PlayerPrefsHolder.Level}"] = remainingFuel;
                    }
                }
                try
                {
                    _ = docRef.SetAsync(myUserData.progress_stats).ContinueWithOnMainThread(task =>
                    {
                        if (task.IsCompleted)
                        {
                            callback("[UpdateLevelProgressStats] - ServerData (sucess)");
                        }
                        else
                        {
                            callback("[UpdateLevelProgressStats] - ServerData (Fail)");
                        }
                    });
                }
                catch (Exception e)
                {
                    Debug.Log("FirestoreController:AddStatsField::" + e.ToString());
                }
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:UpdateStats::" + e.ToString());
        }
    }

    public void UpdateAverageProgressStats(double remainingFuel, Action<string> callback)
    {
        averagePlayerProgress = new List<double>();
        try
        {
            var docRef = database.Collection("average_progress_stats").Document("average_progress");

            _ = docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                var snapshot = task.Result;
                if (snapshot.Exists)
                {
                    myUserData.average_progress_stats = snapshot.ConvertTo<Dictionary<string, object>>();
                }

                if (myUserData.average_progress_stats == null)
                {
                    myUserData.average_progress_stats = new Dictionary<string, object>();
                }

                for (int i = 0; i < 40; i++)
                {
                    A_data a_Data = new A_data { remainingFuel = 0, count = 0 };

                    if (myUserData.average_progress_stats.ContainsKey($"LEVEL {i}"))
                    {
                        Dictionary<string, object> s_stats = myUserData.average_progress_stats[$"LEVEL {i}"] as Dictionary<string, object>;
                        int _count = Convert.ToInt32(s_stats["count"]);
                        double _remainingFuel = Convert.ToDouble(s_stats["remainingFuel"]);

                        if (i == PlayerPrefsHolder.Level)
                        {
                            a_Data.count = 1;
                            a_Data.remainingFuel = remainingFuel;

                            a_Data.count += _count;

                            if (a_Data.count > 0)
                            {
                                a_Data.remainingFuel = (a_Data.remainingFuel + (_remainingFuel * (a_Data.count - 1))) / a_Data.count;
                            }
                            else
                            {
                                a_Data.remainingFuel = 0;
                            }
                        }
                        else
                        {
                            a_Data.count = _count; // Important if count is 0
                            if (a_Data.count > 0)
                            {
                                a_Data.remainingFuel = (a_Data.remainingFuel + (_remainingFuel * a_Data.count)) / a_Data.count;
                            }
                            else
                            {
                                a_Data.remainingFuel = 0;
                            }

                        }
                            myUserData.average_progress_stats[$"LEVEL {i}"] = a_Data;
                    }
                    else
                    {
                        if (i == PlayerPrefsHolder.Level)
                        {
                            a_Data.count = 1;
                            a_Data.remainingFuel = remainingFuel;
                        }
                        myUserData.average_progress_stats.Add($"LEVEL {i}", a_Data);
                    }
                    averagePlayerProgress.Add(a_Data.remainingFuel);
                }

                try
                {
                    _ = docRef.SetAsync(myUserData.average_progress_stats).ContinueWithOnMainThread(task =>
                    {
                        if (task.IsCompleted)
                        {
                            callback("[UpdateAverageProgressStats] - ServerData (sucess)");
                        }
                        else
                        {
                            callback("[UpdateAverageProgressStats] - ServerData (Fail)" + task.Exception);
                        }
                    });
                }
                catch (Exception e)
                {
                    Debug.Log("FirestoreController:AddStatsField::" + e.ToString());
                }
            });
        }
        catch (Exception e)
        {
            Debug.Log("FirestoreController:UpdateStats::" + e.ToString());
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
            var docRef = database.Collection("average_stats").Document($"LEVEL {PlayerPrefsHolder.Level}");

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
            var docRef = database.Collection("best_stats").Document($"LEVEL {PlayerPrefsHolder.Level}");

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

    #region DELETE DOCUMENTS

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

    #endregion
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
}

[FirestoreData]
public class Best_Stats
{
    [FirestoreProperty] public string uid { get; set; }
    [FirestoreProperty] public double remainingFuel { get; set; }
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
    [FirestoreProperty] public double remainingFuel { get; set; }
    [FirestoreProperty] public List<string> time { get; set; }
}

[FirestoreData]
public class A_data
{
    [FirestoreProperty] public int count { get; set; }
    [FirestoreProperty] public double remainingFuel { get; set; }
}