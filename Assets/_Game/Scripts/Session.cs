using System;
using Gamelogic.Extensions;
using Navigation;

namespace Navigation
{
    public static class Session
    {
        public static bool IsRunning;

        public static State State;
        
        public static GameSettingsScriptableObject Settings;

        public static LevelDataScriptableObject CurrentLevel;
        
        public static Aircraft PlayerAircraft;

        public static ComputedRoutes Routes;

        public static RouteScriptableObject OriginalReferenceRoute;

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

        public static MapMode Mode { get; set; } = MapMode.Map;
        public static bool IsMod;

        public static float Zoom => (Mode == MapMode.Plan
            ? Settings.PlanReferenceLength80
            : ZoomMultiplier * Settings.MapReferenceLength80) / 80f;

        public static float ZoomMultiplier;
    }
}

[Serializable]
public class ComputedRoutes
{
    [ReadOnly] public RouteScriptableObject ActiveRoute;
    [ReadOnly] public RouteScriptableObject ModRoute;
    [ReadOnly] public RouteScriptableObject ModeSetWithPosition;
    [ReadOnly] public FixedPointsScriptableObject FixedPoints;
}
