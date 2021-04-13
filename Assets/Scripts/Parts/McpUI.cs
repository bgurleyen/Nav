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
    [SerializeField] Slider _SBSlider;

    public GameObject MCPSwitch;
    static bool CacheSilentSwitch;
    byte _mode = 1;

    void Awake()
    {
        hsToggle.onValueChanged.AddListener(OnHS);
        lNavToggle.onValueChanged.AddListener(OnLNav);
    }


    public void MCPSwitch_Click()
    {
        _mode += 1;
        if (_mode > 3) _mode = 1;
        if (_mode == 1)
        {
            Drawer.Instance.ShowMapMode();
            MCPSwitch.transform.localEulerAngles = new Vector3(-90, 0, -35);
        }

        if (_mode == 2)
        {
            Drawer.Instance.ShowCenterMode();
            MCPSwitch.transform.localEulerAngles = new Vector3(-90, 0, 0);
        }

        if (_mode == 3)
        {
            Drawer.Instance.ShowPlanMode();
            MCPSwitch.transform.localEulerAngles = new Vector3(-90, 0, 40);
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

    static void OnHS(bool toggle)
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

    static void OnLNav(bool toggle)
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


    static void SilentSwitchHeading(bool value)
    {
        CacheSilentSwitch = true;
        Instance.hsToggle.isOn = value;
    }

    static void SilentSwitchLNAV(bool value)
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
