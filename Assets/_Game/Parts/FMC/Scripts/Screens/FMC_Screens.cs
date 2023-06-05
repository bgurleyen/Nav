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
    [SerializeField] private ScreenBase _crzScreen;
    [SerializeField] private ScreenBase _desScreen;
    [SerializeField] private ScreenBase _arrScreen;
    [SerializeField] private ScreenBase _progScreen;
    [SerializeField] private ScreenBase _fixScreen;
    
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
            case FMCScreens.Crz:
                ShowPage(_crzScreen);
                break;
            case FMCScreens.Des:
                ShowPage(_desScreen);
                break;
            case FMCScreens.Arr:
                ShowPage(_arrScreen);
                break;
            case FMCScreens.Prog:
                ShowPage(_progScreen);
                break;
            case FMCScreens.Fix:
                ShowPage(_fixScreen);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(screen), screen, null);
        }
    }
}
