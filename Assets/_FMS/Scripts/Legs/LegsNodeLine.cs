using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LegsNodeLine : MonoBehaviour
{
    public Color idle;
    public Color modified;
    public Color current;
    public Image fLeftBackground;

    [Header("header")]
    public TMP_Text hLeft;
    public TMP_Text hMiddle;

    [Header("header full")]
    public TMP_Text hFull;

    [Header("footer")]
    public TMP_Text fLeft;
    public TMP_Text fRight;

    NodeSelection selection;

    public void DisplayNodeDetails(RoutePoint node, NodeSelection linkedInfo)
    {
        var _distance = linkedInfo.LinkedId == PositionVirtualNode.GetNodeTo.ID
            ? GameManager.Instance.Aircraft.ComputedDistanceLeft
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

            fLeft.text = node.Name;
            var _speedColor = node.GetSpeedIsRestricted(out var _speedDisplayValue)
                ? "#FF00C7"
                : "#EBE0C9";
            var _altitudeColor  = node.GetAltitudeIsRestricted(out var _altDisplayValue) 
                ? "#FF00C7"
                : "#EBE0C9";
            
            fRight.text =
                $"<color={_speedColor}>{_speedDisplayValue} / <color={_altitudeColor}>{_altDisplayValue}";
        }

        if (node.IsModified)
        {
            fLeftBackground.color = modified;
        }
        else if (node.IsCurrent)
        {
            fLeftBackground.color = current;
        }
        else
        {
            fLeftBackground.color = idle;
        }
    }


    public void ShowAsDiscontinuity()
    {
        hLeft.text = "THEN";
        hMiddle.text = "";
        hFull.text = "";

        fLeft.text = "□□□□□";
        fRight.text = "";
    }

    internal void ShowEmpty()
    {
        hLeft.text = "";
        hMiddle.text = "";
        hFull.text = "";

        fLeft.text = "";
        fRight.text = "";

    }
}
