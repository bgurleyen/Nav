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
        for (int i = 0; i < Points.Length; i++)
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

    public bool FindFreeFlightExitPosition(out PathVertexIndex intersectionTargetVertex,
        out float distanceUntilLineIntersection)
    {
        var _planePosition = GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath;
        var _planeDirection = Geometry.GetDirectionFromHeading(GameManager.Instance.Aircraft.Heading);
        var _segmentA = Vector2.zero;
        var _segmentB = Vector2.zero;

        for (var i = 1; i < Points.Length; i++)
        {
            _segmentA = _segmentB;
            // on could take the positions from PathLines
            _segmentB = Geometry.GetNextPosition(_segmentA, Points[i].Distance, Points[i].Degrees);

            if (Points[i].IsHiddenLine || Points[i].IsAfterDiscontinuity)
            {
                continue;
            }

            if (Geometry.FindLineSegmentIntersection(_planePosition, _planeDirection.x, _planeDirection.y,
                    _segmentA, _segmentB, out var _intersection)
                && GameManager.Instance.PathLines.FindClosestVertexToDistanceOnLineActive(
                    (_intersection - _segmentA).magnitude, i, out var _targetVertexIndex,
                    out var _targetVertexPosition))
            {
                intersectionTargetVertex = new PathVertexIndex
                {
                    CurrentNodeIndex = i,
                    HeadingBefore = Geometry.GetHeadingOfDirection(_segmentB - _segmentA),
                    UnreachedPoint = _targetVertexIndex,
                    VertexPosition = _targetVertexPosition
                };
                distanceUntilLineIntersection = (_intersection - _segmentA).magnitude;
                return true;
            }
        }

        intersectionTargetVertex = new PathVertexIndex();
        distanceUntilLineIntersection = 0;
        return false;
    }

    public void ShortcutNodes(int firstNodeToDissolve, int toId,
        out RoutePoint reducedPoint) // @#$ refactor for passed nodes ?
    {
        var _startIndex = GetIndex(firstNodeToDissolve);
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

        var _fromNodeIndex = Mathf.Max(
            PositionVirtualNode.PassedNodeIndex,
            _originalBeforeNodeIndex - 1);

        var _relativeNode = Points[_relativeNodeIndex];
        var _insertPosition = Geometry.GetNextPosition(_relativeNode.CartesianPosition, distance, 360 - rawDegrees);

        var _isInThePast = _fromNodeIndex != _originalBeforeNodeIndex - 1;

        var _indexBeforeInsertion = _isInThePast ? PositionVirtualNode.PassedNodeIndex : GetIndex(beforeNodeId) -1;

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
        if (!_isInThePast && showDiscontinuity)
        {
            _returnNode.IndicateDiscontinuityBefore();
        }

        if (_isInThePast)
        {
            _returnNode.ID = GetNewId() + Points.Length;
        }

        // replace set with new set that also contains insertion node
        var _newSet = new RoutePoint[Points.Length + (_isInThePast ? 2 : 1)];
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

                _newSet[i + 1] = _returnNode;
                _newSet[i + 2] = Points[i];
                _offset = 2;
                continue;
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

    public RoutePoint AddDisplayPositionNode(float neededOffsetDistance = 0)
    {
        // ! Position node should be added in front of the actual position so that the aircraft can safely turn 
        
        const float DISTANCE_THRESHOLD = 0.002f;
        const float FORWARD_THRESHOLD = 3f;

        if (Aircraft.ComputedDistanceLeftOnSegment < FORWARD_THRESHOLD ||
            Aircraft.WalkedDistanceOnSegment < DISTANCE_THRESHOLD)
        {
            Debug.LogWarning("ERROR: Skipped add position node - too close");
            return null;
        }

        var _routePreviousNodeIndex = PositionVirtualNode.PassedNodeIndex;
        var _routePreviousNode = Points[_routePreviousNodeIndex];

        var _activeNextNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
        var _routeNextNode = Points[_activeNextNodeIndex];

        if (Aircraft.IsOnPath)
        {
            // add position node on path

            var _activeNextNode = PositionVirtualNode.GetNodeTo;

            var _activeSegmentDistancePassed = Aircraft.WalkedDistanceOnSegment;

            LastAddedPositionNode = new RoutePoint
            {
                Name = "_Position_",
                Distance = _activeSegmentDistancePassed + FORWARD_THRESHOLD, // @#$
                RawDegrees = _activeNextNode.RawDegrees,
                Details = "P",
                ID = GetNewId(),
            };

            var _newFuturePosition = Geometry.GetNextPosition(Aircraft.PositionFreeOrOnSegment, FORWARD_THRESHOLD,
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


            var _differenceToPosition = Aircraft.PositionFreeOrOnCurvedPath - _routePreviousNode.CartesianPosition;
            var _updatedToAngle = Geometry.AngleBetween(_differenceToPosition, Vector2.up);
            LastAddedPositionNode = new RoutePoint
            {
                Name = "_Position_",
                Distance = (Aircraft.PositionFreeOrOnCurvedPath - _routePreviousNode.CartesianPosition).magnitude,
                RawDegrees = _updatedToAngle,
                Details = "P",
                ID = GetNewId(),
            };

            var _differencePosition = _routeNextNode.CartesianPosition - Aircraft.PositionFreeOrOnCurvedPath;
            var _updatedAngle = Geometry.AngleBetween(_differencePosition, Vector2.up);
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
            if (i == _activeNextNodeIndex)
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