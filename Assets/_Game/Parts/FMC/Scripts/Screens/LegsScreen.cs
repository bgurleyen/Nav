using System;
using System.Collections;
using System.Linq;
using Navigation;
using UnityEngine;
using Unyawn.Utils;

public class LegsScreen : ScreenBase
{
    [SerializeField] private LegsNodeLine[] nodes;
    
    public Action OnLeftCornerPressErase;
    public Action OnExecButtonPress;
    
    private int NodesPerPage => nodes.Length;

    private int TotalPages =>
        Mathf.CeilToInt((Session.VisibleRoute.Points.Length - PositionVirtualNode.NextNodeIndex + _nodesController.TotalPagesCorrection) /
                        (float) NodesPerPage);
    
    private bool IsErase => LastLineLeft == ERASE_TITLE;


    private const string ERASE_TITLE = "<ERASE";
    
    private DisplayNodesController _nodesController;
    private NodeSelection _selectionInfo;
    private NodeSelection _lastSelectionClicked;

    private ScratchPadInterpreter _scratchPadInterpreter;
    private string _scratchPadBuffer = "";
    private int _currentPage;
    private Simulation _simulation;

    protected override void Awake()
    {
        base.Awake();
        UYServiceLocator.Register(this);
    }

    private IEnumerator Start()
    {
        _nodesController = new DisplayNodesController(NodesPerPage);

        _simulation = UYServiceLocator.Get<Simulation>();
        
        _simulation.OnOperationMade += _nodesController.ComputeCorrections;
        
        yield return null;
        
        Session.State.OnMapModeChanged+= OnMapModeChanged;
    }



    private void OnDestroy()
    {
        
        UYServiceLocator.Unregister<LegsScreen>();

        _simulation.OnOperationMade -= _nodesController.ComputeCorrections;
    }

    public override void Show()
    {
        base.Show();
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

    public void DisplayCurrentPage()
    {
        Main.UpdatePageInfo(
            isMod: Session.IsMod,
            firstInfo: Session.IsMod ? "MOD" : "ACT",
            pageTitle: "LEGS",
            secondInfo: "RTE",
            _currentPage,
            TotalPages);

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

        if (_scratchPadInterpreter.IsAltitudeRegulation(out var regulation))
        {
            _simulation.ExecuteAddAltitudeRegulation(new AddAltitudeRegulationCommand
            {
                NodeId = clickedInfo.LinkedId,
                Regulation = regulation
            });
        }

        if (_scratchPadInterpreter.IsSpeedRegulation(out var speedRegulation))
        {
            _simulation.ExecuteAddSpeedRegulation(new AddSpeedRegulationCommand
            {
                NodeId = clickedInfo.LinkedId,
                Regulation = speedRegulation
            });
        }
    }

    public override void OnLineSelectLeft(int index)
    {
        var clickedInfo = _nodesController.GetNodeInfoAtLineIndex(index, _currentPage);
        var clickedNode = clickedInfo.GetRouteNode();
        if (clickedNode == null)
        {
            return;
        }
        _lastSelectionClicked = clickedInfo;
        // user clicks, none is previously selected
        if (NodeSelectionExtensions.GetRouteNode(_selectionInfo) == null)
        {
            // parse if user inputs a name of a point letter by letter
            if (IsFutureNodeName(_scratchPadBuffer, out var lineIndex, out var typedInfo))
            {
                Debug.Log("=written selection text=");
                _selectionInfo = typedInfo;
                _scratchPadBuffer = typedInfo.GetRouteNode().Name;
                typedInfo.GetRouteNode().IsSelected = true;
                UpdateScratchPad(_scratchPadBuffer);
                InterpretScratchpadOnTextChanged(false);
                // continue with second selection as this user press
            }
            else
            {
                Debug.Log("=selection text=");
                _selectionInfo = clickedInfo;
                _scratchPadBuffer = clickedNode.Name;
                clickedNode.IsSelected = true;
                UpdateScratchPad(_scratchPadBuffer);
                InterpretScratchpadOnTextChanged(false);
                return;
            }
        }

        _selectionInfo.GetRouteNode().IsSelected = false;

        if (!string.IsNullOrEmpty(_scratchPadBuffer) &&
            _scratchPadInterpreter.IsRelativeNodeOnDirection(out var distanceOnDirection, out var relativeNodeId))
        {
            _simulation.ExecuteInsertRelativeOnDirectionOnMod(new ExecuteRelativeOnDirectionOnMod
                { FromNodeId = clickedInfo.LinkedId, Distance = distanceOnDirection, RelativeNodeId = relativeNodeId });
        }
        else if (!string.IsNullOrEmpty(_scratchPadBuffer) &&
                 _scratchPadInterpreter.IsRelativeNode(out var angle, out var distance, out relativeNodeId))
        {
            // if this is a relative insert command
            _simulation.ExecuteInsertRelativeOnMod(new InsertRelativeCommand
            {
                BeforeNodeId = clickedInfo.LinkedId, RawDegrees = angle, Distance = distance,
                RelativeNodeId = relativeNodeId
            });
        }
        else
        {
            Session.VisibleRoute.Points.GetNodeIndex(_selectionInfo.LinkedId, out var selectedIndex);
            Session.VisibleRoute.Points.GetNodeIndex(clickedInfo.LinkedId, out var clickedIndex);

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
                { FromNodeId = clickedInfo.LinkedId, ToNodeId = _selectionInfo.LinkedId });
        }

        ClearCurrentOperation();
        
        // if a new node is on the selected position after the operation - update the selection info
        clickedInfo = _nodesController.GetNodeInfoAtLineIndex(index, _currentPage);
        _lastSelectionClicked = clickedInfo;
    }

