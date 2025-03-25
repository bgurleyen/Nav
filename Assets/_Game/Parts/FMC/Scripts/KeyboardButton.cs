using Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeyboardButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClickPress);
    }

    private void OnClickPress()
    {
        var text = GetComponentInChildren<TMP_Text>().text;

        if (int.TryParse(text, out var _nr))
        {
            FMC_Screens.Instance.CurrentScreen.OnNumberPressed(_nr);
        }
        else if (text.Length == 1)
        {
            if (text == Keys.Decimal)
            {
                FMC_Screens.Instance.CurrentScreen.OnDecimalPressed();
            }
            else if (text == Keys.Slash)
            {
                FMC_Screens.Instance.CurrentScreen.OnSlashPressed();
            }
            else // it's a letter
            {
                FMC_Screens.Instance.CurrentScreen.OnCharacterInput(text[0]);
            }
        }
        else
        {
            switch (text)
            {
                case Keys.Sign:
                    FMC_Screens.Instance.CurrentScreen.OnSignPressed();
                    break;
                case Keys.Del:
                    FMC_Screens.Instance.CurrentScreen.OnDeletePress();
                    break;
                case Keys.Clr:
                    FMC_Screens.Instance.CurrentScreen.OnClearPress();
                    break;
            }
        }
    }

    public void OnExecPress()
    {
        FMC_Screens.Instance.CurrentScreen.OnExecPress();
    }

    public void OnPreviousPageClick()
    {
        FMC_Screens.Instance.CurrentScreen.DisplayPrevPage();
    }

    public void OnNextPageClick()
    {
        FMC_Screens.Instance.CurrentScreen.DisplayNextPage();
    }

    public void OnMenuButtonClick()
    {
        FMC_Screens.Instance.ShowPage(FMCScreens.Menu);
    }


    public void OnLEGSButtonClick()
    {
        FMC_Screens.Instance.ShowPage(FMCScreens.Legs);
    }


    public void OnINITButtonClick()
    {
        FMC_Screens.Instance.ShowPage(FMCScreens.Init);
    }

    public void OnRTEButtonClick()
    {
        FMC_Screens.Instance.ShowPage(FMCScreens.Rte);
    }

    public void OnCRZButtonClick()
    {
        FMC_Screens.Instance.ShowPage(FMCScreens.Crz);
    }

    public void OnDESButtonClick()
    {
        FMC_Screens.Instance.ShowPage(FMCScreens.Des);
    }

    public void OnARRButtonClick()
    {
        FMC_Screens.Instance.ShowPage(FMCScreens.Arr);
    }

    public void OnPROGButtonClick()
    {
        FMC_Screens.Instance.ShowPage(FMCScreens.Prog);
    }

    public void OnFIXButtonClick()
    {
        FMC_Screens.Instance.ShowPage(FMCScreens.Fix);
    }

    public void OnPAUSEButtonClick()
    {
        Time.timeScale = Time.timeScale == 0 ? 1 : 0;
    }
}

public static class Keys
{
    public const string Sign = "+/-";
    public const string Decimal = ".";
    public const string Slash = "/";
    
    public const string Del = "DEL";
    public const string Clr = "CLR";
    public const string Sp = "SP";
}

//Hi, it’s working but, the sequence of the operation is changed. The sequence should be: write waypoint, put to topline  ,
//the mod will appear ( also white  dashed lines will appear on the map side) . Then u press execute to accept the change.
//If u check bormal operation, like press first line Elnat will appear at the bottom line then same sequence