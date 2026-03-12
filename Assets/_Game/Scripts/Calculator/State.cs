using System;
using Navigation;

public class State
{
    public ToggleLinkedBool LNAV { get; }
    public ToggleLinkedBool HDG { get; }
    public bool LOCCaptured { get; set; }

    public ToggleLinkedBool LC { get; }
    public ToggleLinkedBool VNAV { get; private set; }
    public ToggleLinkedBool AH { get; }
    public ToggleLinkedBool VS { get; }
    public bool GSCaptured { get; set; }
    public bool AppArmed { get; set; }
    public bool LNAVArmed { get; }
    public bool ILSCapture { get; set; }

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
        VNAV = new ToggleLinkedBool(mcpUI._VNAVToggle, UIOnVNAVAttemptToggle);
        VNAV.Set(true);
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
        LNAV.Switch_Oneway();
        if (LNAV)
        {
            HDG.Set(false);
        }

        TriggerNewNAVState();
    }

    private void UIOnAHAttemptToggle()
    {
        AH.Switch_Oneway();
        NotifyVerticalModeChanged();

        if (AH)
        {
            VNAV.Set(false);
            LC.Set(false);
            VS.Set(false);
        }
    }

    private void UIOnLCAttemptToggle()
    {
        if (Calculator.RAltitude == Calculator.CAltitude)
        {
            return;
        }

        LC.Switch_Oneway();

        if (LC)
        {
            VNAV.Set(false);
            AH.Set(false);
            VS.Set(false);
        }
    }

    private void UIOnVNAVAttemptToggle()
    {
        VNAV.Switch_Oneway();

        if (VNAV)
        {
            LC.Set(false);
            AH.Set(false);
            VS.Set(false);
        }
    }

    private void UIOnVSAttemptToggle()
    {
        if (Calculator.RAltitude == Calculator.CAltitude)
        {
            return;
        }

        VS.Switch_Oneway();
        NotifyVerticalModeChanged();

        if (VS)
        {
            VNAV.Set(false);
            AH.Set(false);
            LC.Set(false);
        }
    }

    public void AutoSetAH(bool state)
    {
        AH.Set(state);
        NotifyVerticalModeChanged();

        if (state)
        {
            VNAV.Set(false);
            VS.Set(false);
            LC.Set(false);
        }
    }
private static void NotifyVerticalModeChanged()
{
    Calculator.Instance?.Toggle_Change();
}

private void UIOnHDGAttemptToggle()
    {
        HDG.Switch_Oneway();

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
