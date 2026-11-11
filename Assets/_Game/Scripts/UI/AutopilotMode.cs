using Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unyawn.Utils;

/// <summary>
/// Autopilot harness: Level Test layout, full sim rules, heading-only ATC, automated XFR/LVL/approach config.
/// </summary>
public static class AutopilotMode
{
    public const string SceneName = "Autopilot";
    public const string MainSceneName = "Main";

    public const int ForcedSpeedMultiplier = 10;
    public const int DefaultStartLevel = 1;
    public const int FinalApproachFlapIdx = 8;
    public const int FinalApproachSpeedKnots = 140;

    static bool _isActive;
    static bool _awaitingLevelPick;
    static bool _pendingApplyConfig;
    static bool _flightAutomationEnabled;
    static bool _requestedDescent;
    static bool _landingConfigApplied;

    static bool _xfr1LitHandled;
    static bool _xfr2LitHandled;
    static int _watchedPassedNodeIndex = -1;

    static int _savedPlayerLevel;
    static bool _hasSavedPlayerLevel;
    static bool _advancing;
    static bool _stableWaitActive;
    static bool _stabilizationReadout;
    static float _levelFlightStartUnscaled;
    static float _lastAppliedRangeNm = -1f;
    static float _lastHeadingCorrectionUnscaled;

    public const float StableWaitTimeoutSeconds = 300f;
    public const float HeadingCorrectionIntervalSeconds = 20f;

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
        ResetAutomationState();
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
        ResetAutomationState();
        _advancing = false;

