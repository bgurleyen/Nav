using Navigation;
using TMPro;
using UnityEngine;
using Unyawn.Utils;

public class LegsNodeLine : MonoBehaviour {    [Header("header")]
    public TMP_Text hLeft;
    public TMP_Text hMiddle;

    [Header("header full")]
    public TMP_Text hFull;

    [Header("footer")]
    public BgText fLeft;
    public TMP_Text fMiddle;
    public BgText fRight;

    public void DisplayNodeDetails(RoutePoint node, NodeSelection linkedInfo) {


        /* //Changes By S.A
          if (Session.ActiveRoute != null) {
             Debug.Log("-----------------------------Node ID changes-----------------------------");
             for (int i = 0; i < Session.ActiveRoute.Points.Length; i++) {
                 //Debug.Log("Session.ActiveRoute[" + i + "]: " + "ID: [" + Session.ActiveRoute.Points[i].ID + "] => Name: [" + Session.ActiveRoute.Points[i].Name + "]");
                 if (Session.ActiveRoute.Points[i].Name == "ELNAT") {
                     Session.ActiveRoute.Points[i].ID = 3;
                 }

             }
         }

         if (Session.ModRoute != null) {

             for (int i = 0; i < Session.ModRoute.Points.Length; i++) {
                 //Debug.Log("Session.ModRoute[" + i + "]: " + "ID: [" + Session.ModRoute.Points[i].ID + "] => Name: [" + Session.ModRoute.Points[i].Name + "]");
                 if (Session.ModRoute.Points[i].Name == "ELNAT") {
                     Session.ModRoute.Points[i].ID = 3;
                 }
             }
         }

         if (Session.ModeSetWithPosition != null) {
             for (int i = 0; i < Session.ModeSetWithPosition.Points.Length; i++) {
                 //Debug.Log("Session.ModeSetWithPosition[" + i + "]: " + "ID: [" + Session.ModeSetWithPosition.Points[i].ID + "] => Name: [" + Session.ModeSetWithPosition.Points[i].Name + "]");
                 if (Session.ModeSetWithPosition.Points[i].Name == "ELNAT") {
                     Session.ModeSetWithPosition.Points[i].ID = 3;
                 }
             }
         }*/


        //Old Code...
        if (node == null || linkedInfo == null)
            return;

        var toNode = PositionVirtualNode.GetNodeToOnActive;
        var distance = toNode != null
            && linkedInfo.LinkedId == toNode.ID
            && Session.PlayerAircraft != null
            ? Session.PlayerAircraft.ComputedDistanceLeftOnSegment
            : node.Distance;
        var courseText = $"{node.RawDegrees:000}°";
        if (UYServiceLocator.Has<LegsScreen>()
            && UYServiceLocator.Get<LegsScreen>().TryGetPendingInterceptCourse(node.ID, out var pendingCourse))
        {
            courseText = $"{pendingCourse:000}°";
        }
        else if (UYServiceLocator.Has<LegsScreen>()
            && UYServiceLocator.Get<LegsScreen>().TryGetModCourseDisplay(node.ID, out var modCourse))
        {
            courseText = $"{modCourse:000}°";
        }
        else if (node.IsLinearApproach)
        {
            courseText = $"{Mathf.RoundToInt(Mathf.Repeat(node.LinearApproachAngle, 360f)):000}°";
            distance = InboundInterceptDistance(node);
        }

        //Debug.Log("IsAddedDiscontinuity: " + linkedInfo.IsAddedDiscontinuity);
        if (linkedInfo.IsAddedDiscontinuity) {
            //Debug.Log("If Condition");
            ShowAsDiscontinuity();
        }
        else {
            //Debug.Log("Else Condition");

            //Debug.Log("IsAfterDiscontinuity: "+ node.IsAfterDiscontinuity);

            if (hFull != null)
                hFull.text = node.IsAfterDiscontinuity ? "--- ROUTE DISCONTINUITY ---" : "";

            if (hLeft != null)
                hLeft.text = node.IsAfterDiscontinuity ? "" : courseText;
            if (hMiddle != null)
                hMiddle.text = node.IsAfterDiscontinuity ? "" : $"{distance:F1}NM";

            if (fLeft != null)
            {
                if (node.IsModified) {
                    fLeft.SetAsModified(node.Name);
                }
                else if (node.GetIsDisplayCurrent) {
                    fLeft.SetAsMagenta(node.Name);
                }
                else {
                    fLeft.SetAsTall(node.Name);
                }
            }

            if (fMiddle != null)
                fMiddle.text = linkedInfo.IsPlanCenter && Session.State != null && Session.State.MapMode == MapMode.Plan
                    ? "<CTR>" : "";

            var isSpeedRestriction = node.GetSpeedIsRestricted(out var speedDisplayValue);
            var isAltRestriction = node.GetAltitudeIsRestricted(out var altDisplayValue);

            // FMS display only: above CO altitude show Mach; stored/computed values stay IAS.
            if (int.TryParse(speedDisplayValue, out var iasKnots) && iasKnots > 0)
            {
                var nodeAltitude = node.Altitude != null && node.Altitude.ComputedValue > 0
                    ? node.Altitude.ComputedValue
                    : node.GetAcceptedAltitude;
                speedDisplayValue = Calculator.FormatFmcSpeedDisplay(iasKnots, nodeAltitude);
            }

            // FMS display: QNH 1013 → FL ≡ feet/100 at/above transition (incl. A/B restrictions)
            altDisplayValue = Calculator.FormatFmcAltitudeDisplay(altDisplayValue);

            // var _speedColor = 
            //     ? "#FF00C7"
            //     : "#EBE0C9";
            // var _altitudeColor  = 
            //     ? "#FF00C7"
            //     : "#EBE0C9";

            var visible = Session.VisibleRoute;
            var speedState = node.IsSpeedModified
                ? BgText.TextState.ModSelection
                : isSpeedRestriction
                    ? visible != null && node.ID == visible.FirstSpeedRegulationNodeId
                        ? BgText.TextState.Magenta
                        : BgText.TextState.TallText
                    : BgText.TextState.SmallText;

            var altState = node.IsAltitudeModified
                ? BgText.TextState.ModSelection
                : isAltRestriction
                    ? visible != null && node.ID == visible.FirstAltRegulationNodeId
                        ? BgText.TextState.Magenta
                        : BgText.TextState.TallText
                    : BgText.TextState.SmallText;

            if (fRight != null)
                fRight.SetText(true, "/",
                new BgText.TextBuilder { State = speedState, Text = speedDisplayValue },
                new BgText.TextBuilder { State = altState, Text = altDisplayValue });
        }
    }

    private void ShowAsDiscontinuity() {
        //Debug.Log("ShowAsDiscontinuity");
        if (hLeft != null)
            hLeft.text = "THEN";
        if (hMiddle != null)
            hMiddle.text = "";
        if (hFull != null)
            hFull.text = "";

        if (fLeft != null)
            fLeft.SetAsTall("□□□□□");

        if (fRight != null)
            fRight.Clear();
    }

    static float InboundInterceptDistance(RoutePoint node)
    {
        var aircraft = Session.PlayerAircraft;
        if (aircraft == null)
            return node.Distance;

        var from = aircraft.NMPosition;
        var track = aircraft.HeadingDegrees;
        if (!Geometry.TryInboundIntercept(from, node.CartesianPosition, node.LinearApproachAngle, track, out var intercept))
            return Vector2.Distance(from, node.CartesianPosition);

        return Vector2.Distance(from, intercept) + Vector2.Distance(intercept, node.CartesianPosition);
    }

    internal void ShowEmpty() {
        hLeft.text = "";
        hMiddle.text = "";
        hFull.text = "";

        fLeft.Clear();
        fMiddle.text = "";
        fRight.Clear();

    }
}
