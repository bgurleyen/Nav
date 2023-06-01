using Navigation.Data;
using TMPro;
using UnityEngine;

public class InitScreen : ScreenBase
{
   [SerializeField] private TMP_Text _gwt;
   [SerializeField] private TMP_Text _destinationRw;
   [SerializeField] private TMP_Text _field;
   [SerializeField] private TMP_Text _rw;
   [SerializeField] private TMP_Text _freq;
   [SerializeField] private TMP_Text _f15;
   [SerializeField] private TMP_Text _f30;
   [SerializeField] private TMP_Text _f40;
   [SerializeField] private TMP_Text _vRef;

   public override void Show()
   {
      base.Show();

      Main.UpdatePageInfo(secondInfo: "APPROACH", pageTitle: "REF", currentPage: 0, totalPages: 1);

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
      
      _gwt.text = initRef.GWT;
      _destinationRw.text = initRef.Destination + initRef.RW;
      _field.text = initRef.Field;
      _rw.text = $"ILS {initRef.RW}/CRS";

      _freq.text = $"{initRef.Freq}/{initRef.Course}";

      _f15.text = $"{initRef.F15}KT";
      _f30.text = $"{initRef.F30}KT";
      _f40.text = $"{initRef.F40}KT";

      _vRef.text = $"30/{initRef.Vref}";
   }
}
