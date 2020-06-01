using UnityEngine;

public class LegsScreen : MonoBehaviour
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
    RoutePoint LastSelectedPoint => lastSelectionClicked == null ? null : VisibleRoute.GetPoint(lastSelectionClicked.LinkedId);
    
    string scratchPadBuffer = "";
    int TotalPages =>  Mathf.CeilToInt((VisibleRoute.Points.Length - StartingNodeIndex + nodesController.TotalPagesCorrection) / (float)NodesPerPage); 

    int currentPage = 0;

    void Start()
    {
        nodesController = new DisplayNodesController(NodesPerPage);
        GameManager.Instance.OnOperationMade += nodesController.ComputeCorrections;
    }

    public void DisplayNextPage()
    {
        currentPage = Mathf.Min(TotalPages - 1, currentPage + 1);
    }

    public void DisplayPrevPage()
    {
        currentPage = Mathf.Max(0, currentPage - 1);
    }

    public void DisplayCurrentPage()
    {
        Main.pageNumber.text = $"{currentPage + 1}/{TotalPages}";
        Main.title.text = IsMod ? "MOD" : "LEGS";

        for (var i = 0; i < nodes.Length; i++)
        {
            var linkedSelection = nodesController.GetNodeInfoAtLineIndex(i, currentPage);

            if (linkedSelection.IsInvalid)
            {
                return;
            }

            if (linkedSelection.IsEmpty)
            {
                nodes[i].ShowEmpty();
            }
            else
            {
                nodes[i].DisplayNodeDetails(VisibleRoute.GetPoint(linkedSelection.LinkedId), linkedSelection);
            }
        }
    }

    public void OnLineSelectLeft(int index)
    {
        var clickedInfo = nodesController.GetNodeInfoAtLineIndex(index, currentPage);
        lastSelectionClicked = clickedInfo;
        // user clicks, none is previously selected
        if (SelectedPoint == null)
        {
            Debug.Log("=selection=");
            selectionInfo = clickedInfo;
            SelectedPoint.IsSelected = true;
            Main.DisplayInfo(SelectedPoint.Name);
            scratchPadBuffer = "";
            return;
        }

        SelectedPoint.IsSelected = false;

        // if this is a relative insert on direction command
        if (!string.IsNullOrEmpty(scratchPadBuffer) && DataHandler.ParseRelativeNodeOnDirection(scratchPadBuffer, out var distanceOnDirection))
        {
            Debug.Log("=relative insert on direction=");
            GameManager.Instance.ExecuteInsertRelativeOnDirectionOnMod(new ExecuteRelativeOnDirectionOnMod { FromNodeId = clickedInfo.LinkedId, Distance = distanceOnDirection });
        }

        // if this is a relative insert command
        if (!string.IsNullOrEmpty(scratchPadBuffer) && DataHandler.ParseRelativeNode(scratchPadBuffer, out var angle, out var distance))
        {
            Debug.Log("=relative insert=");
            GameManager.Instance.ExecuteInsertRelativeOnMod(new InsertRelativeCommand { FromNodeId = clickedInfo.LinkedId, RawDegrees = angle, Distance = distance });
        }
        // if this is a shortcut command
        else
        {
            var selectedIndex = VisibleRoute.GetIndex(selectionInfo.LinkedId);
            var clickedIndex = VisibleRoute.GetIndex(clickedInfo.LinkedId);

            // when user clicks on the node below
            if (selectedIndex < clickedIndex)
            {
                Debug.Log("error");
                ClearCurrentOperation();
                return;
            }

            Debug.Log("=shortcut=");
            GameManager.Instance.ExecuteShortcutOnMod(new ExecuteShortcutOnModeCommand { FromNodeId = clickedInfo.LinkedId, ToNodeId = selectionInfo.LinkedId });
        }

        ClearCurrentOperation();
    }

    public void OnExecPress()
    {
        if (IsMod)
        {
            GameManager.Instance.ApplyMod();
        }
        ClearCurrentOperation();
        ClearSelectionHistory();
    }

    public void OnErasePress()
    {
        GameManager.Instance.EraseMod();
        ClearCurrentOperation();
    }

    public void OnRightCornerPress()
    {
        if (!IsMod)
        {
            return;
        }

        if (LastSelectedPoint != null && LastSelectedPoint.IsModified && DataHandler.ParseLiniarApproach(scratchPadBuffer, out var angle) )
        {
            Debug.Log("=liniar approach= on " + LastSelectedPoint.Name + " with: "+angle );
            GameManager.Instance.ExecuteLinearApproachOnMod(new ExecuteAddLinearApproachCommand { ToNodeId = lastSelectionClicked.LinkedId, Angle = angle });
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


    public void OnNumberPressed(int number)
    {
        if (SelectedPoint == null && lastSelectionClicked == null)
        {
            return;
        }

        OnCharacterInput((char)(number + 48));
    }

    public void OnDecimalPressed()
    {
        if (SelectedPoint == null)
        {
            return;
        }

        OnCharacterInput('.');
    }

    public void OnSlashPressed()
    {
        if (SelectedPoint == null)
        {
            return;
        }

        OnCharacterInput('/');
    }

    public void OnSignPressed()
    {
        if (SelectedPoint == null)
        {
            return;
        }

        OnCharacterInput('-');
    }

    public void OnDeletePress()
    {
        if (scratchPadBuffer.Length == 0) return;

        scratchPadBuffer = scratchPadBuffer.Remove(scratchPadBuffer.Length - 1);
        Main.UpdateScratchPad(scratchPadBuffer);
    }

    void OnCharacterInput(char character)
    {
        if (character == '-' && scratchPadBuffer.Length > 0 && scratchPadBuffer[scratchPadBuffer.Length - 1] == '-')
        {
            scratchPadBuffer = scratchPadBuffer.Remove(scratchPadBuffer.Length - 1);
        }
        else
        {
            scratchPadBuffer += character;
        }
        Main.UpdateScratchPad(scratchPadBuffer, selectionInfo != null);
    }
}
