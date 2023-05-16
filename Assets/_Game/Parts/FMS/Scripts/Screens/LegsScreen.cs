using System;
using System.Linq;
using Legacy;
using Navigation;
using UnityEngine;
using Unyawn.Utils;

public class LegsScreen : ScreenBase
{
    [SerializeField] private LegsNodeLine[] nodes;
    
    public Action OnLeftCornerPressErase;
    
    private static MainScreen Main => MainScreen.Instance;

    private int NodesPerPage => nodes.Length;

    private RoutePoint GetSelectedPoint => _selectionInfo == null ? null : 
        Session.VisibleRoute.GetPoint(_selectionInfo.LinkedId, out var node) ? node : null;

    private RoutePoint LastSelectedPoint =>
        _lastSelectionClicked == null ? null :
        Session.VisibleRoute.GetPoint(_lastSelectionClicked.LinkedId, out var node) ? node : null;
    
    private int TotalPages =>
        Mathf.CeilToInt((Session.VisibleRoute.Points.Length - PositionVirtualNode.NextNodeIndex + _nodesController.TotalPagesCorrection) /
                        (float) NodesPerPage);
    
    private static bool IsErase => Main.LastLineLeft == ERASE_TITLE;

    private const string ERASE_TITLE = "<ERASE";
    
    private DisplayNodesController _nodesController;
    private NodeSelection _selectionInfo;
    private NodeSelection _lastSelectionClicked;

    private ScratchPadInterpreter _scratchPadInterpreter;
    private string _scratchPadBuffer = "";
    private int _currentPage;
    private Simulation _simulation;

    private void Awake()
    {
        UYServiceLocator.Register(this);
    }

    private void Start()
    {
        _nodesController = new DisplayNodesController(NodesPerPage);

        _simulation = UYServiceLocator.Get<Simulation>();
        
        _simulation.OnOperationMade += _nodesController.ComputeCorrections;
    }
    

    private void OnDestroy()
    {
        
        UYServiceLocator.Unregister<LegsScreen>();

        _simulation.OnOperationMade -= _nodesController.ComputeCorrections;
    }

