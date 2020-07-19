using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class LegsScreen : ScreenBase
{
    public LegsNodeLine[] nodes;

    static RouteScriptableObject ActiveRoute => GameManager.Instance.ActiveRoute;
    static RouteScriptableObject ModRoute => GameManager.Instance.ModRoute;
    static bool IsMod => GameManager.Instance.IsMod;
    static RouteScriptableObject VisibleRoute => IsMod ? ModRoute : ActiveRoute;
    static int StartingNodeIndex => GameManager.Instance.UnreachedNodeIndex;
    static MainScreen Main => MainScreen.Instance;
    int NodesPerPage => nodes.Length;

    DisplayNodesController nodesController;

    NodeSelection selectionInfo;
    NodeSelection lastSelectionClicked;

    RoutePoint GetSelectedPoint => selectionInfo == null ? null : VisibleRoute.GetPoint(selectionInfo.LinkedId);

    RoutePoint LastSelectedPoint =>
        lastSelectionClicked == null ? null : VisibleRoute.GetPoint(lastSelectionClicked.LinkedId);


    const string EraseTitle = "<ERASE";

    static bool IsErase => Main.LastLineLeft == EraseTitle;

    ScratchPadInfo scratchPadInfo;
    string scratchPadBuffer = "";

    int TotalPages =>
        Mathf.CeilToInt((VisibleRoute.Points.Length - StartingNodeIndex + nodesController.TotalPagesCorrection) /
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
        Main.UpdatePageInfo(currentPage, TotalPages, IsMod);

        for (var i = 0; i < nodes.Length; i++)
        {
            var _linkedSelection = nodesController.GetNodeInfoAtLineIndex(i, currentPage);

            if (_linkedSelection.IsInvalid)
            {
                return;
            }

            if (_linkedSelection.IsEmpty)
            {
                nodes[i].ShowEmpty();
            }
            else
            {
                nodes[i].DisplayNodeDetails(VisibleRoute.GetPoint(_linkedSelection.LinkedId), _linkedSelection);
            }
        }
    }

    public override void OnLineSelectRight(int index)
    {
        var _clickedInfo = nodesController.GetNodeInfoAtLineIndex(index, currentPage);

        if (scratchPadInfo.IsAddAltitudeRegulation(out var _regulation))
        {
            GameManager.Instance.ExecuteAddAltitudeRegulation(new AddAltitudeRegulationCommand
            {
                NodeId = _clickedInfo.LinkedId,
                Regulation = _regulation
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
            return;
        }

        GetSelectedPoint.IsSelected = false;

        if (!string.IsNullOrEmpty(scratchPadBuffer) &&
            scratchPadInfo.IsRelativeNodeOnDirection(out var _distanceOnDirection))
        {
            GameManager.Instance.ExecuteInsertRelativeOnDirectionOnMod(new ExecuteRelativeOnDirectionOnMod
                {FromNodeId = _clickedInfo.LinkedId, Distance = _distanceOnDirection});
        }
        else if (!string.IsNullOrEmpty(scratchPadBuffer) &&
                 scratchPadInfo.IsRelativeNode(out var _angle, out var _distance))
        {
            // if this is a relative insert command
            GameManager.Instance.ExecuteInsertRelativeOnMod(new InsertRelativeCommand
                {FromNodeId = _clickedInfo.LinkedId, RawDegrees = _angle, Distance = _distance});
        }
        else
        {
            var _selectedIndex = VisibleRoute.GetIndex(selectionInfo.LinkedId);
            var _clickedIndex = VisibleRoute.GetIndex(_clickedInfo.LinkedId);

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
        if (IsMod)
        {
            GameManager.Instance.ApplyMod();
        }

        ClearCurrentOperation();
        ClearSelectionHistory();
    }


    public override void OnRightCornerPress()
    {
        if (!IsMod)
        {
            return;
        }

        if (LastSelectedPoint != null && LastSelectedPoint.IsModified &&
            scratchPadInfo.IsLinearApproach(out var _angle))
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
        if (scratchPadBuffer.Length > 0)
        {
            scratchPadBuffer = scratchPadBuffer.Remove(scratchPadBuffer.Length - 1);
        }

        Main.UpdateScratchPad(scratchPadBuffer);
    }

    /// <summary>
    /// Can do:
    ///  - delete restriction for altitude & speed
    /// </summary>
    public override void OnDeletePress()
    {
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

    struct ScratchPadInfo
    {
        public bool IsValid;
        public RoutePoint Node;
        public int? Angle;
        public int? Distance;
        public string Regulation;

        public bool IsAddAltitudeRegulation(out string regulation)
        {
            regulation = "";
            if (!IsValid || Node != null || Angle != null || Distance != null
                || Regulation == null)
            {
                return false;
            }

            regulation = Regulation;
            return true;
        }

        public bool IsRelativeNode(out int angle, out int distance)
        {
            distance = 0;
            angle = 0;
            if (!IsValid || Regulation != null
                         || Node == null || Angle == null || Distance == null)
            {
                return false;
            }

            Debug.Log("=relative insert=");
            distance = Distance.Value;
            angle = Angle.Value;
            return true;
        }

        public bool IsRelativeNodeOnDirection(out int distance)
        {
            distance = 0;
            if (!IsValid || Angle != null || Regulation != null
                || Node == null || Distance == null)
            {
                return false;
            }

            Debug.Log("=relative insert on direction=");
            distance = Distance.Value;
            return true;
        }

        public bool IsLinearApproach(out int angle)
        {
            angle = 0;
            if (!IsValid || Node != null || Distance != null || Regulation != null
                || Angle == null)
            {
                return false;
            }

            angle = Angle.Value;
            return true;
        }
    }


    void InterpretScratchpadOnTextChanged(bool handleSelection)
    {
        scratchPadInfo = new ScratchPadInfo
        {
            IsValid = true,
            Angle = null,
            Distance = null,
            Regulation = null,
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
                scratchPadInfo.Node = VisibleRoute.Points.FirstOrDefault(x => x.Name == _allLeft);
                if (scratchPadInfo.Node == null)
                {
                    // ABC060/-11    ~~   Can be relative with angle and direction
                    if (int.TryParse(_allLeft.Substring(_allLeft.Length - 3, 3), out var _angle))
                    {
                        scratchPadInfo.Angle = _angle;
                        var _withoutAngle = _allLeft.Substring(0, _allLeft.Length - 3);
                        scratchPadInfo.Node = VisibleRoute.Points.FirstOrDefault(x => x.Name == _withoutAngle);
                    }
                    else
                    {
                        scratchPadInfo.IsValid = false;
                    }
                }

                // make sure the distance is ok 
                if (scratchPadInfo.Node != null)
                {
                    var _allRight =
                        scratchPadBuffer.Substring(_indexOfSlash + 1, scratchPadBuffer.Length - (_indexOfSlash + 1));
                    if (int.TryParse(_allRight, out var _distance))
                    {
                        scratchPadInfo.Distance = _distance;
                    }
                    else
                    {
                        scratchPadInfo.IsValid = false;
                    }
                }
            }
            else
            {
                if (scratchPadBuffer.Length == 3 && int.TryParse(scratchPadBuffer, out var _angle))
                {
                    // Is linear approach
                    scratchPadInfo.Angle = _angle;
                }
                else
                {
                    scratchPadInfo.Node = VisibleRoute.Points.FirstOrDefault(x => x.Name == scratchPadBuffer);
                    if (scratchPadInfo.Node == null)
                    {
                        scratchPadInfo.IsValid = false;
                    }
                }
            }

            if (scratchPadInfo.IsValid)
            {
                if (handleSelection && scratchPadInfo.Node != null)
                {
                    selectionInfo = new NodeSelection
                    {
                        IsEmpty = false,
                        LinkedId = scratchPadInfo.Node.ID,
                        IsAddedDiscontinuity = false,
                        IsStartingPoint = false
                    };

                    GetSelectedPoint.IsSelected = true;
                }
            }
            else
            {
                Debug.Log("Invalid Scratchpad Entry!");
            }
        }

        else
        {
            // look for regulations
            if (scratchPadBuffer[0] == '/' && scratchPadBuffer.Length>1)
            {
                // should be altitude regulation
                var _value = scratchPadBuffer.Substring(1, scratchPadBuffer.Length - 1);
                
                if (DataHandler.ParseAltRegulation(_value, out _, out _, out _))
                {
                    scratchPadInfo.Regulation = _value;
                }
                else
                {
                    scratchPadInfo.IsValid = false;
                }
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