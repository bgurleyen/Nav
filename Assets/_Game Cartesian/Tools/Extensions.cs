using UnityEngine;

namespace Navigation
{
    public static class Extensions
    {
        public static Vector3 ToDisplay(this Vector2 nmPosition)
        {
            var relativeNmPosition = nmPosition - Session.PlayerNMPosition;

            var relativeWorldPosition = new Vector3(
                relativeNmPosition.x,
                relativeNmPosition.y,
                0);

            return relativeWorldPosition / Session.Zoom;
        }
    }
}