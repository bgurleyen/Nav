using MoreMountains.NiceVibrations;

/// <summary>
/// FMC MENU toggles for gameplay SFX and haptics. Labels show the action when enabled (e.g. "Sounds OFF" = tap to mute).
/// </summary>
public static class FlightFeedbackSettings
{
    public static bool SoundsEnabled { get; private set; } = true;
    public static bool VibrationEnabled { get; private set; } = true;

    public static string SoundsMenuLabel => SoundsEnabled ? "Sounds OFF" : "Sounds ON";
    public static string VibrationMenuLabel => VibrationEnabled ? "Vibration OFF" : "Vibration ON";

    public static void ToggleSounds()
    {
        SoundsEnabled = !SoundsEnabled;
    }

    public static void ToggleVibration()
    {
        VibrationEnabled = !VibrationEnabled;
        MMVibrationManager.SetHapticsActive(VibrationEnabled);
    }
}
