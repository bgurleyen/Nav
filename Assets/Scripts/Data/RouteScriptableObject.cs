using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "RouteData", menuName = "ScriptableObjects/RouteData")]
public class RouteScriptableObject : ScriptableObject
{
    public bool ActiveDirectApproach { get; private set; }
    public int FirstSpeedRegulationNodeId { get; set; }
    public int FirstAltRegulationNodeId { get; set; }

    public RoutePoint[] Points;
    public PathLines PathLines { get; private set; } = new PathLines();

    static Aircraft Aircraft => GameManager.Instance.Aircraft;

    public void ComputeCartesianPositions()
    {
        var _currentPosition = Vector2.zero;
        for (var i = 0; i < Points.Length; i++)
        {
            if (i > 0)
            {
                _currentPosition = Geometry.GetNextPosition(_currentPosition, Points[i].Distance, Points[i].Degrees);
            }
            Points[i].CartesianPosition = _currentPosition;
        }
    }

    public void ComputeSet(bool isMod)
    {
        PathLines.ComputeSet(Points, !isMod);
    }

    public void InitIds()
    {
        for (var i = 0; i < Points.Length; i++)
        {
            Points[i].ID = i;
        }
    }

    public Vector2 GetCartesianPosition(int lineIndex)
    {
        if (Points.Length <= lineIndex)
        {
            return Vector2.zero;
        }

        return Points[lineIndex].CartesianPosition;
    }

    public bool GetPoint(int nodeId, out RoutePoint point)
    {
        var index = Points.GetNodeIndex(nodeId);
        return GetPointAt(index, out point);
    }

    public bool GetPointAt(int index, out RoutePoint point)
    {
        if (Points.Length <= index || index < 0)
        {
            point = null;
            return false;
        }

        point = Points[index];
        return true;
    }


    int GetNewId()
    {
        var id = Points.Length;
        while (Points.Any(x => x.ID == id))
        {
            id++;
        }

        return id;
    }

    string GetNewName(string fromNode)
    {
        var intro = fromNode.Substring(0, 3);
        var index = 1;

        while (Points.Any(x => x.Name == intro + index.ToString("00")))
        {
            index++;
        }

        return intro + index.ToString("00");
    }

    public RouteScriptableObject Clone()
    {
        var newSet = CreateInstance<RouteScriptableObject>(); // new DataSetScriptableObject();
        newSet.ActiveDirectApproach = ActiveDirectApproach;
        newSet.FirstAltRegulationNodeId = FirstAltRegulationNodeId;
        newSet.FirstSpeedRegulationNodeId = FirstSpeedRegulationNodeId;
        newSet.Points = new RoutePoint[Points.Length];
        for (var i = 0; i < Points.Length; i++)
        {
            newSet.Points[i] = Points[i].Clone();
        }

        return newSet;
    }

    public void OnPathRejoined()
    {
        ActiveDirectApproach = false;
    }

    public bool FindFreeFlightCloseToPathExitScenario(float maxDistance,out Vector2 futurePosition, out Vector2 centerOfTurn,
        out Vector2 exitPoint,
        out int exitSegmentIndex)
    {
        var nan = new Vector2(-100, -100);


        futurePosition = Geometry.GetNextPosition(Aircraft.PositionFreeOrOnCurvedPath, Aircraft.ForwardThreshold,
            -Aircraft.Heading);

        exitPoint = nan;
        centerOfTurn = nan;

        var currentNodeIndex = GameManager.Instance.Aircraft.RoutePathLocalization.CurrentNodeIndex;
        var aircraftPosition =
            GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath; // would be free since we are in free flight

        exitSegmentIndex = -1;

        for (var i = currentNodeIndex; i < Points.Length; i++)
        {
            if (Points[i].IsHiddenLine || Points[i].IsAfterDiscontinuity)
            {
                continue;
            }

            var segmentStart = Points[i-1].CartesianPosition;
            var segmentEnd = Points[i].CartesianPosition;

            if (Geometry.FindDistanceToSegment(aircraftPosition, segmentStart, segmentEnd,
                out var intersection, out var distance) && distance <= maxDistance)
            {
                exitSegmentIndex = i;
                centerOfTurn = intersection;

                // don't break, keep computing to find the most forward segment that is withing max distance range
            }
        }

        if (exitSegmentIndex < 0)
        {
            return false;
        }
        var nextNextNodePosition = Points[exitSegmentIndex].CartesianPosition;
        
        // just to be sure move the center of turn further to have space for turn
        // - @#$ todo will need to refine the scenarios here
        centerOfTurn = Vector2.Lerp(centerOfTurn, nextNextNodePosition,
            (2*Aircraft.ForwardThreshold) / Vector2.Distance(centerOfTurn, nextNextNodePosition));

        
        exitPoint = Vector2.Lerp(centerOfTurn, nextNextNodePosition,
            Aircraft.ForwardThreshold / Vector2.Distance(centerOfTurn, nextNextNodePosition));
        
        return true;
    }

