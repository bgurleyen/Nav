using System;
using Navigation;
using UnityEngine;

namespace Navigation {
    public static class Session
    {
        public static bool IsRunning;

        public static State State;
        
        public static GameSettingsScriptableObject Settings;

        public static LevelDataScriptableObject CurrentLevel;
        
        public static Aircraft PlayerAircraft;

        public static ComputedRoutes Routes;

        public static RouteScriptableObject OriginalReferenceRoute;
        
        public static Vector2 CenteredPosition;

        public static RouteScriptableObject ActiveRoute
        {
            get => Routes.ActiveRoute;
            set => Routes.ActiveRoute = value;
        }

        public static RouteScriptableObject ILSRoute
        {
            get => Routes.ILSRoute;
            set => Routes.ILSRoute = value;
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

        public static bool IsMod;

        public static float Zoom => (State.MapMode == MapMode.Plan
            ? ZoomMultiplier * Settings.PlanReferenceLength80 
            : ZoomMultiplier * Settings.MapReferenceLength80) / 80f;

        public static float ZoomMultiplier;
    }
}

[Serializable]
public class ComputedRoutes
{
    /*[ReadOnly]*/ public RouteScriptableObject ActiveRoute;
    /*[ReadOnly]*/ public RouteScriptableObject ILSRoute;
    /*[ReadOnly]*/ public RouteScriptableObject ModRoute;
    /*[ReadOnly]*/ public RouteScriptableObject ModeSetWithPosition;
    /*[ReadOnly]*/ public FixedPointsScriptableObject FixedPoints;
}
