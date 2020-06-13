using System;
using Gamelogic.Extensions;
using UnityEngine;

public class FMC_Screens : Singleton<FMC_Screens>
{
    [SerializeField] ScreenBase legsScreen;
    
    public ScreenBase CurrentScreen { get; private set; }


    void Start()
    {
        ShowPage(legsScreen);
    }

    void ShowPage(ScreenBase newPage)
    {
        if (CurrentScreen != null)
        {
            CurrentScreen.Hide();
        }

        CurrentScreen = newPage;
        CurrentScreen.Show();
    }
}
