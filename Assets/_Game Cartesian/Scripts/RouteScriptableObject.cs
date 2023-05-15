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

        
    }
}