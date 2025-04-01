using Navigation;
using Navigation.Data;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
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


    private const string ERASE_TITLE = "<ERASE";
    private ScratchPadInterpreter _scratchPadInterpreter;
    private string _scratchPadBuffer = "";

    private Simulation _simulation;

    private void Start()
    {
        _econSpeed_Mach.text = $"??/??";

        lastHRight.text = "";
        lastHLeft.text = "";
    }

    public override void Show()
    {
        base.Show();


        //Main.UpdatePageInfo(
        //   isMod: Session.IsMod,
        //   secondInfo: "ECON",
        //   pageTitle: "DES",
        //   currentPage: 0, totalPages: 1);


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
        //_econSpeed_Mach.text = $"??/??";

        _wptAltFix.text = des.WptAltFix;

        _arrTansition.text = fmc.Arr.Transition;
        _fpa.text = des.FPA;
        _vb.text = des.VB;
        _vs.text = des.VS;
    }

    public override void OnLineSelectLeft(int index)
    {
        if (_scratchPadInterpreter.IsValid)
        {
            switch (index)
            {
                case 1:
                    Session.IsMod = true;
                    break;
            }
        }
    }

    public override void OnExecPress()
    {
        //OnExecButtonPress?.Invoke();
        //ClearCurrentOperation();
        //ClearSelectionHistory();

        Session.IsMod = false;
        _econSpeed_Mach.text = _scratchPadInterpreter.AltRegulation;
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
        if (_scratchPadBuffer[0] == '/' && _scratchPadBuffer.Length > 1)
        {
            // should be altitude only regulation
            var value = _scratchPadBuffer.Substring(1, _scratchPadBuffer.Length - 1);

            if (DataHandler.ParseAltRegulation(value, out _, out _, out _))
            {
                _scratchPadInterpreter.AltRegulation = value;
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
                _scratchPadInterpreter.AltRegulation = altRegulation;
                _scratchPadInterpreter.SpeedRegulation = speedRegulation;
            }
            else
            {
                _scratchPadInterpreter.IsValid = false;
            }
        }
        else if (int.TryParse(_scratchPadBuffer, out _))
        {
            // can be altitude regulation
            _scratchPadInterpreter.AltRegulation = _scratchPadBuffer;
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
            lastHLeft.text = "ECON";
        }
    }
}
