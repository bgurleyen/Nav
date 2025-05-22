using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PlayerPrefsHolder
{
    public static int Level
    {
        get
        {
            return PlayerPrefs.GetInt("Level", 0);
        }
        set
        {
            PlayerPrefs.SetInt("Level", value);
        }
    }

    public static int Agree
    {
        get
        {
            return PlayerPrefs.GetInt("Agree", 0);
        }
        set
        {
            PlayerPrefs.SetInt("Agree", value);
        }
    }

    public static string UserName
    {
        get
        {
            return PlayerPrefs.GetString("UserName",null);
        }
        set
        {
            PlayerPrefs.SetString("UserName", value);
        }
    }
}
