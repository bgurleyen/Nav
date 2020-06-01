using Gamelogic.Extensions;
using UnityEngine;

public class FMC_Screens : Singleton<FMC_Screens>
{
    public LegsScreen legsScreen;

    private void Update()
    {
        ShowLegsPage();
    }

    void ShowLegsPage()
    {
        legsScreen.DisplayCurrentPage();
    }

    public void OnPressNext()
    {
        legsScreen.DisplayNextPage();
    }

    public void OnPressPrev()
    {
        legsScreen.DisplayPrevPage();
    }

 
    public void OnNodeButtonLeft(int line)
    {
        legsScreen.OnLineSelectLeft(line);
    }

    public void OnNodeButtonRight(int line)
    {
    }

    public void OnExtraButtonLeft()
    {
         legsScreen.OnErasePress();

    }


    public void OnExtraButtonRight()
    {
         legsScreen.OnRightCornerPress();
    }


    public void OnExecPress()
    {
        legsScreen.OnExecPress();
    }
}
