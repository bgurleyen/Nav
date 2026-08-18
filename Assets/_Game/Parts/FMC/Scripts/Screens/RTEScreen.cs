using Navigation;
using Navigation.Data;
using TMPro;
using UnityEngine;

public class RTEScreen : ScreenBase
{
   [SerializeField] private TMP_Text _destination;
   [SerializeField] private TMP_Text _rw;
   [SerializeField] private TMP_Text _origin;
   [SerializeField] private TMP_Text _originHeader;

   public override void Show()
   {
      base.Show();

      Main.UpdatePageInfo(
         isMod: false,
         firstInfo: "ACT",
         secondInfo: "RTE",
         currentPage: 0, totalPages: 1);

      CacheOriginLabels();

      InvokeRepeating(nameof(Refresh), 0, 1f);
   }

   public override void Hide()
   {
      base.Hide();

      CancelInvoke(nameof(Refresh));
   }

   
     private void Refresh()
      {
         var rte = infoFMC.Instance.Fmc.Rte;
         
         _destination.text = rte.Destination ;
         _rw.alignment = TextAlignmentOptions.MidlineRight;
         _rw.text = rte.RW;
         if (_originHeader != null)
         {
            _originHeader.alignment = TextAlignmentOptions.TopLeft;
         }

         if (_origin != null)
         {
            if (!string.IsNullOrEmpty(Session.RteOrigin))
            {
               _origin.text = Session.RteOrigin;
            }

            _origin.alignment = TextAlignmentOptions.MidlineLeft;
            if (_destination != null)
            {
               _origin.enableAutoSizing = false;
               _origin.fontSize = _destination.fontSize;
            }
         }
      }

   private void CacheOriginLabels()
   {
      if (_origin != null && _originHeader != null)
      {
         return;
      }

      var texts = GetComponentsInChildren<TMP_Text>(false);
      for (var i = 0; i < texts.Length; i++)
      {
         var tmp = texts[i];
         if (tmp == null)
         {
            continue;
         }

         if (_origin == null && tmp.text == "xxxx")
         {
            _origin = tmp;
         }
         else if (_originHeader == null && tmp.text == "ORIGIN")
         {
            _originHeader = tmp;
         }
      }
   }
}
