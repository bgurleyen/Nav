using Gamelogic.Extensions;
using UnityEngine;

public class DP_Compass : Singleton<DP_Compass>
{
    public Animator cameraAnimator;

    public void OnMapMode(bool toggle)
    {
        if (!toggle) return;
        cameraAnimator.SetTrigger("Map");

        Drawer.Instance.ShowMapMode();
    }

    public void OnCenterMode(bool toggle)
    {
        if (!toggle) return;
        cameraAnimator.SetTrigger("Center");

        Drawer.Instance.ShowCenterMode();
    }

    public void OnPlanMode(bool toggle)
    {
        if (!toggle) return;
        cameraAnimator.SetTrigger("Center");

        Drawer.Instance.ShowPlanMode();
    }

}
