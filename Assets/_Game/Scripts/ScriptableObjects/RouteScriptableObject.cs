using UnityEngine;
using System.Linq;

namespace Navigation
{
    [CreateAssetMenu(fileName = "RouteData", menuName = "ScriptableObjects/RouteData")]
    public class RouteScriptableObject : ScriptableObject
    {
        public RoutePoint[] Points;
        public TracedRoute TracedRoute { get; private set; }

        public bool ActiveDirectApproach { get; private set; }
        public int FirstSpeedRegulationNodeId { get; set; }
        public int FirstAltRegulationNodeId { get; set; }

        public void Init(bool regenerateIds)
        {
            TracedRoute = new TracedRoute();

            if (regenerateIds)
            {
            
                for (var i = 0; i < Points.Length; i++)
                {
                    Points[i].ID = i;
                }
            }
        }

        public void ComputeCartesianPositions()
        {
            var currentPosition = Vector2.zero;
            for (var i = 0; i < Points.Length; i++)
            {
                if (i > 0)
                {
                    currentPosition = Geometry.GetNextPosition(currentPosition, Points[i].Distance, Points[i].Degrees);
                }

                Points[i].CartesianPosition = currentPosition;
            }
        }

        public void ComputeTrace()
        {
            TracedRoute.Compute(Points);
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


        private int GetNewId(bool safe = false)
        {
            var id = Points.Length;
            while (Points.Any(x => x.ID == id))
            {
                id++;
            }

            return id + (safe ? 50 : 0);
        }

        private string GetNewName(string fromNode)
        {
            var intro = fromNode.Substring(0, 3);
            var index = 1;

            while (Points.Any(x => x.Name == intro + index.ToString("00")))
            {
                index++;
            }

            return intro + index.ToString("00");
        }

        public RouteScriptableObject CloneAndInit()
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

            newSet.Init(false);
            return newSet;
        }

        public void OnPathRejoined(Vector3 oldPositionVertex)
        {
            // PathLines.oldPositionVertex = oldPositionVertex;
            // ActiveDirectApproach = false;
        }


