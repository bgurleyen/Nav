using System;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;

public class FMC_Screens : Singleton<FMC_Screens>
{
    [SerializeField] private FMCScreens _startingScreen;
    [SerializeField] private ScreenBase _menuScreen;
    [SerializeField] private ScreenBase _legsScreen;
    [SerializeField] private ScreenBase _initScreen;
    [SerializeField] private ScreenBase _rteScreen;
    
    public ScreenBase CurrentScreen { get; private set; }


    private void Start()
    {
        _menuScreen.Hide();
        _legsScreen.Hide();
        _initScreen.Hide();
        _rteScreen.Hide();
        
        ShowPage(_startingScreen);
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

    public void ShowPage(FMCScreens screen)
    {
        switch (screen)
        {
            case FMCScreens.Menu:
                ShowPage(_menuScreen);
                break;
            case FMCScreens.Legs:
                ShowPage(_legsScreen);
                break;
            case FMCScreens.Init:
                ShowPage(_initScreen);
                break;
            case FMCScreens.Rte:
                ShowPage(_rteScreen);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(screen), screen, null);
        }
    }
}
