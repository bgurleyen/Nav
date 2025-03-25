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

    [SerializeField] private TMP_Text[] _rads;

    private NodeSelection _selectionInfo;
    private NodeSelection _lastSelectionClicked;

    private RoutePoint selectedRouteNode;

    private int TotalPages => 3;

    private ScratchPadInterpreter _scratchPadInterpreter;
    private string _scratchPadBuffer = "";
    [SerializeField]
    private int _currentPage;

    public override void Show()
    {
        base.Show();

        Main.UpdatePageInfo(secondInfo: "FIX INFO", currentPage: 0, totalPages: 1);

        _scratchPadBuffer = MainScreen.Instance.scratchPadText.GetText();

        InvokeRepeating(nameof(DisplayCurrentPage), 0, 0.2f);
    }

    public override void Hide()
    {
        base.Hide();

        CancelInvoke(nameof(DisplayCurrentPage));
    }

    public override void DisplayNextPage()
    {
        _currentPage = Mathf.Min(TotalPages - 1, _currentPage + 1);
    }

    public override void DisplayPrevPage()
    {
        _currentPage = Mathf.Max(0, _currentPage - 1);
    }

    public override void OnLineSelectLeft(int index)
    {
        base.OnLineSelectLeft(index);

        InterpretScratchpadOnTextChanged();

        if (_scratchPadInterpreter.IsValid)
        {
            switch (index)
            {
                case 0:
                    {
                        Session.Routes.FixedPoints.AddOrUpdateFixedPoint(
                            new FixedPointEntry()
                            {
                                Name = _scratchPadInterpreter.Node.Name,
                                Infos = new FixedPointInfo[] { }
                            }, _currentPage);
                    }
                    break;
                case 1:
                    {
                        Session.Routes.FixedPoints.AddOrUpdateFixedPointInfo(
                            new FixedPointInfo()
                            {
                                NM = _scratchPadInterpreter.NMRegulation,
                                RawDegrees = _scratchPadInterpreter.DegreesRegulation
                            }, index - 1, _currentPage);
                    }
                    break;
                case 2:
                    {
                        Session.Routes.FixedPoints.AddOrUpdateFixedPointInfo(
                            new FixedPointInfo()
                            {
                                NM = _scratchPadInterpreter.NMRegulation,
                                RawDegrees = _scratchPadInterpreter.DegreesRegulation
                            }, index - 1, _currentPage);
                    }
                    break;
                case 3:
                    {
                        Session.Routes.FixedPoints.AddOrUpdateFixedPointInfo(
                            new FixedPointInfo()
                            {
                                NM = _scratchPadInterpreter.NMRegulation,
                                RawDegrees = _scratchPadInterpreter.DegreesRegulation
                            }, index - 1, _currentPage);
                    }
                    break;
            }
        }

        ClearScratchPad();
        Session.Routes.ActiveRoute.ComputeTrace(true);
    }

    public void DisplayCurrentPage()
    {
        Main.UpdatePageInfo(
            isMod: false,
            firstInfo: "",
            pageTitle: "",
            secondInfo: "",
            _currentPage,
            TotalPages);


        var fixPoints = Session.Routes.FixedPoints;
        var wayPoint = fixPoints.GetWaypointAt(_currentPage);

        if (wayPoint == null)
        {
            _pointName.text = "□□□□□";
            foreach (var rad in _rads)
            {
                rad.text = "";
            }
        }
        else
        {
            _pointName.text = wayPoint.Name;

            for (int i = 0; i < 3; i++)
            {
                if (wayPoint.Infos.Length > i)
                {
                    _rads[i].text = $"{wayPoint.Infos[i].RawDegrees?.ToString() ?? "---"}/{wayPoint.Infos[i].NM?.ToString() ?? "---"}";
                }
                else
                {
                    _rads[i].text = "---";
                }
            }
        }
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

    private void InterpretScratchpadOnTextChanged()
    {
        if (string.IsNullOrEmpty(_scratchPadBuffer))
            return;

        _scratchPadInterpreter = new ScratchPadInterpreter
        {
            IsValid = true,
            Node = null,
            DegreesRegulation = null,
            NMRegulation = null
        };

        _scratchPadInterpreter.Node = Session.ActiveRoute.Points.FirstOrDefault(x => x.Name == _scratchPadBuffer);

        if (_scratchPadInterpreter.Node != null)
        {
            if (_scratchPadInterpreter.Node != null)
            {
                Debug.Log(_scratchPadInterpreter.Node.Name);
            }
            if (_scratchPadInterpreter.Node == null)
            {
                _scratchPadInterpreter.IsValid = false;
            }

            if (_scratchPadInterpreter.IsValid)
            {
                if (_scratchPadInterpreter.Node != null)
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
            else if (int.TryParse(_scratchPadBuffer, out var degreeRegulation))
            {
                    _scratchPadInterpreter.DegreesRegulation = degreeRegulation;
            }
            else
            {
                _scratchPadInterpreter.IsValid = false;
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
