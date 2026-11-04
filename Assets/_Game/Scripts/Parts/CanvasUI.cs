using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CanvasUI : MonoBehaviour
{
    const string ExpandStateName = "expandFMC";
    const string RetractStateName = "expandFMC  reverse";

    [SerializeField] private Animator animatorFMV;
    [SerializeField] private Transform landingGearControlsRoot;
    [SerializeField] private Transform flapControlsRoot;

    Coroutine _expandHideRoutine;

    void Awake()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == "Button - large Expand")
                buttons[i].onClick.AddListener(OnExpandFMVClick);
        }

        if (landingGearControlsRoot == null)
            landingGearControlsRoot = FindDeepChild(transform, "pivot 2");
        if (flapControlsRoot == null)
            flapControlsRoot = FindDeepChild(transform, "pivot 3");

        if (landingGearControlsRoot == null || flapControlsRoot == null)
        {
            var mcpRoot = GameObject.Find("3 -- MCP - 3D elements");
            if (mcpRoot != null)
            {
                if (landingGearControlsRoot == null)
                    landingGearControlsRoot = FindDeepChild(mcpRoot.transform, "pivot 2");
                if (flapControlsRoot == null)
                    flapControlsRoot = FindDeepChild(mcpRoot.transform, "pivot 3");
            }
        }

        SnapFullyOpen();
    }

    void Start()
    {
        SnapFullyOpen();
    }

    void SnapFullyOpen()
    {
        if (animatorFMV == null || !animatorFMV.isActiveAndEnabled)
            return;

        animatorFMV.Play(ExpandStateName, 0, 1f);
        animatorFMV.Update(0f);
        SetLandingControlsVisible(false);
    }

    public void OnExpandFMVClick()
    {
        if (animatorFMV == null)
            return;

        if (_expandHideRoutine != null)
        {
            StopCoroutine(_expandHideRoutine);
            _expandHideRoutine = null;
        }

        animatorFMV.ResetTrigger("RetractFMV");
        animatorFMV.SetTrigger("ExpandFMV");
        _expandHideRoutine = StartCoroutine(HideLandingControlsWhenExpandFinishes());
    }

    public void OnRetractFMVClick()
    {
        if (animatorFMV == null)
            return;

        if (_expandHideRoutine != null)
        {
            StopCoroutine(_expandHideRoutine);
            _expandHideRoutine = null;
        }

        SetLandingControlsVisible(true);
        animatorFMV.ResetTrigger("ExpandFMV");
        animatorFMV.SetTrigger("RetractFMV");
    }

    IEnumerator HideLandingControlsWhenExpandFinishes()
    {
        yield return null;

        while (animatorFMV != null)
        {
            if (animatorFMV.IsInTransition(0))
            {
                yield return null;
                continue;
            }

            var state = animatorFMV.GetCurrentAnimatorStateInfo(0);
            if (state.IsName(ExpandStateName) && state.normalizedTime >= 1f)
                break;

            yield return null;
        }

        SetLandingControlsVisible(false);
        _expandHideRoutine = null;
    }

    void SetLandingControlsVisible(bool visible)
    {
        if (landingGearControlsRoot != null)
            landingGearControlsRoot.gameObject.SetActive(visible);
        if (flapControlsRoot != null)
            flapControlsRoot.gameObject.SetActive(visible);
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null)
            return null;

        if (parent.name == name)
            return parent;

        for (var i = 0; i < parent.childCount; i++)
        {
            var found = FindDeepChild(parent.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
