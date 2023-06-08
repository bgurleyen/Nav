using Navigation;

public class State
{
    public ToggleLinkedBool LNAV { get; }
    public ToggleLinkedBool HDG { get; }
    
    public ToggleLinkedBool LC { get; }
    public ToggleLinkedBool VNAV { get; private set; }
    public ToggleLinkedBool AH { get; }
    public ToggleLinkedBool VS { get; }
    public MapMode MapMode { get; private set; }

    private readonly McpUI _mcpUI;

    public State(McpUI mcpUI)
    {
        _mcpUI = mcpUI;

        LNAV = new ToggleLinkedBool(mcpUI.lNavToggle, UIOnLNAVAttemptToggle);
        HDG = new ToggleLinkedBool(mcpUI.hsToggle, UIOnHDGAttemptToggle);
        
        LC = new ToggleLinkedBool(mcpUI._LCToggle, UIOnLCAttemptToggle);
        VNAV = new ToggleLinkedBool(mcpUI._VNAVToggle);
        AH = new ToggleLinkedBool(mcpUI._AHToggle, UIOnAHAttemptToggle);
        VS = new ToggleLinkedBool(mcpUI._VSToggle, UIOnVSAttemptToggle);

        _mcpUI.OnMapModeSet += UIOnMapModeSet;
    }

    private void UIOnMapModeSet(MapMode mode)
    {
        Session.Mode = mode;
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
        else if(LNAV)
        {
            if (!Session.PlayerAircraft.IsOnRoute)
            {
                Session.PlayerAircraft.StartLNavMode();
            }
        }
    }
}