    // to be executed on ACTIVE route
    public bool FindFreeFlightNextNodeExitScenario(out Vector2 futurePosition, out Vector2 centerOfTurn, out Vector2 exitPoint,
        out int exitSegmentIndex)
    {
        futurePosition = Geometry.GetNextPosition(Aircraft.PositionFreeOrOnCurvedPath, Aircraft.ForwardThreshold,
            -Aircraft.Heading);
            
        var currentNodeIndex = GameManager.Instance.Aircraft.RoutePathLocalization.CurrentNodeIndex;
        centerOfTurn = Points[currentNodeIndex].CartesianPosition;
        
        // we rejoin after intersection
        var nextNextNodePosition = Points[currentNodeIndex + 1].CartesianPosition;
        exitPoint = Vector2.Lerp(centerOfTurn, nextNextNodePosition,
            Aircraft.ForwardThreshold / Vector2.Distance(centerOfTurn, nextNextNodePosition));

        exitSegmentIndex = currentNodeIndex + 1;

        return true;
    }

    // to be executed on ACTIVE route
    public bool FindFreeFlightDirectExitScenario(out Vector2 centerOfTurn, out Vector2 exitPoint, out int exitSegmentIndex)
    {

        var nan = new Vector2(-100, -100);

        exitPoint = nan;
        centerOfTurn = nan;
        var aircraftPosition =
            GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath; // would be free since we are in free flight
        var aircraftDirection = Geometry.GetDirectionFromHeading(GameManager.Instance.Aircraft.Heading);
        var segmentEnd = Vector2.zero;

        exitSegmentIndex = -1;
        var intersection = Vector2.zero;

        for (var i = 1; i < Points.Length; i++)
        {
            var segmentStart = segmentEnd;
            // on could take the positions from PathLines
            segmentEnd = Geometry.GetNextPosition(segmentStart, Points[i].Distance, Points[i].Degrees);

            if (Points[i].IsHiddenLine || Points[i].IsAfterDiscontinuity
            ) //@#$ ask if we can join discontinuity segments
            {
                continue;
            }

            if (Geometry.FindLineSegmentIntersection(aircraftPosition, aircraftDirection.x, aircraftDirection.y,
                segmentStart, segmentEnd, out intersection))
            {
                exitSegmentIndex = i;
                break;
            }
        }

        if (exitSegmentIndex >= 0)
        {
            var currentSegmentStart = Points[exitSegmentIndex - 1].CartesianPosition;
            var currentSegmentEnd = Points[exitSegmentIndex].CartesianPosition;

            var towardsBeginning = Vector2.SqrMagnitude(currentSegmentStart - intersection) <
                                    Vector2.SqrMagnitude(currentSegmentEnd - intersection);

            var nextSegmentStart = currentSegmentEnd;

            var countCurrent = Geometry.CircleIntersects(currentSegmentStart, currentSegmentEnd, intersection,
                Aircraft.ForwardThreshold,
                true, out var intersectionCurrent1, out var intersectionCurrent2);

            var nextSegmentEnd = Vector2.zero;
            var intersectionNext1 = Vector2.zero;
            var intersectionNext2 = Vector2.zero;

            int countNext;

            if (Points.Length > exitSegmentIndex + 1)
            {
                nextSegmentEnd = Points[exitSegmentIndex + 1].CartesianPosition;
                countNext = Geometry.CircleIntersects(nextSegmentStart, nextSegmentEnd, intersection,
                    Aircraft.ForwardThreshold,
                    false, out intersectionNext1, out intersectionNext2);
            }

            // find forward intersection

            // if inside turn - prepare turn for corner on the other side of the circle of the aircraft and the exit of turn
            // if not near turn - prepare turn for corner on circle center and the forward intersection

            // create lines with computed vertexes for turns for scenario and return them 


            // the intersections on the current segment are clamped to segment
            if (countCurrent <= 0)
            {
                // should happen if the segment is smaller than the threshold ( go to the next segments ? )
                Debug.LogError("no intersections - segment smaller than threshold?");
                return false;
            }

            // we are not sure of the order of the circle intersections on the segment 
            var firstIsBefore = Vector2.SqrMagnitude(currentSegmentStart - intersectionCurrent1) <
                                 Vector2.SqrMagnitude(currentSegmentStart - intersection);

            if (countCurrent == 2)
            {
                // --> the center of turn can be the intersection since is on the same line with the exit
                centerOfTurn = intersection;
                exitPoint = firstIsBefore ? intersectionCurrent2 : intersectionCurrent1;
                return true;
            }

            if (towardsBeginning)
            {
                // --> the center of turn can be the intersection since is on the same line with the exit
                centerOfTurn = intersection;
                exitPoint = intersectionCurrent1;
                return true;
            }

            // We are will be exiting on the next segment than the intersection
            // --> find center of turn as the intersection between next segment and current direction
            Geometry.FindLineSegmentIntersection(aircraftPosition, aircraftDirection.x, aircraftDirection.y,
                nextSegmentStart, nextSegmentEnd, out var newCenter, false);

            var nextExitPoint = Geometry.IsWithinSegment(nextSegmentStart.x, nextSegmentStart.y,
                nextSegmentEnd.x, nextSegmentEnd.y, intersectionNext1.x, intersectionNext1.y)
                ? intersectionNext1
                : intersectionNext2;

            //if the angle is inwards (meaning the center of turn of further than the exitpoint ) ( see reference image.. ) move exit point further
            var exitIsBackwards = Vector2.SqrMagnitude(nextSegmentEnd - newCenter) <
                                   Vector2.SqrMagnitude(nextSegmentEnd - nextExitPoint);
            if (exitIsBackwards)
            {
                nextExitPoint = Vector2.MoveTowards(newCenter, nextSegmentEnd, Aircraft.ForwardThreshold);
            }

            // todo: if newCenter is passed the next segment end ( when in U turn and bypasses the middle ) -> try next
            // todo: if exit point is near turn ( too close points) -> try next

            centerOfTurn = newCenter;
            exitPoint = nextExitPoint;

            return true;
        }

        return false;
    }


