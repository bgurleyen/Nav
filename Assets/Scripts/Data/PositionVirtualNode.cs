public class PositionVirtualNode
{
    static Aircraft Aircraft => GameManager.Instance.Aircraft;

    public static int PassedNodeIndex => Aircraft.PathLocalization.CurrentNodeIndex - 1;
    public static int NextNodeIndex => Aircraft.PathLocalization.CurrentNodeIndex;
    public static RoutePoint GetNodeFrom => GameManager.Instance.ActiveRoute.Points[PassedNodeIndex];
    public static RoutePoint GetNodeTo => GameManager.Instance.ActiveRoute.Points[PassedNodeIndex + 1]; // todo may need to be adjusted : line goes passed the node, sometimes with a lot
    public static MarkLine CurrentSegment => GameManager.Instance.PathLines.ComputedLines[PassedNodeIndex + 1];

}
