using UnityEngine;

namespace Navigation
{
    public class Pilot
    {
        private Vector2 Direction => Geometry.GetDirectionFromHeading(HeadingDegrees);
        
        public Vector2 NMPosition;
        public float HeadingDegrees;

        private float _currentTurningDegrees;
        private readonly bool _isTracer;

        public Pilot(Vector2 nmPosition, Vector2 initialOrientationTarget, bool isTracer)
        {
            _isTracer = isTracer;
            NMPosition = nmPosition;

            _currentTurningDegrees = Geometry.GetHeadingOfDirection(initialOrientationTarget - nmPosition);
        }
        
        public void TickAdvance()
        {
            NMPosition += Direction * (_isTracer
                ? Session.Settings.StepDistanceTracer
                : Session.Settings.StepDistanceDeltaTime);
        }

        public void TickSteerToPathFoundVertex( Vector2 seekTarget)
        {
            var difDegrees = Geometry.AngleBetween(seekTarget - NMPosition, Direction);

            var lerpDirection = difDegrees < 0 ? -1 : 1;
            _currentTurningDegrees += lerpDirection ;

            HeadingDegrees +=
                lerpDirection * Mathf.Min(Session.Settings.PilotMaxDegreesPathFollow, Mathf.Abs(difDegrees))
                              * (_isTracer 
                                  ? 1 
                                  : Session.Settings.FlyingTickDuration / Session.Settings.TracerTickDuration);
        }
    }
}