using Navigation;
using Navigation.Data;
using TMPro;
using UnityEngine;

public class DESScreen : ScreenBase
{
    private const float LargeFontSize = 40f;
    private const float LargeFontThreshold = 30f;
    [SerializeField] private TMP_Text _rwAltitude;
    //[SerializeField] private TMP_Text _econSpeed_Mach;
    [SerializeField] private BgText _econSpeed_Mach;
    [SerializeField] private TMP_Text _wptAltFix;
    [SerializeField] private TMP_Text _arrTansition;
    [SerializeField] private TMP_Text _fpa;
    [SerializeField] private TMP_Text _vb;
    [SerializeField] private TMP_Text _vs;


    private const string ERASE_TITLE = "<ERASE";
    private ScratchPadInterpreter _scratchPadInterpreter;
    private string _scratchPadBuffer = "";

    private Simulation _simulation;

    private void Start()
    {
        lastHRight.text = "";
        lastHLeft.text = "";
    }

    public override void Show()
    {
        base.Show();


        Main.UpdatePageInfo(
           isMod: Session.IsMod,
           firstInfo: Session.IsMod ? "MOD" : "",
           secondInfo: "ECON",
           pageTitle: "DES",
           currentPage: 0, totalPages: 1);

        InvokeRepeating(nameof(Refresh), 0f, 1f);
    }

    public override void Hide()
    {
        base.Hide();

        CancelInvoke(nameof(Refresh));
        if (_desNowPending)
        {
            _desNowPending = false;
            Session.IsMod = false;
        }
    }


    private void Refresh()
    {
        Main.UpdatePageInfo(
           isMod: Session.IsMod,
           firstInfo: Session.IsMod ? "MOD" : "",
           secondInfo: "ECON",
           pageTitle: "DES",
           currentPage: 0, totalPages: 1);

        if (infoFMC.Instance == null)
            return;

        infoFMC.Instance.UpdateDescentToPointBeforeRunway();
        var des = infoFMC.Live.Des;

        if (!string.IsNullOrEmpty(des.RWAltitude))
        {
            _rwAltitude.text = des.RWAltitude;
        }
        _wptAltFix.text = des.WptAltFix;
        if (_wptAltFix != null)
        {
            _wptAltFix.enableAutoSizing = false;
            _wptAltFix.fontSize = LargeFontSize;
        }

        _arrTansition.text = Calculator.FormatFmcAltitude(Calculator.TransitionAltitudeFeet);
        SetDesValue(_fpa, des.FPA);
        SetDesValue(_vb, des.VB);
        SetDesValue(_vs, des.VS);
        ApplyColumnLayout();
        ApplyLargeFontSize();
        ShowTargetSpeed();
        ShowDesNow();
    }

    private const string DesNowPrompt = "DES NOW>";
    private bool _desNowPending;
    private TMP_Text _desNowLabel;

