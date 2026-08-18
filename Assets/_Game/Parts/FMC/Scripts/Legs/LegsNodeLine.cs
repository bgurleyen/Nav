using System.Collections.Generic;
using Navigation;
using TMPro;
using UnityEngine;

public class LegsNodeLine : MonoBehaviour {
    [Header("header")]
    public TMP_Text hLeft;
    public TMP_Text hMiddle;

    [Header("header full")]
    public TMP_Text hFull;

    [Header("footer")]
    public BgText fLeft;
    public TMP_Text fMiddle;
    public BgText fRight;

    private NodeSelection _selection;

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
        var distance = linkedInfo.LinkedId == PositionVirtualNode.GetNodeToOnActive.ID
            ? Session.PlayerAircraft.ComputedDistanceLeftOnSegment
            : node.Distance;

        //Debug.Log("IsAddedDiscontinuity: " + linkedInfo.IsAddedDiscontinuity);
        if (linkedInfo.IsAddedDiscontinuity) {
            //Debug.Log("If Condition");
            ShowAsDiscontinuity();
            //SetRouteID(Session.ActiveRoute);
            //SetRouteID(Session.ModRoute);
            //SetRouteID(Session.ModeSetWithPosition);
        }
        else {
            //Debug.Log("Else Condition");

            //Debug.Log("IsAfterDiscontinuity: "+ node.IsAfterDiscontinuity);

            hFull.text = node.IsAfterDiscontinuity ? "--- ROUTE DISCONTINUITY ---" : "";
            //Debug.Log(hFull.text);

            hLeft.text = node.IsAfterDiscontinuity ? "" : $"{node.RawDegrees:000}°";
            hMiddle.text = node.IsAfterDiscontinuity ? "" : $"{distance:F1}NM";

            if (node.IsModified) {
                fLeft.SetAsModified(node.Name);
                //Debug.Log("SetAsModified");
            }
            else if (node.GetIsDisplayCurrent) {
                fLeft.SetAsMagenta(node.Name);
                //Debug.Log("SetAsMagenta");
            }
            else {
                fLeft.SetAsTall(node.Name);
            }

            fMiddle.text = linkedInfo.IsPlanCenter && Session.State.MapMode == MapMode.Plan
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

            var speedState = node.IsSpeedModified
                ? BgText.TextState.ModSelection
                : isSpeedRestriction
                    ? node.ID == Session.VisibleRoute.FirstSpeedRegulationNodeId
                        ? BgText.TextState.Magenta
                        : BgText.TextState.TallText
                    : BgText.TextState.SmallText;

            var altState = node.IsAltitudeModified
                ? BgText.TextState.ModSelection
                : isAltRestriction
                    ? node.ID == Session.VisibleRoute.FirstAltRegulationNodeId
                        ? BgText.TextState.Magenta
                        : BgText.TextState.TallText
                    : BgText.TextState.SmallText;

            fRight.SetText(true, "/",
                new BgText.TextBuilder { State = speedState, Text = speedDisplayValue },
                new BgText.TextBuilder { State = altState, Text = altDisplayValue });
        }
    }

    /*public void SetRouteID(RouteScriptableObject routeScriptableObject) {

        if (routeScriptableObject != null) {
            for (int i = 0; i < routeScriptableObject.Points.Length; i++) {
                if (routeScriptableObject.Points[i].Name == "_START_") {
                    routeScriptableObject.Points[i].ID = 0;
                }
                if (routeScriptableObject.Points[i].Name == "DLE") {
                    routeScriptableObject.Points[i].ID = 1;
                }
                if (routeScriptableObject.Points[i].Name == "DV581") {
                    routeScriptableObject.Points[i].ID = 2;
                }
                if (routeScriptableObject.Points[i].Name == "ELNAT") {
                    routeScriptableObject.Points[i].ID = 3;
                }
                if (routeScriptableObject.Points[i].Name == "NORTA") {
                    routeScriptableObject.Points[i].ID = 4;
                }
                if (routeScriptableObject.Points[i].Name == "DLE-5") {
                    routeScriptableObject.Points[i].ID = 5;
                }
                if (routeScriptableObject.Points[i].Name == "DV582") {
                    routeScriptableObject.Points[i].ID = 6;
                }
                if (routeScriptableObject.Points[i].Name == "DV583") {
                    routeScriptableObject.Points[i].ID = 7;
                }
                if (routeScriptableObject.Points[i].Name == "DV584") {
                    routeScriptableObject.Points[i].ID = 8;
                }
                if (routeScriptableObject.Points[i].Name == "DV585") {
                    routeScriptableObject.Points[i].ID = 9;
                }
                if (routeScriptableObject.Points[i].Name == "DV575") {
                    routeScriptableObject.Points[i].ID = 10;
                }
                if (routeScriptableObject.Points[i].Name == "DV574") {
                    routeScriptableObject.Points[i].ID = 11;
                }
                if (routeScriptableObject.Points[i].Name == "DV573") {
                    routeScriptableObject.Points[i].ID = 12;
                }
                if (routeScriptableObject.Points[i].Name == "DV572") {
                    routeScriptableObject.Points[i].ID = 13;
                }
                if (routeScriptableObject.Points[i].Name == "XAVER") {
                    routeScriptableObject.Points[i].ID = 14;
                }
                if (routeScriptableObject.Points[i].Name == "HANB") {
                    routeScriptableObject.Points[i].ID = 15;
                }
                if (routeScriptableObject.Points[i].Name == "RW27R") {
                    routeScriptableObject.Points[i].ID = 16;
                }
            }
        }
    }*/
    public void SetRouteID(RouteScriptableObject routeScriptableObject) {
        if (routeScriptableObject == null) {
            return;
        }

        Dictionary<string, int> nameToIDMap = new Dictionary<string, int> {
        { "_START_", 0 },
        { "DLE", 1 },
        { "DV581", 2 },
        { "ELNAT", 3 },
        { "NORTA", 4 },
        { "DLE-5", 5 },
        { "DV582", 6 },
        { "DV583", 7 },
        { "DV584", 8 },
        { "DV585", 9 },
        { "DV575", 10 },
        { "DV574", 11 },
        { "DV573", 12 },
        { "DV572", 13 },
        { "XAVER", 14 },
        { "HANB", 15 },
        { "RW27R", 16 }
    };

        foreach (var point in routeScriptableObject.Points) {
            if (nameToIDMap.TryGetValue(point.Name, out int id)) {
                point.ID = id;
            }
        }
    }
    private void ShowAsDiscontinuity() {
        //Debug.Log("ShowAsDiscontinuity");
        hLeft.text = "THEN";
        hMiddle.text = "";
        hFull.text = "";

        fLeft.SetAsTall("□□□□□");
        //Debug.Log("THEN  □□□□□");

        fRight.Clear();
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
