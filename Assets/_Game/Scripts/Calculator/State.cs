using System;
using Navigation;

public class State
{
    public ToggleLinkedBool LNAV { get; }
    public ToggleLinkedBool HDG { get; }

    private bool _locCaptured;
    public bool LOCCaptured
    {
        get => _locCaptured;
        set
        {
            if (_locCaptured == value)
                return;

            _locCaptured = value;
            if (value)
                LockLateralModes();
        }
    }

    public ToggleLinkedBool LC { get; }
    public ToggleLinkedBool VNAV { get; private set; }
    public ToggleLinkedBool AH { get; }
    public ToggleLinkedBool VS { get; }

    private bool _gsCaptured;
    public bool GSCaptured
    {
        get => _gsCaptured;
        set
        {
            if (_gsCaptured == value)
                return;

            _gsCaptured = value;
            if (value)
                LockVerticalModes();
        }
    }

    public bool AppArmed { get; set; }
    public bool LNAVArmed { get; }

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
        SyncSpeedWindow();
        AH = new ToggleLinkedBool(mcpUI._AHToggle, UIOnAHAttemptToggle);
        VS = new ToggleLinkedBool(mcpUI._VSToggle, UIOnVSAttemptToggle);

        _mapMode = new RotatingButtonLinkedValue(mcpUI._mapRotatingToggle, UIOnMapModeAttemptChange, (int)MapMode.Map);

        Speed10X = new ToggleLinkedBool(mcpUI._speed10XToggle, UIOnSpeed10XToggle);
    }

    private void LockLateralModes()
    {
        HDG.Set(false);
        LNAV.Set(false);
        HDG.SetInteractable(false);
        LNAV.SetInteractable(false);
    }

    private void LockVerticalModes()
    {
        VS.Set(false);
        VNAV.Set(false);
        AH.Set(false);
        LC.Set(false);

        VS.SetInteractable(false);
        VNAV.SetInteractable(false);
        AH.SetInteractable(false);
        LC.SetInteractable(false);

        NotifyVerticalModeChanged();
        SyncSpeedWindow();
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
        if (LOCCaptured)
            return;

        LNAV.Switch_Oneway();
        if (LNAV)
        {
            HDG.Set(false);
        }

        TriggerNewNAVState();
    }

    private void UIOnAHAttemptToggle()
    {
        if (GSCaptured)
            return;

        AH.Switch_Oneway();
        NotifyVerticalModeChanged();

        if (AH)
        {
            VNAV.Set(false);
            LC.Set(false);
            VS.Set(false);
        }

        SyncSpeedWindow();
    }

    private void UIOnLCAttemptToggle()
    {
        if (GSCaptured)
            return;

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

        SyncSpeedWindow();
    }

    private void UIOnVNAVAttemptToggle()
    {
        if (GSCaptured)
            return;

        VNAV.Switch_Oneway();

        if (VNAV)
        {
            LC.Set(false);
            AH.Set(false);
            VS.Set(false);
        }

        SyncSpeedWindow();
    }

    private void UIOnVSAttemptToggle()
    {
        if (GSCaptured)
            return;

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

        SyncSpeedWindow();
    }

    public void AutoSetAH(bool state)
    {
        if (GSCaptured && state)
            return;

        AH.Set(state);
        NotifyVerticalModeChanged();

        if (state)
        {
            VNAV.Set(false);
            VS.Set(false);
            LC.Set(false);
        }

        SyncSpeedWindow();
    }

    static void SyncSpeedWindow()
    {
        Calculator.Instance?.OnVerticalModeSelected();
    }
private static void NotifyVerticalModeChanged()
{
    Calculator.Instance?.Toggle_Change();
}

private void UIOnHDGAttemptToggle()
    {
        if (LOCCaptured)
            return;

        HDG.Switch_Oneway();

        if (HDG)
        {
            LNAV.Set(false);
        }

        TriggerNewNAVState();
    }

    public void AutoSetLNAV(bool state, bool silent)
    {
        if (LOCCaptured && state)
            return;

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
        if (LOCCaptured && state)
            return;

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
