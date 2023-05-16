using Legacy;
using UnityEngine;

namespace Navigation
{
    public static class Session
    {
        public static Aircraft PlayerAircraft;
        
        public static ComputedRoutes Routes;

        public static RouteScriptableObject ActiveRoute
        {
            get => Routes.ActiveRoute;
            set => Routes.ActiveRoute = value;
        }

        public static RouteScriptableObject ModRoute
        {
            get => Routes.ModRoute;
            set => Routes.ModRoute = value;
        }

        public static RouteScriptableObject ModeSetWithPosition
        {
            get => Routes.ModeSetWithPosition;
            set => Routes.ModeSetWithPosition = value;
        }

        public static RouteScriptableObject VisibleRoute => IsMod ? ModRoute : ActiveRoute;
        
        public static float Zoom;
        public static DrawerMode Mode { get; set; } = DrawerMode.Suspeded;
        public static bool IsMod;

    }
}