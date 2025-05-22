using System;
using Navigation;
using UnityEngine;
using UnityEngine.UI;
using Unyawn.Utils;

public class McpUI : MonoBehaviour
{
    public ToggleButton3DLinker hsToggle;
    public ToggleButton3DLinker lNavToggle;
    
    public ToggleButton3DLinker _VNAVToggle;
    public ToggleButton3DLinker _LCToggle;
    public ToggleButton3DLinker _AHToggle;
    public ToggleButton3DLinker _VSToggle;
    public RotatingRadioButton3DLinker _mapRotatingToggle;
    
    public ToggleButtonLinker _speed10XToggle;
    
    public Text headingText;
    public Slider _SBSlider;
    public Slider _LGSlider;

    public Action<MapMode> OnMapModeSet;

    private bool _cacheSilentSwitch;
    private bool _cacheSilentSwitch_LG;

    private void Awake()
    {
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
            OnMapModeSet?.Invoke(MapMode.Map);
        }

        if (mode == 1)
        {
            OnMapModeSet?.Invoke(MapMode.Center);
        }

        if (mode == 2)
        {
            OnMapModeSet?.Invoke(MapMode.Plan);
        }
    }

    public void RefreshHS()
    {
        headingText.text = Calculator.RHeading.ToString();
    }


   


    public void SBLeverInteract(bool down, bool isSilent = false)
    {
        _cacheSilentSwitch = isSilent;
        _SBSlider.value = down ? 0 : 1;
    }

    public void LGLeverInteract(bool down, bool isSilent = false)
    {
        _cacheSilentSwitch_LG = isSilent;
        _LGSlider.value = down ? 1 : 0;
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

    public void OnLGSliderChanged(float newValue)
    {
        if (_cacheSilentSwitch_LG)
        {
            _cacheSilentSwitch_LG = false;
            return;
        }

        Calculator.Instance?.SetLG(newValue == 0, true);
    }
}