        if (Session.Settings != null)
            Session.Settings.SpeedMultiplier = 1;
    }

    static void ResetAutomationState()
    {
        _flightAutomationEnabled = false;
        _requestedDescent = false;
        _landingConfigApplied = false;
        _xfr1LitHandled = false;
        _xfr2LitHandled = false;
        _watchedPassedNodeIndex = -1;
        _stableWaitActive = false;
        _stabilizationReadout = false;
        _levelFlightStartUnscaled = 0f;
        _lastAppliedRangeNm = -1f;
        _lastHeadingCorrectionUnscaled = 0f;
    }

    public static bool ConsumeAwaitingLevelPick()
    {
        if (!_isActive || !_awaitingLevelPick)
            return false;

        _awaitingLevelPick = false;
        return true;
    }

    public static bool ConsumePendingApplyConfig()
    {
        if (!_isActive || !_pendingApplyConfig)
            return false;

        _pendingApplyConfig = false;
        ResetAutomationState();
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

        _awaitingLevelPick = false;
        _pendingApplyConfig = true;
        ResetAutomationState();
        _advancing = false;
        Session.IsRunning = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainSceneName);
    }

    /// <summary>After Main load: HDG, 10x, speed brake full. Briefing / REQUEST DESCENT stay normal until <see cref="Tick"/>.</summary>
    public static void ApplyInitialConfig()
    {
        if (!_isActive || Session.State == null)
            return;

        Session.State.AutoSetHDG(true);
        Session.State.Speed10X.Set(true);
        Session.Settings.SpeedMultiplier = ForcedSpeedMultiplier;

        Calculator.SetSpeedBrakeDeployed(true);

        _flightAutomationEnabled = true;
        AutopilotDialog.NotifyLevelRunning(PlayerPrefsHolder.Level);

        Debug.Log(
            $"[Autopilot] Level {PlayerPrefsHolder.Level} — HDG, SB FULL, {ForcedSpeedMultiplier}x; automation armed after REQUEST DESCENT");
    }

    public static void EnforceFlightConfig()
    {
        if (!_isActive || _stabilizationReadout || Session.State == null || Session.Settings == null)
            return;

        Session.Settings.SpeedMultiplier = ForcedSpeedMultiplier;
        if (!Session.State.Speed10X)
            Session.State.Speed10X.Set(true);
    }

    public static void Tick()
    {
        if (!_isActive || !_flightAutomationEnabled || _stabilizationReadout)
            return;

        EnforceFlightConfig();
        UpdateMapRangeForDme();
        CheckStableTimeout();

        if (!_requestedDescent)
        {
            TryAutoRequestDescent();
            return;
        }

        if (Move.Instance == null || Calculator.Instance == null || Session.State == null)
            return;

        TryAutopilotLnavLegsOnWaypointPass();
        TryPeriodicNextPointHeadingCorrection();
        TryAutoAckAtc1();
        TryAutoAckAtc2();
        TryAutoArmApproach();
    }

    static void TryAutopilotLnavLegsOnWaypointPass()
    {
        var move = Move.Instance;
        if (move == null || !move.IsCurrentAtcLateralLnav())
            return;

        move.TryAckAtc1WhenLegsMatchesClearance();

        int passed = PositionVirtualNode.PassedNodeIndex;
        if (_watchedPassedNodeIndex < 0)
            _watchedPassedNodeIndex = passed;

        if (passed <= _watchedPassedNodeIndex)
            return;

        _watchedPassedNodeIndex = passed;
        move.ApplyHeadingToNextLegsWaypoint();
        _lastHeadingCorrectionUnscaled = Time.unscaledTime;
    }

    static void TryAutoRequestDescent()
    {
        if (!LevelStartInformation.IsAwaitingRequestDescent)
            return;

        var briefing = Object.FindFirstObjectByType<LevelStartInformation>();
        if (briefing == null)
            return;

        briefing.ButtonClick();
        _requestedDescent = true;
        _stableWaitActive = true;
        _levelFlightStartUnscaled = Time.unscaledTime;
        _lastAppliedRangeNm = -1f;
        _xfr1LitHandled = false;
        _xfr2LitHandled = false;
        _lastHeadingCorrectionUnscaled = Time.unscaledTime;
    }

    static void TryPeriodicNextPointHeadingCorrection()
    {
        if (Move.Instance == null || !Move.Instance.IsCurrentAtcLateralLnav())
            return;

        if (_lastHeadingCorrectionUnscaled <= 0f)
            _lastHeadingCorrectionUnscaled = Time.unscaledTime;

        if (Time.unscaledTime - _lastHeadingCorrectionUnscaled < HeadingCorrectionIntervalSeconds)
            return;

        _lastHeadingCorrectionUnscaled = Time.unscaledTime;
        if (!Move.Instance.TryGetNextHeadingTargetPointIndex(out _))
            return;

        Move.Instance.ApplyHeadingToNextLegsWaypoint();
    }

    static void TryAutoAckAtc1()
    {
        var move = Move.Instance;
        if (move.XFR1 == null)
            return;

        if (!move.XFR1.interactable)
        {
            _xfr1LitHandled = false;
            return;
        }

        if (_xfr1LitHandled)
            return;

        if (move.Atc1 == null || string.IsNullOrWhiteSpace(move.Atc1.text))
            return;
        if (move.Atc1.color != Color.green)
            return;

        _xfr1LitHandled = true;
        Calculator.Instance.XFR1_Click();
    }

    static void TryAutoAckAtc2()
    {
        var move = Move.Instance;
        if (move.XFR2 == null)
            return;

        if (!move.XFR2.interactable)
        {
            _xfr2LitHandled = false;
            return;
        }

        if (_xfr2LitHandled)
            return;

        if (move.Atc2 == null || string.IsNullOrWhiteSpace(move.Atc2.text))
            return;
        if (move.Atc2.color != Color.green)
            return;

        _xfr2LitHandled = true;
        bool finalAltitudeRow = move.IsCurrentInstructionFinalAltitude();

        Calculator.Instance.XFR2_Click();
        Session.State.AutoSetLC(true);

        if (finalAltitudeRow && !_landingConfigApplied)
            ApplyFinalDescentConfig();
    }

    static void ApplyFinalDescentConfig()
    {
        _landingConfigApplied = true;
        var calc = Calculator.Instance;
        if (calc == null)
            return;

        Calculator.Flap_Idx = FinalApproachFlapIdx;
        calc.SetFlaps();

        Calculator.RSpeed = FinalApproachSpeedKnots;
        Calculator.CSpeed = FinalApproachSpeedKnots;
        calc.ShowSpeedWindow();
        calc.SnapSpeedBug();

        calc.SetLG(true, false);
        Calculator.SetSpeedBrakeDeployed(false);

        Debug.Log("[Autopilot] Final descent row — flaps 40, 140 kt, gear down, speed brake stowed");
    }

    static void TryAutoArmApproach()
    {
        if (Session.State.AppArmed || Move.Instance == null)
            return;

        if (!Move.Instance.IsArmApproachAvailable())
            return;

        Move.Instance.OnArmApproachClicked();
    }

    static void UpdateMapRangeForDme()
    {
        if (!_requestedDescent || Move.Instance == null)
            return;

        float dme = Move.Instance.DME();
        float rangeNm = dme > 80f ? 80f : dme > 40f ? 40f : dme > 20f ? 20f : 10f;
        if (Mathf.Abs(rangeNm - _lastAppliedRangeNm) < 0.01f)
            return;

        _lastAppliedRangeNm = rangeNm;
        Session.ZoomMultiplier = 80f / rangeNm;

        if (!UYServiceLocator.Has<Drawer>())
            return;

        var drawer = UYServiceLocator.Get<Drawer>();
        drawer.Clear();
        drawer.Display();
        drawer.RefreshDisplayDistances();
    }

    static void CheckStableTimeout()
    {
        if (!_stableWaitActive)
            return;
        if (Time.unscaledTime - _levelFlightStartUnscaled < StableWaitTimeoutSeconds)
            return;

        _stableWaitActive = false;
        AutopilotLog.AppendLevelResult(PlayerPrefsHolder.Level, "---");
        Session.IsRunning = false;
        AdvanceToNextLevelOrEnd();
    }

    /// <summary>1000 ft readout started. Log the locked result and stop forcing 10x until the next level.</summary>
    public static void BeginStabilizationReadout(bool isStable)
    {
        if (!_isActive)
            return;

        _stableWaitActive = false;
        _stabilizationReadout = true;
        string statusLabel = isStable ? "Stable" : "Not Stable";
        float minutes = (Time.unscaledTime - _levelFlightStartUnscaled) / 60f;
        if (isStable)
            AutopilotLog.AppendLevelResult(PlayerPrefsHolder.Level, statusLabel, minutes);
        else
            AutopilotLog.AppendLevelResult(PlayerPrefsHolder.Level, statusLabel);
    }

    static void AdvanceToNextLevelOrEnd()
    {
        int current = PlayerPrefsHolder.Level;
        int next = current + 1;

        if (next > PlayerPrefsHolder.MaxLevel)
        {
            Debug.Log($"[Autopilot] Completed level {current}. No more levels — ending session.");
            EndSession();
            Time.timeScale = 0f;
            AutopilotDialog.ShowCompleted(current);
            return;
        }

        Debug.Log($"[Autopilot] Level {current} done → auto-advance to {next}");
        PlayerPrefsHolder.Level = next;
        _pendingApplyConfig = true;
        _advancing = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainSceneName);
    }

    /// <summary>
    /// GraphManage finish hook: skip debrief / level matrix and auto-load the next level.
    /// </summary>
    public static bool TryHandleGameFinished()
    {
        if (!_isActive)
            return false;

        if (_advancing)
            return true;

        _advancing = true;
        AdvanceToNextLevelOrEnd();
        return true;
    }
}
