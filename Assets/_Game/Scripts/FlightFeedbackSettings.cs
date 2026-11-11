using MoreMountains.NiceVibrations;
using UnityEngine;

/// <summary>
/// FMC MENU toggles for gameplay SFX and haptics. Labels show the action when enabled (e.g. "Sounds OFF" = tap to mute).
/// Persisted via PlayerPrefs across sessions.
/// </summary>
public static class FlightFeedbackSettings
{
    const string SoundsEnabledKey = "FlightFeedback_SoundsEnabled";
    const string VibrationEnabledKey = "FlightFeedback_VibrationEnabled";

    public static bool SoundsEnabled { get; private set; } = true;
    public static bool VibrationEnabled { get; private set; } = true;

    public static string SoundsMenuLabel => SoundsEnabled ? "Sounds OFF" : "Sounds ON";
    public static string VibrationMenuLabel => VibrationEnabled ? "Vibration OFF" : "Vibration ON";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadFromPlayerPrefs()
    {
        SoundsEnabled = PlayerPrefs.GetInt(SoundsEnabledKey, 1) != 0;
        VibrationEnabled = PlayerPrefs.GetInt(VibrationEnabledKey, 1) != 0;
        MMVibrationManager.SetHapticsActive(VibrationEnabled);
    }

    public static void ToggleSounds()
    {
        SoundsEnabled = !SoundsEnabled;
        PlayerPrefs.SetInt(SoundsEnabledKey, SoundsEnabled ? 1 : 0);
    }

    public static void ToggleVibration()
    {
        VibrationEnabled = !VibrationEnabled;
        PlayerPrefs.SetInt(VibrationEnabledKey, VibrationEnabled ? 1 : 0);
        MMVibrationManager.SetHapticsActive(VibrationEnabled);
    }

    /// <summary>Short tick for an FMC key. Respects the MENU vibration toggle.</summary>
    public static void PulseKey()
    {
        if (!VibrationEnabled)
            return;

        MMVibrationManager.Haptic(HapticTypes.Selection);
    }
}