    public bool TransferPathToRoute(Vector2 exitPoint, int lineIndex, out PathPositionInfo  intersectionRoutePathInfo, out float segmentDistanceUntilIntersection)
    {
        intersectionRoutePathInfo = new PathPositionInfo();
        var segmentStart = Points[lineIndex - 1].CartesianPosition;
        var segmentEnd = Points[lineIndex].CartesianPosition;

        segmentDistanceUntilIntersection = (exitPoint - segmentStart).magnitude;
        if (GameManager.Instance.ActiveRoute.PathLines.FindClosestVertexToDistanceOnLineActive(
            segmentDistanceUntilIntersection, lineIndex, out var targetVertexIndex,
            out var targetVertexPosition))
        {
            intersectionRoutePathInfo = new PathPositionInfo
            {
                CurrentNodeIndex = lineIndex,
                HeadingBefore = GameManager.Instance.Aircraft.Heading,
                UnreachedVertexIndex = targetVertexIndex,
                UnreachedVertexPosition = targetVertexPosition
            };
            return true;
        }

        return false;
    }

    public void ShortcutNodes(int firstIdNodeToDissolve, int toId,
        out RoutePoint reducedPoint) // @#$ refactor for passed nodes ?
    {
        var startIndex = Points.GetNodeIndex(firstIdNodeToDissolve);
        var endIndex = Points.GetNodeIndex(toId);

        var offset = endIndex - startIndex;

        var newSet = new RoutePoint[Points.Length - offset];

        for (var i = 0; i < startIndex; i++)
        {
            newSet[i] = Points[i];
        }

        // compute new distance
        var endPosition =
            Geometry.GetNextPosition(Vector2.zero, Points[startIndex].Distance, Points[startIndex].Degrees);
        for (var i = startIndex; i < endIndex; i++)
        {
            endPosition = Geometry.GetNextPosition(endPosition, Points[i + 1].Distance, Points[i + 1].Degrees);
        }

        //compute new angle
        var angle = Geometry.AngleBetween(endPosition, Vector2.up);

        reducedPoint = Points[endIndex].Clone();
        reducedPoint.ClearDetails();
        reducedPoint.Distance = endPosition.magnitude;
        reducedPoint.RawDegrees = angle;
        reducedPoint.IsModified = true;

        newSet[startIndex] = reducedPoint;

        for (var i = endIndex + 1; i < Points.Length; i++)
        {
            var newIndex = i - offset;
            newSet[newIndex] = Points[i];
        }

        Points = newSet;
    }

