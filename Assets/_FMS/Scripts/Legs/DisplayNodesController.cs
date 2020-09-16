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

        // when an insert have been made with the future position node and has been executed. happening until passing the new position
        var _positionIsTemporaryAhead = ActiveRoute.Points[StartingNodeIndex].IsPositionNode;

        var _thisIsDiscontinuity = false;
        var _thisIsAfterDiscontinuity = false;

        var _linkedIndex = StartingNodeIndex + (_positionIsTemporaryAhead ? 1 : 0);
        var _pointIsValid = false;
        RoutePoint _linkedPoint = null;

        for (var i = 0; i <= _totalLineIndex; i++)
        {
            _pointIsValid = VisibleRoute.GetPointAt(_linkedIndex, out _linkedPoint);

            var _handled = false;
            var _canIncrement = false;
            while (!_handled)
            {
                while (_pointIsValid && _linkedPoint.IsSkippable)
                {
                    _pointIsValid = VisibleRoute.GetPointAt(++_linkedIndex, out _linkedPoint);
                }

                if (!_pointIsValid)
                {
                    break;
                }


                if (_linkedPoint.IsAfterDiscontinuity)
                {
                    if (!_thisIsDiscontinuity)
                    {
                        _thisIsDiscontinuity = true;
                        _handled = true;
                    }
                    else if (!_thisIsAfterDiscontinuity)
                    {
                        _thisIsAfterDiscontinuity = true;
                        _handled = true;
                    }
                    else
                    {
                        _thisIsDiscontinuity = false;
                        _thisIsAfterDiscontinuity = false;
                        _pointIsValid = VisibleRoute.GetPointAt(++_linkedIndex, out _linkedPoint);
                    }
                }
                else
                {
                    _thisIsDiscontinuity = false;
                    _thisIsAfterDiscontinuity = false;
                    _handled = true;
                    _canIncrement = true;
                }
            }

            if (_canIncrement)
            {
                if (i < _totalLineIndex)
                {
                    _linkedIndex++;
                }
            }
        }


        var _linkedId = _pointIsValid ? _linkedPoint.ID : -1;

        var _isStartingPoint = _linkedIndex == StartingNodeIndex;

        // Debug.Log(
        //     $"_linkedIndex:{_linkedIndex}  index:{_totalLineIndex} ");

        return new NodeSelection
        {
            LinkedId = _linkedId,
            IsAddedDiscontinuity = _thisIsDiscontinuity && !_thisIsAfterDiscontinuity,
            IsEmpty = !_pointIsValid,
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