    public override void OnExecPress()
    {
        OnExecButtonPress?.Invoke();

        ClearCurrentOperation();
        ClearSelectionHistory();
    }


    public override void OnRightCornerPress()
    {
        if (Session.State.MapMode == MapMode.Plan)
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
                _simulation.ExecuteLinearApproachOnMod(new ExecuteAddLinearApproachCommand
                    { ToNodeId = _lastSelectionClicked.LinkedId, Angle = angle });
            }
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

        UpdateScratchPad(_scratchPadBuffer);
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

        InterpretScratchpadOnTextChanged(true);

        UpdateScratchPad(_scratchPadBuffer, _selectionInfo != null);
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

        if (NodeSelectionExtensions.GetRouteNode(_selectionInfo) != null || _lastSelectionClicked != null)
        {
            if (handleSelection && NodeSelectionExtensions.GetRouteNode(_selectionInfo) != null)
            {
                _selectionInfo.GetRouteNode().IsSelected = false;
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

    private bool IsFutureNodeName(string text, out int lineIndex, out NodeSelection linkedSelection)
    {
        lineIndex = -1;
        linkedSelection = null;
        for (int page = _currentPage; page < TotalPages; page++)
        {
            for (var i = 0; i < nodes.Length; i++)
            {
                linkedSelection = _nodesController.GetNodeInfoAtLineIndex(i, page);

                if (!linkedSelection.IsInvalid && !linkedSelection.IsEmpty)
                {
                    Session.VisibleRoute.GetPoint(linkedSelection.LinkedId, out var node);
                    if (node.Name == text)
                    {
                        lineIndex = i;
                        return true;
                    }
                }
                else
                {
                    //nodes[i].ShowEmpty();
                }
            }
        }

        return false;
    }

    /// <summary>
    /// If the text represents a node in the original path route, which is not visible on screen anymore
    /// </summary>
    /// <param name="text"></param>
    /// <param name="lineIndex"></param>
    /// <param name="linkedSelection"></param>
    /// <returns></returns>
    private bool IsPassedOriginalNodeName(string text, out RoutePoint originalNodeInfo)
    {
        return Session.OriginalReferenceRoute.GetPointByName(text, out originalNodeInfo);
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

    private void OnMapModeChanged(MapMode obj)
    {
        lastFRight.SetAsDefault(obj == MapMode.Plan ? "STEP":"");

        if (obj == MapMode.Plan)
        {
            _nodesController.DoPlanModeStep(true);
        }
    }

    public void DisplayOperation(string value , string details = "", bool tallDetails= false)
    {
        lastFLeft.text = $"<{value}";
        switch (tallDetails)
        {
            case true:
                lastFRight.SetAsTall(details);
                break;
            default:
                lastFRight.SetAsDefault(details);
                break;
        }

        MainScreen.Instance.scratchPadText.Clear();
    }

    public void UpdateScratchPad(string buffer, bool withStatus = true)
    {
        MainScreen.Instance.scratchPadText.SetAsDefault(buffer);
        if (withStatus)
        {
            lastFLeft.text = "ok";
        }
    }

}