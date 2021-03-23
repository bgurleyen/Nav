using System.Linq;
using UnityEngine;

public class LegsScreen : ScreenBase
{
    [SerializeField] LegsNodeLine[] nodes;
    
    public static RouteScriptableObject VisibleRoute => GameManager.Instance.IsMod ? ModRoute : ActiveRoute;
    
    static RouteScriptableObject ActiveRoute => GameManager.Instance.ActiveRoute;
    static RouteScriptableObject ModRoute => GameManager.Instance.ModRoute;
    static MainScreen Main => MainScreen.Instance;
    
    int NodesPerPage => nodes.Length;
    RoutePoint GetSelectedPoint => selectionInfo == null ? null : 
        VisibleRoute.GetPoint(selectionInfo.LinkedId, out var _node) ? _node : null;
    RoutePoint LastSelectedPoint =>
        lastSelectionClicked == null ? null :
        VisibleRoute.GetPoint(lastSelectionClicked.LinkedId, out var _node) ? _node : null;

    DisplayNodesController nodesController;
    NodeSelection selectionInfo;
    NodeSelection lastSelectionClicked;


    const string EraseTitle = "<ERASE";

    static bool IsErase => Main.LastLineLeft == EraseTitle;

    ScratchPadInterpreter scratchPadInterpreter;
    string scratchPadBuffer = "";

    int TotalPages =>
        Mathf.CeilToInt((VisibleRoute.Points.Length - PositionVirtualNode.NextNodeIndex + nodesController.TotalPagesCorrection) /
                        (float) NodesPerPage);

    int currentPage = 0;

    void Start()
    {
        nodesController = new DisplayNodesController(NodesPerPage);
        GameManager.Instance.OnOperationMade += nodesController.ComputeCorrections;
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
        currentPage = Mathf.Min(TotalPages - 1, currentPage + 1);
    }

    public override void DisplayPrevPage()
    {
        currentPage = Mathf.Max(0, currentPage - 1);
    }

    public void DisplayCurrentPage()
    {
        Main.UpdatePageInfo(currentPage, TotalPages, GameManager.Instance.IsMod, "LEGS");

        // Debug.Log("start");
        for (var i = 0; i < nodes.Length; i++)
        {
            var _linkedSelection = nodesController.GetNodeInfoAtLineIndex(i, currentPage);

            if (_linkedSelection.IsInvalid || _linkedSelection.IsEmpty)
            {
                nodes[i].ShowEmpty();
            }
            else
            {
                VisibleRoute.GetPoint(_linkedSelection.LinkedId, out var _node);
                nodes[i].DisplayNodeDetails(_node, _linkedSelection);
            }
        }
    }

    public override void OnLineSelectRight(int index)
    {
        var _clickedInfo = nodesController.GetNodeInfoAtLineIndex(index, currentPage);

        if (ScratchPadInterpreter.IsDeletePending(scratchPadBuffer))
        {
            GameManager.Instance.ExecuteDeleteRestrictions(new DeleteRestrictionsCommand
            {
                NodeId = _clickedInfo.LinkedId
            });
        }

        if (scratchPadInterpreter.IsAltitudeRegulation(out var _regulation))
        {
            GameManager.Instance.ExecuteAddAltitudeRegulation(new AddAltitudeRegulationCommand
            {
                NodeId = _clickedInfo.LinkedId,
                Regulation = _regulation
            });
        }

        if (scratchPadInterpreter.IsSpeedRegulation(out var _speedRegulation))
        {
            GameManager.Instance.ExecuteAddSpeedRegulation(new AddSpeedRegulationCommand
            {
                NodeId = _clickedInfo.LinkedId,
                Regulation = _speedRegulation
            });
        }
    }

