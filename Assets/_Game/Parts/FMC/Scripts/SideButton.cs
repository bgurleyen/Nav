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
        if (IsLeft(index, out var lineIndex, out var isExtra))
        {
            if (!isExtra)
            {
                //Debug.Log("*** lineIndex: " + lineIndex);
                FMC_Screens.Instance.CurrentScreen.OnLineSelectLeft(lineIndex);
            }
            else
            {
                //Debug.Log("OnLeftCornerPress");
                FMC_Screens.Instance.CurrentScreen.OnLeftCornerPress();
            }
        }
        else
        {
            if (!isExtra)
            {
                FMC_Screens.Instance.CurrentScreen.OnLineSelectRight(lineIndex);
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
