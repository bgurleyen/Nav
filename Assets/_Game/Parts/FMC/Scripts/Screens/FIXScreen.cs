using Navigation;
using Navigation.Data;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class FIXScreen : ScreenBase
{
    [SerializeField] private TMP_Text _pointName;

    [SerializeField] private TMP_Text _rad;
    [SerializeField] private TMP_Text _eta;
    [SerializeField] private TMP_Text _dtg;
    [SerializeField] private TMP_Text _alt;
    [SerializeField] private TMP_Text _radSecond;
    [SerializeField] private TMP_Text _etaSecond;
    [SerializeField] private TMP_Text _dtgSecond;
    [SerializeField] private TMP_Text _altSecond;
    [SerializeField] private TMP_Text _radThird;
    [SerializeField] private TMP_Text _etaThird;
    [SerializeField] private TMP_Text _dtgThird;
    [SerializeField] private TMP_Text _altThird;

    private NodeSelection _selectionInfo;
    private NodeSelection _lastSelectionClicked;

    private RoutePoint selectedRouteNode;

    private ScratchPadInterpreter _scratchPadInterpreter;
    private string _scratchPadBuffer = "";

    private bool isFixSelect;
    private bool cir1;
    private bool cir2;
    private bool cir3;

    public override void Show()
    {
        base.Show();

        Main.UpdatePageInfo(secondInfo: "FIX INFO", currentPage: 0, totalPages: 1);

        _scratchPadBuffer = MainScreen.Instance.scratchPadText.GetText();

        InvokeRepeating(nameof(Refresh), 0, 1f);
    }

    public override void Hide()
    {
        base.Hide();

        CancelInvoke(nameof(Refresh));
    }

    public override void OnLineSelectLeft(int index)
    {
        base.OnLineSelectLeft(index);

        InterpretScratchpadOnTextChanged(true);

        if (_scratchPadInterpreter.IsValid)
        {
            switch (index)
            {
                case 0:
                    _pointName.text = _scratchPadBuffer;

                    _scratchPadInterpreter.Node = Session.VisibleRoute.Points.FirstOrDefault(x => x.Name == _scratchPadBuffer);
                    selectedRouteNode = _scratchPadInterpreter.Node;

                    Session.Routes.FixedPoints.AddOrUpdateFixedPointEntry(_scratchPadInterpreter.Node.Name, null);
                    InitData();
                    ClearScratchPad();
                    break;

                case 1:
                    Session.Routes.FixedPoints.AddOrUpdateFixedPointEntry(selectedRouteNode.Name, new FixedPointInfo() { NM = _scratchPadInterpreter.NMRegulation, RawDegrees = _scratchPadInterpreter.DegreesRegulation });
                    UpdateData(ref _rad, _scratchPadBuffer);
                    ClearScratchPad();
                    break;

                case 2:
                    Session.Routes.FixedPoints.AddOrUpdateFixedPointEntry(selectedRouteNode.Name, new FixedPointInfo() { NM = _scratchPadInterpreter.NMRegulation, RawDegrees = _scratchPadInterpreter.DegreesRegulation });
                    UpdateData(ref _radSecond, _scratchPadBuffer);
                    ClearScratchPad();
                    break;

                case 3:
                    Session.Routes.FixedPoints.AddOrUpdateFixedPointEntry(selectedRouteNode.Name, new FixedPointInfo() { NM = _scratchPadInterpreter.NMRegulation, RawDegrees = _scratchPadInterpreter.DegreesRegulation });
                    UpdateData(ref _radThird, _scratchPadBuffer);
                    ClearScratchPad();
                    break;
            }

            Session.Routes.ActiveRoute.ComputeTrace(true);
        }
    }

    //public override void OnLineSelectRight(int index)
    //{
    //    base.OnLineSelectRight(index);

    //    switch (index)
    //    {
    //        case 0:
    //            break;
    //    }
    //}

    private void Refresh()
    {
        //var initRef = infoFMC.Instance.Fmc.Initref;

        //_gwt.text = initRef.GWT;
        //_destinationRw.text = initRef.Destination + initRef.RW;
        //_field.text = initRef.Field;
        //_rw.text = $"ILS {initRef.RW}/CRS";

        //_freq.text = $"{initRef.Freq}/{initRef.Course}";

        //_f15.text = $"{initRef.F15}KT";
        //_f30.text = $"{initRef.F30}KT";
        //_f40.text = $"{initRef.F40}KT";

        //_vRef.text = $"30/{initRef.Vref}";
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

        InterpretScratchpadOnTextChanged(true);

        UpdateScratchPad(_scratchPadBuffer, _selectionInfo != null);
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

        UpdateScratchPad(_scratchPadBuffer);
    }

    private struct ScratchPadInterpreter
    {
        public bool IsValid;
        public RoutePoint Node;
        public int? DegreesRegulation;
        public int? NMRegulation;

        public static bool IsDeletePending(string buffer)
        {
            return buffer == MainScreen.Keywords.DELETE;
        }
    }

    private void InterpretScratchpadOnTextChanged(bool handleSelection)
    {

        _scratchPadInterpreter = new ScratchPadInterpreter
        {
            IsValid = true,
            Node = null,
            DegreesRegulation = null,
            NMRegulation = null
        };

        if (selectedRouteNode == null)
        {
            if (_scratchPadBuffer.Contains('/'))
            {
                var indexOfSlash = _scratchPadBuffer.IndexOf('/');
                var allLeft = _scratchPadBuffer[..indexOfSlash];
                // ABC/-11 or ABC060/-11
                _scratchPadInterpreter.Node = Session.VisibleRoute.Points.FirstOrDefault(x => x.Name == allLeft);
                //if (_scratchPadInterpreter.Node == null)
                //{
                //    // ABC060/-11    ~~   Can be relative with angle and direction
                //    if (int.TryParse(allLeft.Substring(allLeft.Length - 3, 3), out var angle))
                //    {
                //        _scratchPadInterpreter.Angle = angle;
                //        var withoutAngle = allLeft[..^3];
                //        _scratchPadInterpreter.Node = Session.VisibleRoute.Points.FirstOrDefault(x => x.Name == withoutAngle);
                //    }
                //    else
                //    {
                //        _scratchPadInterpreter.IsValid = false;
                //    }
                //}

                // make sure the distance is ok 
                //if (_scratchPadInterpreter.Node != null)
                //{
                //    var allRight =
                //        _scratchPadBuffer.Substring(indexOfSlash + 1, _scratchPadBuffer.Length - (indexOfSlash + 1));
                //    if (int.TryParse(allRight, out var distance))
                //    {
                //        _scratchPadInterpreter.Distance = distance;
                //    }
                //    else
                //    {
                //        _scratchPadInterpreter.IsValid = false;
                //    }
                //}
            }
            else
            {
                if (int.TryParse(_scratchPadBuffer, out var number))
                {
                    if (_scratchPadBuffer.Length == 3)
                    {
                        // Is linear approach
                        //_scratchPadInterpreter.Angle = number;
                    }


                }
                else
                {
                    _scratchPadInterpreter.Node = Session.VisibleRoute.Points.FirstOrDefault(x => x.Name == _scratchPadBuffer);

                    if (_scratchPadInterpreter.Node != null)
                    {
                        Debug.Log(_scratchPadInterpreter.Node.Name);
                    }
                    if (_scratchPadInterpreter.Node == null)
                    {
                        _scratchPadInterpreter.IsValid = false;
                    }
                }
            }

            if (_scratchPadInterpreter.IsValid)
            {
                if (handleSelection && _scratchPadInterpreter.Node != null)
                {
                    Debug.Log("_scratchPadIntrerpreter.IsValid: " + _scratchPadInterpreter.IsValid);
                    _selectionInfo = new NodeSelection
                    {
                        IsEmpty = false,
                        LinkedId = _scratchPadInterpreter.Node.ID,
                        IsAddedDiscontinuity = false,
                        IsStartingPoint = false
                    };

                    _selectionInfo.GetRouteNode().IsSelected = true;
                }
            }
            else
            {
                Debug.LogWarning("Invalid Scratchpad Entry!");
            }
        }

        else
        {
            if (_scratchPadBuffer.Length <= 0) return;
            // look for regulations
            //if (_scratchPadBuffer[0] == '/' && _scratchPadBuffer.Length > 1)
            //{
            //    // should be altitude only regulation
            //    var value = _scratchPadBuffer.Substring(1, _scratchPadBuffer.Length - 1);

            //    if (DataHandler.ParseAltRegulation(value, out _, out _, out _))
            //    {
            //        _scratchPadInterpreter.AltRegulation = value;
            //        _scratchPadInterpreter.SpeedRegulation = null;
            //    }
            //    else
            //    {
            //        _scratchPadInterpreter.IsValid = false;
            //    }
            //}
            if (_scratchPadBuffer[0] == '/' && _scratchPadBuffer.Length > 1)
            {
                // should be nm only regulation
                var value = _scratchPadBuffer.Substring(1, _scratchPadBuffer.Length - 1);

                if (int.TryParse(value, out var regulation))
                {
                    _scratchPadInterpreter.NMRegulation = regulation;
                    _scratchPadInterpreter.DegreesRegulation = null;
                }
                else
                {
                    _scratchPadInterpreter.IsValid = false;
                }

                Debug.Log(value);
            }
            else if (_scratchPadBuffer[_scratchPadBuffer.Length - 1] == '/' && _scratchPadBuffer.Length > 1)
            {
                // should be degree only regulation
                var value = _scratchPadBuffer.Substring(0, _scratchPadBuffer.Length - 1);
                if (int.TryParse(value, out var regulation))
                {
                    _scratchPadInterpreter.DegreesRegulation = regulation;
                    _scratchPadInterpreter.NMRegulation = null;
                }
                else
                {
                    _scratchPadInterpreter.IsValid = false;
                }

                Debug.Log(value);
            }
            else if (_scratchPadBuffer.Contains('/') && _scratchPadBuffer.Length > 3)
            {
                // may be speed & alt regulation
                var slashIndex = _scratchPadBuffer.IndexOf('/');
                var degree = _scratchPadBuffer.Substring(0, slashIndex);
                var nm = _scratchPadBuffer.Substring(slashIndex + 1, _scratchPadBuffer.Length - (slashIndex + 1));

                if (int.TryParse(nm, out var nmRegulation) && int.TryParse(degree, out var degreeRegulation))
                {
                    _scratchPadInterpreter.DegreesRegulation = degreeRegulation;
                    _scratchPadInterpreter.NMRegulation = nmRegulation;
                }
                else
                {
                    _scratchPadInterpreter.IsValid = false;
                }
            }
            else if (int.TryParse(_scratchPadBuffer, out _))
            {
                // can be altitude regulation
                if (int.TryParse(_scratchPadBuffer, out var degreeRegulation))
                    _scratchPadInterpreter.DegreesRegulation = degreeRegulation;
            }
        }
    }

    public void UpdateScratchPad(string buffer, bool withStatus = true)
    {
        MainScreen.Instance.scratchPadText.SetAsDefault(buffer);
        if (withStatus)
        {
            //lastFLeft.text = "ok";
        }
    }

    private void InitData()
    {
        _rad.text = "---";
        _radSecond.text = "---";
        _radThird.text = "---";
    }

    private void UpdateData(ref TMP_Text _rad, string radDis)
    {
        _rad.text = radDis;
    }

    private void ClearScratchPad()
    {
        //Debug.Log("----- Clear Current Operation -----");
        _scratchPadBuffer = "";
        MainScreen.Instance.scratchPadText.label.text = "";
    }
}
