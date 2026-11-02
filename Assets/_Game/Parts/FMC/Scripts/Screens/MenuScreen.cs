using Navigation;
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
      }
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
        if (holder == null || holder.childCount < 5)
            return;

        ShowDefaultTopLineMenuChoice(holder.GetChild(0));
        ShowLinkPromptOnly(holder.GetChild(3), "/privacy>");
        ShowLinkPromptOnly(holder.GetChild(4), "ZFlite.com>");
        HideMenuLineChoice(holder.GetChild(5));
    }

    static void HideMenuLineChoice(Transform lineRoot)
    {
        for (int i = 0; i < lineRoot.childCount; i++)
        {
            var child = lineRoot.GetChild(i);
            if (child.name == "Menu Line Choice")
                child.gameObject.SetActive(false);
        }
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
}

