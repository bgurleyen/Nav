using Navigation;

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

   public override void Show()
   {
      base.Show();

      Main.UpdatePageInfo(
         isMod: false,
         pageTitle: "MENU");
   }
}
