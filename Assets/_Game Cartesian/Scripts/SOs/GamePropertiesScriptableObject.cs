using Legacy;
using UnityEngine;

[CreateAssetMenu]
public class GamePropertiesScriptableObject : ScriptableObject
{
    public float MinZoom = 5;
    public float NMToUnit = 1;

    [Space]
    public float DrawerUnitLength = 0.6f;

}

public static class Session
{
    public static float Zoom;
    public static Vector2 PlayerNMPosition;
    public static float PlayerHeadingDegrees;
    public static DrawerMode Mode { get; set; } = DrawerMode.Center;
}
