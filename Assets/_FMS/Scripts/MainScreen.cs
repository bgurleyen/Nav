using Gamelogic.Extensions;
using TMPro;
using UnityEngine;

public class MainScreen : Singleton<MainScreen>
{
    [SerializeField] Color modified;
    [SerializeField] Color idle;
    
    [Header("top header")]
    [SerializeField] BgText leftInfo1;
    [SerializeField] TMP_Text leftInfo2;
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

    public void UpdatePageInfo(int currentPage, int totalPages, bool isMod, string pageTitle)
    {
        pageNumber.text = $"{currentPage + 1}/{totalPages}";
        if (isMod)
        {
            leftInfo1.SetAsModified("MOD");
        }
        else
        {
            leftInfo1.SetAsDefault("ACT");
        }
        title.text = pageTitle;
    }

    public void UpdateScratchPad(string buffer, bool withStatus = true)
    {
        scratchPadText.text = buffer;
        if (withStatus)
        {
            lastFLeft.text = "ok";
        }
    }

    public void DisplayOperation(string value , string details = "")
    {
        lastFLeft.text = $"<{value}";
        lastFRight.text = $"{details}";
        scratchPadText.text = "";
    }
    
    public static class Keywords
    {
        public const string DELETE = "DELETE";
        public const string ERASE = "ERASE";
    }
}
