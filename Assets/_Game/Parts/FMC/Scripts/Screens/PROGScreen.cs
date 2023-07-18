using Navigation.Data;
using TMPro;
using UnityEngine;

public class PROGScreen : ScreenBase
{
   [SerializeField] private TMP_Text _fromNode;
   [SerializeField] private TMP_Text _nextNodeDegrees;
   [SerializeField] private TMP_Text _nextNode;
   [SerializeField] private TMP_Text _secondNodeDegrees;
   [SerializeField] private TMP_Text _secondNode;
   [SerializeField] private TMP_Text _destinationNode;
   
   [SerializeField] private TMP_Text _alt;
   [SerializeField] private TMP_Text _ata;
   [SerializeField] private TMP_Text _fuelFrom;
   
   [SerializeField] private TMP_Text _dtg;
   [SerializeField] private TMP_Text _etaNext;
   [SerializeField] private TMP_Text _fuelNext;
   
   [SerializeField] private TMP_Text _altSecond;
   [SerializeField] private TMP_Text _etaSecond;
   [SerializeField] private TMP_Text _fuelSecond;
   
   [SerializeField] private TMP_Text _altDestination;
   [SerializeField] private TMP_Text _etaDestination;
   [SerializeField] private TMP_Text _fuelDestination;
   
   [SerializeField] private TMP_Text _wind;
   [SerializeField] private TMP_Text _fuel;
   
   public override void Show()
   {
      base.Show();

      Main.UpdatePageInfo(
         isMod: false,
         firstInfo: "-------",
         secondInfo: "PROGRESS",
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
         
        
      }
}
