using UnityEngine;

public class SideButton : MonoBehaviour
{
    private int index;

    private void Awake()
    {
        if (!int.TryParse(name, out index))
            index = 0;
    }

    public void OnClick()
    {
        var screen = FMC_Screens.Instance != null ? FMC_Screens.Instance.CurrentScreen : null;
        if (screen == null)
            return;

        if (IsLeft(index, out var lineIndex, out var isExtra))
        {
            if (!isExtra)
            {
                screen.OnLineSelectLeft(lineIndex);
            }
            else
            {
                screen.OnLeftCornerPress();
            }
        }
        else
        {
            if (!isExtra)
            {
                screen.OnLineSelectRight(lineIndex);
            }
            else
            {
                screen.OnRightCornerPress();
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
