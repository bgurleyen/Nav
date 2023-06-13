using Navigation;

public class State
{
    public ToggleLinkedBool LNAV { get; }
    public ToggleLinkedBool HDG { get; }
    
    public ToggleLinkedBool LC { get; }
    public ToggleLinkedBool VNAV { get; private set; }
    public ToggleLinkedBool AH { get; }
    public ToggleLinkedBool VS { get; }

    public MapMode MapMode
    {
        get => (MapMode)(int)_mapMode;

        private set => _mapMode.Set((int)value);
    }


    private RotatingButtonLinkedValue _mapMode { get; }

    private readonly Drawer _linkedDrawer;

    public State(McpUI mcpUI, Drawer drawer)
    {
        _linkedDrawer = drawer;

        LNAV = new ToggleLinkedBool(mcpUI.lNavToggle, UIOnLNAVAttemptToggle);
        HDG = new ToggleLinkedBool(mcpUI.hsToggle, UIOnHDGAttemptToggle);
        
        LC = new ToggleLinkedBool(mcpUI._LCToggle, UIOnLCAttemptToggle);
        VNAV = new ToggleLinkedBool(mcpUI._VNAVToggle);
        AH = new ToggleLinkedBool(mcpUI._AHToggle, UIOnAHAttemptToggle);
        VS = new ToggleLinkedBool(mcpUI._VSToggle, UIOnVSAttemptToggle);

        _mapMode = new RotatingButtonLinkedValue(mcpUI._mapRotatingToggle, UIOnMapModeAttemptChange, (int)MapMode.Map);

    }

    private void UIOnMapModeAttemptChange()
    {
        _mapMode.Increment();
        _linkedDrawer.OnUIMapModeSet();
    }

    public void AutoSetMapMode(MapMode mode)
    {
        MapMode = mode;
        
        _linkedDrawer.OnUIMapModeSet();
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