        // to be executed on ACTIVE route
        public bool FindFreeFlightDirectExitScenario(out Vector2 tipOfTurn, out Vector2 exitPoint,
            out int exitSegmentIndex)
        {
            var nan = new Vector2(-100, -100);

            exitPoint = nan;
            tipOfTurn = nan;
            var aircraftPosition = Session.PlayerAircraft.NMPosition; // would be free since we are in free flight
            var aircraftDirection = Geometry.GetDirectionFromHeading(Session.PlayerAircraft.HeadingDegrees);
            var segmentEnd = Vector2.zero;

            exitSegmentIndex = -1;
            var intersection = Vector2.zero;

            for (var i = 1; i < Points.Length; i++)
            {
                var segmentStart = segmentEnd;

                segmentEnd = Points[i].CartesianPosition;

                if (Points[i].IsHiddenLine ||
                    Points[i].IsAfterDiscontinuity) //$^% ask if we can join discontinuity segments
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

            if (exitSegmentIndex < 0)
            {
                return false;
            }

            tipOfTurn = intersection;

            MakeSureForTurningSpace(ref tipOfTurn, out exitPoint, ref exitSegmentIndex);

            return true;

        }

        // move the tip of turn further to have space for turn
        private void MakeSureForTurningSpace(ref Vector2 tipOfTurn, out Vector2 exitPoint,
            ref int exitSegmentIndex)
        {
            var nextNextNodePosition = Points[exitSegmentIndex].CartesianPosition;
            var exitDirection = nextNextNodePosition - tipOfTurn;
            var headingDiff = Vector2.Dot(exitDirection, Geometry.GetDirectionFromHeading(Session.PlayerAircraft.HeadingDegrees));

            var minProduct = 0;
            // add more offset if the directions are opposite
            // var neededExitPointOffset = headingDiff < minProduct ? 1:0.5f;
            var neededTipOffset = (headingDiff < minProduct ? 4 : 1f) * GameSettingsScriptableObject.GetMinRadius;

            Debug.Log($"headingDiff: {headingDiff} < dotProd: {minProduct}");

            exitPoint = Vector2.Lerp(tipOfTurn, nextNextNodePosition,
                neededTipOffset / Vector2.Distance(tipOfTurn, nextNextNodePosition));


            // check if is to close to end of line - should exit in the next one
            while (Points.Length > exitSegmentIndex + 1 &&
                   Vector2.SqrMagnitude(nextNextNodePosition - exitPoint) <
                   Session.Settings.ForwardThreshold * Session.Settings.ForwardThreshold)
            {
                Debug.Log(
                    $"too close to corner exit in next segment ({Vector2.SqrMagnitude(nextNextNodePosition - tipOfTurn)})");

                tipOfTurn = nextNextNodePosition;
                exitSegmentIndex++;
                nextNextNodePosition = Points[exitSegmentIndex].CartesianPosition;


                exitDirection = nextNextNodePosition - tipOfTurn;
                headingDiff = Vector2.Dot(exitDirection, Geometry.GetDirectionFromHeading(Session.PlayerAircraft.HeadingDegrees));
                neededTipOffset = (headingDiff < minProduct ? 4 : 1f) * GameSettingsScriptableObject.GetMinRadius;

                tipOfTurn = Vector2.Lerp(tipOfTurn, nextNextNodePosition,
                    neededTipOffset / Vector2.Distance(tipOfTurn, nextNextNodePosition));

                exitPoint = Vector2.Lerp(tipOfTurn, nextNextNodePosition,
                    neededTipOffset / Vector2.Distance(tipOfTurn, nextNextNodePosition));
            }

        }

        // public bool TransferPathToRoute(Vector2 exitPoint, int lineIndex,
        //     out PathPositionInfo intersectionRoutePathInfo, out float segmentDistanceUntilIntersection)
        // {
        //     intersectionRoutePathInfo = new PathPositionInfo();
        //     var segmentStart = Points[lineIndex - 1].CartesianPosition;
        //     var segmentEnd = Points[lineIndex].CartesianPosition;
        //
        //     segmentDistanceUntilIntersection = (exitPoint - segmentStart).magnitude;
        //     if (Session.ActiveRoute.TracedRoute.FindClosestVertexToPositionOnLineActive(
        //             exitPoint, lineIndex, out var targetVertexIndex,
        //             out var targetVertexPosition))
        //     {
        //         intersectionRoutePathInfo = new PathPositionInfo
        //         {
        //             CurrentNodeIndex = lineIndex,
        //             HeadingBefore = Session.PlayerAircraft.HeadingDegrees
        //         };
        //         return true;
        //     }
        //
        //     return false;
        // }

        public void ShortcutNodes(int firstIdNodeToDissolve, int toId,
            out RoutePoint reducedPoint) // $^% refactor for passed nodes ?
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
            var angle = Geometry.PositiveAngleBetween(endPosition, Vector2.up);

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

        public void AddRelativeNodeOnDirection(int nodeId, int distance, int relativeNodeId,
            out RoutePoint insertionNode,
            out RoutePoint afterInsertion) // $^% todo refactor for passed nodes ?
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

        private void AddRelativeNodeAfter(int relativeFromNodeId, float rawDegrees, int distance,
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
            afterInsertion.RawDegrees = Geometry.PositiveAngleBetween(Vector2.up, returnDirection);
            afterInsertion.Distance = returnDirection.magnitude;

            // simulate the curve to the the needed offset
            // @£$
           // var lastLine = new MarkLine(relativeFromNode, _drawerUnitLength);
            //lastLine.InitBeginning();

            //LinesComputer.ComputeLine(lastLine, out var testLine, insertionNode, afterInsertion);

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

            var indexBeforeInsertion =
                isInThePast ? PositionVirtualNode.PassedNodeIndexForMode : originalBeforeNodeIndex - 1;

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
            if (showDiscontinuity)
            {
                returnNode.IndicateDiscontinuityBefore();
            }

            if (isInThePast)
            {
                returnNode.ID = GetNewId() + Points.Length;
            }

            // replace set with new set that also contains insertion node
            var inThePastExtraNodes = indexBeforeInsertion - relativeNodeIndex + 1;
            var newSet = new RoutePoint[Points.Length + (isInThePast ? 1 + inThePastExtraNodes : 1)];
            var offset = 0;
            for (var i = 0; i < Points.Length; i++)
            {
                if (i == indexBeforeInsertion + 1)
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
            ShortcutNodes(PositionVirtualNode.GetNodeTo.ID, toNodeId, out var reducedPoint);

            reducedPoint.IndicateDirectApproach(angle);

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

        public void AddCloseRejoinIntersectionNode()
        {
            // insert intersection node
            if (TracedRoute.FindCloseToRouteSegmentDestination(
                    Session.Settings.RejoinDistance,
                    out var lastFoundSegmentVertex,
                    out var lastFoundSegmentVertexIndex,
                    out var lastFoundSegmentIndex,
                    out var foundAtDistanceOnSegment,
                    out var reachedRouteEnd,
                    segmentBeginningIsAlwaysValid: false))
            {
                var intersectionNodeToAdd = RoutePoint.ConstructFromPosition(
                    lastFoundSegmentVertex,
                    Points[lastFoundSegmentIndex - 1],
                    GetNewId(true),
                    "",
                    "_Close_rejoin");

                // replace set with new set that also contains insertion node
                var newSet = new RoutePoint[Points.Length + 1];
                var offset = 0;
                for (var i = 0; i < Points.Length; i++)
                {
                    if (i == lastFoundSegmentIndex)
                    {
                        newSet[i] = intersectionNodeToAdd;
                        offset = 1;
                    }

                    newSet[i + offset] = Points[i];
                }

                Points = newSet;
            }

            var nodeBeforePosition = Session.PlayerAircraft.IsOnRoute
                ? PositionVirtualNode.GetNodeFrom
                : Points[0];

            var airplanePositionNodeToAdd = RoutePoint.ConstructFromPosition(
                Session.PlayerAircraft.NMPosition,
                nodeBeforePosition,
                GetNewId(true),
                "P",
                "_Position_0");

            var futurePosition = Geometry.GetNextPosition(
                Session.PlayerAircraft.NMPosition,
                Session.Settings.ForwardThreshold,
                -Session.PlayerAircraft.HeadingDegrees);

            var frontOfAirplanePositionNodeToAdd = RoutePoint.ConstructFromPosition(
                futurePosition,
                airplanePositionNodeToAdd,
                GetNewId(true) + 1,
                "P",
                "_Position_");


            var activeNextNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
            var activeNextNodeOnMode = Points[activeNextNodeIndex];


            var differencePosition = activeNextNodeOnMode.CartesianPosition - futurePosition;

            var updatedAngle = Geometry.PositiveAngleBetween(differencePosition, Vector2.up);

            activeNextNodeOnMode.RawDegrees = updatedAngle;
            activeNextNodeOnMode.Distance = differencePosition.magnitude;

            // replace set with new set that also contains insertion node
            var newSet2 = new RoutePoint[Points.Length + 2];
            var offset2 = 0;
            for (var i = 0; i < Points.Length; i++)
            {
                if (i == PositionVirtualNode.NextNodeIndex)
                {
                    newSet2[i] = airplanePositionNodeToAdd;
                    newSet2[i + 1] = frontOfAirplanePositionNodeToAdd;
                    offset2 = 2;
                }

                newSet2[i + offset2] = Points[i];
            }

            Points = newSet2;

            ComputeCartesianPositions();

            ComputeTrace();
        }

        public void AddDisplayPositionNode()
        {
            // ! Position node is added in front of the actual position so that the aircraft can safely turn 

            var nodeBeforePosition = Session.PlayerAircraft.IsOnRoute
                ? PositionVirtualNode.GetNodeFrom
                : Points[0];
            
             var airplanePositionNodeToAdd = RoutePoint.ConstructFromPosition(
                 Session.PlayerAircraft.NMPosition,
                 nodeBeforePosition,
                 GetNewId(true),
                 "P",
                 "_Position_0");

             var futurePosition = Geometry.GetNextPosition(
                 Session.PlayerAircraft.NMPosition,
                 Session.Settings.ForwardThreshold,
                 -Session.PlayerAircraft.HeadingDegrees);

             var frontOfAirplanePositionNodeToAdd = RoutePoint.ConstructFromPosition(
                 futurePosition,
                 airplanePositionNodeToAdd,
                 GetNewId(true) + 1,
                 "P",
                 "_Position_");

            if (Session.PlayerAircraft.IsOnRoute)
            {
                var activeNextNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
                var activeNextNodeOnMode = Points[activeNextNodeIndex];


                var differencePosition = activeNextNodeOnMode.CartesianPosition - futurePosition;

                var updatedAngle = Geometry.PositiveAngleBetween(differencePosition, Vector2.up);

                activeNextNodeOnMode.RawDegrees = updatedAngle;
                activeNextNodeOnMode.Distance = differencePosition.magnitude;
            }
            else
            {
                var routeNextNode = Points[1];
                var differencePosition = routeNextNode.CartesianPosition - futurePosition;
                var updatedAngle = Geometry.PositiveAngleBetween(differencePosition, Vector2.up);
                routeNextNode.RawDegrees = updatedAngle;
                routeNextNode.Distance = differencePosition.magnitude;
            }

            airplanePositionNodeToAdd.IndicateHiddenLine();
            if (ActiveDirectApproach)
            {
                frontOfAirplanePositionNodeToAdd.IndicateHiddenLine();
            }

            // replace set with new set that also contains insertion node
            var newSet = new RoutePoint[Points.Length + 2];
            var offset = 0;
            for (var i = 0; i < Points.Length; i++)
            {
                if (i == PositionVirtualNode.NextNodeIndex)
                {
                    newSet[i] = airplanePositionNodeToAdd;
                    newSet[i + 1] = frontOfAirplanePositionNodeToAdd;
                    offset = 2;
                }

                newSet[i + offset] = Points[i];
            }

            Points = newSet;
            
            ComputeCartesianPositions();
        }
    }
}