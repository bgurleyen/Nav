using Navigation;
using Navigation.Data;
using TMPro;
using UnityEngine;

public class CRZScreen : ScreenBase
{
    //[SerializeField] private TMP_Text _crzAltitude;
    [SerializeField] private BgText _crzAltitude;
    //[SerializeField] private TMP_Text _crzSpeed;
    [SerializeField] private BgText _crzSpeed;
    [SerializeField] private TMP_Text _actualWind;
    [SerializeField] private TMP_Text _destination;
    [SerializeField] private TMP_Text _fuelAtDestination;

    private const string ERASE_TITLE = "<ERASE";
    private ScratchPadInterpreter _scratchPadInterpreter;
    private string _scratchPadBuffer = "";

    private int? crzAltitude;

    public override void Show()
    {
        base.Show();

        //InvokeRepeating(nameof(Refresh), 0, 1f);
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
           pageTitle: "CRZ",
           currentPage: 0, totalPages: 1);


        var initRef = infoFMC.Instance.Fmc.Initref;
        var crz = infoFMC.Instance.Fmc.Crz;

        //_crzAltitude.SetAsDefault(crz.Altitude);
        if (Session.IsMod)
        {
            _crzAltitude.SetAsModified(crzAltitude == null
                ? crz.Altitude
                : Calculator.FormatFmcAltitudeFromEntry(crzAltitude) ?? crz.Altitude);
        }
        else
        {
            _crzAltitude.SetAsDefault(crzAltitude == null
                ? crz.Altitude
                : Calculator.FormatFmcAltitudeFromEntry(crzAltitude) ?? crz.Altitude);
        }
        if (!Session.IsMod)
        {
            _crzSpeed.SetAsDefault(crz.Speed);
        }
        _actualWind.text = crz.ActualWind;
        _destination.text = initRef.Destination;
        _fuelAtDestination.text = crz.FuelAtDestination;
    }

    public override void OnLineSelectLeft(int index)
    {
        InterpretScratchpadOnTextChanged();

        if (ScratchPadInterpreter.IsDeletePending(_scratchPadBuffer))
        {
            _scratchPadBuffer = string.Empty;
            crzAltitude = null;
            ClearScratchPad();
        }

        switch (index)
        {
            case 0:
                if (ScratchPadInterpreter.IsDeletePending(_scratchPadBuffer))
                {
                    _scratchPadBuffer = string.Empty;
                    crzAltitude = null;
                    ClearScratchPad();
                    break;
                }

                if (!_scratchPadInterpreter.IsValid) break;

                var pendingAlt = Calculator.FormatFmcAltitudeFromEntry(_scratchPadInterpreter.AltRegulation);
                if (pendingAlt == null) break;

                _crzAltitude.SetAsModified(pendingAlt);
                crzAltitude = _scratchPadInterpreter.AltRegulation;
                Session.IsMod = true;
                ClearScratchPad();
                break;
            case 1:
                if (ScratchPadInterpreter.IsDeletePending(_scratchPadBuffer))
                {
                    _scratchPadBuffer = string.Empty;
                    _crzSpeed.SetAsDefault(infoFMC.Instance.Fmc.Crz.Speed);
                    ClearScratchPad();
                    break;
                }

                if (!_scratchPadInterpreter.IsValid) break;

                if (_scratchPadInterpreter.SpeedRegulation != null)
                {
                    _crzSpeed.SetAsModified(
                        Calculator.FormatFmcSpeedDisplay(
                            _scratchPadInterpreter.SpeedRegulation.Value,
                            Session.CurrentLevel.levelInfo.CrzAltitude,
                            machDigits: 3));
                }
                else
                {
                    _crzSpeed.SetAsModified(_scratchPadBuffer);
                }
                Session.IsMod = true;
                ClearScratchPad();
                break;
                //case 1:
                //    _crzSpeed.SetAsModified(_scratchPadBuffer);
                //    ClearScratchPad();
                //    break;
        }
    }

    public override void OnExecPress()
    {
        if (crzAltitude != null)
        {
            var feet = Calculator.NormalizeAltitudeEntryToFeet(crzAltitude.Value);
            if (feet > 0)
            {
                Calculator.Instance?.ApplyFmcCruiseAltitude(feet);
                crzAltitude = null;
            }
        }

        Session.IsMod = false;
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
        public int? AltRegulation;
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
        /*public bool IsAltitudeRegulation(out string regulation)
        {
            regulation = "";
            if (!IsValid || AltRegulation == null)
            {
                return false;
            }

            regulation = AltRegulation;
            return true;
        }*/

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

        // Legs-compatible: /alt, speed/, speed/alt
        if (_scratchPadBuffer.Length < 1)
        {
            _scratchPadInterpreter.IsValid = false;
        }
        else if (_scratchPadBuffer[0] == '/' && _scratchPadBuffer.Length > 1)
        {
            var value = _scratchPadBuffer.Substring(1, _scratchPadBuffer.Length - 1);

            if (int.TryParse(value, out var regulation) &&
                Calculator.NormalizeAltitudeEntryToFeet(regulation) > 0)
            {
                _scratchPadInterpreter.AltRegulation = regulation;
                _scratchPadInterpreter.SpeedRegulation = null;
            }
            else
            {
                _scratchPadInterpreter.IsValid = false;
            }
        }
        else if (_scratchPadBuffer[_scratchPadBuffer.Length - 1] == '/' && _scratchPadBuffer.Length > 1)
        {
            var value = _scratchPadBuffer.Substring(0, _scratchPadBuffer.Length - 1);
            if (int.TryParse(value, out var regulation))
            {
                _scratchPadInterpreter.AltRegulation = null;
                _scratchPadInterpreter.SpeedRegulation = regulation;
            }
        }
        else if (_scratchPadBuffer.Contains('/') && _scratchPadBuffer.Length > 3)
        {
            var slashIndex = _scratchPadBuffer.IndexOf('/');
            var speed = _scratchPadBuffer.Substring(0, slashIndex);
            var alt = _scratchPadBuffer.Substring(slashIndex + 1, _scratchPadBuffer.Length - (slashIndex + 1));

            if (int.TryParse(speed, out var speedRegulation) &&
                int.TryParse(alt, out var altRegulation) &&
                Calculator.NormalizeAltitudeEntryToFeet(altRegulation) > 0)
            {
                _scratchPadInterpreter.AltRegulation = altRegulation;
                _scratchPadInterpreter.SpeedRegulation = speedRegulation;
            }
            else
            {
                _scratchPadInterpreter.IsValid = false;
            }
        }
        else if (int.TryParse(_scratchPadBuffer, out var altRegulation))
        {
            if (Calculator.NormalizeAltitudeEntryToFeet(altRegulation) > 0)
                _scratchPadInterpreter.AltRegulation = altRegulation;
            else
                _scratchPadInterpreter.IsValid = false;
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
