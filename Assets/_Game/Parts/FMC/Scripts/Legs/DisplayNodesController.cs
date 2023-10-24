using System;
using Navigation;
using UnityEngine;

public class DisplayNodesController
{
    public DisplayNodesController(int nodesPerPage)
    {
        _nodesPerPage = nodesPerPage;
    }


    // the current code that is centered when in PLAN mode.
    public int PlanCenterNodeIndex { get; private set; }
    
    public int TotalPagesCorrection { get; private set; }

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
        for (var i = PositionVirtualNode.NextNodeIndex; i < VisibleRoute.Points.Length; i++)
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

        // when an insert have been made with the future position node and has been executed. happening until passing the new position
        var positionIsTemporaryAhead = Session.ActiveRoute.Points[PositionVirtualNode.NextNodeIndex].IsPositionNode;

        var thisIsDiscontinuity = false;
        var thisIsAfterDiscontinuity = false;

        var linkedIndex = PositionVirtualNode.NextNodeIndex + (positionIsTemporaryAhead ? 1 : 0);
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
                    if (PlanCenterNodeIndex >= i)
                    {
                        SetPlanCenterNode(0);
                    }
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

        var isStartingPoint = linkedIndex == PositionVirtualNode.NextNodeIndex;

        // Debug.Log(
        //     $"_linkedIndex:{_linkedIndex}  index:{_totalLineIndex} ");

        return new NodeSelection
        {
            LinkedId = linkedId,
            IsAddedDiscontinuity = thisIsDiscontinuity && !thisIsAfterDiscontinuity,
            IsEmpty = !pointIsValid,
            IsStartingPoint = isStartingPoint,
            IsPlanCenter = PlanCenterNodeIndex ==pagedLineIndex 
        }; // not showing the first point as is NOW
        
    }

    public void DoPlanModeStep(bool reset)
    {
        SetPlanCenterNode(PlanCenterNodeIndex + (reset?0:1));
    }

    private void SetPlanCenterNode(int value)
    {
        PlanCenterNodeIndex = value;
        var page = PlanCenterNodeIndex / _nodesPerPage;
        var line = PlanCenterNodeIndex % _nodesPerPage;
        
        if (Session.ActiveRoute.GetPoint(GetNodeInfoAtLineIndex(line, page).LinkedId, out var point, out _))
        {
            Session.CenteredPosition = point.CartesianPosition;
        }
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

public static class NodeSelectionExtensions
{
    public static RoutePoint GetRouteNode(this NodeSelection selectionInfo)
    {
          return selectionInfo == null ? null : 
                Session.VisibleRoute.GetPoint(selectionInfo.LinkedId, out var node, out _) ? node : null;
    }
    
}