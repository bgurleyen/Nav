using Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeyboardButton : MonoBehaviour
{
    private void Awake()
    {
        var button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnClickPress);
    }

    static bool BlockedByPaywall()
    {
        if (Entitlements.HasFullUnlock)
            return false;

        PaywallDialog.Show();
        return true;
    }

    private void OnClickPress()
    {
        if (BlockedByPaywall())
            return;

        FlightFeedbackSettings.PulseKey();

        var label = GetComponentInChildren<TMP_Text>();
        if (label == null || !TryCurrentScreen(out var screen))
            return;

        var text = label.text ?? "";

        if (int.TryParse(text, out var _nr))
        {
            screen.OnNumberPressed(_nr);
        }
        else if (text.Length == 1)
        {
            if (text == Keys.Decimal)
            {
                screen.OnDecimalPressed();
            }
            else if (text == Keys.Slash)
            {
                screen.OnSlashPressed();
            }
            else // it's a letter
            {
                screen.OnCharacterInput(text[0]);
            }
        }
        else
        {
            switch (text)
            {
                case Keys.Sign:
                    screen.OnSignPressed();
                    break;
                case Keys.Del:
                    screen.OnDeletePress();
                    break;
                case Keys.Clr:
                    screen.OnClearPress();
                    break;
            }
        }
    }

    static bool TryCurrentScreen(out ScreenBase screen)
    {
        screen = FMC_Screens.Instance != null ? FMC_Screens.Instance.CurrentScreen : null;
        return screen != null;
    }

    public void OnExecPress()
    {
        if (BlockedByPaywall())
            return;

        if (TryCurrentScreen(out var screen))
            screen.OnExecPress();
    }

    public void OnPreviousPageClick()
    {
        if (BlockedByPaywall())
            return;

        if (TryCurrentScreen(out var screen))
            screen.DisplayPrevPage();
    }

    public void OnNextPageClick()
    {
        if (BlockedByPaywall())
            return;

        if (TryCurrentScreen(out var screen))
            screen.DisplayNextPage();
    }

    public void OnMenuButtonClick()
    {
        if (BlockedByPaywall())
            return;

        FMC_Screens.Instance?.ShowPage(FMCScreens.Menu);
    }


    public void OnLEGSButtonClick()
    {
        if (BlockedByPaywall())
            return;

        FMC_Screens.Instance?.ShowPage(FMCScreens.Legs);
    }


    public void OnINITButtonClick()
    {
        if (BlockedByPaywall())
            return;

        FMC_Screens.Instance?.ShowPage(FMCScreens.Init);
    }

    public void OnRTEButtonClick()
    {
        if (BlockedByPaywall())
            return;

        FMC_Screens.Instance?.ShowPage(FMCScreens.Rte);
    }

    public void OnCRZButtonClick()
    {
        if (BlockedByPaywall())
            return;

        FMC_Screens.Instance?.ShowPage(FMCScreens.Crz);
    }

    public void OnDESButtonClick()
    {
        if (BlockedByPaywall())
            return;

        FMC_Screens.Instance?.ShowPage(FMCScreens.Des);
    }

    public void OnARRButtonClick()
    {
        if (BlockedByPaywall())
            return;

        FMC_Screens.Instance?.ShowPage(FMCScreens.Arr);
    }

    public void OnPROGButtonClick()
    {
        if (BlockedByPaywall())
            return;

        FMC_Screens.Instance?.ShowPage(FMCScreens.Prog);
    }

    public void OnFIXButtonClick()
    {
        if (BlockedByPaywall())
            return;

        FMC_Screens.Instance?.ShowPage(FMCScreens.Fix);
    }

    public void OnPAUSEButtonClick()
    {
        if (BlockedByPaywall())
            return;

        if (LevelStartInformation.IsAwaitingRequestDescent || GraphManage.IsEndScreenVisible)
            return;

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