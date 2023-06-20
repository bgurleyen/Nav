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

    

    [Header("scratch pad")]
    [SerializeField]
    public BgText scratchPadText;
    


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

   

    
    public static class Keywords
    {
        public const string DELETE = "DELETE";
        public const string ERASE = "ERASE";
    }
}
