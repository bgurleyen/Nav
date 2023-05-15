using System;
using UnityEngine;

namespace Navigation
{
    public class PlayerAircraft : MovingActor<AircraftPropertiesScriptableObject>
    {
        public float CurrentTurningDegrees = 15;

        public float TargetHeading { get; private set; }
        public float HeadingDegrees { get; private set; }

        public override Vector2 Direction => Geometry.GetDirectionFromHeading(HeadingDegrees);

        public bool IsFreeFlight;
        public bool IsOnRoute = true;

        private Vector3 _upwardsHeaderLineTop = new(0, 1.2f, 0);
        private LineRenderer _turningHeaderLine;

        private Vector2 _pathJoinFoundVertex;

        protected override void Awake()
        {
            base.Awake();

            _turningHeaderLine = GetComponent<LineRenderer>();
            _upwardsHeaderLineTop = Vector3.up * _turningHeaderLine.GetPosition(1).magnitude;
        }

        public override void SimulateTick(float deltaTime, RouteScriptableObject activeRoute)
        {
            base.SimulateTick(deltaTime, activeRoute);

            DrawHeadingLine();

            HeadingDegrees += CurrentTurningDegrees * deltaTime * 0.1f;

            // We follow the path ( with the closest guide )
            if (!IsFreeFlight)
            {
                if (!activeRoute.FindFreeFlightCloseToPathExitScenario(
                        Properties.MaxRejoinCloseNMDistance,
                        out _pathJoinFoundVertex))
                {
                    Debug.LogError("No Intersection Point Found");
                    IsFreeFlight = true;
                }
                else
                {
                    SteerToPathFoundVertex();
                }
            }
        }

        private void DrawHeadingLine()
        {
            var rotated = Quaternion.Euler(0, 0, -CurrentTurningDegrees) * _upwardsHeaderLineTop;

            _turningHeaderLine.SetPosition(1, rotated);
        }

        public void StartHeadingMode()
        {
            IsFreeFlight = true;
            IsOnRoute = false;
        }

        public void StartLNavMode(Aircraft.RejoinRouteMode mode)
        {
            switch (mode)
            {
                case Aircraft.RejoinRouteMode.Manual:
                    IsFreeFlight = false;
                    break;
                // when needed rejoin: ex. after apply MOD
                case Aircraft.RejoinRouteMode.NextRouteNode:
                    //ComputeTempPathForNextNode();
                    IsFreeFlight = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }


        private void SteerToPathFoundVertex()
        {
            var difDegrees = Geometry.AngleBetween(_pathJoinFoundVertex - NMPosition, Direction);

            var lerpDirection = CurrentTurningDegrees < difDegrees ? 1 : -1;
            CurrentTurningDegrees += lerpDirection * 0.1f;
        }

        private void OnDrawGizmos()
        {
            if (!IsFreeFlight)
            {
                Gizmos.DrawSphere(transform.position + _pathJoinFoundVertex.ToDisplay(), 0.2f);
            }
        }
    }
}