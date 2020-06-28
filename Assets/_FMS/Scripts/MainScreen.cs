using Gamelogic.Extensions;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MainScreen : Singleton<MainScreen>
{
    [Header("top header")]
    [SerializeField] TMP_Text leftInfo;
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text pageNumber;


    [Header("last button line")]
    [SerializeField] TMP_Text lastHLeft;
    [SerializeField] TMP_Text lastHRight;
    [SerializeField] TMP_Text lastFLeft;
    [SerializeField] TMP_Text lastFRight;

    [Header("scratch pad")]
    [SerializeField] TMP_Text scratchPadText;


    public string LastLineLeft => lastFLeft.text;
   

    public void UpdatePageInfo(int currentPage, int totalPages, bool isMod)
    {
        pageNumber.text = $"{currentPage + 1}/{totalPages}";
        title.text = isMod ? "MOD" : "LEGS";
    }

    // public void DisplayInfo(string nodeName)
    // {
    //     nodeNameTemp = nodeName;
    //     scratchPadText.text = nodeName;
    //     lastFLeft.text = $"ok";
    // }

    public void UpdateScratchPad(string buffer, bool withStatus = true)
    {
        scratchPadText.text = buffer;
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
    }
}
