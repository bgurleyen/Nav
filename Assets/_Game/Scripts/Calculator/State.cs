using System;
using Navigation;

public class State
{
    public ToggleLinkedBool LNAV { get; }
    public ToggleLinkedBool HDG { get; }
    
    public ToggleLinkedBool LC { get; }
    public ToggleLinkedBool VNAV { get; private set; }
    public ToggleLinkedBool AH { get; }
    public ToggleLinkedBool VS { get; }
    
    public ToggleLinkedBool Speed10X { get; }

    public MapMode MapMode
    {
        get => (MapMode)(int)_mapMode;

        private set
        {
            _mapMode.Set((int)value);
            OnMapModeChanged?.Invoke(MapMode);
        }
    }

    public Action<MapMode> OnMapModeChanged;


    private RotatingButtonLinkedValue _mapMode { get; }

    public State(McpUI mcpUI)
    {

        LNAV = new ToggleLinkedBool(mcpUI.lNavToggle, UIOnLNAVAttemptToggle);
        HDG = new ToggleLinkedBool(mcpUI.hsToggle, UIOnHDGAttemptToggle);
        
        LC = new ToggleLinkedBool(mcpUI._LCToggle, UIOnLCAttemptToggle);
        VNAV = new ToggleLinkedBool(mcpUI._VNAVToggle);
        AH = new ToggleLinkedBool(mcpUI._AHToggle, UIOnAHAttemptToggle);
        VS = new ToggleLinkedBool(mcpUI._VSToggle, UIOnVSAttemptToggle);

        _mapMode = new RotatingButtonLinkedValue(mcpUI._mapRotatingToggle, UIOnMapModeAttemptChange, (int)MapMode.Map);

        Speed10X = new ToggleLinkedBool(mcpUI._speed10XToggle, UIOnSpeed10XToggle);
    }

    private void UIOnSpeed10XToggle()
    {
        Speed10X.Switch();
        Session.Settings.SpeedMultiplier = Speed10X ? 10 : 1;
    }

    private void UIOnMapModeAttemptChange()
    {
        _mapMode.Increment();
        OnMapModeChanged?.Invoke(MapMode);
    }

    public void AutoSetMapMode(MapMode mode)
    {
        MapMode = mode;
    }

    private void UIOnLNAVAttemptToggle()
    {
        LNAV.Switch();
        if (LNAV)
        {
            HDG.Set(false);
        }

        TriggerNewNAVState();
    }

    private void UIOnAHAttemptToggle()
    {
        AH.Switch();
    }

    private void UIOnLCAttemptToggle()
    {
        if (Calculator.RAltitude == Calculator.CAltitude)
        {
            return;
        }

        LC.Switch();

       
    }

    private void UIOnVSAttemptToggle()
    {
        if (Calculator.RAltitude == Calculator.CAltitude)
        {
            return;
        }
        
        VS.Switch();
    }

    public void AutoSetAH(bool state)
    {
        AH.Set(state);
    }

    private void UIOnHDGAttemptToggle()
    {
        HDG.Switch();

        if (HDG)
        {
            LNAV.Set(false);
        }
        
        TriggerNewNAVState();
    }

    public void AutoSetLNAV(bool state, bool silent)
    {
        if (LNAV == state)
        {
            return;
        }
        
        LNAV.Set(state);

        if (state)
        {
            HDG.Set(false);
        }

        if (!silent)
        {
            TriggerNewNAVState();
        }
    }

    public void AutoSetHDG(bool state)
    {
        HDG.Set(state);
        if (state)
        {
            LNAV.Set(false);
        }
        
        TriggerNewNAVState();
    }

    private void TriggerNewNAVState()
    {
        if (HDG)
        {
            Session.PlayerAircraft.StartHeadingMode();
        }
        else if (LNAV)
        {
            Session.PlayerAircraft.TryRejoinRoute();
        }
    }
}