    public void ClearModifiedFlags()
    {
        for (var i = 0; i < Points.Length; i++)
        {
            Points[i].IsModified = false;
            Points[i].IsSpeedModified = false;
            Points[i].IsAltitudeModified = false;
        }
    }

    public void AddRelativeNodeOnDirection(int nodeId, int distance, int relativeNodeId, out RoutePoint insertionNode,
        out RoutePoint afterInsertion) // @#$ todo refactor for passed nodes ?
    {
        var nodeIndex = Points.GetNodeIndex(nodeId);
        var node = Points[nodeIndex];

        float distanceToBefore;
        float distanceFromAfter;
        afterInsertion = null;

        if (distance <= 0)
        {
            afterInsertion = node;
            distanceToBefore = -distance;
            distanceFromAfter = afterInsertion.Distance - distanceToBefore;
        }
        else // if > 0
        {
            if (Points.Length > nodeIndex + 1)
            {
                afterInsertion = Points[nodeIndex + 1];
                distanceToBefore = afterInsertion.Distance - distance;
            }
            else
            {
                Debug.LogError("Not Possible");
                insertionNode = null;
                return;
            }

            distanceFromAfter = distance;
        }

        insertionNode = new RoutePoint
        {
            Name = GetNewName(node.Name),
            Distance = distanceFromAfter,
            RawDegrees = afterInsertion.RawDegrees,
            ID = GetNewId()
        };

        afterInsertion.Distance = distanceToBefore;

        // replace set with new set that also contains insertion node
        var newSet = new RoutePoint[Points.Length + 1];
        var offset = 0;
        for (var i = 0; i < Points.Length; i++)
        {
            if (i == nodeIndex + (distance > 0 ? 1 : 0))
            {
                newSet[i] = insertionNode;
                offset = 1;
            }

            newSet[i + offset] = Points[i];
        }

        Points = newSet;
    }

