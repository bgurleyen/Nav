using Navigation;
using TMPro;
using UnityEngine;

public class MenuScreen : ScreenBase
{
   public override void OnLineSelectLeft(int index)
   {
      base.OnLineSelectLeft(index);

      switch (index)
      {
         case 0:
            FMSScreens.ShowPage(FMCScreens.Legs);
            break;
         case 4:
            FlightFeedbackSettings.ToggleVibration();
            ApplyMenuLinkPrompts();
            break;
      }
   }

    public override void OnLeftCornerPress()
    {
        base.OnLeftCornerPress();
        FlightFeedbackSettings.ToggleSounds();
        ApplyMenuLinkPrompts();
    }

    public override void OnLineSelectRight(int index)
    {
        base.OnLineSelectRight(index);

        switch (index)
        {
            case 3:
                Application.OpenURL(ZfliteUrls.Privacy);
                break;
            case 4:
                Application.OpenURL(ZfliteUrls.Site);
                break;
        }
    }

    public override void Show()
   {
      base.Show();

      Main.UpdatePageInfo(
         isMod: false,
         pageTitle: "MENU");

      ApplyMenuLinkPrompts();
   }

    void ApplyMenuLinkPrompts()
    {
        var holder = transform.Find("holder");
        if (holder == null || holder.childCount < 6)
            return;

        ShowDefaultTopLineMenuChoice(holder.GetChild(0));
        ShowLinkPromptOnly(holder.GetChild(3), "/privacy>");
        ShowLeftAndRightPrompts(holder.GetChild(4), FlightFeedbackSettings.VibrationMenuLabel, "ZFlite.com>");
        ShowLeftPromptOnly(holder.GetChild(5), FlightFeedbackSettings.SoundsMenuLabel);
    }

    static void ShowDefaultTopLineMenuChoice(Transform lineRoot)
    {
        Transform menuChoice = null;
        for (int i = 0; i < lineRoot.childCount; i++)
        {
            var child = lineRoot.GetChild(i);
            if (child.name == "Menu Line Choice")
                menuChoice = child;
        }

        if (menuChoice == null)
            return;

        menuChoice.gameObject.SetActive(true);

        var footer = menuChoice.Find("footer");
        if (footer == null)
            return;

        for (int i = 0; i < footer.childCount; i++)
        {
            var column = footer.GetChild(i);
            column.gameObject.SetActive(true);

            var bg = column.GetComponent<BgText>();
            if (bg == null)
                continue;

            if (column.name == "f left")
                bg.SetAsDefault("<FMC");
            else if (column.name == "f center")
                bg.SetAsDefault("<ACT>");
            else if (column.name == "f right")
                bg.Clear();
        }
    }

    static void ShowLinkPromptOnly(Transform lineRoot, string prompt)
    {
        Transform menuChoice = null;
        for (int i = 0; i < lineRoot.childCount; i++)
        {
            var child = lineRoot.GetChild(i);
            if (child.name == "Menu Line Choice")
                menuChoice = child;
        }

        if (menuChoice == null)
            return;

        menuChoice.gameObject.SetActive(true);

        var footer = menuChoice.Find("footer");
        if (footer == null)
            return;

        for (int i = 0; i < footer.childCount; i++)
        {
            var column = footer.GetChild(i);
            bool isRight = column.name == "f right";
            column.gameObject.SetActive(isRight);
            if (!isRight)
                continue;

            var bg = column.GetComponent<BgText>();
            if (bg != null)
                bg.SetAsDefault(prompt);
        }
    }

    static void ShowLeftAndRightPrompts(Transform lineRoot, string leftPrompt, string rightPrompt)
    {
        Transform menuChoice = null;
        for (int i = 0; i < lineRoot.childCount; i++)
        {
            var child = lineRoot.GetChild(i);
            if (child.name == "Menu Line Choice")
                menuChoice = child;
        }

        if (menuChoice == null)
            return;

        menuChoice.gameObject.SetActive(true);

        var footer = menuChoice.Find("footer");
        if (footer == null)
            return;

        for (int i = 0; i < footer.childCount; i++)
        {
            var column = footer.GetChild(i);
            var bg = column.GetComponent<BgText>();
            if (column.name == "f left")
            {
                column.gameObject.SetActive(true);
                SetFooterColumnWidth(column, 0f, 0.44f);
                if (bg != null)
                {
                    bg.SetAsDefault(leftPrompt);
                    PrepareSingleLineLabel(bg);
                }
            }
            else if (column.name == "f right")
            {
                column.gameObject.SetActive(true);
                SetFooterColumnWidth(column, 0.44f, 1f);
                if (bg != null)
                {
                    bg.SetAsDefault(rightPrompt);
                    PrepareSingleLineLabel(bg);
                }
            }
            else
            {
                column.gameObject.SetActive(false);
            }
        }
    }

    static void ShowLeftPromptOnly(Transform lineRoot, string prompt)
    {
        Transform menuChoice = null;
        for (int i = 0; i < lineRoot.childCount; i++)
        {
            var child = lineRoot.GetChild(i);
            if (child.name == "Menu Line Choice")
                menuChoice = child;
        }

        if (menuChoice == null)
            return;

        menuChoice.gameObject.SetActive(true);

        var footer = menuChoice.Find("footer");
        if (footer == null)
            return;

        for (int i = 0; i < footer.childCount; i++)
        {
            var column = footer.GetChild(i);
            bool isLeft = column.name == "f left";
            column.gameObject.SetActive(isLeft);
            if (!isLeft)
                continue;

            SetFooterColumnWidth(column, 0f, 0.58f);
            var bg = column.GetComponent<BgText>();
            if (bg != null)
            {
                bg.SetAsDefault(prompt);
                PrepareSingleLineLabel(bg);
            }
        }
    }

    static void SetFooterColumnWidth(Transform column, float anchorMinX, float anchorMaxX)
    {
        var rt = column as RectTransform;
        if (rt == null)
            return;

        rt.anchorMin = new Vector2(anchorMinX, 0f);
        rt.anchorMax = new Vector2(anchorMaxX, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    static void PrepareSingleLineLabel(BgText bg)
    {
        if (bg == null || bg.label == null)
            return;

        bg.label.enableWordWrapping = false;
        bg.label.overflowMode = TextOverflowModes.Overflow;
        bg.label.ForceMeshUpdate(true);
    }
}
