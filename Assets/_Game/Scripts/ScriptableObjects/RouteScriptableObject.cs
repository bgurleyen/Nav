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

        public void ComputeTrace(bool isMod = false)
        {
            TracedRoute.Compute(Points, isMod);
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

        public bool GetPointByName(string pointName, out RoutePoint point)
        {
            for (int i = 0; i < Points.Length; i++)
            {
                var p = Points[i];
                if (p.Name == pointName)
                {
                    point = p;
                    return true;
                }
            }

            point = null;
            return false;
        }

        public bool GetPoint(int nodeId, out RoutePoint point, out int index)
        {
            Points.GetNodeIndex(nodeId, out index);
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

        // Method to remove duplicates that were added afterwards
        public void RemoveSubsequentDuplicates()
        {
            var seenIds = new HashSet<string>();
            var uniquePoints = new List<RoutePoint>();

            // Iterate through the Points array and add only the first occurrence of each ID to the new list
            foreach (var point in Points)
            {
                if (!seenIds.Contains(point.Name))
                {
                    seenIds.Add(point.Name);
                    uniquePoints.Add(point);
                }
            }

            // Assign the unique points back to the Points array
            Points = uniquePoints.ToArray();
            Debug.Log("Afterrasdasd ::" + Points.Length);
        }

        // to be executed on ACTIVE route
        public bool FindFreeFlightDirectExitScenario(out RoutePosition routeIntersection)
        {
            var nan = new Vector2(-100, -100);

            routeIntersection = new RoutePosition { SegmentIndex = -1, SegmentVertexIndex = -1, SegmentVertex = nan };

            var aircraftPosition = Session.PlayerAircraft.NMPosition; // would be free since we are in free flight
            var aircraftDirection = Geometry.GetDirectionFromHeading(Session.PlayerAircraft.HeadingDegrees);
            var segmentEnd = Vector2.zero;


            for (var i = 1; i < Points.Length; i++)
            {
                var segmentStart = segmentEnd;

                segmentEnd = Points[i].CartesianPosition;

                if (Points[i].IsHiddenLine ||
                    Points[i].IsAfterDiscontinuity) //$^% ask if we can join discontinuity segments
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

                TracedRoute.ComputedLines[routeIntersection.SegmentIndex].FindFurthestSeekTargetOnSegment(
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
            out RoutePoint reducedPoint) // $^% refactor for passed nodes ?
        {
            Points.GetNodeIndex(firstIdNodeToDissolve, out var startIndex);

            Points.GetNodeIndex(toId, out var endIndex);

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

        private void AddRelativeNodeAfter(int relativeFromNodeId, float rawDegrees, int distance,
            out RoutePoint insertionNode, out RoutePoint afterInsertion, bool addCurveOffset = true)
        {
            Points.GetNodeIndex(relativeFromNodeId, out var relativeFromNodeIndex);
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

        public void AddDirectToCartesianNodeBefore(int originalRouteNodeId, int nextNodeId)
        {
            Session.OriginalReferenceRoute.GetPoint(originalRouteNodeId, out var originalRoutePoint, out _);
            GetPoint(nextNodeId, out var modeAfterNode, out var modIndexAfter);
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

        public void CreateLinearApproach(int toNodeId, int angle)
        {
            RoutePoint reducedPoint;
            var firstNodeToDissolve = PositionVirtualNode.GetNodeToOnActive.ID;
            // the TO point on active route can be already shortcuted on MOD
            if (!Points.GetNodeIndex(firstNodeToDissolve, out _))
            {
                Points.GetNodeIndex(toNodeId, out var toNodeIndex);
                reducedPoint = Points[toNodeIndex];
            }
            else
            {
                //execute shortcut node at [1] until toNode
                ShortcutNodes(PositionVirtualNode.GetNodeToOnActive.ID, toNodeId, out reducedPoint);
            }

            reducedPoint.IndicateDirectApproach(angle);


            // insert fake node as linear approach beginning - very far
            AddRelativeNodeBefore(toNodeId, angle, -300, toNodeId, out var _veryFarNode);

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
        }

        public void AddRejoinIntersectionNode(Vector2 segmentVertex, int segmentIndex)
        {
            var intersectionNodeToAdd = RoutePoint.ConstructFromPosition(
                segmentVertex,
                Points[segmentIndex - 1],
                Points[segmentIndex],
                GetNewId(true),
                "",
                "_rejoin_");

            Debug.Log("================" +
                " _reJoin ================");
            // If we want active route to contain also the path from the current position we can also insert position nodes
            ConstructPositionNodes(Points[segmentIndex - 1], intersectionNodeToAdd,
                out var airplanePositionNodeToAdd, out var frontOfAirplanePositionNodeToAdd);


            InsertNodes(segmentIndex,
                airplanePositionNodeToAdd,
                frontOfAirplanePositionNodeToAdd,
                intersectionNodeToAdd);

            ComputeTrace();
        }

        private int FindNodeBeforePlaneOnMode()
        {
            int foundIndex = 0;
            for (foundIndex = 0;
                 foundIndex < Session.ModRoute.Points.Length &&
                 foundIndex < Session.ActiveRoute.Points.Length; foundIndex++)
            {
                if (Session.ModRoute.Points[foundIndex].ID != Session.ActiveRoute.Points[foundIndex].ID)
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
            var nodeBeforePlaneIndex = FindNodeBeforePlaneOnMode();
            var nodeBeforePosition = Points[nodeBeforePlaneIndex];
            int nodeAfterPlaneIndex = nodeBeforePlaneIndex + 1;

            Debug.Log("------------------------ nodeAfterPlaneIndex :" + nodeAfterPlaneIndex);
            Debug.Log("nodeBeforePosition :" + nodeBeforePosition.Name + "|| Points[nodeAfterPlaneIndex] :" + Points[nodeAfterPlaneIndex].Name);
            ConstructPositionNodes(nodeBeforePosition, Points[nodeAfterPlaneIndex],
                out var airplanePositionNodeToAdd, out var frontOfAirplanePositionNodeToAdd);

            InsertNodes(nodeAfterPlaneIndex,
                airplanePositionNodeToAdd,
                frontOfAirplanePositionNodeToAdd);

            ComputeCartesianPositions();
        }

        private void ConstructPositionNodes(RoutePoint nodeBeforePosition, RoutePoint routeNextNode,
            out RoutePoint airplanePositionNodeToAdd, out RoutePoint frontOfAirplanePositionNodeToAdd)
        {
            airplanePositionNodeToAdd = RoutePoint.ConstructFromPosition(
                Session.PlayerAircraft.NMPosition,
                nodeBeforePosition,
                null,
                GetNewId(true),
                "P",
                "_Position_0");

            // ! another Position node is added in front of the actual position so that the aircraft can safely turn 
            var futurePosition = Geometry.GetNextPosition(
                Session.PlayerAircraft.NMPosition,
                Session.Settings.ForwardThreshold,
                -Session.PlayerAircraft.HeadingDegrees);

            frontOfAirplanePositionNodeToAdd = RoutePoint.ConstructFromPosition(
                futurePosition,
                airplanePositionNodeToAdd,
                routeNextNode,
                GetNewId(true) + 1,
                "P",
                "_Position_");


            airplanePositionNodeToAdd.IndicateHiddenLine();
            if (HasActiveDirectApproach(out _))
            {
                frontOfAirplanePositionNodeToAdd.IndicateHiddenLine();
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
            for (int i = 0; i < Points.Length; i++)
            {

                Debug.Log("ID :" + Points[i].ID + "|| Name :" + Points[i].Name + "|| Details :" + Points[i].Details);
            }
        }

        public void AddNodeAtLast(int NodeId, string name, float rawDegrees, float distance)
        {
            RoutePoint insertionNode = new RoutePoint
            {
                Name = name,
                Distance = distance,
                RawDegrees = rawDegrees,
                ID = NodeId
            };

            var newSet = new RoutePoint[Points.Length + 1];

            for (var i = 0; i < Points.Length; i++)
            {
                newSet[i] = Points[i];
            }
            newSet[Points.Length] = insertionNode;

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
            Points.GetNodeIndex(nodeId, out var nodeIndexToRemove);

            if (nodeIndexToRemove < 0 || nodeIndexToRemove >= Points.Length)
            {
                throw new ArgumentException($"Node with ID {nodeId} not found.");
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