    void AddRelativeNodeAfter(int relativeFromNodeId, float rawDegrees, int distance,
        out RoutePoint insertionNode, out RoutePoint afterInsertion, bool addCurveOffset = true)
    {
        var relativeFromNodeIndex = Points.GetNodeIndex(relativeFromNodeId);
        var relativeFromNode = Points[relativeFromNodeIndex];

        afterInsertion = Points[relativeFromNodeIndex + 1];

        insertionNode = new RoutePoint
        {
            Name = GetNewName(relativeFromNode.Name),
            Distance = distance,
            RawDegrees = rawDegrees,
            ID = GetNewId()
        };

        var insertPosition = Geometry.GetNextPosition(Vector2.zero, distance, rawDegrees);
        var originalToPosition =
            Geometry.GetNextPosition(Vector2.zero, afterInsertion.Distance, afterInsertion.RawDegrees);
        var returnDirection = originalToPosition - insertPosition;
        afterInsertion.RawDegrees = Geometry.AngleBetween(Vector2.up, returnDirection);
        afterInsertion.Distance = returnDirection.magnitude;

        // simulate the curve to the the needed offset
        var lastLine = new MarkLine(relativeFromNode);
        lastLine.InitBeginning();

        LinesComputer.ComputeLine(lastLine, out var testLine, insertionNode, afterInsertion);

        // replace set with new set that also contains insertion node
        var newSet = new RoutePoint[Points.Length + 1];
        var offset = 0;
        for (var i = 0; i < Points.Length; i++)
        {
            if (i == relativeFromNodeIndex + 1)
            {
                newSet[i] = insertionNode;

                offset = 1;
            }

            newSet[i + offset] = Points[i];
        }

        Points = newSet;
    }


    public void AddRelativeNodeBefore(int beforeNodeId, float rawDegrees, int distance, int relativeNodeId,
        out RoutePoint insertionNode,
        bool showDiscontinuity = false)
    {
        var originalBeforeNodeIndex = Points.GetNodeIndex(beforeNodeId);
        var relativeNodeIndex = Points.GetNodeIndex(relativeNodeId);

        var isInThePast = PositionVirtualNode.PassedNodeIndexForMode > originalBeforeNodeIndex - 1;
        
        var relativeNode = Points[relativeNodeIndex];
        var insertPosition = Geometry.GetNextPosition(relativeNode.CartesianPosition, distance, 360 - rawDegrees);

        var indexBeforeInsertion = isInThePast ? PositionVirtualNode.PassedNodeIndexForMode : originalBeforeNodeIndex - 1;

        var positionBeforeInsertion = Points[indexBeforeInsertion].CartesianPosition;

        var insertionAngle = Geometry.AngleOfPosition(insertPosition, positionBeforeInsertion);
        var insertionDistance = (insertPosition - positionBeforeInsertion).magnitude;

        GetPoint(beforeNodeId, out var nodeAfterInsertion);
        
        insertionNode = new RoutePoint
        {
            Name = GetNewName(relativeNode.Name),
            Distance = insertionDistance,
            RawDegrees = insertionAngle,
            ID = GetNewId(),
            Details = nodeAfterInsertion.IsAfterDiscontinuity && showDiscontinuity ? "D" : ""
        };
        
        
        var returnNode = nodeAfterInsertion.Clone();

        // update info of selected to be relative to the inserted instead of the previous which is now previous to inserted
        returnNode.RawDegrees = Geometry.AngleOfPosition(nodeAfterInsertion.CartesianPosition, insertPosition); 
        returnNode.Distance = (nodeAfterInsertion.CartesianPosition - insertPosition).magnitude;
        if ( showDiscontinuity)
        {
            returnNode.IndicateDiscontinuityBefore();
        }

        if (isInThePast)
        {
            returnNode.ID = GetNewId() + Points.Length;
        }

        // replace set with new set that also contains insertion node
        var inThePastExtraNodes = indexBeforeInsertion - relativeNodeIndex + 1;
        var newSet = new RoutePoint[Points.Length + (isInThePast ? 1+ inThePastExtraNodes : 1)];
        var offset = 0;
        for (var i = 0; i < Points.Length; i++)
        {
            if (i == indexBeforeInsertion +1)
            {
                newSet[i] = insertionNode;

                if (!isInThePast)
                {
                    newSet[i + 1] = returnNode;
                    offset = 1;
                    continue;
                }

                else
                {
                    offset = 1;
                    newSet[i + offset] = returnNode;
                    
                    // add all passed nodes after the insertion until current node
                    for (var u = 0; u < inThePastExtraNodes; u++)
                    {
                        offset += 1;
                        newSet[i + offset] = Points[relativeNodeIndex + u + 1];
                    }

                    continue;
                }
            }

            newSet[i + offset] = Points[i];
        }

        Points = newSet;
        
    }

