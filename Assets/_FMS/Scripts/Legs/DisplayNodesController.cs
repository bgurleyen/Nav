using UnityEngine;

public class DisplayNodesController
{
    public DisplayNodesController(int nodesPerPage)
    {
        this.nodesPerPage = nodesPerPage;
    }

    public int TotalPagesCorrection = 0;

    static bool IsMod => GameManager.Instance.IsMod;
    static RouteScriptableObject ActiveRoute => GameManager.Instance.ActiveRoute;
    static RouteScriptableObject ModRoute => GameManager.Instance.ModRoute;
    static RouteScriptableObject VisibleRoute => IsMod ? ModRoute : ActiveRoute;
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
        for (var i = StartingNodeIndex; i < VisibleRoute.Points.Length; i++)
        {
            if (VisibleRoute.Points[i].IsSkippable)
            {
                skipped++;
                continue;
            }

            if (VisibleRoute.Points[i].IsAfterDiscontinuity)
            {
                discontinuities++;
            }
        }

        return discontinuities - skipped;
    }

    public NodeSelection GetNodeInfoAtLineIndex(int lineIndex, int currentPage)
    {
        var _totalLineIndex = lineIndex + currentPage * nodesPerPage;

        var _discontinuityOffset = 0;
        
        // offset for skippable nodes
        var _hiddenOffset = 0;
        
        var _lastDiscontinuity = -1;
        
        // this happens because the curves add distance and the position can become ahead. Needs to be dealt with in a more advanced way 
        var _positionIsTemporaryAhead = ActiveRoute.Points[StartingNodeIndex].IsPositionNode;

        var _computeLineIndex = 0;

        var _linkedIndex = StartingNodeIndex + (_positionIsTemporaryAhead ? 1 : 0);
        for (var i = 0; i <= _totalLineIndex; i++)
        {
            
        }
        
        // for (var i = 1; i < VisibleRoute.Points.Length; i++) // may be optimised - cached
        // {
        //     if (i > _totalLineIndex + _hiddenOffset + _discontinuityOffset )
        //     {
        //         break;
        //     }
        //
        //     if (VisibleRoute.Points[i].IsAfterDiscontinuity)
        //     {
        //         _lastDiscontinuity = i - (_positionIsTemporaryAhead ? 1 : 0);
        //     }
        //
        //     if (VisibleRoute.Points[i - 1].IsAfterDiscontinuity)
        //     {
        //         var _currentLinkedIndex = i - _discontinuityOffset + _hiddenOffset;
        //         if (_currentLinkedIndex - 1 == _lastDiscontinuity)
        //         {
        //             _discontinuityOffset++;
        //         }
        //     }
        //     
        //     // only count hiddens after starting node since all are hidden and shortcuted for linear approach
        //     if (i >= StartingNodeIndex && VisibleRoute.Points[i].IsSkippable)
        //     {
        //         _hiddenOffset++;
        //     }
        // }

         _linkedIndex = _totalLineIndex - _discontinuityOffset + _hiddenOffset;
        var _isEmpty = !VisibleRoute.GetPointAt(_linkedIndex, out var _point);
        var _linkedId = _isEmpty ? -1 : _point.ID;

        var _isStartingPoint = _linkedIndex == StartingNodeIndex;

        var _thisIsSecondLineOfCurrentDiscontinuity = _lastDiscontinuity == _totalLineIndex - 1 + _hiddenOffset;
        var _correction = _thisIsSecondLineOfCurrentDiscontinuity ? -1 : 0;
        var _isDiscontinuity = _lastDiscontinuity + _discontinuityOffset + _correction == _totalLineIndex;

        Debug.Log(
            $"_linkedIndex:{_linkedIndex}  index:{_totalLineIndex}  discontinuityOffse:{_discontinuityOffset}  lastDiscont:{_lastDiscontinuity}  thisissecond:{_thisIsSecondLineOfCurrentDiscontinuity}");

        return new NodeSelection
        {
            LinkedId = _linkedId,
            IsAddedDiscontinuity = _isDiscontinuity,
            IsEmpty = _isEmpty,
            IsStartingPoint = _isStartingPoint
        }; // not showing the first point as is NOW
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