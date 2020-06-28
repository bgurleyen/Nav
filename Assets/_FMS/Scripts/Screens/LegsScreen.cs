using System.Linq;
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

    RoutePoint SelectedPoint => selectionInfo == null ? null : VisibleRoute.GetPoint(selectionInfo.LinkedId);

    RoutePoint LastSelectedPoint =>
        lastSelectionClicked == null ? null : VisibleRoute.GetPoint(lastSelectionClicked.LinkedId);


    const string EraseTitle = "<ERASE";

    static bool IsErase => Main.LastLineLeft == EraseTitle;

    string nodeNameTemp = "";
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

    public override void OnLineSelectLeft(int index)
    {
        var _clickedInfo = nodesController.GetNodeInfoAtLineIndex(index, currentPage);
        lastSelectionClicked = _clickedInfo;
        // user clicks, none is previously selected
        if (SelectedPoint == null)
        {
            Debug.Log("=selection=");
            selectionInfo = _clickedInfo;
            SelectedPoint.IsSelected = true;
            nodeNameTemp = SelectedPoint.Name;
            Main.UpdateScratchPad(nodeNameTemp);
            scratchPadBuffer = "";
            return;
        }

        SelectedPoint.IsSelected = false;

        // if this is a relative insert on direction command
        if (!string.IsNullOrEmpty(scratchPadBuffer) &&
            DataHandler.ParseRelativeNodeOnDirection(scratchPadBuffer, out var _distanceOnDirection))
        {
            Debug.Log("=relative insert on direction=");
            GameManager.Instance.ExecuteInsertRelativeOnDirectionOnMod(new ExecuteRelativeOnDirectionOnMod
                {FromNodeId = _clickedInfo.LinkedId, Distance = _distanceOnDirection});
        }

        // if this is a relative insert command
        if (!string.IsNullOrEmpty(scratchPadBuffer) &&
            DataHandler.ParseRelativeNode(scratchPadBuffer, out var _angle, out var _distance))
        {
            Debug.Log("=relative insert=");
            GameManager.Instance.ExecuteInsertRelativeOnMod(new InsertRelativeCommand
                {FromNodeId = _clickedInfo.LinkedId, RawDegrees = _angle, Distance = _distance});
        }
        // if this is a shortcut command
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
            DataHandler.ParseLinearApproach(scratchPadBuffer, out var _angle))
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


    public override void OnNumberPressed(int number)
    {
        if (SelectedPoint == null && lastSelectionClicked == null)
        {
            return;
        }

        OnCharacterInput((char) (number + 48));
    }

    public override void OnDecimalPressed()
    {
        if (SelectedPoint == null)
        {
            return;
        }

        OnCharacterInput('.');
    }

    public override void OnSlashPressed()
    {
        if (SelectedPoint == null)
        {
            return;
        }

        OnCharacterInput('/');
    }

    public override void OnSignPressed()
    {
        if (SelectedPoint == null)
        {
            return;
        }

        OnCharacterInput('-');
    }

    public override void OnClearPress()
    {
        if (scratchPadBuffer.Length > 0)
        {
            scratchPadBuffer = scratchPadBuffer.Remove(scratchPadBuffer.Length - 1);
        }
        else if (nodeNameTemp.Length > 0)
        {
            nodeNameTemp = nodeNameTemp.Remove(nodeNameTemp.Length - 1);
        }

        Main.UpdateScratchPad(nodeNameTemp + scratchPadBuffer);
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
        // else if (scratchPadBuffer.Length == 0) // if we edit the node name we can select one in case it exists
        // {
        //     var _node = VisibleRoute.Points.FirstOrDefault(x => x.Name == $"{nodeNameTemp}{character}");
        //     if (_node != null)
        //     {
        //         nodeNameTemp += character;
        //         if (SelectedPoint != null)
        //         {
        //             SelectedPoint.IsSelected = false;
        //         }
        //
        //         selectionInfo = new NodeSelection
        //         {
        //             IsEmpty = false,
        //             LinkedId = _node.ID,
        //             IsAddedDiscontinuity = false,
        //             IsStartingPoint = false
        //         };
        //         SelectedPoint.IsSelected = true;
        //     }
        // }
        else
        {
            scratchPadBuffer += character;
        }

        Main.UpdateScratchPad(nodeNameTemp + scratchPadBuffer, selectionInfo != null);
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