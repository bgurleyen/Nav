using System;
using UnityEngine;

public class Aircraft
{
    const float DeltaTime = 0.00003f;
    const float MaxTurningSpeed = 1f;
    
    // position that can be on the generated curved sections of the lines
    public Vector2 PositionFreeOrOnCurvedPath { get; private set; }
    
    public Vector2 PositionFreeOrOnSegment { get; private set; }
    
    public float Heading { get; private set; } // Degrees based rotation
    public float TargetHeading { get; private set; }
    public float WalkedDistanceOnSegment { get; private set; }
    public PathVertexIndex PathLocalization;

    public float ComputedDistanceLeftOnSegment
    {
        get
        {
            if (!IsFreeFlight && IsOnPath)
            {
                return GetWalkedDistanceLeftOnSegment();
            }

            var _nextViableNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
            while (GameManager.Instance.ActiveRoute.Points[_nextViableNodeIndex].IsSkippable)
            {
                _nextViableNodeIndex++;
            }
            return (GameManager.Instance.PathLines.GetNodePosition(_nextViableNodeIndex) -
                    PositionFreeOrOnSegment).magnitude;
        }
    }

    Vector2 CurrentDirection => Geometry.GetDirectionFromHeading(Heading);
    float FrameDistance => speed * DeltaTime;

    float speed;

    public void ResetOnActiveSet(float aircraftSpeed, float altitude)
    {
        speed = aircraftSpeed;
        PositionFreeOrOnCurvedPath = Vector2.zero;
        PositionFreeOrOnSegment = Vector2.zero;
        WalkedDistanceOnSegment = 0;

        if (GameManager.Instance.PathLines.GetFirstDestination(out PathLocalization))
        {
            Heading = PathLocalization.HeadingBefore;
        }
        else
        {
            throw new Exception("Could not reset to data set");
        }
    }

    public bool IsFreeFlight;
    public bool IsOnPath = true;

    public void StartHeadingMode()
    {
        IsFreeFlight = true;
        IsOnPath = false;
    }

    public void StartLNavMode()
    {
        if (GameManager.Instance.ActiveRoute.FindFreeFlightExitPosition(out var _intersectionVertex, out var _distanceUntilLineIntersection))
        {
            IsFreeFlight = false;

            PathLocalization = _intersectionVertex;
            WalkedDistanceOnSegment = _distanceUntilLineIntersection;
        }
        else
        {
            Debug.LogError("No Intersection Point Found");
        }
    }
    
    // on free flight
    void ExecuteStepMove()
    {
        PositionFreeOrOnCurvedPath += CurrentDirection * FrameDistance;
        PositionFreeOrOnSegment = PositionFreeOrOnCurvedPath;
    }

    void ExecuteLerpMove(float stepDistance)
    {
        if (IsOnPath)
        {
            WalkedDistanceOnSegment += stepDistance;
            PositionFreeOrOnSegment = Vector2.Lerp(
                PositionVirtualNode.CurrentSegment.StartPosition,
                PositionVirtualNode.CurrentSegment.EndPosition,
                WalkedDistanceOnSegment / PositionVirtualNode.GetNodeTo.Distance);
        }

        var _distanceLeft = (PathLocalization.VertexPosition - PositionFreeOrOnCurvedPath).magnitude;
        if (_distanceLeft > 0)
        {
            PositionFreeOrOnCurvedPath = Vector2.Lerp(PositionFreeOrOnCurvedPath, PathLocalization.VertexPosition, stepDistance / _distanceLeft);
        }

        if (!IsOnPath)
        {
            PositionFreeOrOnSegment = PositionFreeOrOnCurvedPath;
        }
    }

    void ExecuteStepHeadingCorrection()
    {
        if (Math.Abs(TargetHeading - Heading) > 0.01f)
        {
            Heading = Mathf.MoveTowardsAngle(Heading, TargetHeading, MaxTurningSpeed);
        }
    }

    void IndicateTargetHeading(float heading)
    {
        TargetHeading = heading;
    }

    void AdvanceOnPath()
    {
        ExecuteStepHeadingCorrection();
        
        var _distanceLeft = (PathLocalization.VertexPosition - PositionFreeOrOnCurvedPath).magnitude;

        var _goesOver = _distanceLeft <= FrameDistance;

        if (!_goesOver)
        {
            ExecuteLerpMove(FrameDistance);
            return;
        }

        if (!IsFreeFlight && !IsOnPath)
        {
            IsOnPath = true;
            GameManager.Instance.ActiveRoute.OnPathRejoined();
        }
        
        // move to the corner
        ExecuteLerpMove(_distanceLeft);
        var _leftToAdvance = FrameDistance - _distanceLeft;

        // advance to next point
        if (!GameManager.Instance.PathLines.GetNextDestination(PathLocalization.CurrentNodeIndex,
            PathLocalization.UnreachedPoint, out var _newUnreachedVertex))
        {
            throw new Exception("No destination could be found");
        }

        // reset walked distance if the line has increased
        if (_newUnreachedVertex.CurrentNodeIndex != PathLocalization.CurrentNodeIndex)
        {
            WalkedDistanceOnSegment = 0;
            if (GameManager.Instance.ActiveRoute.Points[_newUnreachedVertex.CurrentNodeIndex].IsAfterDiscontinuity)
            {
                GameManager.Instance.SwitchThroughHeading();
            }
        }

        PathLocalization = _newUnreachedVertex;

        // assume point heading
        TargetHeading = PathLocalization.HeadingBefore;

        // move the rest of the frameDistance
        ExecuteLerpMove(_leftToAdvance);
    }


    public void OnAppliedMod()
    {
        // update path relation as just left from the just added position
        if (!IsOnPath)
        {
            var _lastAddedPositionNode = GameManager.Instance.ActiveRoute.LastAddedPositionNode;
            
            if (GameManager.Instance.PathLines.GetFirstDestinationFromNode(_lastAddedPositionNode, out var _newUnreachedVertex) )
            {
                PathLocalization = _newUnreachedVertex;
            }
        }
    }

    void AdvanceFreeFlight()
    {
        ExecuteStepHeadingCorrection();
        ExecuteStepMove();
    }

    float GetWalkedDistanceLeftOnSegment()
    {
        return PositionVirtualNode.CurrentSegment.ComputedVertexLength -
               WalkedDistanceOnSegment;
    }

    /// <summary>
    /// This is called on FixedUpdate from GameManager
    /// </summary>
    public void Advance()
    {
        if (IsFreeFlight)
        {
            IndicateTargetHeading(Calculator.RHeading);
            AdvanceFreeFlight();
        }
        else
        {
            AdvanceOnPath();
        }
    }
}