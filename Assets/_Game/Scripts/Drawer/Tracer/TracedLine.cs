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

            TracedNMLength = (Vertexes.Length - 1) * Session.Settings.TickStepDistance(true);
        }

        /// <summary>
        /// Continues the trace from where the pilot is
        /// </summary>
        /// <param name="pilot"></param>
        public void TraceFromPilot(Pilot pilot, float seekDistance)
        {
            var tracePositions = new List<Vector2> { pilot.NMPosition };
            
            int lastFoundVertexIndex = 0;
            
            while (tracePositions.Count < 200 && FindFurthestSeekTargetOnSegment(
                       pilot.NMPosition,
                       seekDistance,
                       out var foundVertex,
                       out lastFoundVertexIndex,
                       out var reachedEnd,
                       lastFoundVertexIndex))
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

        /// <returns></returns>
        public bool FindFurthestSeekTargetOnSegment(Vector2 forPosition, float seekDistance, out Vector2 foundVertex,
            out int foundVertexIndex, out bool reachedEnd, int startFromIndex = 0, bool breakOnFirstSolution = false, bool beginningIsAlwaysValid = true)

        {
            foundVertex = Vector2.zero;
            foundVertexIndex = -1;
            reachedEnd = false;

            var maxSqrDistance = seekDistance * seekDistance;

            for (var i = startFromIndex; i < SegmentVertices.Length; i++)
            {
                var vertex = SegmentVertices[i];
                var sqrDistance = (vertex - forPosition).sqrMagnitude;

                if (sqrDistance > maxSqrDistance)
                {
                    // if pilot is already very far from the beginning, consider it a valid target until it gets closed
                    if (beginningIsAlwaysValid && i == startFromIndex)
                    {
                        foundVertex = vertex;
                        foundVertexIndex = i;

                        return true;
                    }

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

            return foundVertexIndex > -1;
        }
    }

   
}