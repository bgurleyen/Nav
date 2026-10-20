using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Navigation
{
    [CreateAssetMenu(fileName = "RouteData", menuName = "ScriptableObjects/RouteData")]
    public class RouteScriptableObject : ScriptableObject
    {
        public RoutePoint[] Points;
        public TracedRoute TracedRoute { get; private set; }

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


        public bool HasActiveDirectApproach(out int linearPointIndex)
        {
            linearPointIndex = -1;
            for (var index = 0; index < Points.Length; index++)
            {
                var t = Points[index];
                if (t.IsLinearApproach)
                {
                    linearPointIndex = index;
                    return true;
                }
            }

            return false;
        }

        public void ComputeCartesianPositions()
        {
            // Point 0 is the route origin. A course intercept places it 200 NM before
            // the fix, not at world zero, and the rest of the chain hangs off that point.
            var currentPosition = Points != null && Points.Length > 0
                ? Points[0].CartesianPosition
                : Vector2.zero;
            for (var i = 0; i < Points.Length; i++)
            {
                if (i > 0)
                {
                    currentPosition = Geometry.GetNextPosition(currentPosition, Points[i].Distance, Points[i].Degrees);
                }

                Points[i].CartesianPosition = currentPosition;
            }
        }

        public void ComputeTrace(bool isMod = false)
        {
            if (TracedRoute == null)
                TracedRoute = new TracedRoute();
            // Points define the path. Call this after a route edit, not every flight frame.
            TracedRoute.Compute(Points, isMod);
        }

        public Vector2 GetCartesianPosition(int lineIndex)
        {
            if (Points == null || lineIndex < 0 || lineIndex >= Points.Length)
                return Vector2.zero;

            var point = Points[lineIndex];
            return point != null ? point.CartesianPosition : Vector2.zero;
        }

        public bool GetPointByName(string pointName, out RoutePoint point)
        {
            if (Points != null)
            {
                for (int i = 0; i < Points.Length; i++)
                {
                    var p = Points[i];
                    if (p != null && p.Name == pointName)
                    {
                        point = p;
                        return true;
                    }
                }
            }

            point = null;
            return false;
        }

        public bool TryGetGatePoint(out RoutePoint point, out int index)
        {
            point = null;
            index = -1;
            if (Points == null)
            {
                return false;
            }

            for (var i = 0; i < Points.Length; i++)
            {
                var p = Points[i];
                if (p == null || !p.IsGate)
                {
                    continue;
                }

                point = p;
                index = i;
                return true;
            }

            return false;
        }

        public bool GetPoint(int nodeId, out RoutePoint point, out int index)
        {
            if (Points == null)
            {
                point = null;
                index = -1;
                return false;
            }

            Points.GetNodeIndex(nodeId, out index);
            return GetPointAt(index, out point);
        }

        public bool GetPointAt(int index, out RoutePoint point)
        {
            if (Points == null || index < 0 || index >= Points.Length)
            {
                point = null;
                return false;
            }

            point = Points[index];
            return point != null;
        }


        private int GetNewId(bool safe = false)
        {
            if (Points == null)
                return safe ? 50 : 0;

            var id = Points.Length;
            while (Points.Any(x => x != null && x.ID == id))
            {
                id++;
            }

            return id + (safe ? 50 : 0);
        }

        private string GetNewName(string fromNode)
        {
            var intro = string.IsNullOrEmpty(fromNode)
                ? "WPT"
                : fromNode.Length >= 3 ? fromNode.Substring(0, 3) : fromNode;
            var index = 1;
            if (Points == null)
                return intro + index.ToString("00");

            while (Points.Any(x => x != null && x.Name == intro + index.ToString("00")))
            {
                index++;
            }

            return intro + index.ToString("00");
        }

        public RouteScriptableObject CloneAndInit()
        {

            //Debug.Log("--- CloneAndInit ---");

            var newSet = CreateInstance<RouteScriptableObject>(); // new DataSetScriptableObject();

            //Debug.Log("-------------- newSet: "+newSet.Points);
            //New Code Shubham...
            //FMC_Screens.Instance.CurrentScreen.OnExecPress();

            newSet.FirstAltRegulationNodeId = FirstAltRegulationNodeId;
            // Debug.Log("---------------FirstAltRegulationNodeID: "+FirstAltRegulationNodeId);

            newSet.FirstSpeedRegulationNodeId = FirstSpeedRegulationNodeId;
            // Debug.Log("---------------FirstSpeedRegulationNodeId: " + FirstSpeedRegulationNodeId);
            newSet.Points = new RoutePoint[Points.Length];

            //Debug.Log("---------------Points.Length: " + Points.Length);

            //Debug.Log("before points::" + Points.Length);

            for (var i = 0; i < Points.Length; i++)
            {
                newSet.Points[i] = Points[i].Clone();
            }


            newSet.Init(false);
            return newSet;
        }

        // to be executed on ACTIVE route
        public bool FindFreeFlightDirectExitScenario(out RoutePosition routeIntersection)
        {
            var nan = new Vector2(-100, -100);

            routeIntersection = new RoutePosition { SegmentIndex = -1, SegmentVertexIndex = -1, SegmentVertex = nan };

            if (Session.PlayerAircraft == null || Points == null || Points.Length < 2)
                return false;

            var aircraftPosition = Session.PlayerAircraft.NMPosition; // would be free since we are in free flight
            var aircraftDirection = Geometry.GetDirectionFromHeading(Session.PlayerAircraft.HeadingDegrees);

            var startI = FindForwardPositionNodeIndex();
            if (startI < 1)
                startI = 1;
            var origin = Points[Mathf.Max(0, startI - 1)];
            if (origin == null)
                return false;
            var segmentEnd = origin.CartesianPosition;

            for (var i = startI; i < Points.Length; i++)
            {
                var segmentStart = segmentEnd;
                var point = Points[i];
                if (point == null)
                    continue;

                segmentEnd = point.CartesianPosition;

                if (point.IsHiddenLine ||
                    point.IsAfterDiscontinuity) //$^% ask if we can join discontinuity segments
                {
                    Debug.Log("Ask if we can join discontinuity segments");
                    continue;
                }

                if (Geometry.FindLineSegmentIntersection(aircraftPosition, aircraftDirection.x, aircraftDirection.y,
                        segmentStart, segmentEnd, out var intersection))
                {
                    routeIntersection.SegmentIndex = i;
                    routeIntersection.SegmentVertex = intersection;
                    break;
                }
            }

            if (routeIntersection.SegmentIndex >= 0)
            {
                var lines = TracedRoute?.ComputedLines;
                var leg = lines != null
                    && routeIntersection.SegmentIndex >= 0
                    && routeIntersection.SegmentIndex < lines.Length
                    ? lines[routeIntersection.SegmentIndex]
                    : null;
                if (leg == null || Session.Settings == null)
                    return false;

                leg.FindFurthestSeekTargetOnSegment(
                    routeIntersection.SegmentVertex,
                    Session.Settings.PilotSeekDistancePathFollow * 1.1f,
                    out _,
                    out routeIntersection.SegmentVertexIndex,
                    out _, beginningIsAlwaysValid: false);

                return true;
            }

            return false;
        }

        public void ShortcutNodes(int firstIdNodeToDissolve, int toId,
            out RoutePoint reducedPoint)
        {
            if (!Points.GetNodeIndex(firstIdNodeToDissolve, out var startIndex) ||
                !Points.GetNodeIndex(toId, out var endIndex) ||
                startIndex < 0 || endIndex < startIndex || endIndex >= Points.Length)
            {
                reducedPoint = null;
                return;
            }

            var offset = endIndex - startIndex;
            var newSet = new RoutePoint[Points.Length - offset];

            for (var i = 0; i < startIndex; i++)
                newSet[i] = Points[i];

            var endPosition =
                Geometry.GetNextPosition(Vector2.zero, Points[startIndex].Distance, Points[startIndex].Degrees);
            for (var i = startIndex; i < endIndex; i++)
                endPosition = Geometry.GetNextPosition(endPosition, Points[i + 1].Distance, Points[i + 1].Degrees);

            var angle = Geometry.PositiveAngleBetween(endPosition, Vector2.up);

            reducedPoint = Points[endIndex].Clone();
            reducedPoint.ClearDetails();
            reducedPoint.Distance = endPosition.magnitude;
            reducedPoint.RawDegrees = angle;
            reducedPoint.IsModified = true;

            newSet[startIndex] = reducedPoint;

            for (var i = endIndex + 1; i < Points.Length; i++)
                newSet[i - offset] = Points[i];

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
            Points.GetNodeIndex(nodeId, out var nodeIndex);
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

        public void AddDirectToCartesianNodeBefore(int originalRouteNodeId, int nextNodeId)
        {
            if (Session.OriginalReferenceRoute == null
                || !Session.OriginalReferenceRoute.GetPoint(originalRouteNodeId, out var originalRoutePoint, out _)
                || originalRoutePoint == null
                || !GetPoint(nextNodeId, out var modeAfterNode, out var modIndexAfter)
                || modeAfterNode == null)
                return;

            GetPointAt(modIndexAfter - 1, out var modNodeBefore);

            // todo to add after discontinuity

            Debug.Log("modeAfterNode :" + modeAfterNode.Name);
            var targetPointToAdd = RoutePoint.ConstructFromPosition(
                originalRoutePoint.CartesianPosition,
                modNodeBefore,
                modeAfterNode,
                modeAfterNode.ID - 1,
                "",
                originalRoutePoint.Name);

            modeAfterNode.IndicateDiscontinuityBefore();


            //Old Code...
            /* var targetPointToAdd = RoutePoint.ConstructFromPosition(
                originalRoutePoint.CartesianPosition,
                modNodeBefore,
                modeAfterNode,
                GetNewId(false),
                "D",
                originalRoutePoint.Name);*/

            Debug.Log("modeAfterNode :" + modeAfterNode.Name + "|| targetPointToAdd: " + targetPointToAdd.Name);
            InsertNodes(modIndexAfter, targetPointToAdd);

            ComputeTrace();
        }


        public void AddRelativeNodeBefore(int beforeNodeId, float rawDegrees, int distance, int relativeNodeId,
            out RoutePoint insertionNode,
            bool showDiscontinuity = false)
        {
            Points.GetNodeIndex(beforeNodeId, out var originalBeforeNodeIndex);
            Points.GetNodeIndex(relativeNodeId, out var relativeNodeIndex);

            var isInThePast = PositionVirtualNode.PassedNodeIndexForMode > originalBeforeNodeIndex - 1;

            var relativeNode = Points[relativeNodeIndex];
            var insertPosition = Geometry.GetNextPosition(relativeNode.CartesianPosition, distance, 360 - rawDegrees);

            var indexBeforeInsertion =
                isInThePast ? PositionVirtualNode.PassedNodeIndexForMode : originalBeforeNodeIndex - 1;

            var positionBeforeInsertion = Points[indexBeforeInsertion].CartesianPosition;

            var insertionAngle = Geometry.AngleOfPosition(insertPosition, positionBeforeInsertion);
            var insertionDistance = (insertPosition - positionBeforeInsertion).magnitude;

            GetPoint(beforeNodeId, out var nodeAfterInsertion, out _);

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

        public const float CourseInterceptLengthNm = 200f;

        /// <summary>Route origin inserted 200 NM before the intercept fix. Not a position stub.</summary>
        public const string CourseInterceptOriginDetails = "I";

        public bool IsCourseInterceptRoute =>
            Points != null && Points.Length > 0 && Points[0].Details == CourseInterceptOriginDetails;

        /// <summary>
        /// Nearest drawn vertex on the route. Used after a course intercept replaces the path ahead.
        /// </summary>
        public void FindClosestPointOnRoute(Vector2 position, out int segmentIndex, out int vertexIndex)
        {
            segmentIndex = 1;
            vertexIndex = 0;
            if (TracedRoute?.ComputedLines == null)
                return;

            var bestSqr = float.MaxValue;
            for (var i = 1; i < TracedRoute.ComputedLines.Length; i++)
            {
                var line = TracedRoute.ComputedLines[i];
                var vertices = line?.SegmentVertices;
                if (vertices == null)
                    continue;

                for (var v = 0; v < vertices.Length; v++)
                {
                    var sqr = (vertices[v] - position).sqrMagnitude;
                    if (sqr >= bestSqr)
                        continue;

                    bestSqr = sqr;
                    segmentIndex = i;
                    vertexIndex = v;
                }
            }

            if (segmentIndex < 1)
                segmentIndex = 1;
            if (segmentIndex >= Points.Length)
                segmentIndex = Mathf.Max(1, Points.Length - 1);
        }

        public void CreateLinearApproach(int toNodeId, int angle)
        {
            if (!Points.GetNodeIndex(toNodeId, out var toIndex) || toIndex < 0)
                return;

            // Refresh world positions while the chain still starts at its current origin.
            ComputeCartesianPositions();

            var course = Mathf.Repeat(angle, 360f);
            var target = Points[toIndex];
            var inbound = Geometry.GetDirectionFromHeading(course);
            var startPosition = target.CartesianPosition - inbound * CourseInterceptLengthNm;

            var start = new RoutePoint
            {
                Name = "INTC",
                ID = GetNewId(),
                Details = CourseInterceptOriginDetails,
                Distance = 0f,
                RawDegrees = course,
                CartesianPosition = startPosition
            };

            // Drop everything before the selected fix. The tail of the old route stays.
            var tailCount = Points.Length - toIndex;
            var newSet = new RoutePoint[tailCount + 1];
            newSet[0] = start;
            for (var i = 0; i < tailCount; i++)
                newSet[i + 1] = Points[toIndex + i];

            target.ClearDetails();
            target.HasHeldIntercept = false;
            target.Distance = CourseInterceptLengthNm;
            target.RawDegrees = course;
            target.IsModified = true;

            // Direct-to leaves a discontinuity on the next leg. The route now continues through it.
            if (newSet.Length > 2 && newSet[2] != null && newSet[2].IsAfterDiscontinuity)
                newSet[2].ClearDetails();

            Points = newSet;
            ComputeCartesianPositions();
        }

        private int FindNodeBeforePlaneOnMode()
        {
            var modPoints = Session.ModRoute?.Points;
            var activePoints = Session.ActiveRoute?.Points;
            if (modPoints == null || activePoints == null)
                return -1;

            int foundIndex = 0;
            for (foundIndex = 0;
                 foundIndex < modPoints.Length &&
                 foundIndex < activePoints.Length; foundIndex++)
            {
                var modPoint = modPoints[foundIndex];
                var activePoint = activePoints[foundIndex];
                if (modPoint == null || activePoint == null || modPoint.ID != activePoint.ID)
                {
                    foundIndex -= 1;
                    break;
                }

                if (foundIndex == PositionVirtualNode.PassedNodeIndex)
                {
                    break;
                }
            }

            return foundIndex;
        }

        public void AddModPositionNodes()
        {
            // Course intercept already replaced the route ahead of the aircraft.
            // Do not splice a present-position stub in front of the 200 NM line.
            if (IsCourseInterceptRoute)
                return;

            if (Points == null)
                return;

            var nodeBeforePlaneIndex = FindNodeBeforePlaneOnMode();
            if (nodeBeforePlaneIndex < 0 || nodeBeforePlaneIndex >= Points.Length - 1)
                return;

            var nodeBeforePosition = Points[nodeBeforePlaneIndex];
            int nodeAfterPlaneIndex = nodeBeforePlaneIndex + 1;
            var routeNextNode = Points[nodeAfterPlaneIndex];
            if (nodeBeforePosition == null || routeNextNode == null)
                return;

            ConstructPositionNodes(nodeBeforePosition, routeNextNode,
                out var airplanePositionNodeToAdd, out var frontOfAirplanePositionNodeToAdd,
                startAheadNm: GetLiveStartAheadNm());

            var intendedStart = airplanePositionNodeToAdd.CartesianPosition;
            var intendedFuture = frontOfAirplanePositionNodeToAdd.CartesianPosition;
            var intendedNext = routeNextNode.CartesianPosition;

            InsertNodes(nodeAfterPlaneIndex,
                airplanePositionNodeToAdd,
                frontOfAirplanePositionNodeToAdd);

            ComputeCartesianPositions();

            SnapPositionStubToWorld(
                nodeBeforePosition,
                airplanePositionNodeToAdd,
                frontOfAirplanePositionNodeToAdd,
                routeNextNode,
                intendedStart,
                intendedFuture,
                intendedNext);
        }

        public int FindForwardPositionNodeIndex()
        {
            for (var i = 0; i < Points.Length; i++)
            {
                if (Points[i].Name == "_Position_")
                    return i;
            }

            for (var i = 0; i < Points.Length; i++)
            {
                if (Points[i].IsPositionNode)
                    return i;
            }

            return -1;
        }

        private const float DirectToLookAheadSeconds = 5f;

        private static float GetLiveStartAheadNm()
        {
            var lookAheadNm = Mathf.Max(0f, Calculator.GS) / 3600f * DirectToLookAheadSeconds;
            var tickFloorNm = Mathf.Max(0.02f, Session.Settings.PlayerTickDistance * 2f);
            return Mathf.Max(lookAheadNm, tickFloorNm);
        }

        private static void SnapPositionStubToWorld(
            RoutePoint nodeBeforePosition,
            RoutePoint airplanePositionNode,
            RoutePoint frontOfAirplanePositionNode,
            RoutePoint routeNextNode,
            Vector2 startPosition,
            Vector2 futurePosition,
            Vector2 nextPosition)
        {
            airplanePositionNode.CartesianPosition = startPosition;
            airplanePositionNode.Distance = Vector2.Distance(startPosition, nodeBeforePosition.CartesianPosition);
            airplanePositionNode.RawDegrees = Geometry.AngleOfPosition(startPosition, nodeBeforePosition.CartesianPosition);

            frontOfAirplanePositionNode.CartesianPosition = futurePosition;
            frontOfAirplanePositionNode.Distance = Vector2.Distance(futurePosition, startPosition);
            frontOfAirplanePositionNode.RawDegrees = Geometry.AngleOfPosition(futurePosition, startPosition);

            if (routeNextNode == null)
                return;

            routeNextNode.CartesianPosition = nextPosition;
            routeNextNode.Distance = Vector2.Distance(nextPosition, futurePosition);
            routeNextNode.RawDegrees = Geometry.AngleOfPosition(nextPosition, futurePosition);
        }

        private void ConstructPositionNodes(RoutePoint nodeBeforePosition, RoutePoint routeNextNode,
            out RoutePoint airplanePositionNodeToAdd, out RoutePoint frontOfAirplanePositionNodeToAdd,
            float startAheadNm = 0f)
        {
            var aircraftPos = Session.PlayerAircraft.NMPosition;
            // Same convention as aircraft movement / ND nose — never flip toward a behind waypoint.
            var forwardDir = Geometry.GetDirectionFromHeading(Session.PlayerAircraft.HeadingDegrees);
            if (forwardDir.sqrMagnitude < 0.0001f)
                forwardDir = Vector2.up;
            else
                forwardDir.Normalize();

            var startPosition = startAheadNm > 0f
                ? aircraftPos + forwardDir * startAheadNm
                : aircraftPos;
            var futurePosition = startPosition + forwardDir * Session.Settings.ForwardThreshold;

            airplanePositionNodeToAdd = RoutePoint.ConstructFromPosition(
                startPosition,
                nodeBeforePosition,
                null,
                GetNewId(true),
                "P",
                "_Position_0");

            frontOfAirplanePositionNodeToAdd = RoutePoint.ConstructFromPosition(
                futurePosition,
                airplanePositionNodeToAdd,
                routeNextNode,
                GetNewId(true) + 1,
                "P",
                "_Position_");

            airplanePositionNodeToAdd.Details = "HP";
            if (HasActiveDirectApproach(out _))
            {
                frontOfAirplanePositionNodeToAdd.Details = "HP";
            }
        }

        private void InsertNodes(int beforeIndex, params RoutePoint[] nodes)
        {
            var newSet = new RoutePoint[Points.Length + nodes.Length];
            var offset = 0;
            for (var i = 0; i < Points.Length; i++)
            {
                if (i == beforeIndex)
                {
                    for (int j = 0; j < nodes.Length; j++)
                    {
                        newSet[i + j] = nodes[j];
                    }

                    offset = nodes.Length;

                }

                newSet[i + offset] = Points[i];
            }


            Points = newSet;
        }

        //code for remove a point from the array at specific index
        public void RemovePoint(int pointId)
        {
            // Find the index of the point to be removed
            int indexToRemove = -1;
            for (int i = 0; i < Points.Length; i++)
            {
                if (Points[i].ID == pointId)
                {
                    indexToRemove = i;
                    break;
                }
            }

            // If the point was not found, exit the method
            if (indexToRemove == -1)
            {
                Debug.LogWarning($"Point with ID {pointId} not found.");
                return;
            }

            // Create a new array with one less element
            var newPoints = new RoutePoint[Points.Length - 1];

            // Copy elements to the new array, skipping the element to be removed
            for (int i = 0, j = 0; i < Points.Length; i++)
            {
                if (i != indexToRemove)
                {
                    newPoints[j++] = Points[i];
                }
            }

            // Assign the new array to the Points field
            Points = newPoints;
        }

        /*public void RemoveNode(int nodeId, out RoutePoint removedNode)
        {
            Points.GetNodeIndex(nodeId, out var nodeIndexToRemove);

            if (nodeIndexToRemove < 0 || nodeIndexToRemove >= Points.Length)
            {
                throw new ArgumentException($"Node with ID {nodeId} not found.");
            }

            removedNode = Points[nodeIndexToRemove].Clone();

            var isInThePast = PositionVirtualNode.PassedNodeIndexForMode > nodeIndexToRemove;

            var newSet = new RoutePoint[Points.Length - 1];
            var offset = 0;

            for (var i = 0; i < Points.Length; i++)
            {
                if (i == nodeIndexToRemove)
                {
                    offset = -1;
                    continue;
                }

                if (i == nodeIndexToRemove + 1)
                {
                    Points[i].Details = "D";
                }

                newSet[i + offset] = Points[i];

                if (i == nodeIndexToRemove + 1 && i < Points.Length)
                {
                    var previousIndex = nodeIndexToRemove - 1;
                    if (previousIndex >= 0)
                    {
                        var previousPosition = Points[previousIndex].CartesianPosition;
                        var currentPosition = Points[i].CartesianPosition;

                        newSet[i + offset].Distance = (currentPosition - previousPosition).magnitude;
                        newSet[i + offset].RawDegrees = Geometry.AngleOfPosition(currentPosition, previousPosition);

                        if (Points[i].IsAfterDiscontinuity)
                        {
                            newSet[i + offset].IndicateDiscontinuityBefore();
                        }
                    }
                }
            }

            Points = newSet;
        }*/

        public void RemoveNode(int nodeId, out RoutePoint removedNode)
        {
            removedNode = null;
            if (Points == null || Points.Length <= 1)
            {
                Debug.LogWarning($"Node with ID {nodeId} was not removed.");
                return;
            }

            Points.GetNodeIndex(nodeId, out var nodeIndexToRemove);

            if (nodeIndexToRemove < 0 || nodeIndexToRemove >= Points.Length)
            {
                Debug.LogWarning($"Node with ID {nodeId} not found.");
                return;
            }

            removedNode = Points[nodeIndexToRemove].Clone();

            var newSet = new RoutePoint[Points.Length - 1];
            for (var i = 0; i < Points.Length; i++)
            {
                if (i == nodeIndexToRemove)
                    continue;

                var targetIndex = i < nodeIndexToRemove ? i : i - 1;
                newSet[targetIndex] = Points[i];

                if (i == nodeIndexToRemove + 1 && i < Points.Length)
                {
                    newSet[targetIndex].Details = "D";
                    if (nodeIndexToRemove > 0)
                    {
                        var previousPosition = Points[nodeIndexToRemove - 1].CartesianPosition;
                        var currentPosition = Points[i].CartesianPosition;
                        newSet[targetIndex].Distance = (currentPosition - previousPosition).magnitude;
                        newSet[targetIndex].RawDegrees = Geometry.AngleOfPosition(currentPosition, previousPosition);

                        if (Points[i].IsAfterDiscontinuity)
                        {
                            newSet[targetIndex].IndicateDiscontinuityBefore();
                        }
                    }
                }
            }

            Points = newSet;
        }
    }
}

public struct RoutePosition : System.IEquatable<RoutePosition>
{
    public Vector2 SegmentVertex;
    public int SegmentVertexIndex;
    public int SegmentIndex;

    public static bool operator ==(RoutePosition a, RoutePosition b) => a.Equals(b);

    public static bool operator !=(RoutePosition a, RoutePosition b) => !a.Equals(b);

    public bool Equals(RoutePosition other) =>
        SegmentIndex == other.SegmentIndex && SegmentVertexIndex == other.SegmentVertexIndex;

    public override bool Equals(object obj) => obj is RoutePosition other && Equals(other);

    public override int GetHashCode() => System.HashCode.Combine(SegmentIndex, SegmentVertexIndex);
}