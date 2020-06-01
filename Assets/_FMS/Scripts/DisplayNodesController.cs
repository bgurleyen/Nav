public class DisplayNodesController
{
    public DisplayNodesController(int nodesPerPage)
    {
        this.nodesPerPage = nodesPerPage;
    }

    public int TotalPagesCorrection = 0;
    
    static bool IsMod => GameManager.Instance.IsMod;
    static DataSetScriptableObject ActiveSet => GameManager.Instance.ActiveSet;
    static DataSetScriptableObject ModSet => GameManager.Instance.ModSet;
    static DataSetScriptableObject VisibleDataSet => IsMod ? ModSet : ActiveSet;
    static int StartingNodeIndex => GameManager.Instance.UnreachedNodeIndex;

    readonly int nodesPerPage;

    public void ComputeCorrections()
    {
        TotalPagesCorrection = GetTotalNodesCorrections(out var _, out var _);
    }

    static int GetTotalNodesCorrections(out int discontinuities, out int skipped)
    {
        discontinuities = 0;
        skipped = 0;
        for (var i = StartingNodeIndex; i < VisibleDataSet.Points.Length; i++)
        {
            if (IsNodeSkippable(VisibleDataSet.Points[i]))
            {
                skipped++;
                continue;
            }

            if (VisibleDataSet.Points[i].IsAfterDiscontinuity)
            {
                discontinuities++;
            }
        }

        return discontinuities - skipped;
    }

    public NodeSelection GetNodeInfoAtLineIndex(int lineIndex, int currentPage)
    {
        var _index = lineIndex + currentPage * nodesPerPage + StartingNodeIndex;

        var _discontinuityOffset = 0;
        var _hiddenOffset = 0;
        var _lastDiscontinuity = -1;

        for (var i = 1; i < VisibleDataSet.Points.Length; i++) // may be opptimised - cached
        {
            if (i > _index + _hiddenOffset)
            {
                break;
            }

            if (VisibleDataSet.Points[i].IsAfterDiscontinuity)
            {
                _lastDiscontinuity = i;
            }

            if (VisibleDataSet.Points[i - 1].IsAfterDiscontinuity)
            {
                _discontinuityOffset++;
            }

            // only count hiddens after starting node since all are hidded and shortcuted for liniar approach
            if (i >= StartingNodeIndex && IsNodeSkippable(VisibleDataSet.Points[i]))
            {
                _hiddenOffset++;
            }
        }

        var _linkedIndex = _index - _discontinuityOffset + _hiddenOffset;
        var _linkedId = VisibleDataSet.Points[_linkedIndex].ID;

        var _isEmpty = VisibleDataSet.Points.Length <= _linkedIndex;
        var _isStartingPoint = _linkedIndex == StartingNodeIndex;
        var _isDiscontinuity = _lastDiscontinuity == _index;

        return new NodeSelection
        {
            LinkedId = _linkedId,
            IsAddedDiscontinuity = _isDiscontinuity,
            IsEmpty = _isEmpty,
            IsStartingPoint = _isStartingPoint
        }; // not showing the first point as is NOW
    }

    static bool IsNodeSkippable(DataPoint point)
    {
        return  (point.IsHiddenLine && !point.IsFirstAfterFreeFlight);
    }
}

public class NodeSelection
{
    public int LinkedId;
    public bool IsAddedDiscontinuity;
    public bool IsEmpty;
    internal bool IsStartingPoint;

    public bool IsInvalid => LinkedId <= 0;
}