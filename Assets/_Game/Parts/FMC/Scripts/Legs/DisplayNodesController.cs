using System;
using Navigation;
using UnityEngine;

public class DisplayNodesController
{
    public DisplayNodesController(int nodesPerPage)
    {
        this._nodesPerPage = nodesPerPage;
    }


    // the current code that is centered when in PLAN mode.
    private int _planCenterNodeIndex = 0;
    
    
    public int TotalPagesCorrection = 0;

    private static RouteScriptableObject VisibleRoute => Session.IsMod ? Session.ModRoute : Session.ActiveRoute;

    private readonly int _nodesPerPage;

    public void ComputeCorrections()
    {
        TotalPagesCorrection = GetTotalNodesCorrections(out _, out _);
    }

    private static int GetTotalNodesCorrections(out int discontinuities, out int skipped)
    {
        discontinuities = 0;
        skipped = 0;
        var nextNodeIndex = Session.ActiveRoute.GetFirstViableNode.index;
        for (var i = nextNodeIndex; i < VisibleRoute.Points.Length; i++)
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
        var pagedLineIndex = lineIndex + currentPage * _nodesPerPage;

        var nextNodeIndex = Session.ActiveRoute.GetFirstViableNode.index;

        // when an insert have been made with the future position node and has been executed. happening until passing the new position
        var positionIsTemporaryAhead = Session.ActiveRoute.Points[nextNodeIndex].IsPositionNode;

        var thisIsDiscontinuity = false;
        var thisIsAfterDiscontinuity = false;

        var linkedIndex = nextNodeIndex + (positionIsTemporaryAhead ? 1 : 0);
        var pointIsValid = false;
        RoutePoint linkedPoint = null;

        for (var i = 0; i <= pagedLineIndex; i++)
        {
            pointIsValid = VisibleRoute.GetPointAt(linkedIndex, out linkedPoint);

            var handled = false;
            var canIncrement = false;
            while (!handled)
            {
                while (pointIsValid && linkedPoint.IsSkippable)
                {
                    pointIsValid = VisibleRoute.GetPointAt(++linkedIndex, out linkedPoint);
                }

                if (!pointIsValid)
                {
                    break;
                }


                if (linkedPoint.IsAfterDiscontinuity)
                {
                    if (!thisIsDiscontinuity)
                    {
                        thisIsDiscontinuity = true;
                        handled = true;
                    }
                    else if (!thisIsAfterDiscontinuity)
                    {
                        thisIsAfterDiscontinuity = true;
                        handled = true;
                    }
                    else
                    {
                        thisIsDiscontinuity = false;
                        thisIsAfterDiscontinuity = false;
                        pointIsValid = VisibleRoute.GetPointAt(++linkedIndex, out linkedPoint);
                    }
                }
                else
                {
                    thisIsDiscontinuity = false;
                    thisIsAfterDiscontinuity = false;
                    handled = true;
                    canIncrement = true;
                }
            }

            if (canIncrement)
            {
                if (i < pagedLineIndex)
                {
                    linkedIndex++;
                }
            }
        }


        var linkedId = pointIsValid ? linkedPoint.ID : -1;

        var isStartingPoint = linkedIndex == nextNodeIndex;

        // Debug.Log(
        //     $"_linkedIndex:{_linkedIndex}  index:{_totalLineIndex} ");

        return new NodeSelection
        {
            LinkedId = linkedId,
            IsAddedDiscontinuity = thisIsDiscontinuity && !thisIsAfterDiscontinuity,
            IsEmpty = !pointIsValid,
            IsStartingPoint = isStartingPoint,
            IsPlanCenter = _planCenterNodeIndex == lineIndex
        }; // not showing the first point as is NOW
        
    }

    public void DoPlanModeStep()
    {
        throw new NotImplementedException();
    }
}

public class NodeSelection
{
    public int LinkedId;
    public bool IsAddedDiscontinuity;
    public bool IsEmpty;
    internal bool IsStartingPoint;
    // for map mode
    internal bool IsPlanCenter;

    public bool IsInvalid => LinkedId <= 0;
}