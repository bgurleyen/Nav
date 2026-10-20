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
    public Slider _LGSlider;

    private bool _cacheSilentSwitch_LG;

    private void Awake()
    {
        UYServiceLocator.Register(this);
    }

    
    public void RefreshHS()
    {
        headingText.text = Calculator.RHeading.ToString();
    }


   


    public void LGLeverInteract(bool down, bool isSilent = false)
    {
        if (_LGSlider == null) return;
        float target = Calculator.SliderValueForGear(down);
        if (Mathf.Approximately(_LGSlider.value, target))
            return;
        _cacheSilentSwitch_LG = isSilent;
        _LGSlider.value = target;
    }

    private void LateUpdate()
    {
        RefreshGearLeverLock();
    }

    /// <summary>
    /// Over 270 kt or M0.82 the gear lever must stay up. Retract is still allowed.
    /// </summary>
    void RefreshGearLeverLock()
    {
        if (_LGSlider == null) return;

        bool locked = Calculator.GearLeverLocked();
        if (!Calculator.LGDown && locked && Calculator.GearExtendedFromSlider(_LGSlider.value))
            LGLeverInteract(false, true);
    }


    public void OnSBSliderChanged(float newValue)
    {
        Calculator.Instance?.SetSB(newValue==0, true);
    }

    public void OnLGSliderChanged(float newValue)
    {
        if (_cacheSilentSwitch_LG)
        {
            _cacheSilentSwitch_LG = false;
            return;
        }

        bool wantDown = Calculator.GearExtendedFromSlider(newValue);
        if (wantDown && Calculator.GearLeverLocked())
        {
            LGLeverInteract(false, true);
            NdPauseOverlay.NotifyCheckSpeed();
            return;
        }

        Calculator.Instance?.SetLG(wantDown, true);
    }
}
