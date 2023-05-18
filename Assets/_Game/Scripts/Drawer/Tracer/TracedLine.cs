using System.Collections.Generic;
using UnityEngine;

namespace Navigation
{
    public class TracedLine
    {
        public Vector2 StartNMPosition;
        public Vector2 EndNMPosition;
        public RoutePoint LinkedPoint { get; private set; }
        public Vector2[] Vertexes;
        public float TracedNMLength { get; private set; }

        private float _seekDistance;
        
        public readonly Vector2[] SegmentVertices;
        

        public TracedLine( float seekDistance, Pilot pilot, Vector2 lastPointPosition, RoutePoint forPoint)
        {
           _seekDistance = seekDistance;
            
            StartNMPosition = lastPointPosition;
            EndNMPosition = forPoint.CartesianPosition;
            LinkedPoint = forPoint;
            var lineDirection = (EndNMPosition - StartNMPosition).normalized;
            var lineLenght = (EndNMPosition - StartNMPosition).magnitude;

            // compute granular intervals on the straight line to prevent calculations everytime
            var vertexCount = Mathf.CeilToInt(lineLenght / Session.Settings.SegmentGranularity);
            SegmentVertices = new Vector2[vertexCount];
            var straightTracerPosition = lastPointPosition;
            for (var i = 0; i < SegmentVertices.Length; i++)
            {
                SegmentVertices[i] = straightTracerPosition;

                straightTracerPosition += lineDirection * Session.Settings.SegmentGranularity;
            }


            TraceFromPilot(pilot);

            TracedNMLength = (Vertexes.Length - 1) * Session.Settings.DrawerUnitLength;
        }

        /// <summary>
        /// Continues the trace from where the pilot is
        /// </summary>
        /// <param name="pilot"></param>
        private void TraceFromPilot(Pilot pilot)
        {
            var tracePositions = new List<Vector2> { pilot.NMPosition };
            int seekFromIndex = 0;
            while (tracePositions.Count < 200 && FindFurthestSeekTargetOnSegment(
                       pilot.NMPosition,
                       out var foundVertex,
                       out seekFromIndex,
                       out var reachedEnd,
                       seekFromIndex))
            {
                pilot.TickSteerToPathFoundVertex(foundVertex);
                pilot.TickAdvance();
                tracePositions.Add(pilot.NMPosition);

                if (reachedEnd)
                {
                    break;
                }

            }

            if (tracePositions.Count == 200)
            {
                Debug.LogError("Cannot reach trace destination");
            }

            Vertexes = tracePositions.ToArray();
        }

        /// <summary>
        /// traces a path with initial curve ( from the last segment trance ) and ends straight.
        /// </summary>
        /// <param name="forPosition"></param>
        /// <param name="foundVertex"></param>
        /// <param name="foundVertexIndex"></param>
        /// <param name="reachedEnd"></param>
        /// <param name="startFromIndex"></param>
        /// <returns></returns>
        private bool FindFurthestSeekTargetOnSegment(Vector2 forPosition, out Vector2 foundVertex, out int foundVertexIndex, out bool reachedEnd, int startFromIndex = 0)
        {
            foundVertex = Vector2.zero;
            foundVertexIndex = -1;
            reachedEnd = false;
            
            var maxSqrDistance = _seekDistance *_seekDistance;
        
            for (var i = startFromIndex; i < SegmentVertices.Length; i++)
            {
                var vertex = SegmentVertices[i];
                var sqrDistance = (vertex - forPosition).sqrMagnitude;

                if (sqrDistance > maxSqrDistance)
                {

                    // if pilot is already very far from the beginning, consider it a valid target until it gets closed
                    if (i == startFromIndex)
                    {
                        foundVertex = vertex;
                        foundVertexIndex = i;

                        return true;
                    }
                    
                    continue;
                }

                foundVertex = vertex;
                foundVertexIndex = i;

                if (i == SegmentVertices.Length - 1)
                {
                    reachedEnd = true;
                }
            }

            return foundVertexIndex > 0;
        }
    }

    public class Pilot
    {
        private Vector2 Direction => Geometry.GetDirectionFromHeading(HeadingDegrees);
        
        public Vector2 NMPosition;
        public float HeadingDegrees;

        private float _currentTurningDegrees;
        private bool _isTracer;

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

            HeadingDegrees += lerpDirection * Mathf.Min(Session.Settings.PilotMaxDegreesPathFollow, Mathf.Abs(difDegrees)) ;
        }
    }
}