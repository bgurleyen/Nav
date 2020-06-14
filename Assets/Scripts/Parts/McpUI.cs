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

    void Awake()
    {
        hsToggle.onValueChanged.AddListener(OnHS);
        lNavToggle.onValueChanged.AddListener(OnLNav);
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


    public void TriggerOnHS(int heading)
    {
        Calculator.RHeading = heading;
        headingText.text = Calculator.RHeading.ToString();
        hsToggle.isOn = true;
    }

    public void TriggerOnLNav()
    {
        lNavToggle.isOn = true;
    }
}
