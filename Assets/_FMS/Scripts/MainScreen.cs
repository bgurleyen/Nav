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
    [SerializeField] BgText lastFRight;
    

    [Header("scratch pad")]
    [SerializeField] BgText scratchPadText;


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
        scratchPadText.SetAsDefault(buffer);
        if (withStatus)
        {
            lastFLeft.text = "ok";
        }
    }

    public void DisplayOperation(string value , string details = "", bool tallDetails= false)
    {
        lastFLeft.text = $"<{value}";
        switch (tallDetails)
        {
            case true:
                lastFRight.SetAsTall(details);
                break;
            default:
                lastFRight.SetAsDefault(details);
                break;
        }

        scratchPadText.Clear();
    }
    
    public static class Keywords
    {
        public const string DELETE = "DELETE";
        public const string ERASE = "ERASE";
    }
}
