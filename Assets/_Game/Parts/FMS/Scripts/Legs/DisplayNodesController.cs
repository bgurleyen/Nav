using Navigation;
using UnityEngine;

public class DisplayNodesController
{
    public DisplayNodesController(int nodesPerPage)
    {
        this.nodesPerPage = nodesPerPage;
    }

    public int TotalPagesCorrection = 0;

    private static RouteScriptableObject VisibleRoute => Session.IsMod ? Session.ModRoute : Session.ActiveRoute;

    private readonly int nodesPerPage;

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
        var pagedLineIndex = lineIndex + currentPage * nodesPerPage;

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
            IsStartingPoint = isStartingPoint
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