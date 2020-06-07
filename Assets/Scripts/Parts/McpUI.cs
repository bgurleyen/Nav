using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class McpUI : MonoBehaviour
{
    public void OnMapMode(bool toggle)
    {
        if (!toggle) return;

        Drawer.Instance.ShowMapMode();
    }

    public void OnCenterMode(bool toggle)
    {
        if (!toggle) return;

        Drawer.Instance.ShowCenterMode();
    }

    public void OnPlanMode(bool toggle)
    {
        if (!toggle) return;

        Drawer.Instance.ShowPlanMode();
    }


    public void OnHS(bool toggle)
    {
        if (!toggle) return;
        GameManager.Instance.SwitchFreeFlight(true);
    }

    public void OnLNav(bool toggle)
    {
        if (!toggle) return;
        GameManager.Instance.SwitchFreeFlight(false);
    }
    
    
}
