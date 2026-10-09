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
        public bool IsStraightRay { get; private set; }
        
        public readonly Vector2[] SegmentVertices;
        

        public TracedLine( float seekDistance, Pilot pilot, Vector2 lastPointPosition, RoutePoint forPoint, Vector2? straightEnd = null)
        {
            StartNMPosition = lastPointPosition;
            EndNMPosition = forPoint.CartesianPosition;
            LinkedPoint = forPoint;
            var geometryEnd = straightEnd ?? EndNMPosition;
            var lineLenght = (geometryEnd - StartNMPosition).magnitude;
            if (float.IsNaN(lineLenght) || float.IsInfinity(lineLenght) || lineLenght < 0f)
                lineLenght = 0f;
            var lineDirection = lineLenght > 0.0001f
                ? (geometryEnd - StartNMPosition) / lineLenght
                : Vector2.up;

            // compute granular intervals on the straight line to prevent calculations everytime
            // Zero-length legs (duplicate coords) must still expose at least one vertex.
            // Cap the count so a broken direct-to distance cannot allocate millions of points and lock the frame.
            var step = Mathf.Max(0.05f, Session.Settings.SegmentGranularity);
            var vertexCount = Mathf.Clamp(Mathf.CeilToInt(lineLenght / step), 1, 4000);
            SegmentVertices = new Vector2[vertexCount];
            var straightTracerPosition = lastPointPosition;
            for (var i = 0; i < SegmentVertices.Length; i++)
            {
                SegmentVertices[i] = straightTracerPosition;

                if (lineLenght > 0f)
                    straightTracerPosition += lineDirection * Session.Settings.SegmentGranularity;
            }

            IsStraightRay = straightEnd.HasValue;
            if (straightEnd.HasValue)
            {
                Vertexes = new[] { StartNMPosition, geometryEnd };
                TracedNMLength = lineLenght;
                return;
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
                var segmentDir = EndNMPosition - StartNMPosition;
                if (segmentDir.sqrMagnitude > 0.0001f)
                    pilot.NotifyTargetCourse(Geometry.GetHeadingOfDirection(segmentDir));

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

            //Debug.Log($"tracePositions : {tracePositions.Count}");
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

            if (SegmentVertices == null || SegmentVertices.Length == 0)
            {
                foundVertex = forPosition;
                lastFoundVertexIndex = 0;
                reachedEnd = true;
                return true;
            }

            startFromIndex = Mathf.Clamp(startFromIndex, 0, SegmentVertices.Length - 1);
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

        /// <summary>
        /// Path that joins the inbound course and tracks it to the waypoint.
        /// </summary>
        public static TracedLine ForInterceptCourse(Vector2 from, RoutePoint waypoint)
        {
            var corners = new List<Vector2> { from };
            if (TryResolveIntercept(from, waypoint, out var intercept)
                && (intercept - from).sqrMagnitude > 0.01f
                && (intercept - waypoint.CartesianPosition).sqrMagnitude > 0.01f)
            {
                corners.Add(intercept);
            }

            corners.Add(waypoint.CartesianPosition);
            return new TracedLine(from, waypoint, corners);
        }

        static bool TryResolveIntercept(Vector2 from, RoutePoint waypoint, out Vector2 intercept)
        {
            if (waypoint.HasHeldIntercept)
            {
                intercept = waypoint.HeldInterceptPoint;
                return true;
            }

            var track = Session.PlayerAircraft != null
                ? Session.PlayerAircraft.HeadingDegrees
                : waypoint.LinearApproachAngle;
            if (!Geometry.TryInboundIntercept(from, waypoint.CartesianPosition, waypoint.LinearApproachAngle, track,
                    out intercept))
                return false;

            // Freeze on the active route so a later heading change does not slide the intercept.
            var active = Session.ActiveRoute?.Points;
            if (active != null && System.Array.IndexOf(active, waypoint) >= 0)
            {
                waypoint.HasHeldIntercept = true;
                waypoint.HeldInterceptPoint = intercept;
            }

            return true;
        }

        private TracedLine(Vector2 from, RoutePoint forPoint, List<Vector2> corners)
        {
            StartNMPosition = from;
            EndNMPosition = forPoint.CartesianPosition;
            LinkedPoint = forPoint;
            IsStraightRay = false;

            var granularity = Mathf.Max(0.05f, Session.Settings.SegmentGranularity);
            var dense = new List<Vector2>();
            var length = 0f;
            for (var c = 0; c < corners.Count - 1; c++)
            {
                var a = corners[c];
                var b = corners[c + 1];
                var leg = Vector2.Distance(a, b);
                length += leg;
                var steps = Mathf.Max(1, Mathf.CeilToInt(leg / granularity));
                for (var s = 0; s < steps; s++)
                    dense.Add(Vector2.Lerp(a, b, s / (float)steps));
            }

            dense.Add(corners[corners.Count - 1]);
            SegmentVertices = dense.ToArray();
            Vertexes = corners.ToArray();
            TracedNMLength = length;
        }
    }

   
}