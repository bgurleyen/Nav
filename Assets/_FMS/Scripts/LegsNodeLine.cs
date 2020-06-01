using System;
using System.Collections;
using System.Collections.Generic;
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
    public TMP_Text fRight1;
    public TMP_Text fRight2;

    NodeSelection selection;

    public void DisplayNodeDetails(DataPoint node, NodeSelection linkedInfo)
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
            fRight1.text = node.DisplaySpeed.ToString();
            fRight2.text = node.DisplayAltitude.ToString();// todo show ping if is regulation
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
        fRight1.text = "";
        fRight2.text = "";
    }

    internal void ShowEmpty()
    {
        hLeft.text = "";
        hMiddle.text = "";
        hFull.text = "";

        fLeft.text = "";
        fRight1.text = "";
        fRight2.text = "";

    }
}
