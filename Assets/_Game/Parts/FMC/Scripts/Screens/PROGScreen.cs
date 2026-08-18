using Navigation.Data;
using TMPro;
using UnityEngine;

public class PROGScreen : ScreenBase
{
    private const float LargeFontSize = 40f;
    private const float LargeFontThreshold = 30f;
    private const float HeaderLabelFontSize = 30f;
    private const float CharacterWidth = 42f;
    private const float SecondColumnShiftX = -1f * CharacterWidth;

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
           firstInfo: "",
           secondInfo: "",
           pageTitle: "PROGRESS",
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
        var progRef = infoFMC.Instance.Fmc.Prog;

        _fromNode.text = progRef.PrvName;
        _nextNode.text = progRef.NxtName;
        _secondNode.text = progRef.SecondName;
        _destinationNode.text = progRef.Destination;

        _alt.text = progRef.PrvCrossAltitude;
        _dtg.text = progRef.NxtDTG;
        _altSecond.text = progRef.SecondDTG;
        _altDestination.text = progRef.DestDTG;

        _ata.text = progRef.PrvActualTime;
        _etaNext.text = progRef.NxtETA;
        _etaSecond.text = progRef.SecondETA;
        _etaDestination.text = progRef.DestETA;

        _fuelFrom.text = progRef.PrvActualFuel;
        _fuelNext.text = progRef.NxtFUEL;
        _fuelSecond.text = progRef.SecondFUEL;
        _fuelDestination.text = progRef.DestFUEL;

        _fuel.text = progRef.FuelQty;
        _wind.text = progRef.ActualWind;
        HideTrackLabels();
        ApplyColumnLayout();
        ApplyLargeFontSize();
        ApplyAltDtgLabelSize();
    }

    private void ApplyColumnLayout()
    {
        var texts = GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < texts.Length; i++)
        {
            var tmp = texts[i];
            if (tmp == null)
            {
                continue;
            }

            var name = tmp.gameObject.name;
            if (name != "h left" && name != "left label")
            {
                continue;
            }

            tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
        }

        var rects = GetComponentsInChildren<RectTransform>(true);
        for (var i = 0; i < rects.Length; i++)
        {
            var rt = rects[i];
            if (rt == null)
            {
                continue;
            }

            var name = rt.name;
            if (name == "h left" || name == "left label")
            {
                var min = rt.anchorMin;
                min.x = 0f;
                rt.anchorMin = min;
                var pos = rt.anchoredPosition;
                pos.x = 0f;
                rt.anchoredPosition = pos;
            }
            else if (name == "h middle" || name == "mid label")
            {
                var pos = rt.anchoredPosition;
                pos.x = -0.16435242f + SecondColumnShiftX;
                rt.anchoredPosition = pos;
            }
        }
    }

    private void HideTrackLabels()
    {
        HideTrackLabel(_nextNodeDegrees);
        HideTrackLabel(_secondNodeDegrees);
    }

    private static void HideTrackLabel(TMP_Text tmp)
    {
        if (tmp == null)
        {
            return;
        }

        tmp.text = "";
        tmp.gameObject.SetActive(false);
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

            tmp.ForceMeshUpdate();
            if (tmp.fontSize <= LargeFontThreshold)
            {
                continue;
            }

            tmp.enableAutoSizing = false;
            tmp.fontSize = LargeFontSize;
        }
    }

    private void ApplyAltDtgLabelSize()
    {
        var texts = GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < texts.Length; i++)
        {
            var tmp = texts[i];
            if (tmp == null || tmp.gameObject.name != "h middle")
            {
                continue;
            }

            var label = tmp.GetParsedText();
            if (!string.Equals(label, "alt", System.StringComparison.OrdinalIgnoreCase)
                && !string.Equals(label, "dtg", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            tmp.enableAutoSizing = false;
            tmp.fontSize = HeaderLabelFontSize;
        }
    }
}