    public override void Show()
    {
        base.Show();
        InvokeRepeating(nameof(DisplayCurrentPage), 0, 0.1f);
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

    public void DisplayCurrentPage()
    {
        Main.UpdatePageInfo(_currentPage, TotalPages, Session.IsMod, "LEGS");

        // Debug.Log("start");
        for (var i = 0; i < nodes.Length; i++)
        {
            var linkedSelection = _nodesController.GetNodeInfoAtLineIndex(i, _currentPage);

            if (linkedSelection.IsInvalid || linkedSelection.IsEmpty)
            {
                nodes[i].ShowEmpty();
            }
            else
            {
                Session.VisibleRoute.GetPoint(linkedSelection.LinkedId, out var _node);
                nodes[i].DisplayNodeDetails(_node, linkedSelection);
            }
        }
    }

    public override void OnLineSelectRight(int index)
    {
        var clickedInfo = _nodesController.GetNodeInfoAtLineIndex(index, _currentPage);

        if (ScratchPadInterpreter.IsDeletePending(_scratchPadBuffer))
        {
            _simulation.ExecuteDeleteRestrictions(new DeleteRestrictionsCommand
            {
                NodeId = clickedInfo.LinkedId
            });
        }

        if (_scratchPadInterpreter.IsAltitudeRegulation(out var _regulation))
        {
            _simulation.ExecuteAddAltitudeRegulation(new AddAltitudeRegulationCommand
            {
                NodeId = clickedInfo.LinkedId,
                Regulation = _regulation
            });
        }

        if (_scratchPadInterpreter.IsSpeedRegulation(out var _speedRegulation))
        {
            _simulation.ExecuteAddSpeedRegulation(new AddSpeedRegulationCommand
            {
                NodeId = clickedInfo.LinkedId,
                Regulation = _speedRegulation
            });
        }
    }

    public override void OnLineSelectLeft(int index)
    {
        var clickedInfo = _nodesController.GetNodeInfoAtLineIndex(index, _currentPage);
        _lastSelectionClicked = clickedInfo;
        // user clicks, none is previously selected
        if (GetSelectedPoint == null)
        {
            Debug.Log("=selection text=");
            _selectionInfo = clickedInfo;
            _scratchPadBuffer = GetSelectedPoint.Name;
            GetSelectedPoint.IsSelected = true;
            Main.UpdateScratchPad(_scratchPadBuffer);
            InterpretScratchpadOnTextChanged(false);
            return;
        }

        GetSelectedPoint.IsSelected = false;

        if (!string.IsNullOrEmpty(_scratchPadBuffer) &&
            _scratchPadInterpreter.IsRelativeNodeOnDirection(out var distanceOnDirection, out var _relativeNodeId))
        {
            _simulation.ExecuteInsertRelativeOnDirectionOnMod(new ExecuteRelativeOnDirectionOnMod
                {FromNodeId = clickedInfo.LinkedId, Distance = distanceOnDirection, RelativeNodeId = _relativeNodeId});
        }
        else if (!string.IsNullOrEmpty(_scratchPadBuffer) &&
                 _scratchPadInterpreter.IsRelativeNode(out var angle, out var distance, out _relativeNodeId))
        {
            // if this is a relative insert command
            _simulation.ExecuteInsertRelativeOnMod(new InsertRelativeCommand
                {BeforeNodeId = clickedInfo.LinkedId, RawDegrees = angle, Distance = distance, RelativeNodeId = _relativeNodeId});
        }
        else
        {
            var selectedIndex = Session.VisibleRoute.Points.GetNodeIndex(_selectionInfo.LinkedId);
            var clickedIndex = Session.VisibleRoute.Points.GetNodeIndex(clickedInfo.LinkedId);

            // when user clicks on the node below
            if (selectedIndex < clickedIndex)
            {
                Debug.Log("error");
                ClearCurrentOperation();
                return;
            }

            // if this is a shortcut command
            Debug.Log("=shortcut=");
            _simulation.ExecuteShortcutOnMod(new ExecuteShortcutOnModeCommand
                {FromNodeId = clickedInfo.LinkedId, ToNodeId = _selectionInfo.LinkedId});
        }

        ClearCurrentOperation();
    }

    public override void OnExecPress()
    {
        if (Session.IsMod)
        {
            GameManager.Instance.ApplyMod();
        }

        ClearCurrentOperation();
        ClearSelectionHistory();
    }


    public override void OnRightCornerPress()
    {
        if (!Session.IsMod)
        {
            return;
        }

        if (LastSelectedPoint is { IsModified: true } &&
            _scratchPadInterpreter.IsLinearApproach(out var angle))
        {
            Debug.Log("=linear approach= on " + LastSelectedPoint.Name + " with: " + angle);
            _simulation.ExecuteLinearApproachOnMod(new ExecuteAddLinearApproachCommand
                {ToNodeId = _lastSelectionClicked.LinkedId, Angle = angle});
        }
    }

    public override void OnLeftCornerPress()
    {
        if (IsErase)
        {
            OnLeftCornerPressErase?.Invoke();
            ClearCurrentOperation();
        }
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

        Main.UpdateScratchPad(_scratchPadBuffer);
    }

    public override void OnDeletePress()
    {
        _scratchPadBuffer = MainScreen.Keywords.DELETE;
        Main.UpdateScratchPad(_scratchPadBuffer);
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

        Main.UpdateScratchPad(_scratchPadBuffer, _selectionInfo != null);
    }

    private struct ScratchPadInterpreter
    {
        public bool IsValid;
        public RoutePoint Node;
        public int? Angle;
        public int? Distance;
        public string AltRegulation;
        public int? SpeedRegulation;

        public bool IsSpeedRegulation(out int regulation)
        {
            regulation = -1;
            if (!IsValid || Node != null || Angle != null || Distance != null
                || SpeedRegulation == null)
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
            if (!IsValid || AltRegulation != null || SpeedRegulation != null
                         || Node == null || Angle == null || Distance == null)
            {
                return false;
            }

            Debug.Log("=relative insert=");
            distance = Distance.Value;
            angle = Angle.Value;
            relativeNodeId = Node.ID;
            return true;
        }

        public bool IsRelativeNodeOnDirection(out int distance, out int relativeNodeId)
        {
            distance = 0;
            relativeNodeId = 0;
            if (!IsValid || Angle != null || AltRegulation != null || SpeedRegulation != null
                || Node == null || Distance == null)
            {
                return false;
            }

            Debug.Log("=relative insert on direction=");
            distance = Distance.Value;
            relativeNodeId = Node.ID;
            return true;
        }

        public bool IsLinearApproach(out int angle)
        {
            angle = 0;
            if (!IsValid || Node != null || Distance != null || AltRegulation != null || SpeedRegulation != null
                || Angle == null)
            {
                return false;
            }

            angle = Angle.Value;
            return true;
        }


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
            Angle = null,
            Distance = null,
            AltRegulation = null,
            SpeedRegulation = null,
            Node = null
        };

        if (GetSelectedPoint != null || _lastSelectionClicked != null)
        {
            if (handleSelection && GetSelectedPoint != null)
            {
                GetSelectedPoint.IsSelected = false;
            }

            if (_scratchPadBuffer.Contains('/'))
            {
                var indexOfSlash = _scratchPadBuffer.IndexOf('/');
                var allLeft = _scratchPadBuffer[..indexOfSlash];
                // ABC/-11 or ABC060/-11
                _scratchPadInterpreter.Node = Session.VisibleRoute.Points.FirstOrDefault(x => x.Name == allLeft);
                if (_scratchPadInterpreter.Node == null)
                {
                    // ABC060/-11    ~~   Can be relative with angle and direction
                    if (int.TryParse(allLeft.Substring(allLeft.Length - 3, 3), out var angle))
                    {
                        _scratchPadInterpreter.Angle = angle;
                        var withoutAngle = allLeft[..^3];
                        _scratchPadInterpreter.Node = Session.VisibleRoute.Points.FirstOrDefault(x => x.Name == withoutAngle);
                    }
                    else
                    {
                        _scratchPadInterpreter.IsValid = false;
                    }
                }

                // make sure the distance is ok 
                if (_scratchPadInterpreter.Node != null)
                {
                    var allRight =
                        _scratchPadBuffer.Substring(indexOfSlash + 1, _scratchPadBuffer.Length - (indexOfSlash + 1));
                    if (int.TryParse(allRight, out var distance))
                    {
                        _scratchPadInterpreter.Distance = distance;
                    }
                    else
                    {
                        _scratchPadInterpreter.IsValid = false;
                    }
                }
            }
            else
            {
                if (int.TryParse(_scratchPadBuffer, out var number))
                {
                    if (_scratchPadBuffer.Length == 3)
                    {
                        // Is linear approach
                        _scratchPadInterpreter.Angle = number;
                    }

                   
                }
                else
                {
                    _scratchPadInterpreter.Node = Session.VisibleRoute.Points.FirstOrDefault(x => x.Name == _scratchPadBuffer);
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
                    _selectionInfo = new NodeSelection
                    {
                        IsEmpty = false,
                        LinkedId = _scratchPadInterpreter.Node.ID,
                        IsAddedDiscontinuity = false,
                        IsStartingPoint = false
                    };

                    GetSelectedPoint.IsSelected = true;
                }
            }
            else
            {
                Debug.LogWarning("Invalid Scratchpad Entry!");
            }
        }

        else
        {
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
        }
    }

    private void ClearCurrentOperation()
    {
        _scratchPadBuffer = "";
        _selectionInfo = null;
    }

    private void ClearSelectionHistory()
    {
        _lastSelectionClicked = null;
    }
}