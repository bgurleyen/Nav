using Navigation;

public class InitScreen : ScreenBase
{
   public override void Show()
   {
      base.Show();

      Main.UpdatePageInfo(secondInfo: "APPROACH", pageTitle: "REF", currentPage: 0, totalPages: 1);
   }
}
