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

    public void UpdatePageInfo( bool isMod = false, string firstInfo = "", string pageTitle = "", string secondInfo = "",int currentPage = -1, int totalPages = -1)
    {
        leftInfo2.text = secondInfo;
        pageNumber.text = currentPage == -1 ? "" : $"{currentPage + 1}/{totalPages}";

        if (isMod)
        {
            leftInfo1.SetAsModified(firstInfo);
        }
        else
        {
            leftInfo1.SetAsDefault(firstInfo);
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
