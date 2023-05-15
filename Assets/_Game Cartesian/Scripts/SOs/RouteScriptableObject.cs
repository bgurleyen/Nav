using Legacy;
using UnityEngine;

namespace Navigation
{
    [CreateAssetMenu(fileName = "RouteData", menuName = "Cartesian/RouteData")]
    public class RouteScriptableObject : ScriptableObject
    {
        public RoutePoint[] Points;

        public bool ActiveDirectApproach { get; private set; }
        public int FirstSpeedRegulationNodeId { get; set; }
        public int FirstAltRegulationNodeId { get; set; }

        public PathLines PathLines { get; private set; }

        private float _drawerUnitLength;
        public float TotalSqrLenght { get; private set; }

        public void Init(float drawerUnitLength)
        {
            _drawerUnitLength = drawerUnitLength;
            PathLines = new PathLines(drawerUnitLength);
            
            for (var i = 0; i < Points.Length; i++)
            {
                Points[i].ID = i;
            }
        }

        public void ComputeCartesianPositions()
        {
            var currentPosition = Vector2.zero;
            for (var i = 0; i < Points.Length; i++)
            {
                if (i > 0)
                {
                    currentPosition = Geometry.GetNextPosition(currentPosition, Points[i].Distance, Points[i].Degrees);
                }

                Points[i].CartesianPosition = currentPosition;
            }
        }

        public void ComputeSet(bool isMod = false)
        {
            PathLines.ComputeSet(Points, !isMod);

            TotalSqrLenght = 0;
            var lastPoint = PathLines.ComputedLines[1].Vertexes[0];

            for (int i = 1; i < PathLines.ComputedLines.Length; i++)
            {
                var computedLine = PathLines.ComputedLines[i];

                for (int j = 0; j < computedLine.Vertexes.Length; j++)
                {
                    var vertex = computedLine.Vertexes[j];
                    TotalSqrLenght += (vertex - lastPoint).sqrMagnitude;
                    lastPoint = vertex;
                }
            }
        }

        public Vector2 GetCartesianPosition(int lineIndex)
        {
            if (Points.Length <= lineIndex)
            {
                return Vector2.zero;
            }

            return Points[lineIndex].CartesianPosition;
        }

        public bool GetPoint(int nodeId, out RoutePoint point)
        {
            var index = Points.GetNodeIndex(nodeId);
            return GetPointAt(index, out point);
        }

        public bool GetPointAt(int index, out RoutePoint point)
        {
            if (Points.Length <= index || index < 0)
            {
                point = null;
                return false;
            }

            point = Points[index];
            return true;
        }


        public RouteScriptableObject CloneAndInit()
        {
            var newSet = CreateInstance<RouteScriptableObject>(); // new DataSetScriptableObject();
            newSet.ActiveDirectApproach = ActiveDirectApproach;
            newSet.FirstAltRegulationNodeId = FirstAltRegulationNodeId;
            newSet.FirstSpeedRegulationNodeId = FirstSpeedRegulationNodeId;
            newSet.Points = new RoutePoint[Points.Length];
            for (var i = 0; i < Points.Length; i++)
            {
                newSet.Points[i] = Points[i].Clone();
            }

            newSet.Init(_drawerUnitLength);
            return newSet;
        }


        public bool FindFreeFlightCloseToPathExitScenario(float maxDistance, out Vector2 foundVertex)
        {
            int foundLine = -1;
            int foundVertexIndex = -1;
            foundVertex = Vector2.zero;
            float foundDistance = -1;

            var maxSqrDistance = maxDistance * maxDistance;
            
            // 0 = start line, empty
            for (int i = 1; i < PathLines.ComputedLines.Length; i++)
            {
                var computedLine = PathLines.ComputedLines[i];

                for (int j = 0; j < computedLine.Vertexes.Length; j++)
                {
                    var vertex = (Vector2)computedLine.Vertexes[j];

                    var sqrDistance = (vertex - Session.PlayerNMPosition).sqrMagnitude;
                    // if is further that max distance
                    if (sqrDistance > maxSqrDistance)
                    {
                        continue;
                    }

                    // if is within maxDistance limits, but more forward
                    foundVertex = vertex;
                    foundLine = i;
                    foundVertexIndex = j;
                    foundDistance = sqrDistance;
                }
            }

            if (foundDistance <= 0)
            {
                return false;
            }
            
            return true;
        }
    }
}