using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SideButton : MonoBehaviour
{
    public void OnClick(int index)
    {
        if (IsLeft(index, out var lineIndex, out bool isExtra))
        {
            if (!isExtra)
            {
                FMC_Screens.Instance.OnNodeButtonLeft(lineIndex);
            }
            else
            {
                FMC_Screens.Instance.OnExtraButtonLeft();
            }
        }
        else
        {
            if (!isExtra)
            {
                FMC_Screens.Instance.OnNodeButtonRight(lineIndex);
            }
            else
            {
                FMC_Screens.Instance.OnExtraButtonRight();
            }
        }
    }

    bool IsLeft(int index, out int lineIndex, out bool isExtra)
    {
        lineIndex = Mathf.Abs(index) - 1;
        isExtra = lineIndex == 5;
        return index > 0;
    }
}
