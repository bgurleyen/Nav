using Navigation;
using UnityEngine;

public class PositionVirtualNode
{
    public static int PassedNodeIndex =>
        Session.PlayerAircraft != null ? Session.PlayerAircraft.CurrentSegmentIndex - 1 : 0;
    public static int PassedNodeIndexForMode =>
        Session.State != null && Session.State.LNAV && Session.PlayerAircraft != null
            ? Session.PlayerAircraft.CurrentSegmentIndex - 1
            : 0;

    public static int NextNodeIndex =>
        Session.PlayerAircraft != null ? Session.PlayerAircraft.CurrentSegmentIndex : 0;
    public static RoutePoint GetNodeToOnActive => GetActiveRoutePoint(NextNodeIndex); // todo may need to be adjusted : line goes passed the node, sometimes with a lot

    private static RoutePoint GetActiveRoutePoint(int index)
    {
        var points = Session.ActiveRoute?.Points;
        if (points == null || points.Length == 0)
        {
            return null;
        }

        return points[ClampIndex(index, points.Length)];
    }

    private static int ClampIndex(int index, int length)
    {
        if (index < 0)
        {
            return 0;
        }

        if (index >= length)
        {
            return length - 1;
        }

        return index;
    }
}