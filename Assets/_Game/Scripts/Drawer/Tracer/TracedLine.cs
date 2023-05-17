using System.Collections.Generic;
using UnityEngine;

namespace Navigation
{
    public class TracedLine
    {
        public Vector2 StartNMPosition;
        public Vector2 EndNMPosition;
        public RoutePoint LinkedPoint { get; private set; }

        private readonly float _seekDistance;
        
        private readonly Vector2[] _straightVertices;
        public Vector2[] Vertexes;

        public TracedLine(float granularity, float seekDistance, Pilot pilot, Vector2 lastPointPosition, RoutePoint forPoint)
        {
            _seekDistance = seekDistance;
            StartNMPosition = lastPointPosition;
            EndNMPosition = forPoint.CartesianPosition;
            LinkedPoint = forPoint;
            var lineDirection = (EndNMPosition - StartNMPosition).normalized;
            var lineLenght = (EndNMPosition - StartNMPosition).magnitude;

            // compute granular intervals on the straight line to prevent calculations everytime
            var vertexCount = Mathf.CeilToInt(lineLenght / granularity);
            _straightVertices = new Vector2[vertexCount];
            var straightTracerPosition = lastPointPosition;
            for (var i = 0; i < _straightVertices.Length; i++)
            {
                _straightVertices[i] = straightTracerPosition;

                straightTracerPosition += lineDirection * granularity;
            }


            TraceFromPilot(pilot);
        }

        /// <summary>
        /// Continues the trace from where the pilot is
        /// </summary>
        /// <param name="pilot"></param>
        private void TraceFromPilot(Pilot pilot)
        {
            var tracePositions = new List<Vector2> { pilot.NMPosition };
            while (FindFurthestSeekTarget(
                       pilot.NMPosition,
                       out var foundVertex,
                       out var reachedEnd))
            {
                pilot.TickAdvance( foundVertex);
                tracePositions.Add(pilot.NMPosition);

                if (reachedEnd)
                {
                    break;
                }

            }

            Vertexes = tracePositions.ToArray();
        }

        private bool FindFurthestSeekTarget(Vector2 forPosition, out Vector2 foundVertex, out bool reachedEnd)
        {
            foundVertex = Vector2.zero;
            reachedEnd = false;
            var foundSqrDistance = -1f;
        
            var maxSqrDistance = _seekDistance *_seekDistance;
            
            for (var i = 0; i < _straightVertices.Length; i++)
            {
                var vertex = _straightVertices[i];
                var sqrDistance = (vertex - forPosition).sqrMagnitude;

                if (sqrDistance > maxSqrDistance)
                {
                    continue;
                }

                foundVertex = vertex;
                foundSqrDistance = sqrDistance;

                if (i == _straightVertices.Length - 1)
                {
                    reachedEnd = true;
                }
            }

            return !(foundSqrDistance <= 0);
        }
    }

    public class Pilot
    {
        public Vector2 NMPosition => _nmPosition;
        
        private Vector2 Direction => Geometry.GetDirectionFromHeading(_headingDegrees);
        private Vector2 _nmPosition;

        private readonly float _stepDistance;
        
        private float _currentTurningDegrees;
        private float _headingDegrees;

        public Pilot(Vector2 nmPosition, Vector2 initialOrientationTarget, float stepDistance)
        {
            _nmPosition = nmPosition;
            _stepDistance = stepDistance;

            _currentTurningDegrees = Geometry.GetHeadingOfDirection(initialOrientationTarget - nmPosition);
        }
        
        public void TickAdvance( Vector2 toTarget)
        {
             TickSteerToPathFoundVertex( toTarget);
             _nmPosition += Direction * _stepDistance;
        }

        private void TickSteerToPathFoundVertex( Vector2 seekTarget)
        {
            var difDegrees = Geometry.AngleBetween(seekTarget - _nmPosition, Direction);

            var lerpDirection = difDegrees < 0 ? -1 : 1;
            _currentTurningDegrees += lerpDirection ;

            _headingDegrees += lerpDirection * Mathf.Min(20, Mathf.Abs(difDegrees));
        }
    }
}