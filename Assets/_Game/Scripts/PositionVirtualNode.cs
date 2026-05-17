using Navigation;
using UnityEngine;

public class PositionVirtualNode
{
    public static int PassedNodeIndex => Session.PlayerAircraft.CurrentSegmentIndex - 1;
    public static int PassedNodeIndexForMode => Session.State.LNAV
        ? Session.PlayerAircraft.CurrentSegmentIndex - 1
        : 0;

    public static int NextNodeIndex => Session.PlayerAircraft.CurrentSegmentIndex;
    public static RoutePoint GetNodeFromOnActive => GetActiveRoutePoint(PassedNodeIndex);
    public static RoutePoint GetNodeToOnActive => GetActiveRoutePoint(NextNodeIndex); // todo may need to be adjusted : line goes passed the node, sometimes with a lot
    public static TracedLine CurrentTracedLine => GetCurrentTracedLine();

    private static RoutePoint GetActiveRoutePoint(int index)
    {
        var points = Session.ActiveRoute?.Points;
        if (points == null || points.Length == 0)
        {
            return null;
        }

        return points[ClampIndex(index, points.Length)];
    }

    private static TracedLine GetCurrentTracedLine()
    {
        var computedLines = Session.ActiveRoute?.TracedRoute?.ComputedLines;
        if (computedLines == null || computedLines.Length == 0)
        {
            return null;
        }

        return computedLines[ClampIndex(NextNodeIndex, computedLines.Length)];
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