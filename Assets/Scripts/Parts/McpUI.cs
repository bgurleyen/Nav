using System;
using System.Collections;
using System.Collections.Generic;
using Gamelogic.Extensions;
using UnityEngine;
using UnityEngine.UI;

public class McpUI : Singleton<McpUI>
{
    [SerializeField] Toggle hsToggle;
    [SerializeField] Toggle lNavToggle;
    [SerializeField] Text headingText;
    
    byte mode = 1;
    public GameObject MCPSwitch;
    void Awake()
    {
        hsToggle.onValueChanged.AddListener(OnHS);
        lNavToggle.onValueChanged.AddListener(OnLNav);
    }

    public void MCPSwitch_Click()
    {
        mode += 1;
        if (mode > 3) mode = 1;
        if (mode == 1)
        {
            Drawer.Instance.ShowMapMode();
            MCPSwitch.transform.localEulerAngles = new Vector3(-90, 0, -35);
        }

        if (mode == 2)
        {
            Drawer.Instance.ShowCenterMode();
            MCPSwitch.transform.localEulerAngles = new Vector3(-90, 0, 0);
        }

        if (mode == 3)
        {
            Drawer.Instance.ShowPlanMode();
            MCPSwitch.transform.localEulerAngles = new Vector3(-90, 0, 40);
        }
    }

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


    static void OnHS(bool toggle)
    {
        if (!toggle) return;
        GameManager.Instance.SwitchFreeFlight(true);
    }

    static void OnLNav(bool toggle)
    {
        if (!toggle) return;
        GameManager.Instance.SwitchFreeFlight(false);
    }


    public void RefreshHS()
    {
        headingText.text = Calculator.RHeading.ToString();
    }
}
