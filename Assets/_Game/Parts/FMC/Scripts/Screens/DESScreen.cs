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

        InvokeRepeating(nameof(Refresh), 0, 0.2f);
    }

    public override void Hide()
    {
        base.Hide();

        CancelInvoke(nameof(Refresh));
    }


    private void Refresh()
    {
        Main.UpdatePageInfo(
           isMod: Session.IsMod,
           firstInfo: Session.IsMod ? "MOD" : "",
           secondInfo: "ECON",
           pageTitle: "DES",
           currentPage: 0, totalPages: 1);

        var des = infoFMC.Instance.Fmc.Des;

        if (!string.IsNullOrEmpty(des.RWAltitude))
        {
            _rwAltitude.text = des.RWAltitude;
        }
        if (!Session.IsMod)
        {
            _econSpeed_Mach.SetAsTall(string.IsNullOrEmpty(des.EconSpeed) ? "??/??" : des.EconSpeed);
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

    public override void OnLineSelectLeft(int index)
    {
        InterpretScratchpadOnTextChanged();

        switch (index)
        {
            case 1:
                {
                    if (ScratchPadInterpreter.IsDeletePending(_scratchPadBuffer))
                    {
                        _scratchPadBuffer = string.Empty;
                        var econ = infoFMC.Instance.Fmc.Des.EconSpeed;
                        _econSpeed_Mach.SetAsTall(string.IsNullOrEmpty(econ) ? "??/??" : econ);
                        ClearScratchPad();
                        break;
                    }

                    if (!_scratchPadInterpreter.IsValid) break;

                    Session.IsMod = true;
                    _econSpeed_Mach.SetAsModified(_scratchPadBuffer);
                    ClearScratchPad();
                    break;
                }
        }
    }

    public override void OnExecPress()
    {
        Session.IsMod = false;
        _econSpeed_Mach.SetAsTall(_econSpeed_Mach.GetText());
        //_econSpeed_Mach.text = _scratchPadInterpreter.AltRegulation;
    }


    public override void OnRightCornerPress()
    {
        /*if (Session.State.MapMode == MapMode.Plan)
        {
            _nodesController.DoPlanModeStep(false);
        }
        else
        {
            if (!Session.IsMod)
            {
                return;
            }

            if (_lastSelectionClicked.GetRouteNode() is { IsModified: true } &&
                _scratchPadInterpreter.IsLinearApproach(out var angle))
            {
                Debug.Log("=linear approach= on " + _lastSelectionClicked.GetRouteNode().Name + " with: " + angle);
                _simulation.ExecuteLinearApproachOnMod(new ExecuteAddLinearApproachCommand { ToNodeId = _lastSelectionClicked.LinkedId, Angle = angle });
                Debug.Log("OnRightCornerPress");
            }

        }*/
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
        MainScreen.Instance.scratchPadText.SetAsDefault(buffer);
        if (withStatus)
        {
            //lastHLeft.text = "ECON";
        }
    }

    private void ClearScratchPad()
    {
        _scratchPadBuffer = "";
        MainScreen.Instance.scratchPadText.Clear();
    }
}
