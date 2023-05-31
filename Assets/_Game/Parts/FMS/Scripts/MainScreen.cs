using Gamelogic.Extensions;
using TMPro;
using UnityEngine;

public class MainScreen : Singleton<MainScreen>
{
    [SerializeField] private Color modified;
    [SerializeField] private Color idle;
    
    [Header("top header")]
    [SerializeField] private BgText leftInfo1;
    [SerializeField] private TMP_Text leftInfo2;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text pageNumber;


    [Header("last button line")]
    [SerializeField] private TMP_Text lastHLeft;
    [SerializeField] private TMP_Text lastHRight;
    [SerializeField] private TMP_Text lastFLeft;
    [SerializeField] private BgText lastFRight;
    

    [Header("scratch pad")]
    [SerializeField]
    private BgText scratchPadText;


    public string LastLineLeft => lastFLeft.text;

    public void UpdatePageInfo(string pageTitle, string secondInfo)
    {
        pageNumber.text = "";
        leftInfo1.SetText(false,"");
        leftInfo2.text = "";
        title.text = pageTitle;
    }

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
