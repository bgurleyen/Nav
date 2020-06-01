public class PositionVirtualNode
{
    static Aircraft Aircraft => GameManager.Instance.Aircraft;

    public static int PassedNodeIndex => Aircraft.UnreachedVertex.CurrentLine - 1;
    public static DataPoint GetNodeFrom => GameManager.Instance.ActiveSet.Points[PassedNodeIndex];
    public static DataPoint GetNodeTo => GameManager.Instance.ActiveSet.Points[PassedNodeIndex + 1]; // todo may need to be adjusted : line goes passed the node, sometimes with a lot
    static MarkLine OnLine => GameManager.Instance.PathLines.ComputedLines[PassedNodeIndex + 1];

    public static float ComputedDistancePassed => Aircraft.WalkedDistanceOnLine;
}
