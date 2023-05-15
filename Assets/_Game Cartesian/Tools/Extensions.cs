using UnityEngine;

namespace Navigation 
{
    public static class Extensions
    {
        public static Vector3 ToDisplay(this Vector2 nmPosition, CompassPropertiesScriptableObject properties)
        {
            return new Vector3( 
                nmPosition.x,
                nmPosition.y,
                0);
        }
    }
}