using Navigation.Data;
using TMPro;
using UnityEngine;

public class DESScreen : ScreenBase
{

   [SerializeField] private TMP_Text _rwAltitude;
   [SerializeField] private TMP_Text _econSpeed_Mach;
   [SerializeField] private TMP_Text _wptAltFix;
   [SerializeField] private TMP_Text _arrTansition;
   [SerializeField] private TMP_Text _fpa;
   [SerializeField] private TMP_Text _vb;
   [SerializeField] private TMP_Text _vs;
   
   public override void Show()
   {
      base.Show();
      

      Main.UpdatePageInfo(
         isMod: false,
         secondInfo: "ECON",
         pageTitle: "DES",
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
         var des = infoFMC.Instance.Fmc.Des;

         _rwAltitude.text = des.RWAltitude;
         _econSpeed_Mach.text = $"??/??";

         _wptAltFix.text = des.WptAltFix;

         _arrTansition.text = fmc.Arr.Transition;
         _fpa.text = des.FPA;
         _vb.text = des.VB;
         _vs.text = des.VS;
      }
}
