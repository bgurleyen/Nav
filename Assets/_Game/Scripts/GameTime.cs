using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gamelogic.Extensions;

public class GameTime : Singleton<GameTime>
{
    public static float timer;
    public static string timerFormatted;
    public static string timerFMC;

    private void Update()
    {
        timer = Time.realtimeSinceStartup;
        timerFormatted = FormatTime(timer);
        timerFMC = FormatFMCTime(timer);
    }
    public static string FormatTime(float time)
    {
        return Mathf.Floor(time / 3600).ToString("00") + ":" +
                 (Mathf.Floor(time / 60)- Mathf.Floor(time / 3600)*60).ToString("00") + ":"+
                 Mathf.FloorToInt(time % 60).ToString("00");
    }
    public static string FormatFMCTime(float time)
    {
        return Mathf.Floor(time / 3600).ToString("00") + 
                 (Mathf.Floor(time / 60) - Mathf.Floor(time / 3600) * 60).ToString("00") + "z";
    }

}