    public override void OnLineSelectLeft(int index)
    {
        var _clickedInfo = nodesController.GetNodeInfoAtLineIndex(index, currentPage);
        lastSelectionClicked = _clickedInfo;
        // user clicks, none is previously selected
        if (GetSelectedPoint == null)
        {
            Debug.Log("=selection text=");
            selectionInfo = _clickedInfo;
            scratchPadBuffer = GetSelectedPoint.Name;
            GetSelectedPoint.IsSelected = true;
            Main.UpdateScratchPad(scratchPadBuffer);
            InterpretScratchpadOnTextChanged(false);
            return;
        }

        GetSelectedPoint.IsSelected = false;

        if (!string.IsNullOrEmpty(scratchPadBuffer) &&
            scratchPadInterpreter.IsRelativeNodeOnDirection(out var _distanceOnDirection, out var _relativeNodeId))
        {
            GameManager.Instance.ExecuteInsertRelativeOnDirectionOnMod(new ExecuteRelativeOnDirectionOnMod
                {FromNodeId = _clickedInfo.LinkedId, Distance = _distanceOnDirection, RelativeNodeId = _relativeNodeId});
        }
        else if (!string.IsNullOrEmpty(scratchPadBuffer) &&
                 scratchPadInterpreter.IsRelativeNode(out var _angle, out var _distance, out _relativeNodeId))
        {
            // if this is a relative insert command
            GameManager.Instance.ExecuteInsertRelativeOnMod(new InsertRelativeCommand
                {BeforeNodeId = _clickedInfo.LinkedId, RawDegrees = _angle, Distance = _distance, RelativeNodeId = _relativeNodeId});
        }
        else
        {
            var _selectedIndex = VisibleRoute.Points.GetNodeIndex(selectionInfo.LinkedId);
            var _clickedIndex = VisibleRoute.Points.GetNodeIndex(_clickedInfo.LinkedId);

            // when user clicks on the node below
            if (_selectedIndex < _clickedIndex)
            {
                Debug.Log("error");
                ClearCurrentOperation();
                return;
            }

            // if this is a shortcut command
            Debug.Log("=shortcut=");
            GameManager.Instance.ExecuteShortcutOnMod(new ExecuteShortcutOnModeCommand
                {FromNodeId = _clickedInfo.LinkedId, ToNodeId = selectionInfo.LinkedId});
        }

        ClearCurrentOperation();
    }

    public override void OnExecPress()
    {
        if (GameManager.Instance.IsMod)
        {
            GameManager.Instance.ApplyMod();
        }

        ClearCurrentOperation();
        ClearSelectionHistory();
    }


    public override void OnRightCornerPress()
    {
        if (!GameManager.Instance.IsMod)
        {
            return;
        }

        if (LastSelectedPoint != null && LastSelectedPoint.IsModified &&
            scratchPadInterpreter.IsLinearApproach(out var _angle))
        {
            Debug.Log("=linear approach= on " + LastSelectedPoint.Name + " with: " + _angle);
            GameManager.Instance.ExecuteLinearApproachOnMod(new ExecuteAddLinearApproachCommand
                {ToNodeId = lastSelectionClicked.LinkedId, Angle = _angle});
        }
    }

    public override void OnLeftCornerPress()
    {
        if (IsErase)
        {
            GameManager.Instance.EraseMod();
            ClearCurrentOperation();
        }
    }

    public override void OnClearPress()
    {
        // to generalize to all operations
        if (ScratchPadInterpreter.IsDeletePending(scratchPadBuffer))
        {
            scratchPadBuffer = string.Empty;
        }
        
        if (scratchPadBuffer.Length > 0)
        {
            scratchPadBuffer = scratchPadBuffer.Remove(scratchPadBuffer.Length - 1);
        }

        Main.UpdateScratchPad(scratchPadBuffer);
    }

    public override void OnDeletePress()
    {
        scratchPadBuffer = MainScreen.Keywords.DELETE;
        Main.UpdateScratchPad(scratchPadBuffer);
    }

    public override void OnCharacterInput(char character)
    {
        if (character == '-' && scratchPadBuffer.Length > 0 && scratchPadBuffer[scratchPadBuffer.Length - 1] == '-')
        {
            scratchPadBuffer = scratchPadBuffer.Remove(scratchPadBuffer.Length - 1);
        }
        else
        {
            scratchPadBuffer += character;
        }

        InterpretScratchpadOnTextChanged(true);

        Main.UpdateScratchPad(scratchPadBuffer, selectionInfo != null);
    }

    struct ScratchPadInterpreter
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


