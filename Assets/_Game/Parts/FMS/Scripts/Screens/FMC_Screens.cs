using System;
using Gamelogic.Extensions;
using UnityEngine;

public class FMC_Screens : Singleton<FMC_Screens>
{
    [SerializeField] private ScreenBase legsScreen;
    
    public ScreenBase CurrentScreen { get; private set; }


    private void Start()
    {
        ShowPage(legsScreen);
    }

    private void ShowPage(ScreenBase newPage)
    {
        if (CurrentScreen != null)
        {
            CurrentScreen.Hide();
        }

        CurrentScreen = newPage;
        CurrentScreen.Show();
    }
}
