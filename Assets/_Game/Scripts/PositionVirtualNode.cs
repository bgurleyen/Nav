using Navigation;

public class PositionVirtualNode
{
    public static int PassedNodeIndex => Session.PlayerAircraft.CurrentSegmentIndex - 1;
    public static int PassedNodeIndexForMode => Session.State.LNAV
        ? Session.PlayerAircraft.CurrentSegmentIndex - 1 
        : 0;

    public static int NextNodeIndex => Session.PlayerAircraft.CurrentSegmentIndex;
    public static RoutePoint GetNodeFromOnActive => Session.ActiveRoute.Points[PassedNodeIndex];
    public static RoutePoint GetNodeToOnActive => Session.ActiveRoute.Points[PassedNodeIndex + 1]; // todo may need to be adjusted : line goes passed the node, sometimes with a lot
    public static TracedLine CurrentTracedLine => Session.ActiveRoute.TracedRoute.ComputedLines[PassedNodeIndex + 1];
}