    void InterpretScratchpadOnTextChanged(bool handleSelection)
    {
        scratchPadInterpreter = new ScratchPadInterpreter
        {
            IsValid = true,
            Angle = null,
            Distance = null,
            AltRegulation = null,
            SpeedRegulation = null,
            Node = null
        };

        if (GetSelectedPoint != null || lastSelectionClicked != null)
        {
            if (handleSelection && GetSelectedPoint != null)
            {
                GetSelectedPoint.IsSelected = false;
            }

            if (scratchPadBuffer.Contains('/'))
            {
                var _indexOfSlash = scratchPadBuffer.IndexOf('/');
                var _allLeft = scratchPadBuffer.Substring(0, _indexOfSlash);
                // ABC/-11 or ABC060/-11
                scratchPadInterpreter.Node = VisibleRoute.Points.FirstOrDefault(x => x.Name == _allLeft);
                if (scratchPadInterpreter.Node == null)
                {
                    // ABC060/-11    ~~   Can be relative with angle and direction
                    if (int.TryParse(_allLeft.Substring(_allLeft.Length - 3, 3), out var _angle))
                    {
                        scratchPadInterpreter.Angle = _angle;
                        var _withoutAngle = _allLeft.Substring(0, _allLeft.Length - 3);
                        scratchPadInterpreter.Node = VisibleRoute.Points.FirstOrDefault(x => x.Name == _withoutAngle);
                    }
                    else
                    {
                        scratchPadInterpreter.IsValid = false;
                    }
                }

                // make sure the distance is ok 
                if (scratchPadInterpreter.Node != null)
                {
                    var _allRight =
                        scratchPadBuffer.Substring(_indexOfSlash + 1, scratchPadBuffer.Length - (_indexOfSlash + 1));
                    if (int.TryParse(_allRight, out var _distance))
                    {
                        scratchPadInterpreter.Distance = _distance;
                    }
                    else
                    {
                        scratchPadInterpreter.IsValid = false;
                    }
                }
            }
            else
            {
                if (int.TryParse(scratchPadBuffer, out var _number))
                {
                    if (scratchPadBuffer.Length == 3)
                    {
                        // Is linear approach
                        scratchPadInterpreter.Angle = _number;
                    }

                   
                }
                else
                {
                    scratchPadInterpreter.Node = VisibleRoute.Points.FirstOrDefault(x => x.Name == scratchPadBuffer);
                    if (scratchPadInterpreter.Node == null)
                    {
                        scratchPadInterpreter.IsValid = false;
                    }
                }
            }

            if (scratchPadInterpreter.IsValid)
            {
                if (handleSelection && scratchPadInterpreter.Node != null)
                {
                    selectionInfo = new NodeSelection
                    {
                        IsEmpty = false,
                        LinkedId = scratchPadInterpreter.Node.ID,
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
            if (scratchPadBuffer[0] == '/' && scratchPadBuffer.Length > 1)
            {
                // should be altitude only regulation
                var _value = scratchPadBuffer.Substring(1, scratchPadBuffer.Length - 1);

                if (DataHandler.ParseAltRegulation(_value, out _, out _, out _))
                {
                    scratchPadInterpreter.AltRegulation = _value;
                    scratchPadInterpreter.SpeedRegulation = null;
                }
                else
                {
                    scratchPadInterpreter.IsValid = false;
                }
            }
            else if (scratchPadBuffer[scratchPadBuffer.Length - 1] == '/' && scratchPadBuffer.Length > 1)
            {
                // should be speed only regulation
                var _value = scratchPadBuffer.Substring(0, scratchPadBuffer.Length - 1);
                if (int.TryParse(_value, out var _regulation))
                {
                    scratchPadInterpreter.AltRegulation = null;
                    scratchPadInterpreter.SpeedRegulation = _regulation;
                }
            }
            else if (scratchPadBuffer.Contains('/') && scratchPadBuffer.Length > 3)
            {
                // may be speed & alt regulation
                var _slashIndex = scratchPadBuffer.IndexOf('/');
                var _speed = scratchPadBuffer.Substring(0, _slashIndex);
                var _altRegulation =
                    scratchPadBuffer.Substring(_slashIndex + 1, scratchPadBuffer.Length - (_slashIndex + 1));
                if (int.TryParse(_speed, out var _speedRegulation) &&
                    DataHandler.ParseAltRegulation(_altRegulation, out _, out _, out _))
                {
                    scratchPadInterpreter.AltRegulation = _altRegulation;
                    scratchPadInterpreter.SpeedRegulation = _speedRegulation;
                }
                else
                {
                    scratchPadInterpreter.IsValid = false;
                }
            }
            else if (int.TryParse(scratchPadBuffer, out var _number))
            {
                // can be altitude regulation
                scratchPadInterpreter.AltRegulation = scratchPadBuffer;
            }
        }
    }

    void ClearCurrentOperation()
    {
        scratchPadBuffer = "";
        selectionInfo = null;
    }

    void ClearSelectionHistory()
    {
        lastSelectionClicked = null;
    }
}