using System;
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
        
        public readonly Vector2[] SegmentVertices;
        

        public TracedLine( float seekDistance, Pilot pilot, Vector2 lastPointPosition, RoutePoint forPoint)
        {
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

            TraceFromPilot(pilot, seekDistance);

            TracedNMLength = (Vertexes.Length - 1) * Session.Settings.TracerTickDistance();
        }

        /// <summary>
        /// Continues the trace from where the pilot is
        /// </summary>
        /// <param name="pilot"></param>
        public void TraceFromPilot(Pilot pilot, float seekDistance)
        {
            const int SAFE_MAX_POSITIONS = 200;
            var tracePositions = new List<Vector2> { pilot.NMPosition };
            
            int lastFoundVertexIndex = 0;

            float cachedLastHeading = 999;
            
            while (tracePositions.Count < SAFE_MAX_POSITIONS && FindFurthestSeekTargetOnSegment(
                       pilot.NMPosition,
                       seekDistance,
                       out var foundVertex,
                       out lastFoundVertexIndex,
                       out var reachedEnd,
                       lastFoundVertexIndex,
                       breakOnFirstSolution:true))
            {
                pilot.TickSteerToPathFoundVertex(foundVertex, out var heading );
                pilot.TickAdvance();
                if (Math.Abs(heading - cachedLastHeading) < 0.001f && tracePositions.Count > 1)
                {
                    tracePositions.RemoveAt(tracePositions.Count - 1);
                }

                cachedLastHeading = heading;
                tracePositions.Add(pilot.NMPosition);

                if (reachedEnd)
                {
                    break;
                }

            }

            if (tracePositions.Count == SAFE_MAX_POSITIONS)
            {
                Debug.LogError("Cannot reach trace destination");
            }

            Vertexes = tracePositions.ToArray();
        }

        /// <summary>
        /// traces a path with initial curve ( from the last segment trance ) and ends straight.
        /// </summary>

        /// <returns></returns>
        public bool FindFurthestSeekTargetOnSegment(Vector2 forPosition, float seekDistance, out Vector2 foundVertex,
            out int lastFoundVertexIndex, out bool reachedEnd, int startFromIndex = 0, bool breakOnFirstSolution = false, bool beginningIsAlwaysValid = true)

        {
            foundVertex = Vector2.zero;
            lastFoundVertexIndex = 0;
            var foundVertexIndex = -1;
            reachedEnd = false;

            var maxSqrDistance = seekDistance * seekDistance;


            var fistSqrDistance = (SegmentVertices[startFromIndex] - forPosition).sqrMagnitude;
            if (beginningIsAlwaysValid && fistSqrDistance > maxSqrDistance)
            {
                // if pilot is already very far from the beginning, consider it a valid target until it gets closed
                foundVertex = SegmentVertices[startFromIndex];
                lastFoundVertexIndex = startFromIndex;

                return true;
            }
            
            
            // if needed we can to a lerp to find the closest segment vertex to start from 
            // var hasIntersection = Geometry.FindDistanceToSegment(forPosition, StartNMPosition, EndNMPosition,
            //     out var closest,
            //     out var distance);
            //
            // var isWithinSegment = hasIntersection && distance <= seekDistance;

            for (var i = startFromIndex; i < SegmentVertices.Length; i++)
            {
                var vertex = SegmentVertices[i];
                var sqrDistance = (vertex - forPosition).sqrMagnitude;

                if (sqrDistance > maxSqrDistance)
                {
                    if (i < SegmentVertices.Length - 1)
                    {
                        if (breakOnFirstSolution)
                        {
                            break;
                        }
                    }
                    continue;
                }

                foundVertex = vertex;
                foundVertexIndex = i;
            }

            if (foundVertexIndex == SegmentVertices.Length - 1)
            {
                reachedEnd = true;
            }

            if (foundVertexIndex > -1)
            {
                lastFoundVertexIndex = foundVertexIndex;
                return true;
            }
            return false;
        }
    }

   
}