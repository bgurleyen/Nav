using Navigation.Data;
using TMPro;
using UnityEngine;

public class ARRScreen : ScreenBase
{

   [SerializeField] private TMP_Text _star;
   [SerializeField] private TMP_Text _rw;
   [SerializeField] private TMP_Text _transitionFlag;
   [SerializeField] private TMP_Text _transition;
   
   public override void Show()
   {
      base.Show();
      var fmc = infoFMC.Instance.Fmc;

      Main.UpdatePageInfo(
         isMod: false,
         secondInfo: fmc.Initref.Destination,
         pageTitle: "ARRIVALS",
         currentPage: 0, totalPages: 1);
  

      InvokeRepeating(nameof(Refresh), 0, 1f);
   }

   public override void Hide()
   {
      base.Hide();

      CancelInvoke(nameof(Refresh));
   }

   
     private void Refresh()
      {
         var fmc = infoFMC.Instance.Fmc;

         _star.text = fmc.Arr.STAR;
         _rw.text = fmc.Arr.RW;
         _transitionFlag.text = "<ACT>";
         _transition.text = fmc.Arr.Transition;
      }
}
