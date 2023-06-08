using System;
using Navigation;

public class State
{
    public ToggleLinkedBool LNAV { get; private set; }
    public ToggleLinkedBool HDG { get; private set; }
    
    public ToggleLinkedBool LC { get; private set; }
    public ToggleLinkedBool VNAV { get; private set; }
    public ToggleLinkedBool AH { get; private set; }
    public ToggleLinkedBool VS { get; private set; }
    public MapMode MapMode { get; private set; }

    private readonly McpUI _mcpUI;

    public State(McpUI mcpUI)
    {
        _mcpUI = mcpUI;

        LNAV = new ToggleLinkedBool(mcpUI.lNavToggle, UIOnLNAVAttemptToggle);
        HDG = new ToggleLinkedBool(mcpUI.hsToggle, UIOnHDGAttemptToggle);
        
        LC = new ToggleLinkedBool(mcpUI._LCToggle);
        VNAV = new ToggleLinkedBool(mcpUI._VNAVToggle);
        AH = new ToggleLinkedBool(mcpUI._AHToggle, UIOnAHAttemptToggle);
        VS = new ToggleLinkedBool(mcpUI._VSToggle);

        _mcpUI.OnMapModeSet += UIOnMapModeSet;
    }

    private void UIOnMapModeSet(MapMode mode)
    {
        Session.Mode = mode;
    }

    private void UIOnLNAVAttemptToggle()
    {
        LNAV.Switch();
    }

    private void UIOnAHAttemptToggle()
    {
        AH.Switch();
    }
    
    public void AutoSetAH(bool state)
    {
        AH.Set(state);
    }

    private void UIOnHDGAttemptToggle()
    {
        if (HDG)
        {
            Session.PlayerAircraft.StartHeadingMode();
        }
        else
        {
            if (!Session.PlayerAircraft.IsOnRoute)
            {
                Session.PlayerAircraft.StartLNavMode();
            }
        }
    }

}
