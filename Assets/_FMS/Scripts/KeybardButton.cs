using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeybardButton : MonoBehaviour
{
    public void OnClickPress(int value)
    {
        FMC_Screens.Instance.legsScreen.OnNumberPressed(value);
    }

    public void OnClickPressDecimal()
    {
        FMC_Screens.Instance.legsScreen.OnDecimalPressed();

    }

    public void OnSlashPress()
    {
        FMC_Screens.Instance.legsScreen.OnSlashPressed();
    }

    public void OnSignPress()
    {
        FMC_Screens.Instance.legsScreen.OnSignPressed();
    }

    public void OnDeletePress()
    {

        FMC_Screens.Instance.legsScreen.OnDeletePress();
    }
}
