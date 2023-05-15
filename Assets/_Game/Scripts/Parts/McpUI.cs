using System;
using System.Collections;
using System.Collections.Generic;
using Gamelogic.Extensions;
using UnityEngine;
using UnityEngine.UI;

public class McpUI : Singleton<McpUI>
{
    [SerializeField] private Toggle hsToggle;
    [SerializeField] private Toggle lNavToggle;
    [SerializeField] private Text headingText;
    [SerializeField] private Slider _SBSlider;

    private static bool CacheSilentSwitch;

    private void Awake()
    {
        hsToggle.onValueChanged.AddListener(OnHS);
        lNavToggle.onValueChanged.AddListener(OnLNav);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="mode"> 0 = M, 1 = C, 2 = P</param>
    public void MCPSwitch_Click(float mode)
    {
        if (mode == 0)
        {
            Drawer.Instance.ShowMapMode();
        }

        if (mode == 1)
        {
            Drawer.Instance.ShowCenterMode();
        }

        if (mode == 2)
        {
            Drawer.Instance.ShowPlanMode();
        }
    }


    public void RefreshHS()
    {
        headingText.text = Calculator.RHeading.ToString();
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

    private static void OnHS(bool toggle)
    {
        if (CacheSilentSwitch)
        {
            CacheSilentSwitch = false;
        }
        else
        {
            if (toggle)
            {
                GameManager.Instance.PressSwitchFreeFlight(true);
                SilentSwitchLNAV(false);
            }
        }
    }

    private static void OnLNav(bool toggle)
    {
        if (CacheSilentSwitch)
        {
            CacheSilentSwitch = false;
        }
        else
        {
            if (toggle)
            {
                GameManager.Instance.PressSwitchFreeFlight(false);
                SilentSwitchHeading(false);
            }
        }
    }


    private static void SilentSwitchHeading(bool value)
    {
        CacheSilentSwitch = true;
        Instance.hsToggle.isOn = value;
    }

    private static void SilentSwitchLNAV(bool value)
    {
        CacheSilentSwitch = true;
        Instance.lNavToggle.isOn = value;
    }

    public void SBLeverInteract(bool down, bool isSilent = false)
    {
        CacheSilentSwitch = isSilent;
        _SBSlider.value = down ? 0 : 1;
    }

    
    public void OnSBSliderChanged(float newValue)
    {
        if (CacheSilentSwitch)
        {
            CacheSilentSwitch = false;
            return;
        }

        Calculator.Instance.SetSB(newValue == 0, true);
    }
}
