using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SideButton : MonoBehaviour
{
    private int index;

    private void Awake()
    {
        index = int.Parse(name);
    }

    public void OnClick()
    {
        if (IsLeft(index, out var _lineIndex, out var _isExtra))
        {
            if (!_isExtra)
            {
                FMC_Screens.Instance.CurrentScreen.OnLineSelectLeft(_lineIndex);
            }
            else
            {
                FMC_Screens.Instance.CurrentScreen.OnLeftCornerPress();
            }
        }
        else
        {
            if (!_isExtra)
            {
                FMC_Screens.Instance.CurrentScreen.OnLineSelectRight(_lineIndex);
            }
            else
            {
                FMC_Screens.Instance.CurrentScreen.OnRightCornerPress();
            }
        }
    }

    private static bool IsLeft(int index, out int lineIndex, out bool isExtra)
    {
        lineIndex = Mathf.Abs(index) - 1;
        isExtra = lineIndex == 5;
        return index > 0;
    }
}
