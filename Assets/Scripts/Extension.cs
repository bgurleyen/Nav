using UnityEngine;
using Gamelogic.Extensions;
using Lean.Pool;

public static class Extension
{
    public static Vector3 ToDisplay(this Vector2 pos)
    {
        var _finalPosition = new Vector2(pos.x, pos.y);

        var _walkedPosition = GameManager.Instance.Aircraft.Position;
        var _walkedRotation = GameManager.Instance.Aircraft.Heading;

        switch (Drawer.Instance.Mode)
        {
            case DrawerMode.Map:
            case DrawerMode.Center:
                // walked
                _finalPosition -= _walkedPosition;
                // walked rotation
                _finalPosition = _finalPosition.Rotate(_walkedRotation);
                break;
            case DrawerMode.Plan:
                // centered
                _finalPosition -= GameManager.Instance.PathLines.CenteredPosition;
                break;
        }

        // zoom
        _finalPosition *= Drawer.Instance.Zoom;

        return _finalPosition;
    }


    public static void DespawnChildred<T>(Transform holder, LeanGameObjectPool pool) where T : MonoBehaviour
    {
        var _lines = holder.GetComponentsInChildren<T>();
        foreach (var l in _lines)
        {
            pool.Despawn(l.gameObject);
        }
    }
}

public enum DrawerMode { Map, Center, Plan, Suspeded}
