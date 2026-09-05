using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Other traffic flies its own waypoint route from spawn (virtual/start point)
/// onto the ILS and then to the runway. Movement does not depend on scene
/// GameObjects named "AC (n)" — those are optional EditMap labels only.
/// While the player is on final, trailing/joining traffic holds so that
/// 2.5 NM horizontal separation is kept.
/// </summary>
public class OtherAC
{
    public const float FinalSeparationNm = 2.5f;
    private const float IntermediateCaptureNm = 1.5f;
    private const float RunwayCaptureNm = 0.25f;

    private ACInfo[] _acItems;

    private float dx, dy, hyp;
    private float x, y;
    private float Speed, SpeedCo;
    private string s;
    private Vector2 PtPos;
    private int PointIndex, AltitudeR;
    private float AltitudeC;
    private Vector2 finalPosition;
    private int HeadingW;

    private string _aircraftKey;

    private int _acIndex;
    private int _currentPointIndex = 1;
    private int _lastPositiveSpeed = 160;
    private bool _landed;

    private GameObject _ac;

    public OtherAC(int acIndex, LevelDataScriptableObject levelData)
    {
        _acIndex = acIndex;
        _aircraftKey = "AC (" + (acIndex + 1) + ")";

        var table = levelData != null && levelData.otherACs != null && acIndex < levelData.otherACs.Length
            ? levelData.otherACs[acIndex]
            : null;
        _acItems = table != null && table.ACItems != null ? table.ACItems : Array.Empty<ACInfo>();

        if (_acItems.Length == 0)
        {
            _landed = true;
            return;
        }

        AltitudeC = _acItems[0].Altitude;
        if (_acItems[0].Speed > 0)
            _lastPositiveSpeed = _acItems[0].Speed;

        finalPosition = Move.Instance.PointPos(_acItems[0].Point);

        // Optional EditMap label. Missing objects must not freeze ND traffic.
        _ac = GameObject.Find(_aircraftKey);

        if (Move.Instance != null)
        {
            Move.Instance.ACPositions[_aircraftKey] = finalPosition;
            Move.Instance.ACTexts[_aircraftKey] = "";
            ApplyMapTransform();
        }
    }

    public void Tick(Dictionary<string, string> acTexts, Dictionary<string, Vector2> acPositions)
    {
        if (_landed || _acItems == null || _currentPointIndex >= _acItems.Length)
        {
            Despawn(acTexts, acPositions);
            return;
        }

        PointIndex = _acItems[_currentPointIndex].Point;
        AltitudeR = _acItems[_currentPointIndex].Altitude;
        Speed = _acItems[_currentPointIndex].Speed;

        if (Speed > 0)
            _lastPositiveSpeed = Speed;

        int moveSpeed = Speed > 0 ? Speed : _lastPositiveSpeed;

        PtPos = Move.Instance.PointPos(PointIndex);

        Calculator.WindElements WE = Calculator.CalculateWindElements(AltitudeR, moveSpeed, HeadingW);
        float GS = Mathf.Max(1f, (float)WE.GS);

        EnsureDisplayed(acTexts, acPositions);

        SpeedCo = Session.Settings.AircraftTickDistance(GS);

        x = finalPosition.x;
        y = finalPosition.y;

        dx = PtPos.x - x;
        dy = PtPos.y - y;

        HeadingW = 90 - (int)(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);

        hyp = Mathf.Sqrt(dx * dx + dy * dy);

        bool lastPoint = _currentPointIndex >= _acItems.Length - 1;

        if (hyp < 0.001f)
        {
            finalPosition = PtPos;
            if (lastPoint)
            {
                Despawn(acTexts, acPositions);
                return;
            }

            _currentPointIndex += 1;
            AltitudeC = AltitudeR;
            Publish(acTexts, acPositions);
            return;
        }

        float step = Mathf.Min(SpeedCo, hyp);
        Vector2 proposed = new Vector2(x + dx / hyp * step, y + dy / hyp * step);

        if (ShouldHoldForPlayerFinal(finalPosition, proposed))
        {
            Publish(acTexts, acPositions);
            return;
        }

        AltitudeC -= (AltitudeC - AltitudeR) / hyp * step;
        finalPosition = proposed;

        Publish(acTexts, acPositions);

        float remaining = hyp - step;
        float captureNm = lastPoint ? RunwayCaptureNm : IntermediateCaptureNm;
        if (remaining <= captureNm)
        {
            if (lastPoint)
            {
                Despawn(acTexts, acPositions);
                return;
            }

            _currentPointIndex += 1;
            AltitudeC = AltitudeR;
        }
    }

