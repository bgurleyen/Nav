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
    private RouteScriptableObject _lastVisibleRoute;
    private int _displayStartIndex;

    public int DisplayStartIndex => GetDisplayStartIndex();

    public void ComputeCorrections()
    {
        TotalPagesCorrection = GetTotalNodesCorrections(out _, out _);
    }

    private int GetTotalNodesCorrections(out int discontinuities, out int skipped)
    {
        discontinuities = 0;
        skipped = 0;
        var startIndex = GetDisplayStartIndex();
        for (var i = startIndex; i < VisibleRoute.Points.Length; i++)
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

        var startIndex = GetDisplayStartIndex();

        // when an insert have been made with the future position node and has been executed. happening until passing the new position
        var activeRouteStartIndex = Mathf.Clamp(startIndex, 0, Session.ActiveRoute.Points.Length - 1);
        var positionIsTemporaryAhead = Session.ActiveRoute.Points[activeRouteStartIndex].IsPositionNode;

        var thisIsDiscontinuity = false;
        var thisIsAfterDiscontinuity = false;

        var linkedIndex = startIndex + (positionIsTemporaryAhead ? 1 : 0);
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

        var isStartingPoint = linkedIndex == startIndex;

        // Debug.Log(
        //     $"_linkedIndex:{_linkedIndex}  index:{_totalLineIndex} ");

        return new NodeSelection
        {
            LinkedId = linkedId,
            IsAddedDiscontinuity = thisIsDiscontinuity && !thisIsAfterDiscontinuity,
            IsEmpty = !pointIsValid,
            IsStartingPoint = isStartingPoint,
            IsPlanCenter = PlanCenterNodeIndex == pagedLineIndex
        }; // not showing the first point as is NOW

    }

    private int GetDisplayStartIndex()
    {
        var visibleRoute = VisibleRoute;
        if (visibleRoute == null || visibleRoute.Points == null || visibleRoute.Points.Length == 0)
        {
            _lastVisibleRoute = visibleRoute;
            _displayStartIndex = 0;
            return 0;
        }

        var nextNodeIndex = Mathf.Clamp(PositionVirtualNode.NextNodeIndex, 0, visibleRoute.Points.Length - 1);
        var syncedStartIndex = GetSyncedStartIndex(visibleRoute, nextNodeIndex);

        if (_lastVisibleRoute != visibleRoute)
        {
            _lastVisibleRoute = visibleRoute;
            _displayStartIndex = syncedStartIndex;
        }
        else
        {
            _displayStartIndex = Mathf.Max(_displayStartIndex, syncedStartIndex);
        }

        return _displayStartIndex;
    }

    private static int GetSyncedStartIndex(RouteScriptableObject visibleRoute, int fallbackIndex)
    {
        if (Session.ActiveRoute == null || Session.ActiveRoute.Points == null || Session.ActiveRoute.Points.Length == 0)
        {
            return fallbackIndex;
        }

        if (TryFindCurrentVisibleRouteIndex(visibleRoute, out var currentVisibleIndex))
        {
            return currentVisibleIndex;
        }

        var activePassedNodeIndex = Mathf.Clamp(PositionVirtualNode.PassedNodeIndex + 1, 0,
            Session.ActiveRoute.Points.Length - 1);
        if (TryFindVisibleRouteIndex(visibleRoute, activePassedNodeIndex, out var passedSyncIndex))
        {
            return passedSyncIndex;
        }

        var activeNextNodeIndex = Mathf.Clamp(PositionVirtualNode.NextNodeIndex, 0, Session.ActiveRoute.Points.Length - 1);
        if (TryFindVisibleRouteIndex(visibleRoute, activeNextNodeIndex, out var nextSyncIndex))
        {
            return nextSyncIndex;
        }

        return fallbackIndex;
    }

    private static bool TryFindCurrentVisibleRouteIndex(RouteScriptableObject visibleRoute, out int visibleRouteIndex)
    {
        for (var i = 0; i < visibleRoute.Points.Length; i++)
        {
            if (visibleRoute.Points[i].IsSkippable)
            {
                continue;
            }

            if (visibleRoute.Points[i].GetIsDisplayCurrent)
            {
                visibleRouteIndex = i;
                return true;
            }
        }

        visibleRouteIndex = -1;
        return false;
    }

    private static bool TryFindVisibleRouteIndex(RouteScriptableObject visibleRoute, int activeRouteIndex,
        out int visibleRouteIndex)
    {
        for (var i = activeRouteIndex; i < Session.ActiveRoute.Points.Length; i++)
        {
            if (!Session.ActiveRoute.GetPointAt(i, out var activePoint) ||
                activePoint.IsSkippable)
            {
                continue;
            }

            if (!visibleRoute.Points.GetNodeIndex(activePoint.ID, out visibleRouteIndex))
            {
                continue;
            }

            if (visibleRoute.Points[visibleRouteIndex].IsSkippable)
            {
                continue;
            }

            return true;
        }

        visibleRouteIndex = -1;
        return false;
    }

    public void DoPlanModeStep(bool reset)
    {
        SetPlanCenterNode(PlanCenterNodeIndex + (reset ? 0 : 1));
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