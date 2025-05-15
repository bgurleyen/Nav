using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using XCharts.Runtime;
using XUGL;

public class GraphManage : MonoBehaviour
{
    public DataManage dataManage;
    public LineChart chart;
    public LineChart FFChart;
    public LineChart LevelChart;

    public static Action<bool> OnGameFinish;


    [Header("Other-Temp")]
    public CanvasGroup mainGroup;
    public CanvasGroup graphGroup;

    private void Awake()
    {
    }

    private void OnEnable()
    {
        OnGameFinish += OnGameFinished;
    }

    private void OnDisable()
    {
        OnGameFinish -= OnGameFinished;
    }

    void Start()
    {
        ChartInitRuntimeSetting(ref FFChart, 0, false);

        ChartInitRuntimeSetting(ref chart, 0, true);
        ChartInitRuntimeSetting(ref chart, 1, true);
        ChartInitRuntimeSetting(ref chart, 2, true);

        ChartInitRuntimeProgressSetting(ref LevelChart);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            //DataViewInGraph();
            //AvgDataViewInGraph();
            //OnGameFinished(true);
        }


        if (Input.GetKeyDown(KeyCode.Keypad0))
        {
            dataManage.LoadGame((data) =>
            {
                ChartSetRuntimeData(ref chart, 0, data);
                Debug.Log("[GetMineProfileData] - LocalData (sucess)");
            });
        }
    }

    void DataViewInGraph()
    {
        dataManage.LoadGame((data) =>
        {
            //FUEL FLOW
            ChartSetRuntimeData(ref FFChart, 0, data.fuelFlow);
            Debug.Log("[GetMineFuelFlowData] - LocalData (sucess)");

            //FLIGHT PROFILE

            //Mine-Series
            ChartSetRuntimeData(ref chart, 0, data);
            Debug.Log("[GetMineProfileData] - LocalData (sucess)");

            //Best-Series
            dataManage.firestoreController.UpdateBestStats(data.remainingFuel,
            (callback1) =>
            {
                Debug.Log(callback1);
                ChartSetRuntimeData(ref chart, 1, data);
                chart.RefreshChart(1);
            },
            (callback2) =>
            {
                Debug.Log(callback2);
                ChartSetRuntimeData(ref chart, 1, dataManage.firestoreController.bestPlayerLevels[PlayerPrefsHolder.Level]);
                chart.RefreshChart(1);
            });

            //Average-Series
            dataManage.firestoreController.UpdateAverageStats(data, (x) =>
            {
                Debug.Log(x);
                List<S_data> averageRawData = dataManage.firestoreController.averagePlayer;
                L_data averageData = new L_data();

                averageData.altitude = new List<double>() { };
                averageData.speed = new List<double>() { };
                averageData.flap = new List<double>() { };
                averageData.speedBrake = new List<double>() { };
                averageData.landingGear = new List<double>() { };

                for (int i = 0; i < averageRawData.Count; i++)
                {
                    averageData.altitude.Add(averageRawData[i].altitude);
                    averageData.speed.Add(averageRawData[i].speed);
                    averageData.landingGear.Add(averageRawData[i].landingGear);
                    averageData.flap.Add(averageRawData[i].flap);
                    averageData.speedBrake.Add(averageRawData[i].speedBrake);
                }

                Debug.Log(averageData.altitude.Count);
                ChartSetRuntimeData(ref chart, 2, averageData);

                var lastVal = chart.EnsureChartComponent<YAxis>().GetLastLabelValue();
                chart.EnsureChartComponent<YAxis>().interval = lastVal - 1000;

                chart.RefreshChart(2);
            });
        });
    }

    void OnGameFinished(bool isFinish)
    {
        dataManage.SaveGame();
        //dataManage.SaveProgress();
        mainGroup.alpha = 0;
        graphGroup.alpha = 1;
        graphGroup.blocksRaycasts = true;
        DataViewInGraph();
    }

    void OnFinish2()
    {
        FFChart.gameObject.SetActive(false);
        chart.gameObject.SetActive(false);
        LevelChart.gameObject.SetActive(true);

        dataManage.LoadProgress((data) =>
        {
            //Mine-ProgressSeries
            ChartSetRuntimeProgressData(ref LevelChart, 0, data.progress);
            LevelChart.RefreshChart(0);
            Debug.Log("[GetMineProgressData] - LocalData (sucess)");

            //Best-ProgressSeries
            dataManage.firestoreController.GetBestPlayerProgressData(dataManage.firestoreController.myUserData.best_stats.uid,
            (callback1) =>
            {
                Debug.Log(callback1);
                ChartSetRuntimeProgressData(ref LevelChart, 1, data.progress);
                chart.RefreshChart(1);
            },
            (callback2) =>
            {
                Debug.Log(callback2);
                ChartSetRuntimeProgressData(ref LevelChart, 1, dataManage.firestoreController.bestPlayerProgress);
                chart.RefreshChart(1);
            });

            //Average-ProgressSeries
            dataManage.firestoreController.UpdateAverageProgressStats(data.progress[PlayerPrefsHolder.Level], (x) =>
            {
                Debug.Log(x);
                ChartSetRuntimeProgressData(ref LevelChart, 2, dataManage.firestoreController.averagePlayerProgress);
                LevelChart.RefreshChart(2);
            });
        });

        LevelChart.RefreshChart();
    }

    int Click = 0;

    public void OnContinueButtonClick()
    {
        if (Click == 1)
        {
            PlayerPrefsHolder.Level += 1;
            SceneManager.LoadScene("EMPTY");
        }

        if (Click == 0)
        {
            OnFinish2();
            Click = 1;
        }
    }


    private void ChartInitRuntimeSetting(ref LineChart chart, int serieIndex, bool isShowAxisLabel)
    {
        chart.DefaultTimeLineChart();

        chart.GetChartComponent<XAxis>().axisLabel.show = isShowAxisLabel;

        Serie serie = chart.series[serieIndex];
        serie.ClearData();
    }

    private void ChartSetRuntimeData(ref LineChart chart, int serieIndex, List<double> lineData)
    {
        Serie serie = chart.series[serieIndex];

        DateTime timeSeries = DateTime.Parse("08-05-2025 12:03:46").AddHours(-5).AddMinutes(-30);
        for (int i = 0; i < lineData.Count; i++)
        {
            timeSeries = timeSeries.AddSeconds(1);
            //chart.AddData(serieIndex, timeSeries, lineData[i]);
            chart.series[serieIndex].AddData(lineData[i]);
        }
    }






    private void ChartSetRuntimeData(ref LineChart chart, int serieIndex, DDL_data data)
    {
        Serie serie = chart.series[serieIndex];
        Serie serie1 = RunTimeAddSeries(ref chart, serie);

        double lastSelectedSpeed = data.speed[0];

        DateTime timeSeries = DateTime.Parse("00:00:00").AddHours(-5).AddMinutes(-30);
        //DateTime timeSeries = DateTime.Parse("15-05-2025 00:00:00").AddHours(-5).AddMinutes(-30);
        for (int i = 0; i < data.altitude.Count; i++)
        {
            timeSeries = timeSeries.AddSeconds(1);
            chart.AddData(serieIndex, timeSeries, data.altitude[i]);
            //chart.series[serieIndex].AddData(data.altitude[i]);
            chart.series[serieIndex].largeThreshold = data.altitude.Count + 1;

            SerieData sData = chart.series[serieIndex].data[i];

            var speedResult = IsSpeedChange(data.speed, i, ref lastSelectedSpeed);
            if (speedResult.Item1 == true)
            {
                AddLabelSymbol(ref sData, Convert.ToInt32(speedResult.Item2));
            }

            var landingResult = IsValueChange(data.landingGear, i);
            if (landingResult.Item1 == true)
            {
                if (landingResult.Item2 == true)
                {
                    AddArrowDownSymbol(ref sData);
                }
                else
                {
                    AddArrowUpSymbol(ref sData);
                }
            }

            var flapResult = IsFlapValueChange(data.flap, i);
            if (flapResult.Item1 == true)
            {
                AddFlapSymbol(ref sData, flapResult.Item2);
            }

            if (data.speedBrake[i] == 1)
            {
                serie.data[i].ignore = true;
                chart.AddData(3,timeSeries,data.altitude[i]);
            }
        }
    }

    private void ChartSetRuntimeData(ref LineChart chart, int serieIndex, L_data data)
    {
        Serie serie = chart.series[serieIndex];
        Serie serie1 = RunTimeAddSeries(ref chart, serie);

        double lastSelectedSpeed = data.speed[0];

        DateTime timeSeries = DateTime.Parse("00:00:00").AddHours(-5).AddMinutes(-30);
        for (int i = 0; i < data.altitude.Count; i++)
        {
            timeSeries = timeSeries.AddSeconds(1);
            chart.AddData(serieIndex, timeSeries, data.altitude[i]);
            chart.series[serieIndex].largeThreshold = data.altitude.Count + 1;

            SerieData sData = chart.series[serieIndex].data[i];

            var speedResult = IsSpeedChange(data.speed, i, ref lastSelectedSpeed);
            if (speedResult.Item1 == true)
            {
                AddLabelSymbol(ref sData, Convert.ToInt32(speedResult.Item2));
            }

            var landingResult = IsValueChange(data.landingGear, i);
            if (landingResult.Item1 == true)
            {
                if (landingResult.Item2 == true)
                {
                    AddArrowDownSymbol(ref sData);
                }
                else
                {
                    AddArrowUpSymbol(ref sData);
                }
            }

            var flapResult = IsFlapValueChange(data.flap, i);
            if (flapResult.Item1 == true)
            {
                AddFlapSymbol(ref sData, flapResult.Item2);
            }

            if (data.speedBrake[i] == 1)
            {
                serie.data[i].ignore = true;
                
                chart.AddData($"Dash_{serie.serieName}", timeSeries, data.altitude[i]);
            }
        }
    }





    #region Progress_Chart
    private void ChartInitRuntimeProgressSetting(ref LineChart chart)
    {
        XAxis xAxis = chart.EnsureChartComponent<XAxis>();

        xAxis.ClearData();
        for (int i = 0; i < 40; i++)
        {
            xAxis.data.Add($"{i}");
        }

        chart.series[0].data.Clear();
        chart.series[1].data.Clear();
        chart.series[2].data.Clear();
    }

    private void ChartSetRuntimeProgressData(ref LineChart chart, int serieIndex, List<double> lineData)
    {
        Serie serie = chart.series[serieIndex];

        for (int i = 0; i < 40; i++)
        {
            if (lineData[i] == 0)
                break;
            else
                serie.AddData(lineData[i]);
        }
    }
    #endregion




    Serie RunTimeAddSeries(ref LineChart chart, Serie actualSerie)
    {
        Serie serie = chart.AddSerie<XCharts.Runtime.Line>($"Dash_{actualSerie.serieName}", true, false);

        serie.lineStyle = new LineStyle() {
            type = LineStyle.Type.Dashed,
            width = actualSerie.lineStyle.width,
            color = actualSerie.lineStyle.color,
        };

        serie.lineType = LineType.Smooth;
        serie.symbol.show = false;
        
        return serie;
    }


    #region ValueChange

    private (bool, bool) IsValueChange(bool[] check, int index)
    {
        if (index == 0) return (false, false);

        if (check[index] != check[index - 1])
        {
            return (true, check[index]);
        }
        return (false, false);
    }

    //private (bool, bool) IsValueChange(List<Avg> check, int index)
    //{
    //    if (index == 0) return (false, false);

    //    if (Mathf.Round((float)check[index].landingGear) != Mathf.Round((float)check[index - 1].landingGear))
    //    {
    //        return (true, Convert.ToBoolean(check[index].landingGear));
    //    }
    //    return (false, false);
    //}

    //SpecialCase (Refine code Remain)
    //private (bool, int) IsValueFlapChange(List<Avg> check, int index)
    //{
    //    if (index == 0) return (false, 0);

    //    if (check[index].flap != check[index - 1].flap)
    //    {
    //        return (true, Convert.ToInt32(check[index].flap));
    //    }
    //    return (false, 0);
    //}

    private (bool, bool) IsValueChange(float[] check, int index)
    {
        if (index == 0) return (false, false);

        if (check[index] != check[index - 1])
        {
            return (true, Convert.ToBoolean(check[index]));
        }
        return (false, false);
    }

    private (bool, bool) IsValueChange(List<bool> check, int index)
    {
        if (index == 0) return (false, false);

        if (check[index] != check[index - 1])
        {
            return (true, check[index]);
        }
        return (false, false);
    }

    private (bool, int) IsValueChange(int[] check, int index)
    {
        if (index == 0) return (false, 0);

        if (check[index] != check[index - 1])
        {
            return (true, check[index]);
        }
        return (false, 0);
    }


    private (bool, int) IsSpeedChange(int[] speed, int index, ref float lastSelectedSpeed)
    {
        if (index == 0)
        {
            lastSelectedSpeed = speed[0];
            return (true, speed[0]);
        }

        if (Mathf.Abs(speed[index] - lastSelectedSpeed) >= 20)
        {
            lastSelectedSpeed = speed[index];
            return (true, speed[index]);
        }

        return (false, speed[index]);
    }

    //private (bool, double) IsSpeedChange(List<Avg> speed, int index, ref double lastSelectedSpeed)
    //{
    //    if (index == 0)
    //    {
    //        lastSelectedSpeed = speed[0].speed;
    //        return (true, speed[0].speed);
    //    }

    //    if (Math.Abs(speed[index].speed - lastSelectedSpeed) >= 20)
    //    {
    //        lastSelectedSpeed = speed[index].speed;
    //        return (true, speed[index].speed);
    //    }

    //    return (false, speed[index].speed);
    //}

    private (bool, double) IsSpeedChange(double[] speed, int index, ref double lastSelectedSpeed)
    {
        if (index == 0)
        {
            lastSelectedSpeed = speed[0];
            return (true, speed[0]);
        }

        if (Math.Abs(speed[index] - lastSelectedSpeed) >= 20)
        {
            lastSelectedSpeed = speed[index];
            return (true, speed[index]);
        }

        return (false, speed[index]);
    }

    private (bool, int) IsSpeedChange(List<int> speed, int index, ref float lastSelectedSpeed)
    {
        if (index == 0)
        {
            lastSelectedSpeed = speed[0];
            return (true, speed[0]);
        }

        if (Mathf.Abs(speed[index] - lastSelectedSpeed) >= 20)
        {
            lastSelectedSpeed = speed[index];
            return (true, speed[index]);
        }

        return (false, speed[index]);
    }



    private (bool, bool) IsValueChange(List<double> check, int index)
    {
        if (index == 0) return (false, false);

        if (Mathf.RoundToInt((float)check[index]) != Mathf.RoundToInt((float)check[index - 1]))
        {
            return (true, Convert.ToBoolean(Mathf.RoundToInt((float)check[index])));
        }
        return (false, false);
    }
    private (bool, int) IsFlapValueChange(List<double> check, int index)
    {
        if (index == 0) return (false, 0);

        if (Mathf.RoundToInt((float)check[index]) != Mathf.RoundToInt((float)check[index - 1]))
        {
            return (true, Mathf.RoundToInt((float)check[index]));
        }
        return (false, 0);
    }
    private (bool, double) IsSpeedChange(List<double> speed, int index, ref double lastSelectedSpeed)
    {
        if (index == 0)
        {
            lastSelectedSpeed = speed[0];
            return (true, speed[0]);
        }

        if (Math.Abs(speed[index] - lastSelectedSpeed) >= 20)
        {
            lastSelectedSpeed = speed[index];
            return (true, speed[index]);
        }

        return (false, speed[index]);
    }

    #endregion

    #region Symbols

    private void AddArrowDownSymbol(ref SerieData serieData)
    {
        SerieSymbol _Symbol = serieData.EnsureComponent<SerieSymbol>();

        _Symbol.type = SymbolType.DownArrow;
        _Symbol.size = 0.05f;
    }

    private void AddArrowUpSymbol(ref SerieData serieData)
    {
        SerieSymbol _Symbol = serieData.EnsureComponent<SerieSymbol>();

        _Symbol.type = SymbolType.UpArrow;
        _Symbol.size = 0.05f;
    }

    private void AddFlapSymbol(ref SerieData serieData, int flapIndex)
    {
        SerieSymbol _Symbol = serieData.EnsureComponent<SerieSymbol>();

        _Symbol.type = SymbolType.Circle;
        _Symbol.size = 15f;

        LabelStyle _Label = serieData.EnsureComponent<LabelStyle>();
        switch (flapIndex)
        {
            case 0:
                _Label.formatter = "UP";
                break;
            case 1:
                _Label.formatter = "2";
                break;
            case 2:
                _Label.formatter = "5";
                break;
            case 3:
                _Label.formatter = "10";
                break;
            case 4:
                _Label.formatter = "15";
                break;
            case 5:
                _Label.formatter = "25";
                break;
            case 6:
                _Label.formatter = "30";
                break;
            case 7:
                _Label.formatter = "40";
                break;
        }
        //_Label.textStyle.show = true;
        //_Label.textStyle = new TextStyle()
        //{
        //    color = Color.white,
        //    fontSize = 15,
        //};
    }

    private void AddLabelSymbol(ref SerieData serieData, int knots)
    {
        LabelStyle _Label = serieData.EnsureComponent<LabelStyle>();

        _Label.formatter = $"{knots}";
        _Label.offset = new Vector3(0, 15, 0);
        _Label.rotate = 45;
        _Label.textStyle.show = true;
        _Label.textStyle = new TextStyle()
        {
            color = Color.white,
            fontSize = 15,
        };
        //_Label.size = 0.05f;
    }
    #endregion
}
