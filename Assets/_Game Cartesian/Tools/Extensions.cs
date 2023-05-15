using Gamelogic.Extensions;
using Legacy;
using UnityEngine;

namespace Navigation
{
    public static class Extensions
    {
        public static Vector3 ToDisplay(this Vector2 nmPosition)
        {
            switch (Session.Mode)
            {
                case DrawerMode.Map:
                case DrawerMode.Center:
                    // walked
                    nmPosition -= Session.PlayerNMPosition;
                    // walked rotation
                    nmPosition = nmPosition.Rotate(Session.PlayerHeadingDegrees);
                    break;
                case DrawerMode.Plan:
                    // centered
                    // nmPosition -= GameManager.Instance.ActiveRoute.PathLines.CenteredPosition;
                    break;
            }

            return nmPosition / Session.Zoom;
        }
    }
    
}