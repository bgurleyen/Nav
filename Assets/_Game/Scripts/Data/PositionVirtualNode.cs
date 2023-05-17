using Navigation;

public class PositionVirtualNode
{

    public static int PassedNodeIndex => Session.PlayerAircraft.PositionFreeOrClosestOnRouteSegment.CurrentNodeIndex - 1;
    public static int PassedNodeIndexForMode => Session.PlayerAircraft.IsOnRoute 
        ? Session.PlayerAircraft.PositionFreeOrClosestOnRouteSegment.CurrentNodeIndex - 1 
        : 0;

    public static int NextNodeIndex => !Session.PlayerAircraft.IsOnRoute && Session.IsMod
        ? 1
        : Session.PlayerAircraft.PositionFreeOrClosestOnRouteSegment.CurrentNodeIndex;
    public static RoutePoint GetNodeFrom => Session.ActiveRoute.Points[PassedNodeIndex];
    public static RoutePoint GetNodeTo => Session.ActiveRoute.Points[PassedNodeIndex + 1]; // todo may need to be adjusted : line goes passed the node, sometimes with a lot
    public static TracedLine CurrentTracedLine => Session.ActiveRoute.TracedRoute.ComputedLines[PassedNodeIndex + 1];
}