    public void CreateLinearApproach(int toNodeId, int angle)
    {
        //execute shortcut node at [1] until toNode
        ShortcutNodes(PositionVirtualNode.GetNodeTo.ID, toNodeId, out var _reducedPoint);

        _reducedPoint.IndicateDirectApproach(angle);

        // insert fake node as linear approach beginning - very far
        AddRelativeNodeBefore(toNodeId, angle, -500, toNodeId, out var _veryFarNode);

        // insert fake node as current destination : before very far,  in the place of original next node
        // AddRelativeNodeBefore(_veryFarNode.ID, angle, 500, out var _);

        // hide all lines until linear approach
        for (var i = 0; i < Points.Length; i++)
        {
            if (Points[i].IsLinearApproach)
            {
                break;
            }

            Points[i].IndicateHiddenLine();
        }

        ActiveDirectApproach = true;
    }

    public void AddDisplayPositionNode()
    {
        // ! Position node is added in front of the actual position so that the aircraft can safely turn 
        RoutePoint lastAddedPositionNode;
        

        var routePreviousNodeIndex = PositionVirtualNode.PassedNodeIndex;
        var routePreviousNode = Points[routePreviousNodeIndex];


        if (Aircraft.IsOnRoute)
        {
            // add position node on path

            var activeNextNode = PositionVirtualNode.GetNodeTo;
            var activeSegmentDistancePassed = Aircraft.WalkedDistanceOnSegment;
            
            var activeNextNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
            var routeNextNode = Points[activeNextNodeIndex];

            lastAddedPositionNode = new RoutePoint
            {
                Name = "_Position_",
                Distance = activeSegmentDistancePassed + Aircraft.ForwardThreshold,
                RawDegrees = activeNextNode.RawDegrees,
                Details = "P",
                ID = GetNewId(),
            };

            var newFuturePosition = Geometry.GetNextPosition(Aircraft.PositionFreeOrOnRouteSegment, Aircraft.ForwardThreshold,
                activeNextNode.Degrees);
            
            var differencePosition = routeNextNode.CartesianPosition - newFuturePosition;
            
            var updatedAngle = Geometry.AngleBetween(differencePosition, Vector2.up);

            routeNextNode.RawDegrees = updatedAngle;
            routeNextNode.Distance = differencePosition.magnitude;
        }
        else
        {
            // add position node as from where the aircraft is
            
            // also the position need to be forward with the threshold in the heading direction


            // there is now from node to make cu curve correctly so for now is just not added any forward offset
            // var _nextFuturePosition = Geometry.GetNextPosition(Aircraft.PositionFreeOrOnSegment, FORWARD_THRESHOLD, Aircraft.Heading);
            var nextFuturePosition = Aircraft.PositionFreeOrOnRouteSegment;
            var routeNextNode = Points[1];
            var angleTo = Geometry.GetHeadingOfDirection(nextFuturePosition);
            
            lastAddedPositionNode = new RoutePoint
            {
                Name = "_Position_",
                Distance = nextFuturePosition.magnitude,
                RawDegrees =  angleTo,
                Details = "P",
                ID = GetNewId(),
                CartesianPosition = nextFuturePosition
            };

            var differencePosition = routeNextNode.CartesianPosition - nextFuturePosition;
            var updatedAngle = Geometry.GetHeadingOfDirection(differencePosition);
            routeNextNode.RawDegrees = updatedAngle;
            routeNextNode.Distance = differencePosition.magnitude;
        }

        if (ActiveDirectApproach)
        {
            lastAddedPositionNode.IndicateHiddenLine();
        }


        // replace set with new set that also contains insertion node
        var newSet = new RoutePoint[Points.Length + 1];
        var offset = 0;
        for (var i = 0; i < Points.Length; i++)
        {
            if (i == PositionVirtualNode.NextNodeIndex)
            {
                newSet[i] = lastAddedPositionNode;
                offset = 1;
            }

            newSet[i + offset] = Points[i];
        }

        Points = newSet;
    }

}