using Navigation;
using TMPro;
using UnityEngine;

public class LegsNodeLine : MonoBehaviour
{
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

    public void DisplayNodeDetails(RoutePoint node, NodeSelection linkedInfo)
    {
        var distance = linkedInfo.LinkedId == PositionVirtualNode.GetNodeToOnActive.ID
            ? Session.PlayerAircraft.ComputedDistanceLeftOnSegment
            : node.Distance;

        if (linkedInfo.IsAddedDiscontinuity)
        {
            ShowAsDiscontinuity();
        }
        else
        {
            hFull.text = node.IsAfterDiscontinuity ? "--- ROUTE DISCONTINUITY ---" : "";

            hLeft.text = node.IsAfterDiscontinuity ? "" : $"{node.RawDegrees:000}°";
            hMiddle.text = node.IsAfterDiscontinuity ? "" : $"{distance:F1}NM";

            if (node.IsModified)
            {
                fLeft.SetAsModified(node.Name);
            }
            else if (node.GetIsDisplayCurrent)
            {
                fLeft.SetAsMagenta(node.Name);
            }
            else
            {
                fLeft.SetAsDefault(node.Name);
            }

            fMiddle.text = linkedInfo.IsPlanCenter && Session.State.MapMode == MapMode.Plan
                ? "<CTR>" : "";

            var isSpeedRestriction = node.GetSpeedIsRestricted(out var speedDisplayValue);
            var isAltRestriction = node.GetAltitudeIsRestricted(out var altDisplayValue);
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

            fRight.SetText(true,"/",
                new BgText.TextBuilder {State = speedState, Text = speedDisplayValue},
                new BgText.TextBuilder {State = altState, Text = altDisplayValue});
        }
    }


    private void ShowAsDiscontinuity()
    {
        hLeft.text = "THEN";
        hMiddle.text = "";
        hFull.text = "";

        fLeft.SetAsDefault( "□□□□□");
        fRight.Clear();
    }

    internal void ShowEmpty()
    {
        hLeft.text = "";
        hMiddle.text = "";
        hFull.text = "";

        fLeft.Clear();
        fMiddle.text = "";
        fRight.Clear();

    }
}
