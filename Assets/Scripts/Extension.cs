using UnityEngine;
using Gamelogic.Extensions;
using Lean.Pool;

public static class Extension
{
    public static Vector3 ToDisplay(this Vector2 pos)
    {
        var finalPosition = new Vector2(pos.x, pos.y);

        var walkedPosition = GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath;
        var walkedRotation = GameManager.Instance.Aircraft.Heading;

        switch (Drawer.Instance.Mode)
        {
            case DrawerMode.Map:
            case DrawerMode.Center:
                // walked
                finalPosition -= walkedPosition;
                // walked rotation
                finalPosition = finalPosition.Rotate(walkedRotation);
                break;
            case DrawerMode.Plan:
                // centered
                finalPosition -= GameManager.Instance.ActiveRoute.PathLines.CenteredPosition;
                break;
        }

        // zoom
        finalPosition *= Drawer.Instance.Zoom;

        return finalPosition;
    }


    public static void DespawnChildred<T>(Transform holder, LeanGameObjectPool pool) where T : MonoBehaviour
    {
        var lines = holder.GetComponentsInChildren<T>();
        foreach (var l in lines)
        {
            pool.Despawn(l.gameObject);
        }
    }
    
    public static int GetNodeIndex(this RoutePoint[] points,int nodeId)
    {
        for (var i = 0; i < points.Length; i++)
        {
            if (points[i].ID == nodeId)
            {
                return i;
            }
        }

        return -1;
    }
    
}

public enum DrawerMode { Map, Center, Plan, Suspeded}