    internal static bool ShouldHoldForPlayerFinal(
        Vector2 otherPos,
        Vector2 proposedPos,
        Vector2 playerPos,
        Vector2 runwayPos,
        bool playerOnFinal,
        float minSeparationNm)
    {
        if (!playerOnFinal)
            return false;

        float distAfter = Vector2.Distance(proposedPos, playerPos);
        if (distAfter >= minSeparationNm)
            return false;

        float otherDme = Vector2.Distance(otherPos, runwayPos);
        float playerDme = Vector2.Distance(playerPos, runwayPos);
        bool vacatingAhead = otherDme + 0.05f < playerDme;
        return !vacatingAhead;
    }

    private bool ShouldHoldForPlayerFinal(Vector2 currentPos, Vector2 proposedPos)
    {
        if (Move.Instance == null || Session.PlayerAircraft == null)
            return false;

        var route = Session.OriginalReferenceRoute;
        if (route == null || route.Points == null || route.Points.Length == 0)
            return false;

        Vector2 runwayPos = Move.Instance.PointPos(route.Points.Length - 1);
        return ShouldHoldForPlayerFinal(
            currentPos,
            proposedPos,
            Session.PlayerAircraft.NMPosition,
            runwayPos,
            Move.Instance.IsPlayerOnFinalApproach(),
            FinalSeparationNm);
    }

    private void EnsureDisplayed(Dictionary<string, string> acTexts, Dictionary<string, Vector2> acPositions)
    {
        if (!acPositions.ContainsKey(_aircraftKey))
            acPositions.Add(_aircraftKey, finalPosition);
        if (!acTexts.ContainsKey(_aircraftKey))
            acTexts.Add(_aircraftKey, "");
    }

    private void Publish(Dictionary<string, string> acTexts, Dictionary<string, Vector2> acPositions)
    {
        EnsureDisplayed(acTexts, acPositions);
        acPositions[_aircraftKey] = finalPosition;
        ApplyMapTransform();

        s = AltitudeC - Calculator.CAltitude < 0 ? "-" : "+";
        if (Mathf.Abs(AltitudeC - (float)Calculator.CAltitude) < 1000)
            s += "0";
        if (AltitudeC - (int)Calculator.CAltitude < 3000 && (int)Calculator.CAltitude - AltitudeC < 6000)
        {
            s += (int)(Mathf.Abs(AltitudeC - (float)Calculator.CAltitude) / 100);
            SetMapLabel("" + _acIndex);
        }
        else
        {
            s = "";
        }

        acTexts[_aircraftKey] = s;
    }

    private void Despawn(Dictionary<string, string> acTexts, Dictionary<string, Vector2> acPositions)
    {
        _landed = true;
        if (acPositions != null)
            acPositions.Remove(_aircraftKey);
        if (acTexts != null)
            acTexts.Remove(_aircraftKey);
        SetMapLabel("");
    }

    private void ApplyMapTransform()
    {
        if (_ac != null)
            _ac.transform.localPosition = finalPosition;
    }

    private void SetMapLabel(string text)
    {
        if (_ac == null)
            return;

        var label = _ac.GetComponent<Text>();
        if (label != null)
            label.text = text;
    }
}
