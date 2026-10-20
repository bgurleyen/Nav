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

            if (Session.PlayerAircraft != null && Session.State != null)
            {
                switch (Session.State.MapMode)
                {
                    case MapMode.Map:
                    case MapMode.Center:
                        finalPosition -= Session.PlayerAircraft.NMPosition;
                        finalPosition = finalPosition.Rotate(Session.PlayerAircraft.DisplayHeadingDegrees);
                        break;
                    case MapMode.Plan:
                        finalPosition -= Session.CenteredPosition;
                        break;
                }
            }

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
            if (points == null)
            {
                nodeIndex = -1;
                return false;
            }

            for (var i = 0; i < points.Length; i++)
            {
                if (points[i] != null && points[i].ID == nodeId)
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