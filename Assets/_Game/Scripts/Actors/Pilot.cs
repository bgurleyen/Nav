using UnityEngine;

namespace Navigation
{
    public class Pilot
    {
        private Vector2 Direction => Geometry.GetDirectionFromHeading(HeadingDegrees);

        public Vector2 NMPosition;
        public float HeadingDegrees;

        public float CurrentTurningDegrees { get; private set; }
        private readonly bool _isTracer;

        public Pilot(Vector2 nmPosition, Vector2 initialOrientationTarget, bool isTracer)
        {
            _isTracer = isTracer;
            NMPosition = nmPosition;

            HeadingDegrees = Geometry.GetHeadingOfDirection(initialOrientationTarget - nmPosition);
            CurrentTurningDegrees = 0;
        }

        public void TickAdvance()
        {
            NMPosition += Direction * (_isTracer
                ? Session.Settings.StepDistanceTracer
                : Session.Settings.StepDistanceDeltaTime);
        }

        public void TickSteerToPathFoundVertex(Vector2 seekTarget)
        {
            var difDegrees = Geometry.AngleBetween(seekTarget - NMPosition, Direction);

            UpdateHeadingTowardsAngleDiff(difDegrees);
        }

        public void TickSteerToTargetHeading(float targetHeading)
        {
            var a = -Geometry.AngleDelta(HeadingDegrees, targetHeading);

            if (Mathf.Abs(a) > 0.01f)
            {
                UpdateHeadingTowardsAngleDiff(a);
            }
        }

        private void UpdateHeadingTowardsAngleDiff(float difDegrees)
        {
            var lerpDirection = difDegrees < 0 ? -1 : 1;

            var targetCurrentTurningDegrees =
                lerpDirection * Mathf.Min(Session.Settings.PilotMaxDegreesPathFollow, Mathf.Abs(difDegrees))
                              * (_isTracer
                                  ? 1
                                  : Session.Settings.FlyingTickDuration / Session.Settings.TracerTickDuration);

            CurrentTurningDegrees = Mathf.MoveTowards(CurrentTurningDegrees, targetCurrentTurningDegrees,
                Session.Settings.PilotMaxDegreesPathFollow);

            HeadingDegrees += CurrentTurningDegrees;

            HeadingDegrees %= 360;
        }
    }
}