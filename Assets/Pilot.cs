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
        private float _wholeTurn;
        private bool _wholeTurnActive;
        private float _latchedTargetHeading;
        private float _remainingToTarget;

        private const float WholeTurnStartDeg = 3f;
        private const float WholeTurnEndDeg = 3f;

        public Pilot(Vector2 nmPosition, Vector2 initialOrientationTarget, bool isTracer)
        {
            _isTracer = isTracer;
            NMPosition = nmPosition;

           DisplayHeadingDegrees = HeadingDegrees = Geometry.GetHeadingOfDirection(initialOrientationTarget - nmPosition);
          
        }

        public void TickAdvance()
        {
            var step = _isTracer ? Session.Settings.TracerTickDistance() : Session.Settings.PlayerTickDistance;
            var forwardFactor = ForwardProgressFactor(_wholeTurn);
            var advanceNm = step * forwardFactor;
            // Debug.Log(
            //     $"[Tick] WholeTurn={_wholeTurn:F1}°  remaining={_remainingToTarget:F1}°  " +
            //     $"tickAdvance={advanceNm:F4} NM  (base={step:F4} ×{forwardFactor:F2})");
            NMPosition += Direction * advanceNm;
            if (!_isTracer)
            {
                DisplayHeadingDegrees = Mathf.LerpAngle(DisplayHeadingDegrees, HeadingDegrees,
                    Session.Settings.TickFlyingRotationDelayMultiplier);

                CachedDisplayLastAngleDiff = -Geometry.AngleDelta(DisplayHeadingDegrees, HeadingDegrees);
                Calculator.CachedDisplayLastAngleDiff = (int)CachedDisplayLastAngleDiff;
            }
        }

        /// <summary>
        /// Turns over 90° slow forward progress: 90° → 1x, 180° → 0.25x.
        /// Uses WholeTurn (captured at turn start), not the shrinking remaining angle.
        /// </summary>
        private static float ForwardProgressFactor(float absTurnDegrees)
        {
            if (absTurnDegrees <= 90f)
                return 1f;
            var t = Mathf.InverseLerp(90f, 180f, absTurnDegrees);
            return Mathf.Lerp(1f, 0.25f, t);
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

        /// <summary>
        /// targetHeading = final course after the turn. WholeTurn is |heading − target|
        /// captured once at turn start and held until rollout onto that same course.
        /// </summary>
        public void NotifyTargetCourse(float targetHeading)
        {
            _remainingToTarget = Mathf.Abs(Geometry.AngleDelta(targetHeading, HeadingDegrees));

            if (!_wholeTurnActive)
            {
                if (_remainingToTarget > WholeTurnStartDeg)
                {
                    _wholeTurnActive = true;
                    _wholeTurn = _remainingToTarget;
                    _latchedTargetHeading = targetHeading;
                }
                return;
            }

            if (Mathf.Abs(Geometry.AngleDelta(targetHeading, _latchedTargetHeading)) > 5f)
            {
                _wholeTurn = _remainingToTarget;
                _latchedTargetHeading = targetHeading;
            }
            else
            {
                _remainingToTarget = Mathf.Abs(Geometry.AngleDelta(_latchedTargetHeading, HeadingDegrees));
            }

            if (_remainingToTarget <= WholeTurnEndDeg)
            {
                _wholeTurnActive = false;
                _wholeTurn = 0f;
            }
        }

        public void ClearWholeTurn()
        {
            _wholeTurnActive = false;
            _wholeTurn = 0f;
            _remainingToTarget = 0f;
        }

        private void UpdateHeadingTowardsAngleDiff(float difDegrees)
        {
            // Outside corridor: turn only in the XTE-reducing direction.
            if (!_isTracer && Move.IsOutsideBorder && Move.XteReduceHeadingSign != 0f
                && Mathf.Abs(difDegrees) > 0.5f)
            {
                difDegrees = Move.XteReduceHeadingSign * Mathf.Abs(difDegrees);
            }

            var lerpDirection = difDegrees < 0 ? -1 : 1;
            var currentTurningDegrees = lerpDirection *
                                        Mathf.Min(Session.Settings.TickMaxRotation(_isTracer) * Move.TurnRateMul,
                                            Mathf.Abs(difDegrees));

            HeadingDegrees += currentTurningDegrees;
            HeadingDegrees %= 360;
        }
    }
}