    /// <summary>6R data line: the large "right label" next to the 6R header.</summary>
    private TMP_Text DesNowLabel()
    {
        if (_desNowLabel != null || lastHRight == null)
            return _desNowLabel;

        // Line prefab: header/h right and footer/right label.
        var line = lastHRight.transform.parent != null ? lastHRight.transform.parent.parent : null;
        if (line != null)
        {
            foreach (var t in line.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t != lastHRight && t.gameObject.name == "right label")
                {
                    _desNowLabel = t;
                    break;
                }
            }
        }
        if (_desNowLabel == null)
            _desNowLabel = lastHRight;
        return _desNowLabel;
    }

    private void ShowDesNow()
    {
        var label = DesNowLabel();
        if (label == null)
            return;

        var calc = Calculator.Instance;
        bool available = calc != null && calc.DesNowAvailable;
        if (!available)
            _desNowPending = false;
        label.text = available ? DesNowPrompt : "";
        label.horizontalAlignment = HorizontalAlignmentOptions.Right;
    }

    public override void OnRightCornerPress()
    {
        var calc = Calculator.Instance;
        if (calc == null || !calc.DesNowAvailable)
            return;

        _desNowPending = true;
        Session.IsMod = true;
        Refresh();
    }

    private void ShowTargetSpeed()
    {
        if (_econSpeed_Mach == null)
        {
            return;
        }

        _econSpeed_Mach.SetAsTall(Calculator.FormatMachIas(
            (int)System.Math.Round(Calculator.DesEconMach() * 100.0),
            Calculator.DesEconIas()));
        var label = _econSpeed_Mach.label;
        if (label != null)
        {
            label.horizontalAlignment = HorizontalAlignmentOptions.Left;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
        }

        var rt = _econSpeed_Mach.transform as RectTransform;
        if (rt == null)
        {
            return;
        }

        var min = rt.anchorMin;
        min.x = 0f;
        rt.anchorMin = min;
        var pos = rt.anchoredPosition;
        pos.x = 0f;
        rt.anchoredPosition = pos;
    }

    private void SetDesValue(TMP_Text tmp, string value)
    {
        if (tmp == null)
        {
            return;
        }

        tmp.text = value ?? "";
        tmp.ForceMeshUpdate(true);
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
            if (name != "h left" && name != "left label")
            {
                continue;
            }

            var min = rt.anchorMin;
            min.x = 0f;
            rt.anchorMin = min;
            var pos = rt.anchoredPosition;
            pos.x = 0f;
            rt.anchoredPosition = pos;
        }
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

    public override void OnExecPress()
    {
        if (_desNowPending)
        {
            _desNowPending = false;
            Calculator.Instance?.StartDesNow();
        }
        Session.IsMod = false;
        ShowTargetSpeed();
        ShowDesNow();
    }

    public override void OnLeftCornerPress()
    {
        /*if (IsErase)
        {
            OnLeftCornerPressErase?.Invoke();
            ClearCurrentOperation();
            //ClearSelectionHistory();
        }*/
    }


    public override void OnClearPress()
    {
        // to generalize to all operations
        if (ScratchPadInterpreter.IsDeletePending(_scratchPadBuffer))
        {
            _scratchPadBuffer = string.Empty;
        }

        if (_scratchPadBuffer.Length > 0)
        {
            _scratchPadBuffer = _scratchPadBuffer.Remove(_scratchPadBuffer.Length - 1);
        }

        UpdateScratchPad(_scratchPadBuffer, false);
    }

    public override void OnDeletePress()
    {
        _scratchPadBuffer = MainScreen.Keywords.DELETE;
        UpdateScratchPad(_scratchPadBuffer);
    }

    public override void OnCharacterInput(char character)
    {
        if (character == '-' && _scratchPadBuffer.Length > 0 && _scratchPadBuffer[_scratchPadBuffer.Length - 1] == '-')
        {
            _scratchPadBuffer = _scratchPadBuffer.Remove(_scratchPadBuffer.Length - 1);
        }
        else
        {
            _scratchPadBuffer += character;
        }

        InterpretScratchpadOnTextChanged();

        UpdateScratchPad(_scratchPadBuffer, false);
    }

    private struct ScratchPadInterpreter
    {
        public bool IsValid;
        public string AltRegulation;
        public int? SpeedRegulation;

        public bool IsSpeedRegulation(out int regulation)
        {
            regulation = -1;
            if (!IsValid || SpeedRegulation == null)
            {
                return false;
            }

            regulation = SpeedRegulation.Value;
            return true;
        }
        public bool IsAltitudeRegulation(out string regulation)
        {
            regulation = "";
            if (!IsValid || AltRegulation == null)
            {
                return false;
            }

            regulation = AltRegulation;
            return true;
        }

        public bool IsRelativeNode(out int angle, out int distance, out int relativeNodeId)
        {
            distance = 0;
            angle = 0;
            relativeNodeId = 0;
            if (!IsValid || AltRegulation != null || SpeedRegulation != null)
            {
                return false;
            }

            //Debug.Log("=relative insert=");
            return true;
        }

        public bool IsRelativeNodeOnDirection(out int distance, out int relativeNodeId)
        {
            distance = 0;
            relativeNodeId = 0;
            if (!IsValid || AltRegulation != null || SpeedRegulation != null)
            {
                return false;
            }

            //Debug.Log("=relative insert on direction=");
            return true;
        }

        public bool IsLinearApproach(out int angle)
        {
            angle = 0;
            if (!IsValid || AltRegulation != null || SpeedRegulation != null)
            {
                return false;
            }

            return true;
        }

        public static bool IsDeletePending(string buffer)
        {
            return buffer == MainScreen.Keywords.DELETE;
        }
    }

    private void InterpretScratchpadOnTextChanged()
    {
        _scratchPadInterpreter = new ScratchPadInterpreter
        {
            IsValid = true,
            AltRegulation = null,
            SpeedRegulation = null,
        };

        // look for regulations
        if (_scratchPadBuffer.Length < 1)
        {
            _scratchPadInterpreter.IsValid = false;
        }
        else if (_scratchPadBuffer[0] == '/' && _scratchPadBuffer.Length > 1)
        {
            // should be altitude only regulation
            var value = _scratchPadBuffer.Substring(1, _scratchPadBuffer.Length - 1);

            if (DataHandler.ParseAltRegulation(value, out _, out _, out _))
            {
                _scratchPadInterpreter.AltRegulation =
                    Calculator.NormalizeAltitudeRegulationToFeet(value);
                _scratchPadInterpreter.SpeedRegulation = null;
            }
            else
            {
                _scratchPadInterpreter.IsValid = false;
            }
        }
        else if (_scratchPadBuffer[_scratchPadBuffer.Length - 1] == '/' && _scratchPadBuffer.Length > 1)
        {
            // should be speed only regulation
            var value = _scratchPadBuffer.Substring(0, _scratchPadBuffer.Length - 1);
            if (int.TryParse(value, out var regulation))
            {
                _scratchPadInterpreter.AltRegulation = null;
                _scratchPadInterpreter.SpeedRegulation = regulation;
            }
        }
        else if (_scratchPadBuffer.Contains('/') && _scratchPadBuffer.Length > 3)
        {
            // may be speed & alt regulation
            var slashIndex = _scratchPadBuffer.IndexOf('/');
            var speed = _scratchPadBuffer.Substring(0, slashIndex);
            var altRegulation =
                _scratchPadBuffer.Substring(slashIndex + 1, _scratchPadBuffer.Length - (slashIndex + 1));
            if (int.TryParse(speed, out var speedRegulation) &&
                DataHandler.ParseAltRegulation(altRegulation, out _, out _, out _))
            {
                _scratchPadInterpreter.AltRegulation =
                    Calculator.NormalizeAltitudeRegulationToFeet(altRegulation);
                _scratchPadInterpreter.SpeedRegulation = speedRegulation;
            }
            else
            {
                _scratchPadInterpreter.IsValid = false;
            }
        }
        else if (DataHandler.ParseAltRegulation(_scratchPadBuffer, out _, out _, out _))
        {
            _scratchPadInterpreter.AltRegulation =
                Calculator.NormalizeAltitudeRegulationToFeet(_scratchPadBuffer);
        }
        else
        {
            _scratchPadInterpreter.IsValid = false;
        }
    }








    public void UpdateScratchPad(string buffer, bool withStatus = true)
    {
        MainScreen.Pad?.SetAsDefault(buffer);
        if (withStatus)
        {
            //lastHLeft.text = "ECON";
        }
    }

    private void ClearScratchPad()
    {
        _scratchPadBuffer = "";
        MainScreen.Pad?.Clear();
    }
}
