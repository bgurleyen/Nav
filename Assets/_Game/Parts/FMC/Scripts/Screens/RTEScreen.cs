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
   TMP_Text _levelLabel;

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
         var fmc = infoFMC.Live;
         if (fmc == null)
         {
            RefreshLevelLabel();
            return;
         }

         var rte = fmc.Rte;
         
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

         RefreshLevelLabel();
      }

   void RefreshLevelLabel()
   {
      if (_levelLabel == null)
         _levelLabel = CreateLevelLabel();
      if (_levelLabel == null)
         return;

      var info = Session.CurrentLevel != null ? Session.CurrentLevel.levelInfo : null;
      _levelLabel.text = info != null && info.LevelNumber > 0
         ? "LEVEL " + info.LevelNumber
         : "LEVEL";

      if (_origin == null)
         return;

      _levelLabel.font = _origin.font;
      _levelLabel.fontSharedMaterial = _origin.fontSharedMaterial;
      _levelLabel.color = _origin.color;
      _levelLabel.fontStyle = _origin.fontStyle;
      _levelLabel.enableAutoSizing = false;
      _levelLabel.fontSize = _origin.fontSize;
      _levelLabel.alignment = _origin.alignment;
   }

   TMP_Text CreateLevelLabel()
   {
      if (_origin == null)
         return null;

      var sourceLine = _origin.transform;
      while (sourceLine != null && sourceLine.name != "Three Columns HF Line")
         sourceLine = sourceLine.parent;
      if (sourceLine == null)
         return null;

      Transform bottomRow = null;
      var rows = GetComponentsInChildren<Transform>(true);
      for (var i = 0; i < rows.Length; i++)
      {
         if (rows[i].name == "LineEntry 5")
         {
            bottomRow = rows[i];
            break;
         }
      }
      if (bottomRow == null)
         return null;

      var copy = Instantiate(sourceLine.gameObject, bottomRow);
      copy.name = "Level Line";
      var rect = copy.GetComponent<RectTransform>();
      rect.anchorMin = Vector2.zero;
      rect.anchorMax = Vector2.one;
      rect.offsetMin = Vector2.zero;
      rect.offsetMax = Vector2.zero;
      rect.localScale = Vector3.one;

      TMP_Text left = null;
      var labels = copy.GetComponentsInChildren<TMP_Text>(true);
      for (var i = 0; i < labels.Length; i++)
      {
         var label = labels[i];
         if (label.gameObject.name == "left label")
         {
            left = label;
            label.gameObject.SetActive(true);
            continue;
         }

         label.gameObject.SetActive(false);
      }

      return left;
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
