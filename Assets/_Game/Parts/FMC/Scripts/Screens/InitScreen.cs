using Navigation.Data;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public class InitScreen : ScreenBase
{
   private const float VrefHeaderFontSize = 26.1f;
   private const float LargeFontSize = 40f;
   private const float LargeFontThreshold = 30f;
   private const float DestinationRwFontSize = 30f;
   private static readonly Regex FieldElevationRegex = new Regex(
      @"^(?<ft>\d+)FT(?<m>\d+)(?<unit>M)?$",
      RegexOptions.Compiled);

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
      _field.text = FormatFieldElevation(initRef.Field);
      _rw.text = $"ILS {initRef.RW}/CRS";

      _freq.text = $"{initRef.Freq}/{initRef.Course}";

      _f15.text = $"{initRef.F15}KT";
      _f30.text = $"{initRef.F30}KT";
      _f40.text = $"{initRef.F40}KT";

      _vRef.text = $"30/{initRef.Vref}";
      ApplyLargeFontSize();
   }

   private void ApplyLargeFontSize()
   {
      var texts = GetComponentsInChildren<TMP_Text>(true);
      for (var i = 0; i < texts.Length; i++)
      {
         var tmp = texts[i];
         if (tmp == null)
         {
            continue;
         }

         if (tmp == _destinationRw)
         {
            tmp.enableAutoSizing = false;
            tmp.fontSize = DestinationRwFontSize;
            continue;
         }

         tmp.ForceMeshUpdate();
         if (tmp.fontSize <= LargeFontThreshold)
         {
            continue;
         }

         tmp.enableAutoSizing = false;
         tmp.fontSize = LargeFontSize;
      }
   }

   private static string FormatFieldElevation(string field)
   {
      if (string.IsNullOrEmpty(field))
      {
         return field;
      }

      var match = FieldElevationRegex.Match(field);
      if (!match.Success)
      {
         return field;
      }

      var formatted = $"{match.Groups["ft"].Value}<size={VrefHeaderFontSize}>FT</size>{match.Groups["m"].Value}";
      if (match.Groups["unit"].Success)
      {
         formatted += $"<size={VrefHeaderFontSize}>M</size>";
      }

      return formatted;
   }
}
