using UnityEngine;
using UnityEngine.UI;

public class CanvasUI : MonoBehaviour
{
    [SerializeField] private Animator animatorFMV;

    void Awake()
    {
        // The expand button's serialized click target is empty, so the inspector call never runs.
        var buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == "Button - large Expand")
                buttons[i].onClick.AddListener(OnExpandFMVClick);
        }

        SnapFullyOpen();
    }

    void Start()
    {
        // Default state is the retract clip. Re-apply after the animator initializes so the first frame is open.
        SnapFullyOpen();
    }

    void SnapFullyOpen()
    {
        if (animatorFMV == null || !animatorFMV.isActiveAndEnabled)
            return;

        animatorFMV.Play("expandFMC", 0, 1f);
        animatorFMV.Update(0f);
    }

    public void OnExpandFMVClick()
    {
        if (animatorFMV == null)
            return;

        animatorFMV.ResetTrigger("RetractFMV");
        animatorFMV.SetTrigger("ExpandFMV");
    }

    public void OnRetractFMVClick()
    {
        if (animatorFMV == null)
            return;

        animatorFMV.ResetTrigger("ExpandFMV");
        animatorFMV.SetTrigger("RetractFMV");
    }
}
