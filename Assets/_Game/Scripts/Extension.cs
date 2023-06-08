using System;
using UnityEngine;
using Gamelogic.Extensions;
using Lean.Pool;
using UnityEngine.Events;

namespace Navigation
{
    public static class Extension
    {
        public static Vector3 ToDisplay(this Vector2 pos)
        {
            var finalPosition = new Vector2(pos.x, pos.y);

            var walkedPosition = Session.PlayerAircraft.NMPosition;
            var aircraftHeadingRotation = Session.PlayerAircraft.DisplayHeadingDegrees;

            switch (Session.Mode)
            {
                case MapMode.Map:
                case MapMode.Center:
                    // walked
                    finalPosition -= walkedPosition;
                    // walked rotation
                    finalPosition = finalPosition.Rotate(aircraftHeadingRotation);
                    break;
                case MapMode.Plan:
                    // centered
                    finalPosition -= Session.ActiveRoute.TracedRoute.CenteredPosition;
                    break;
            }

            // zoom
            finalPosition *= Session.Zoom;

            return finalPosition;
        }


        public static void DespawnChildren<T>(Transform holder, LeanGameObjectPool pool) where T : MonoBehaviour
        {
            var lines = holder.GetComponentsInChildren<T>();
            foreach (var l in lines)
            {
                pool.Despawn(l.gameObject);
            }
        }

        public static bool GetNodeIndex(this RoutePoint[] points, int nodeId, out int nodeIndex)
        {
            for (var i = 0; i < points.Length; i++)
            {
                if (points[i].ID == nodeId)
                {
                    nodeIndex = i;
                    return true;
                }
            }

            nodeIndex =  -1;
            return false;
        }

    }

    [Serializable]
    public class FloatUnityEvent : UnityEvent<float>
    {
    }

}