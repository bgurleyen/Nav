using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "RouteData", menuName = "ScriptableObjects/RouteData")]
public class RouteScriptableObject : ScriptableObject
{
    public bool ActiveDirectApproach { get; private set; }
    public int FirstSpeedRegulationNodeId { get; set; }
    public int FirstAltRegulationNodeId { get; set; }

    public RoutePoint[] Points;

    static Aircraft Aircraft => GameManager.Instance.Aircraft;
    public RoutePoint LastAddedPositionNode { get; private set; }

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

    public void InitIds()
    {
        for (var i = 0; i < Points.Length; i++)
        {
            Points[i].ID = i;
        }
    }

    public bool GetPoint(int nodeId, out RoutePoint point)
    {
        var _index = GetIndex(nodeId);
        return GetPointAt(_index, out point);
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

    public int GetIndex(int nodeId)
    {
        for (var i = 0; i < Points.Length; i++)
        {
            if (Points[i].ID == nodeId)
            {
                return i;
            }
        }

        return -1;
    }

    int GetNewId()
    {
        var _id = Points.Length;
        while (Points.Any(x => x.ID == _id))
        {
            _id++;
        }

        return _id;
    }

    string GetNewName(string fromNode)
    {
        var _intro = fromNode.Substring(0, 3);
        var _index = 1;

        while (Points.Any(x => x.Name == _intro + _index.ToString("00")))
        {
            _index++;
        }

        return _intro + _index.ToString("00");
    }

    public RouteScriptableObject Clone()
    {
        var _newSet = CreateInstance<RouteScriptableObject>(); // new DataSetScriptableObject();
        _newSet.ActiveDirectApproach = ActiveDirectApproach;
        _newSet.FirstAltRegulationNodeId = FirstAltRegulationNodeId;
        _newSet.FirstSpeedRegulationNodeId = FirstSpeedRegulationNodeId;
        _newSet.Points = new RoutePoint[Points.Length];
        for (var i = 0; i < Points.Length; i++)
        {
            _newSet.Points[i] = Points[i].Clone();
        }

        return _newSet;
    }

    public void OnPathRejoined()
    {
        ActiveDirectApproach = false;
    }

    public bool FindFreeFlightDirectExitScenario(out Vector2 centerOfTurn, out Vector2 exitPoint, out int exitSegmentIndex)
    {

        var _nan = new Vector2(-100, -100);

        exitPoint = _nan;
        centerOfTurn = _nan;
        var _aircraftPosition =
            GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath; // would be free since we are in free flight
        var _aircraftDirection = Geometry.GetDirectionFromHeading(GameManager.Instance.Aircraft.Heading);
        var _segmentEnd = Vector2.zero;

        exitSegmentIndex = -1;
        var _intersection = Vector2.zero;

        for (var i = 1; i < Points.Length; i++)
        {
            var _segmentStart = _segmentEnd;
            // on could take the positions from PathLines
            _segmentEnd = Geometry.GetNextPosition(_segmentStart, Points[i].Distance, Points[i].Degrees);

            if (Points[i].IsHiddenLine || Points[i].IsAfterDiscontinuity
            ) //@#$ ask if we can join discontinuity segments
            {
                continue;
            }

            if (Geometry.FindLineSegmentIntersection(_aircraftPosition, _aircraftDirection.x, _aircraftDirection.y,
                _segmentStart, _segmentEnd, out _intersection))
            {
                exitSegmentIndex = i;
                break;
            }
        }

        if (exitSegmentIndex >= 0)
        {
            var _currentSegmentStart = Points[exitSegmentIndex - 1].CartesianPosition;
            var _currentSegmentEnd = Points[exitSegmentIndex].CartesianPosition;

            var _towardsBeginning = Vector2.SqrMagnitude(_currentSegmentStart - _intersection) <
                                    Vector2.SqrMagnitude(_currentSegmentEnd - _intersection);

            var _nextSegmentStart = _currentSegmentEnd;

            var _countCurrent = Geometry.CircleIntersects(_currentSegmentStart, _currentSegmentEnd, _intersection,
                Aircraft.ForwardThreshold,
                true, out var _intersectionCurrent1, out var _intersectionCurrent2);

            var _nextSegmentEnd = Vector2.zero;
            var _intersectionNext1 = Vector2.zero;
            var _intersectionNext2 = Vector2.zero;

            int _countNext;

            if (Points.Length > exitSegmentIndex + 1)
            {
                _nextSegmentEnd = Points[exitSegmentIndex + 1].CartesianPosition;
                _countNext = Geometry.CircleIntersects(_nextSegmentStart, _nextSegmentEnd, _intersection,
                    Aircraft.ForwardThreshold,
                    false, out _intersectionNext1, out _intersectionNext2);
            }

            // find forward intersection

            // if inside turn - prepare turn for corner on the other side of the circle of the aircraft and the exit of turn
            // if not near turn - prepare turn for corner on circle center and the forward interserction

            // create lines with computed vertexes for turns for scenario and return them 


            // the intersections on the current segment are clamped to segment
            if (_countCurrent <= 0)
            {
                // should happen if the segment is smaller than the threshold ( go to the nexts segments ? )
                Debug.LogError("no intersections - segment smaller than threshold?");
                return false;
            }

            // we are not sure of the order of the circle intersections on the circle
            var _firstIsBefore = Vector2.SqrMagnitude(_currentSegmentStart - _intersectionCurrent1) <
                                 Vector2.SqrMagnitude(_currentSegmentStart - _intersection);

            if (_countCurrent == 2)
            {
                // --> the center of turn can be the intersection since is on the same line with the exit
                centerOfTurn = _intersection;
                exitPoint = _firstIsBefore ? _intersectionCurrent2 : _intersectionCurrent1;
                return true;
            }

            if (_towardsBeginning)
            {
                // --> the center of turn can be the intersection since is on the same line with the exit
                centerOfTurn = _intersection;
                exitPoint = _intersectionCurrent1;
                return true;
            }

            // We are will be exiting on the next segment than the intersection
            // --> find center of turn as the intersection between next segment and current direction
            Geometry.FindLineSegmentIntersection(_aircraftPosition, _aircraftDirection.x, _aircraftDirection.y,
                _nextSegmentStart, _nextSegmentEnd, out var _newCenter, false);

            var _exitPoint = Geometry.IsWithinSegment(_nextSegmentStart.x, _nextSegmentStart.y,
                _nextSegmentEnd.x, _nextSegmentEnd.y, _intersectionNext1.x, _intersectionNext1.y)
                ? _intersectionNext1
                : _intersectionNext2;

            //if the angle is inwards (meaning the center of turn of further than the exitpoint ) ( see reference image.. ) move exit point further
            var _exitIsBackwards = Vector2.SqrMagnitude(_nextSegmentEnd - _newCenter) <
                                   Vector2.SqrMagnitude(_nextSegmentEnd - _exitPoint);
            if (_exitIsBackwards)
            {
                _exitPoint = Vector2.MoveTowards(_newCenter, _nextSegmentEnd, Aircraft.ForwardThreshold);
            }

            // todo: if newCenter is passed the next segment end ( when in U turn and bypasses the middle ) -> try next
            // todo: if exit point is near turn ( too close points) -> try next

            centerOfTurn = _newCenter;
            exitPoint = _exitPoint;

            return true;
        }

        return false;
    }


    public bool LinkToRoute(Vector2 exitPoint, int lineIndex, out PathPositionInfo  intersectionTargetVertex, out float distanceUntilLineIntersection)
    {
        intersectionTargetVertex = new PathPositionInfo();
        distanceUntilLineIntersection = 0;
        var _segmentStart = Points[lineIndex - 1].CartesianPosition;
        var _segmentEnd = Points[lineIndex].CartesianPosition;

        if (GameManager.Instance.PathLines.FindClosestVertexToDistanceOnLineActive(
            (exitPoint - _segmentStart).magnitude, lineIndex, out var _targetVertexIndex,
            out var _targetVertexPosition))
        {
            intersectionTargetVertex = new PathPositionInfo
            {
                CurrentNodeIndex = lineIndex,
                HeadingBefore = Geometry.GetHeadingOfDirection(_segmentEnd - _segmentStart),
                UnreachedVertexIndex = _targetVertexIndex,
                UnreachedVertexPosition = _targetVertexPosition
            };
            distanceUntilLineIntersection = (exitPoint - _segmentStart).magnitude;
            return true;
        }

        return false;
    }

    public void ShortcutNodes(int firstIdNodeToDissolve, int toId,
        out RoutePoint reducedPoint) // @#$ refactor for passed nodes ?
    {
        var _startIndex = GetIndex(firstIdNodeToDissolve);
        var _endIndex = GetIndex(toId);

        var _offset = _endIndex - _startIndex;

        var _newSet = new RoutePoint[Points.Length - _offset];

        for (var i = 0; i < _startIndex; i++)
        {
            _newSet[i] = Points[i];
        }

        // compute new distance
        var _endPosition =
            Geometry.GetNextPosition(Vector2.zero, Points[_startIndex].Distance, Points[_startIndex].Degrees);
        for (var i = _startIndex; i < _endIndex; i++)
        {
            _endPosition = Geometry.GetNextPosition(_endPosition, Points[i + 1].Distance, Points[i + 1].Degrees);
        }

        //compute new angle
        var _angle = Geometry.AngleBetween(_endPosition, Vector2.up);

        reducedPoint = Points[_endIndex].Clone();
        reducedPoint.ClearDetails();
        reducedPoint.Distance = _endPosition.magnitude;
        reducedPoint.RawDegrees = _angle;
        reducedPoint.IsModified = true;

        _newSet[_startIndex] = reducedPoint;

        for (var i = _endIndex + 1; i < Points.Length; i++)
        {
            var _newIndex = i - _offset;
            _newSet[_newIndex] = Points[i];
        }

        Points = _newSet;
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
        var _nodeIndex = GetIndex(nodeId);
        var _node = Points[_nodeIndex];

        float _distanceToBefore;
        float _distanceFromAfter;
        afterInsertion = null;

        if (distance <= 0)
        {
            afterInsertion = _node;
            _distanceToBefore = -distance;
            _distanceFromAfter = afterInsertion.Distance - _distanceToBefore;
        }
        else // if > 0
        {
            if (Points.Length > _nodeIndex + 1)
            {
                afterInsertion = Points[_nodeIndex + 1];
                _distanceToBefore = afterInsertion.Distance - distance;
            }
            else
            {
                Debug.LogError("Not Possible");
                insertionNode = null;
                return;
            }

            _distanceFromAfter = distance;
        }

        insertionNode = new RoutePoint
        {
            Name = GetNewName(_node.Name),
            Distance = _distanceFromAfter,
            RawDegrees = afterInsertion.RawDegrees,
            ID = GetNewId()
        };

        afterInsertion.Distance = _distanceToBefore;

        // replace set with new set that also contains insertion node
        var _newSet = new RoutePoint[Points.Length + 1];
        var _offset = 0;
        for (var i = 0; i < Points.Length; i++)
        {
            if (i == _nodeIndex + (distance > 0 ? 1 : 0))
            {
                _newSet[i] = insertionNode;
                _offset = 1;
            }

            _newSet[i + _offset] = Points[i];
        }

        Points = _newSet;
    }

    void AddRelativeNodeAfter(int relativeFromNodeId, float rawDegrees, int distance,
        out RoutePoint insertionNode, out RoutePoint afterInsertion, bool addCurveOffset = true)
    {
        var _relativeFromNodeIndex = GetIndex(relativeFromNodeId);
        var _relativeFromNode = Points[_relativeFromNodeIndex];

        afterInsertion = Points[_relativeFromNodeIndex + 1];

        insertionNode = new RoutePoint
        {
            Name = GetNewName(_relativeFromNode.Name),
            Distance = distance,
            RawDegrees = rawDegrees,
            ID = GetNewId()
        };

        var _insertPosition = Geometry.GetNextPosition(Vector2.zero, distance, rawDegrees);
        var _originalToPosition =
            Geometry.GetNextPosition(Vector2.zero, afterInsertion.Distance, afterInsertion.RawDegrees);
        var _returnDirection = _originalToPosition - _insertPosition;
        afterInsertion.RawDegrees = Geometry.AngleBetween(Vector2.up, _returnDirection);
        afterInsertion.Distance = _returnDirection.magnitude;

        // simulate the curve to the the needed offset
        var _lastLine = new MarkLine(_relativeFromNode);
        _lastLine.InitBeginning();

        Drawer.ComputeLine(_lastLine, out var _testLine, insertionNode, afterInsertion);

        // replace set with new set that also contains insertion node
        var _newSet = new RoutePoint[Points.Length + 1];
        var _offset = 0;
        for (var i = 0; i < Points.Length; i++)
        {
            if (i == _relativeFromNodeIndex + 1)
            {
                _newSet[i] = insertionNode;

                _offset = 1;
            }

            _newSet[i + _offset] = Points[i];
        }

        Points = _newSet;
    }


    public void AddRelativeNodeBefore(int beforeNodeId, float rawDegrees, int distance, int relativeNodeId,
        out RoutePoint insertionNode,
        bool showDiscontinuity = false)
    {
        var _originalBeforeNodeIndex = GetIndex(beforeNodeId);
        var _relativeNodeIndex = GetIndex(relativeNodeId);

        var _isInThePast = PositionVirtualNode.PassedNodeIndexForMode > _originalBeforeNodeIndex - 1;
        
        var _relativeNode = Points[_relativeNodeIndex];
        var _insertPosition = Geometry.GetNextPosition(_relativeNode.CartesianPosition, distance, 360 - rawDegrees);

        var _indexBeforeInsertion = _isInThePast ? PositionVirtualNode.PassedNodeIndexForMode : _originalBeforeNodeIndex - 1;

        var _positionBeforeInsertion = Points[_indexBeforeInsertion].CartesianPosition;

        var _insertionAngle = Geometry.AngleOfPosition(_insertPosition, _positionBeforeInsertion);
        var _insertionDistance = (_insertPosition - _positionBeforeInsertion).magnitude;

        var _nodeAfterInsertion = Points[GetIndex(beforeNodeId)];
        
        insertionNode = new RoutePoint
        {
            Name = GetNewName(_relativeNode.Name),
            Distance = _insertionDistance,
            RawDegrees = _insertionAngle,
            ID = GetNewId(),
            Details = _nodeAfterInsertion.IsAfterDiscontinuity && showDiscontinuity ? "D" : ""
        };
        
        
        var _returnNode = _nodeAfterInsertion.Clone();

        // update info of selected to be relative to the inserted instead of the previous which is now previous to inserted
        _returnNode.RawDegrees = Geometry.AngleOfPosition(_nodeAfterInsertion.CartesianPosition, _insertPosition); 
        _returnNode.Distance = (_nodeAfterInsertion.CartesianPosition - _insertPosition).magnitude;
        if ( showDiscontinuity)
        {
            _returnNode.IndicateDiscontinuityBefore();
        }

        if (_isInThePast)
        {
            _returnNode.ID = GetNewId() + Points.Length;
        }

        // replace set with new set that also contains insertion node
        var _inThePastExtraNodes = _indexBeforeInsertion - _relativeNodeIndex + 1;
        var _newSet = new RoutePoint[Points.Length + (_isInThePast ? 1+ _inThePastExtraNodes : 1)];
        var _offset = 0;
        for (var i = 0; i < Points.Length; i++)
        {
            if (i == _indexBeforeInsertion +1)
            {
                _newSet[i] = insertionNode;

                if (!_isInThePast)
                {
                    _newSet[i + 1] = _returnNode;
                    _offset = 1;
                    continue;
                }

                else
                {
                    _offset = 1;
                    _newSet[i + _offset] = _returnNode;
                    
                    // add all passed nodes after the insertion until current node
                    for (var u = 0; u < _inThePastExtraNodes; u++)
                    {
                        _offset += 1;
                        _newSet[i + _offset] = Points[_relativeNodeIndex + u + 1];
                    }

                    continue;
                }
            }

            _newSet[i + _offset] = Points[i];
        }

        Points = _newSet;
        
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

    public RoutePoint AddDisplayPositionNode()
    {
        // ! Position node is added in front of the actual position so that the aircraft can safely turn 
        

        var _routePreviousNodeIndex = PositionVirtualNode.PassedNodeIndex;
        var _routePreviousNode = Points[_routePreviousNodeIndex];


        if (Aircraft.IsOnRoute)
        {
            // add position node on path

            var _activeNextNode = PositionVirtualNode.GetNodeTo;
            var _activeSegmentDistancePassed = Aircraft.WalkedDistanceOnSegment;
            
            var _activeNextNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
            var _routeNextNode = Points[_activeNextNodeIndex];

            LastAddedPositionNode = new RoutePoint
            {
                Name = "_Position_",
                Distance = _activeSegmentDistancePassed + Aircraft.ForwardThreshold,
                RawDegrees = _activeNextNode.RawDegrees,
                Details = "P",
                ID = GetNewId(),
            };

            var _newFuturePosition = Geometry.GetNextPosition(Aircraft.PositionFreeOrOnRouteSegment, Aircraft.ForwardThreshold,
                _activeNextNode.Degrees);
            
            var _differencePosition = _routeNextNode.CartesianPosition - _newFuturePosition;
            
            var _updatedAngle = Geometry.AngleBetween(_differencePosition, Vector2.up);

            _routeNextNode.RawDegrees = _updatedAngle;
            _routeNextNode.Distance = _differencePosition.magnitude;
        }
        else
        {
            // add position node as from where the aircraft is
            
            // also the position need to be forward with the threshold in the heading direction


            // there is now from node to make cu curve correctly so for now is just not added any forward offset
            // var _nextFuturePosition = Geometry.GetNextPosition(Aircraft.PositionFreeOrOnSegment, FORWARD_THRESHOLD, Aircraft.Heading);
            var _nextFuturePosition = Aircraft.PositionFreeOrOnRouteSegment;
            var _routeNextNode = Points[1];
            var _angleTo = Geometry.GetHeadingOfDirection(_nextFuturePosition);
            
            LastAddedPositionNode = new RoutePoint
            {
                Name = "_Position_",
                Distance = _nextFuturePosition.magnitude,
                RawDegrees =  _angleTo,
                Details = "P",
                ID = GetNewId(),
                CartesianPosition = _nextFuturePosition
            };

            var _differencePosition = _routeNextNode.CartesianPosition - _nextFuturePosition;
            var _updatedAngle = Geometry.GetHeadingOfDirection(_differencePosition);
            _routeNextNode.RawDegrees = _updatedAngle;
            _routeNextNode.Distance = _differencePosition.magnitude;
        }

        if (ActiveDirectApproach)
        {
            LastAddedPositionNode.IndicateHiddenLine();
        }


        // replace set with new set that also contains insertion node
        var _newSet = new RoutePoint[Points.Length + 1];
        var _offset = 0;
        for (var i = 0; i < Points.Length; i++)
        {
            if (i == PositionVirtualNode.NextNodeIndex)
            {
                _newSet[i] = LastAddedPositionNode;
                _offset = 1;
            }

            _newSet[i + _offset] = Points[i];
        }

        Points = _newSet;


        return LastAddedPositionNode;
    }
}