using Navigation.Data;
using TMPro;
using UnityEngine;

public class CRZScreen : ScreenBase
{

   [SerializeField] private TMP_Text _crzAltitude;
   [SerializeField] private TMP_Text _crzSpeed;
   [SerializeField] private TMP_Text _actualWind;
   [SerializeField] private TMP_Text _destination;
   [SerializeField] private TMP_Text _fuelAtDestination;
   
   public override void Show()
   {
      base.Show();

      Main.UpdatePageInfo(
         isMod: false,
         secondInfo: "ECON",
         pageTitle: "CRZ",
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
         var crz = infoFMC.Instance.Fmc.Crz;

         _crzAltitude.text = "??";
         _crzSpeed.text = "??";
         _actualWind.text = crz.ActualWind;
         _destination.text = initRef.Destination ;
         _fuelAtDestination.text = crz.FuelAtDestination;
      }
}
