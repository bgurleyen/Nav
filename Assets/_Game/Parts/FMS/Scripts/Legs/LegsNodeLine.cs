using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LegsNodeLine : MonoBehaviour
{
    [Header("header")]
    public TMP_Text hLeft;
    public TMP_Text hMiddle;

    [Header("header full")]
    public TMP_Text hFull;

    [Header("footer")] 
    public BgText fLeft;
    public BgText fRight;

    private NodeSelection selection;

    public void DisplayNodeDetails(RoutePoint node, NodeSelection linkedInfo)
    {
        var _distance = linkedInfo.LinkedId == PositionVirtualNode.GetNodeTo.ID
            ? GameManager.Instance.Aircraft.ComputedDistanceLeftOnSegment
            : node.Distance;

        if (linkedInfo.IsAddedDiscontinuity)
        {
            ShowAsDiscontinuity();
        }
        else
        {
            hFull.text = node.IsAfterDiscontinuity ? "--- ROUTE DISCONTINUITY ---" : "";

            hLeft.text = node.IsAfterDiscontinuity ? "" : $"{node.RawDegrees:000}°";
            hMiddle.text = node.IsAfterDiscontinuity ? "" : $"{_distance:F1}NM";

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


            var _isSpeedRestriction = node.GetSpeedIsRestricted(out var _speedDisplayValue);
            var _isAltRestriction = node.GetAltitudeIsRestricted(out var _altDisplayValue);
            // var _speedColor = 
            //     ? "#FF00C7"
            //     : "#EBE0C9";
            // var _altitudeColor  = 
            //     ? "#FF00C7"
            //     : "#EBE0C9";

            var _speedState = node.IsSpeedModified
                ? BgText.TextState.ModSelection
                : _isSpeedRestriction
                    ? node.ID == LegsScreen.VisibleRoute.FirstSpeedRegulationNodeId 
                        ? BgText.TextState.Magenta
                        : BgText.TextState.TallText
                    : BgText.TextState.SmallText;

            var _altState = node.IsAltitudeModified
                ? BgText.TextState.ModSelection
                : _isAltRestriction
                    ? node.ID == LegsScreen.VisibleRoute.FirstAltRegulationNodeId
                        ? BgText.TextState.Magenta
                        : BgText.TextState.TallText
                    : BgText.TextState.SmallText;

            fRight.SetText(true,"/",
                new BgText.TextBuilder {State = _speedState, Text = _speedDisplayValue},
                new BgText.TextBuilder {State = _altState, Text = _altDisplayValue});
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
        fRight.Clear();

    }
}
