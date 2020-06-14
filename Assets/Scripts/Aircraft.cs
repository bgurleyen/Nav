using System;
using UnityEngine;

public class Aircraft
{
    const float DeltaTime = 0.0001f;
    const float MaxTurningSpeed = 1f;

    public Vector2 Position { get; private set; }
    public float Heading { get; private set; } // Degrees based rotation
    public float TargetHeading { get; private set; }
    public float WalkedDistanceOnLine { get; private set; }
    
    public PathVertexIndex UnreachedVertex;

    public float ComputedDistanceLeft
    {
        get
        {
            if (!IsFreeFlight && IsOnPath)
            {
                return GetWalkedDistanceLeftOnLine();
            }
            else
            {
                var _nextViableNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
                while (GameManager.Instance.ActiveRoute.Points[_nextViableNodeIndex].IsSkippable)
                {
                    _nextViableNodeIndex++;
                }
                return (GameManager.Instance.PathLines.GetNodePosition(_nextViableNodeIndex) -
                        Position).magnitude;
            }
        }
    }

    Vector2 CurrentDirection => Geometry.GetDirectionFromHeading(Heading);
    float FrameDistance => speed * DeltaTime;

    float speed;

    public void ResetOnActiveSet(float aircraftSpeed, float altitude)
    {
        speed = aircraftSpeed;
        Position = Vector2.zero;
        WalkedDistanceOnLine = 0;

        if (GameManager.Instance.PathLines.GetFirstDestination(out UnreachedVertex))
        {
            Heading = UnreachedVertex.HeadingBefore;
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
        if (GameManager.Instance.ActiveRoute.FindFreeFlightExitPosition(out var _intersectionVertex, out var _distanceUntilVertex))
        {
            IsFreeFlight = false;

            UnreachedVertex = _intersectionVertex;
            WalkedDistanceOnLine = _distanceUntilVertex;
        }
        else
        {
            Debug.LogError("No Intersection Point Found");
        }
    }
    
    void ExecuteStepMove()
    {
        Position += CurrentDirection * FrameDistance;
    }

    void ExecuteLerpMove(float stepDistance)
    {
        if (IsOnPath)
        {
            WalkedDistanceOnLine += stepDistance;
        }

        var _distanceLeft = (UnreachedVertex.VertexPosition - Position).magnitude;
        if (_distanceLeft > 0)
        {
            Position = Vector2.Lerp(Position, UnreachedVertex.VertexPosition, stepDistance / _distanceLeft);
        }
    }

    void ExecuteStepHeadingCorrection()
    {
        if (Math.Abs(TargetHeading - Heading) > 0.01f)
        {
            Heading = Mathf.MoveTowardsAngle(Heading, TargetHeading, MaxTurningSpeed);
        }
    }

    public void IndicateTargetHeading(float heading)
    {
        TargetHeading = heading;
    }

    void AdvanceOnPath()
    {
        ExecuteStepHeadingCorrection();
        
        var _distanceLeft = (UnreachedVertex.VertexPosition - Position).magnitude;

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
        if (!GameManager.Instance.PathLines.GetNextDestination(UnreachedVertex.CurrentLine,
            UnreachedVertex.UnreachedPoint, out var _newUnreachedVertex))
        {
            throw new Exception("No destination could be found");
        }

        // reset walked distance if the line has increased
        if (_newUnreachedVertex.CurrentLine != UnreachedVertex.CurrentLine)
        {
            WalkedDistanceOnLine = 0;
        }

        UnreachedVertex = _newUnreachedVertex;

        // assume point heading
        TargetHeading = UnreachedVertex.HeadingBefore;

        // move the rest of the frameDistance
        ExecuteLerpMove(_leftToAdvance);
    }

    
    void AdvanceFreeFlight()
    {
        ExecuteStepHeadingCorrection();
        ExecuteStepMove();
    }

    public float GetWalkedDistanceLeftOnLine()
    {
        return GameManager.Instance.PathLines.ComputedLines[UnreachedVertex.CurrentLine].ComputedLength -
               WalkedDistanceOnLine;
    }

    /// <summary>
    /// This is called on FixedUpdate from Gamemanager
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