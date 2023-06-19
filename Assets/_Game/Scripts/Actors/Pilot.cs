using UnityEngine;

namespace Navigation
{
    public class Pilot
    {
        private Vector2 Direction => Geometry.GetDirectionFromHeading(HeadingDegrees);

        public Vector2 NMPosition;
        public float HeadingDegrees;
        public float DisplayHeadingDegrees;

        public float CachedDisplayLastAngleDiff { get; private set; }
        private readonly bool _isTracer;

        public Pilot(Vector2 nmPosition, Vector2 initialOrientationTarget, bool isTracer)
        {
            _isTracer = isTracer;
            NMPosition = nmPosition;

           DisplayHeadingDegrees = HeadingDegrees = Geometry.GetHeadingOfDirection(initialOrientationTarget - nmPosition);
        }

        public void TickAdvance()
        {
            NMPosition += Direction *
                          (_isTracer ? Session.Settings.TracerTickDistance() : Session.Settings.PlayerTickDistance);
            if (!_isTracer)
            {
                DisplayHeadingDegrees = Mathf.LerpAngle(DisplayHeadingDegrees, HeadingDegrees,
                    Session.Settings.TickFlyingRotationDelayMultiplier);

                CachedDisplayLastAngleDiff = -Geometry.AngleDelta(DisplayHeadingDegrees, HeadingDegrees);
            }
        }

        public void TickSteerToPathFoundVertex(Vector2 seekTarget, out float heading)
        {
            heading = Geometry.GetHeadingOfDirection(seekTarget - NMPosition);

            TickSteerToTargetHeading(heading);
        }

        public void TickSteerToTargetHeading(float targetHeading)
        {
            var difDegrees = -Geometry.AngleDelta(HeadingDegrees, targetHeading);

            UpdateHeadingTowardsAngleDiff(difDegrees);
            
            
        }

        private void UpdateHeadingTowardsAngleDiff(float difDegrees)
        {
            var lerpDirection = difDegrees < 0 ? -1 : 1;
            var currentTurningDegrees = lerpDirection *
                                        Mathf.Min(Session.Settings.TickMaxRotation(_isTracer), Mathf.Abs(difDegrees));

            HeadingDegrees += currentTurningDegrees;

            HeadingDegrees %= 360;
        }
    }
}