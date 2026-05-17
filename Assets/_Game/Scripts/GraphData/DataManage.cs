using Navigation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using XCharts.Runtime;

public class DataManage : MonoBehaviour
{
    public FirestoreController firestoreController;
    private SaveLoadManager saveLoadManager;
    public string savePath => $"Saves/LEVEL {PlayerPrefsHolder.Level}.json";
    public string saveProgressPath => $"Saves/PROGRESS.json";

    [Space]
    [Header("CATCH-DATA")]
    [SerializeField] private DDL_data m_data = new DDL_data();
    Progress_Data progress_data = new Progress_Data();

    private void Awake()
    {
        IStorage storage = new LocalFileStorage();
        saveLoadManager = new SaveLoadManager(new JsonSerializer(), storage);
    }
    public void Start()
    {
        m_data.time.Add(DateTime.Now.ToString());
        //InvokeRepeating(nameof(FillDataInArray), 0, 1f);
    }

    public double ConvertToLowerHundred(double value)
    {
        return Math.Floor(value / 100.0) * 100;
    }

    public double RoundDownToNearestTen(double value)
    {
        return Math.Floor(value / 10) * 10;
    }

    public void FillDataInArray()
    {
        m_data.speed.Add(RoundDownToNearestTen(Calculator.CSpeed));
        m_data.altitude.Add(ConvertToLowerHundred(Calculator.CAltitude));
        m_data.flap.Add(Calculator.Flap_Idx);
        m_data.speedBrake.Add(Convert.ToInt32(Calculator.SBUp));
        m_data.landingGear.Add(Convert.ToInt32(Calculator.LGDown));
        m_data.fuelFlow.Add((Calculator.dispFF + (Calculator.FF - Calculator.FF / 10 * 10)) / 100);

        if (Session.State != null)
        {
            //if (Session.State.LC) m_data.verticalMode.Add("MCP SPD");
            //else if (Session.State.AH) m_data.verticalMode.Add("ALT");
            //else if (Session.State.VS) m_data.verticalMode.Add("VS");
            //else if (Session.State.VNAV) m_data.verticalMode.Add("VNAV");
            //else if (Session.State.GSCaptured) m_data.verticalMode.Add("GS");
            //else m_data.verticalMode.Add("");

            if (Session.State.LC) m_data.verticalMode.Add(1);
            else if (Session.State.AH) m_data.verticalMode.Add(2);
            else if (Session.State.VS) m_data.verticalMode.Add(3);
            else if (Session.State.VNAV) m_data.verticalMode.Add(4);
            else if (Session.State.GSCaptured) m_data.verticalMode.Add(5);
            else m_data.verticalMode.Add(0);
        }
    }


