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
    
    public void InitIds()
    {
        for(var i=0;i<Points.Length;i++)
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
        for(int i=0;i<Points.Length;i++)
        {
            if(Points[i].ID == nodeId)
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

    public RouteScriptableObject Clone()
    {
        var _newSet = CreateInstance<RouteScriptableObject>();// new DataSetScriptableObject();
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
  
    public bool FindFreeFlightExitPosition(out PathVertexIndex intersectionTargetVertex, out float distanceUntilVertex)
    {
        var _planePosition = GameManager.Instance.Aircraft.Position;
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
                distanceUntilVertex = (_intersection - _segmentA).magnitude;
                return true;
            }
        }

        intersectionTargetVertex = new PathVertexIndex();
        distanceUntilVertex = 0;
        return false;
    }

    public void ShortcutNodes(int firstNodeToDissolve, int toId, out RoutePoint reducedPoint) // @#$ refactor for passed nodes ?
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
        var _endPosition = Geometry.GetNextPosition(Vector2.zero, Points[_startIndex].Distance, Points[_startIndex].Degrees);
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

    public void AddRelativeNodeOnDirection(int nodeId, int distance, out RoutePoint insertionNode, out RoutePoint afterInsertion) // @#$ todo refactor for passed nodes ?
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
            Name = _node.Name + "01",
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
            Name = _relativeFromNode.Name + "01",
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
        
        Drawer.ComputeLine(_lastLine, out var _testLine,insertionNode, afterInsertion);
        
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
    

    public void AddRelativeNodeBefore(int relativeToNodeId, float rawDegrees, int distance, out RoutePoint insertionNode,
        bool showDiscontinuity = false)
    {
        var _relativeToNodeIndex = GetIndex(relativeToNodeId);

        var _fromNodeIndex = Mathf.Max(
            PositionVirtualNode.PassedNodeIndex,
            _relativeToNodeIndex - 1);
        var _isInThePast = false;

        var _originalRelativeToNode = Points[_relativeToNodeIndex];
        var _newDegrees = 360 - rawDegrees;

        Vector2 _insertPosition;
        if (_fromNodeIndex <= _relativeToNodeIndex - 1)
        {
            // find end position relative to the node before selected ( meaning with the data from selected, because they refer to the state before the node )
            _insertPosition = Geometry.GetNextPosition(Vector2.zero, _originalRelativeToNode.Distance, _originalRelativeToNode.Degrees);
            _insertPosition = Geometry.GetNextPosition(_insertPosition, distance, _newDegrees);
        }
        else // if the relative is in the past ( aircraft passed the relative node while pending mod modification )
        {
            _isInThePast = true;
            _insertPosition = Vector2.zero;
            for (var i = _fromNodeIndex; i > _relativeToNodeIndex; i--)
            {
                var _node = Points[i];
                _insertPosition = Geometry.GetPreviousPosition(_insertPosition, _node.Distance, _node.Degrees);
            }
            _insertPosition = Geometry.GetNextPosition(_insertPosition, distance, _newDegrees);
        }

        var _insertionAngle = Geometry.AngleBetween(_insertPosition, Vector2.up);
        insertionNode = new RoutePoint
        {
            Name = _originalRelativeToNode.Name + "01",
            Distance = _insertPosition.magnitude,
            RawDegrees =_insertionAngle,
            ID = GetNewId()
        };

        var _returnNode = _originalRelativeToNode.Clone();

        // update info of selected to be relative to the inserted instead of the previous which is now previous to inserted
        // ** probably need to refer to the original in active set to show the old relative values TRK
        _returnNode.RawDegrees = Geometry.ReverseParallelAngle(rawDegrees);
        _returnNode.Distance = distance;
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
            if (i == _fromNodeIndex + 1)
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
        AddRelativeNodeBefore(toNodeId,  angle, -500, out var _veryFarNode);

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

    public RoutePoint AddPositionNode(float neededOffsetDistance = 0)
    {
        const float DISTANCE_THRESHOLD = 0.002f;

        if (Aircraft.ComputedDistanceLeft < DISTANCE_THRESHOLD ||
            PositionVirtualNode.ComputedDistancePassed < DISTANCE_THRESHOLD)
        {
            Debug.LogWarning("Skipped add position node");
            return null;
        }

        var _activeNextNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
        var _routeNextNode = Points[_activeNextNodeIndex];

        if (Aircraft.IsOnPath)
        {
            // add position node on path

            var _activeNextNode = PositionVirtualNode.GetNodeTo;

            var _activeDistancePassed = PositionVirtualNode.ComputedDistancePassed;

            LastAddedPositionNode = new RoutePoint
            {
                Name = "_Position_",
                Distance = _activeDistancePassed,
                RawDegrees = _activeNextNode.RawDegrees,
                Details = "P",
                ID = GetNewId(),
            };

            // the aircraft can be during a modification and so the angles are updated each fream
            var _nextPosition = Geometry.GetNextPosition(Vector2.zero, _routeNextNode.Distance, _routeNextNode.Degrees);
            var _differencePosition = _nextPosition - Aircraft.Position;
            
            var _updatedAngle = Geometry.AngleBetween(_differencePosition, Vector2.up);
        
            _routeNextNode.RawDegrees = _updatedAngle;
            _routeNextNode.Distance = _differencePosition.magnitude;
        }
        else
        {
            // add position node as from where the aircraft is

            var _routePreviousNodeIndex = PositionVirtualNode.PassedNodeIndex;
            var _routePreviousNode = Points[_routePreviousNodeIndex];

            var _differenceToPosition = Aircraft.Position - _routePreviousNode.CartesianPosition;
            var _updatedToAngle = Geometry.AngleBetween(_differenceToPosition, Vector2.up);
            LastAddedPositionNode = new RoutePoint
            {
                Name = "_Position_",
                Distance = (Aircraft.Position - _routePreviousNode.CartesianPosition).magnitude,
                RawDegrees = _updatedToAngle,
                Details = "P",
                ID = GetNewId(),
            };

            var _differencePosition = _routeNextNode.CartesianPosition - Aircraft.Position;
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



