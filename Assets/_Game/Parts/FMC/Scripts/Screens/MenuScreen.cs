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

    //public override void OnLineSelectRight(int index)
    //{
    //    base.OnLineSelectRight(index);

    //    Debug.Log(index);

    //    switch (index)
    //    {
    //        case 4:
    //            break;
    //    }
    //}

    public override void OnRightCornerPress()
    {
        base.OnRightCornerPress();

        Application.OpenURL("https://games4pilot.com");
    }

    public override void Show()
   {
      base.Show();

      Main.UpdatePageInfo(
         isMod: false,
         pageTitle: "MENU");
   }
}