    float timer = 0f;
    float repeatRate = 1f;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= repeatRate)
        {
            timer = 0f;

            FillDataInArray();
            if (Session.State.Speed10X)
            {
                for (int i = 0; i < 9; i++)
                {
                   // Debug.Log(i);
                    FillDataInArray();
                }
            }
        }


        if (Input.GetKeyDown(KeyCode.S))
        {
            //m_time.Add(DateTime.Now.ToString());
            SaveGame();
        };

        if (Input.GetKeyDown(KeyCode.K))
        {
            /*LoadGame((data) =>
            {
                Dictionary<string, object> levelData = new Dictionary<string, object>();

                levelData.Add("altitude", data.altitude);
                levelData.Add("speed", data.speed);
                levelData.Add("flap", data.flap);
                levelData.Add("speedBrake", data.speedBrake);
                levelData.Add("landingGear", data.landingGear);
                levelData.Add("verticalMode", data.verticalMode);
                levelData.Add("fuelFlow", data.fuelFlow);
                levelData.Add("remainingFuel", data.remainingFuel);
                levelData.Add("time", data.time);

                firestoreController.UpdateStats(levelData, (x) =>
                {
                    Debug.Log(x);   
                });
            });*/


            //LoadGame((data) =>
            //{
            //    Dictionary<string, object> levelData = new Dictionary<string, object>();

            //    firestoreController.UpdateBestStats(data.remainingFuel, (x) =>
            //    {
            //        Debug.Log(x);
            //    });
            //});

        }
    }

    public async void SaveGame()
    {
        m_data.time.Add(DateTime.Now.ToString());
        m_data.remainingFuel = System.Math.Round(Calculator.totalFuel / 100, 2);

        await saveLoadManager.SaveAsync(m_data, savePath);
        SaveProgress(m_data.remainingFuel);

        //Below For FirebaseSave
        L_data l_Data = new L_data();

        l_Data.altitude = m_data.altitude;
        l_Data.speed = m_data.speed;
        l_Data.flap = m_data.flap;
        l_Data.landingGear = m_data.landingGear;
        l_Data.speedBrake = m_data.speedBrake;
        l_Data.verticalMode = m_data.verticalMode;
        l_Data.fuelFlow = m_data.fuelFlow;
        l_Data.remainingFuel = m_data.remainingFuel;
        l_Data.time = m_data.time;

        firestoreController.UpdateStats(l_Data, (x) =>
        {
            Debug.Log(x);
        });

        firestoreController.UpdateLevelProgressStats(m_data.remainingFuel, (x) =>
        {
            Debug.Log(x);
        });

        Debug.Log("Game Saved");
    }

    public async void SaveProgress(double _remainingFuel)
    {
        progress_data = await saveLoadManager.LoadAsync<Progress_Data>(saveProgressPath);

        if (progress_data == null)
        {
            progress_data = new Progress_Data();
            //progress_data.progress = new List<double>();

            for (int i = 0; i < 40; i++)
            {
                progress_data.progress.Add(0);

                if (i == PlayerPrefsHolder.Level)
                {
                    progress_data.progress[PlayerPrefsHolder.Level] = _remainingFuel;
                }
            }
        }
        else
        {
            progress_data.progress[PlayerPrefsHolder.Level] = m_data.remainingFuel;
        }
        await saveLoadManager.SaveAsync(progress_data, saveProgressPath);
    }

    public async void LoadProgress(Action<Progress_Data> levelData)
    {
        Progress_Data data = await saveLoadManager.LoadAsync<Progress_Data>(saveProgressPath);

        if (data != null)
        {
            levelData?.Invoke(data);
        }
        else
        {
            Debug.LogWarning("No save data found");
        }
    }

    public async void LoadGame(Action<DDL_data> levelData)
    {
        DDL_data data = await saveLoadManager.LoadAsync<DDL_data>(savePath);

        if (data != null)
        {
            //transform.position = data.Position;
            levelData?.Invoke(data);
        }
        else
        {
            Debug.LogWarning("No save data found");
        }
    }

    /*public async void LoadAverageGame(Action<DataDesign> levelData)
    {
        await Task.Delay(5000);

        DataDesign dataDesign = new DataDesign();

        if (firestoreController.cdata.altitudes != null)
        {
            List<Altitude> _altitudes = firestoreController.cdata.altitudes;

            List<int> alt = new List<int>();
            for (int i = 0; i < _altitudes.Count; i++)
            {
                alt.Add(_altitudes[i].average);
            }

            dataDesign.altitude = alt.ToArray();

            levelData?.Invoke(dataDesign);
            Debug.Log("Game Loaded");
        }
        else
        {
            Debug.LogWarning("No save data found");
        }
    }*/

    /*public async Task<DataDesign> LoadLevelFile(int _levelIndex)
    {
        DataDesign data = await saveLoadManager.LoadAsync<DataDesign>($"Saves/LEVEL {_levelIndex}.json");

        if (data != null)
        {
            return data;
        }
        else
        {
            Debug.LogWarning("No save data found");
            return null;
        }
    }*/
}