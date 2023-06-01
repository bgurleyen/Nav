using System;
using UnityEngine;
using UnityEngine.UI;
using Unyawn.Utils;

public class McpUI : MonoBehaviour
{
    [SerializeField] private Toggle hsToggle;
    [SerializeField] private Toggle lNavToggle;
    [SerializeField] private Text headingText;
    [SerializeField] private Slider _SBSlider;

    public Action OnMapModeSet;
    public Action OnCenterModeSet;
    public Action OnPlanModeSet;
    public Action<bool> OnFreeFlightToggle;

    private bool _cacheSilentSwitch;

    private void Awake()
    {
        hsToggle.onValueChanged.AddListener(OnHS);
        lNavToggle.onValueChanged.AddListener(OnLNav);
        
        UYServiceLocator.Register(this);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="mode"> 0 = M, 1 = C, 2 = P</param>
    public void MCPSwitch_Click(float mode)
    {
        if (mode == 0)
        {
            OnMapModeSet?.Invoke();
        }

        if (mode == 1)
        {
            OnCenterModeSet?.Invoke();
        }

        if (mode == 2)
        {
            OnPlanModeSet?.Invoke();
        }
    }


    public void RefreshHS()
    {
        headingText.text = Calculator.RHeading.ToString();
    }



    private void OnHS(bool toggle)
    {
        if (_cacheSilentSwitch)
        {
            _cacheSilentSwitch = false;
        }
        else
        {
            if (toggle)
            {
                SilentSwitchLNAV(false);
                
                OnFreeFlightToggle?.Invoke(true);
            }
        }
    }

    private void OnLNav(bool toggle)
    {
        if (_cacheSilentSwitch)
        {
            _cacheSilentSwitch = false;
        }
        else
        {
            if (toggle)
            {
                SilentSwitchHeading(false);
                
                OnFreeFlightToggle?.Invoke(false);
            }
        }
    }


    private void SilentSwitchHeading(bool value)
    {
        _cacheSilentSwitch = true;
        hsToggle.isOn = value;
    }

    private void SilentSwitchLNAV(bool value)
    {
        _cacheSilentSwitch = true;
        lNavToggle.isOn = value;
    }

    public void SBLeverInteract(bool down, bool isSilent = false)
    {
        _cacheSilentSwitch = isSilent;
        _SBSlider.value = down ? 0 : 1;
    }

    
    public void OnSBSliderChanged(float newValue)
    {
        if (_cacheSilentSwitch)
        {
            _cacheSilentSwitch = false;
            return;
        }

        Calculator.Instance?.SetSB(newValue == 0, true);
    }
}
