using Navigation;
using Navigation.Data;
using TMPro;
using UnityEngine;

public class RTEScreen : ScreenBase
{

   [SerializeField] private TMP_Text _destination;
   [SerializeField] private TMP_Text _rw;
   
   public override void Show()
   {
      base.Show();

      Main.UpdatePageInfo(
         isMod: false,
         firstInfo: "ACT",
         secondInfo: "RTE",
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
         var initRef = infoFMC.Instance.Fmc.Initref;
         
         _destination.text = initRef.Destination ;
         _rw.text = initRef.RW;
      }
}
