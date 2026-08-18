using Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Isolated level-flythrough test harness. Active only when launched via the "Level Test" scene.
/// Normal Main / EMPTY play never sets <see cref="IsActive"/>.
/// </summary>
public static class LevelTestMode
{
    public const string SceneName = "Level Test";
    public const string MainSceneName = "Main";

    public const int InitialSpeedKnots = 320;
    public const int ForcedSpeedMultiplier = 10;

    static bool _isActive;
    static bool _awaitingLevelPick;
    static bool _pendingApplyConfig;
    static bool _finishInvoked;
    static bool _advancing;
    static int _savedPlayerLevel;
    static bool _hasSavedPlayerLevel;

    public static bool IsActive => _isActive;

    public static void BeginSession()
    {
        if (!_hasSavedPlayerLevel)
        {
            _savedPlayerLevel = PlayerPrefsHolder.Level;
            _hasSavedPlayerLevel = true;
        }

        _isActive = true;
        _awaitingLevelPick = true;
        _pendingApplyConfig = false;
        _finishInvoked = false;
        _advancing = false;
        PlayerPrefsHolder.ShowLevelSelectOnLoad = false;
    }

    public static void EndSession()
    {
        if (_hasSavedPlayerLevel)
        {
            PlayerPrefsHolder.Level = _savedPlayerLevel;
            _hasSavedPlayerLevel = false;
        }

        _isActive = false;
        _awaitingLevelPick = false;
        _pendingApplyConfig = false;
        _finishInvoked = false;
        _advancing = false;
        PlayerPrefsHolder.ShowLevelSelectOnLoad = false;

        if (Session.Settings != null)
            Session.Settings.SpeedMultiplier = 1;
    }

    /// <summary>Calculator.Start: show level-pick dialog once after first Main load.</summary>
    public static bool ConsumeAwaitingLevelPick()
    {
        if (!_isActive || !_awaitingLevelPick)
            return false;

        _awaitingLevelPick = false;
        return true;
    }

    /// <summary>Calculator.Start: apply 320 kt / 10x / APP after level chosen or auto-advance.</summary>
    public static bool ConsumePendingApplyConfig()
    {
        if (!_isActive || !_pendingApplyConfig)
            return false;

        _pendingApplyConfig = false;
        _finishInvoked = false;
        _advancing = false;
        return true;
    }

    public static void StartFromLevel(int level)
    {
        if (!_isActive)
            return;

        PlayerPrefsHolder.Level = PlayerPrefsHolder.ClampLevel(level);
        if (PlayerPrefsHolder.Level < PlayerPrefsHolder.FirstRankedLevel)
            PlayerPrefsHolder.Level = PlayerPrefsHolder.FirstRankedLevel;

        // Abort current run (if any) and restart chosen level from the beginning.
        _awaitingLevelPick = false;
        _pendingApplyConfig = true;
        _finishInvoked = false;
        _advancing = false;
        Session.IsRunning = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainSceneName);
    }

    public static void ApplyFlightConfig()
    {
        if (!_isActive || Session.State == null)
            return;

        Calculator.CSpeed = InitialSpeedKnots;
        Calculator.RSpeed = InitialSpeedKnots;

        if (Calculator.Instance != null)
        {
            if (Calculator.Instance.txtCSpeed != null)
                Calculator.Instance.txtCSpeed.text = InitialSpeedKnots.ToString();
            if (Calculator.Instance.txtRSpeed != null)
                Calculator.Instance.txtRSpeed.text = InitialSpeedKnots.ToString();
            if (Calculator.Instance.txtRSpeed_overTape != null)
                Calculator.Instance.txtRSpeed_overTape.text = InitialSpeedKnots.ToString();
        }

        Session.State.Speed10X.Set(true);
        Session.Settings.SpeedMultiplier = ForcedSpeedMultiplier;
        Session.State.AppArmed = false;
        Session.State.AutoSetLNAV(true, true);

        Session.IsRunning = true;
        Time.timeScale = 1f;
        LevelTestDialog.NotifyLevelRunning(PlayerPrefsHolder.Level);

        Debug.Log(
            $"[LevelTest] Level {PlayerPrefsHolder.Level} — start {InitialSpeedKnots} kt (manual) @ {ForcedSpeedMultiplier}x, ATC ignored (APP arms at VS_nx=3)");
    }

    /// <summary>Keep 10x; do not override pilot-selected IAS or APP arm state.</summary>
    public static void EnforceFlightConfig()
    {
        if (!_isActive || Session.State == null || Session.Settings == null)
            return;

        Session.Settings.SpeedMultiplier = ForcedSpeedMultiplier;
        if (!Session.State.Speed10X)
            Session.State.Speed10X.Set(true);
    }

    /// <summary>
    /// GraphManage finish hook: skip debrief/cloud and auto-load next level.
    /// Returns true when the normal finish UI must be skipped.
    /// </summary>
    public static bool TryHandleGameFinished()
    {
        if (!_isActive)
            return false;

        if (_advancing)
            return true;

        _advancing = true;
        _finishInvoked = true;

        int current = PlayerPrefsHolder.Level;
        int next = current + 1;

        if (next > PlayerPrefsHolder.MaxLevel)
        {
            Debug.Log($"[LevelTest] Completed level {current}. No more levels — ending session.");
            EndSession();
            Time.timeScale = 0f;
            LevelTestDialog.ShowCompleted(current);
            return true;
        }

        Debug.Log($"[LevelTest] Level {current} done → auto-advance to {next}");
        PlayerPrefsHolder.Level = next;
        _pendingApplyConfig = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainSceneName);
        return true;
    }

    /// <summary>True once per level when DME/route end should finish the test run.</summary>
    public static bool TryClaimFinish()
    {
        if (!_isActive || _finishInvoked || _advancing)
            return false;

        _finishInvoked = true;
        return true;
    }
}
