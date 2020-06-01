using Gamelogic.Extensions;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MainScreen : Singleton<MainScreen>
{
    [Header("top header")]
    public TMP_Text leftInfo;
    public TMP_Text title;
    public TMP_Text pageNumber;


    [Header("last button line")]
    public TMP_Text lastHLeft;
    public TMP_Text lastHRight;
    public TMP_Text lastFLeft;
    public TMP_Text lastFRight;

    [Header("scratch pad")]
    public TMP_Text scratchPadText;


    string nodeNameTemp = "";

    public void DisplayInfo(string nodeName)
    {
        nodeNameTemp = nodeName;
        scratchPadText.text = nodeName;
        lastFLeft.text = $"ok";
    }

    public void UpdateScratchPad(string buffer, bool withStatus = true)
    {
        scratchPadText.text = nodeNameTemp + buffer;
        if (withStatus)
        {
            lastFLeft.text = "ok";
        }
    }

    public void DisplayOperation(string value = "ERASE", string details = "RTE DATA")
    {
        lastFLeft.text = $"<{value}";
        lastFRight.text = $"{details}";
        scratchPadText.text = "";
        nodeNameTemp = "";
    }



}
