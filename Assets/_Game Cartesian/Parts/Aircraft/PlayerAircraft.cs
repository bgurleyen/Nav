using UnityEngine;

namespace Navigation
{
    public class PlayerAircraft : MovingActor
    {
        public float CurrentTurningDegrees = 15;
        
        public float TargetHeading { get; private set; }
        public float HeadingDegrees { get; private set; }


        public override Vector2 Direction => Geometry.GetDirectionFromHeading(HeadingDegrees);
        private Vector3 _upwardsHeaderLineTop = new(0, 1.2f, 0);
        private LineRenderer _turningHeaderLine;

        protected override void Awake()
        {
            base.Awake();

            _turningHeaderLine = GetComponent<LineRenderer>();
            _upwardsHeaderLineTop = Vector3.up * _turningHeaderLine.GetPosition(1).magnitude;
        }

        public override void SimulateTick(float deltaTime)
        {
            base.SimulateTick(deltaTime);
            
            DrawHeadingLine();

            HeadingDegrees += CurrentTurningDegrees * deltaTime * 0.1f;

        }

        private void DrawHeadingLine()
        {
            var rotated = Quaternion.Euler(0, 0, -CurrentTurningDegrees) * _upwardsHeaderLineTop;
            
            _turningHeaderLine.SetPosition(1, rotated);
        }
    }
}