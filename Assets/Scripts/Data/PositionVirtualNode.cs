public class PositionVirtualNode
{
    static Aircraft Aircraft => GameManager.Instance.Aircraft;

    public static int PassedNodeIndex => Aircraft.PathLocalization.CurrentNodeIndex - 1;
    public static int PassedNodeIndexForMode => Aircraft.IsOnPath 
        ? Aircraft.PathLocalization.CurrentNodeIndex - 1 
        : 0;

    public static int NextNodeIndex => !GameManager.Instance.Aircraft.IsOnPath && GameManager.Instance.IsMod
        ? 1
        : Aircraft.PathLocalization.CurrentNodeIndex;
    public static RoutePoint GetNodeFrom => GameManager.Instance.ActiveRoute.Points[PassedNodeIndex];
    public static RoutePoint GetNodeTo => GameManager.Instance.ActiveRoute.Points[PassedNodeIndex + 1]; // todo may need to be adjusted : line goes passed the node, sometimes with a lot
    public static MarkLine CurrentSegment => GameManager.Instance.PathLines.ComputedLines[PassedNodeIndex + 1];

}
