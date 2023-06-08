using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.EventSystems;

public class ToggleButton3DLinker : MonoBehaviour, IPointerUpHandler, IPointerDownHandler
{
    [SerializeField] private Renderer indicatorMesh;
    [SerializeField] private Material onMat;
    [SerializeField] private Material offMat;

    private Material[] _materials;
    private Animator _meshAnimator;

    public Action UserAttemptSwitch;

    /// <summary>
    /// [ newState, return ]
    /// </summary>
    private void Awake()
    {
        _materials = indicatorMesh.materials;
        _meshAnimator = indicatorMesh.gameObject.GetComponent<Animator>();
    }

    public void SetState(bool on)
    {
        _materials[1] = on ? onMat : offMat;
        indicatorMesh.materials = _materials;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _meshAnimator.SetTrigger("Pressed");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _meshAnimator.SetTrigger("Normal");
        UserAttemptSwitch?.Invoke();
    }
}

public class ToggleLinkedBool
{
    private readonly ToggleButton3DLinker _linkedToggle;
    private bool _state;

    public static implicit operator bool(ToggleLinkedBool i) => i._state;

    public ToggleLinkedBool(ToggleButton3DLinker toggle, bool initialState = false)
    {
        _linkedToggle = toggle;
        toggle.UserAttemptSwitch += Switch;
        Set(initialState);
    }

    public ToggleLinkedBool(ToggleButton3DLinker toggle, Action onUserPress, bool initialState = false)
    {
        _linkedToggle = toggle;
        toggle.UserAttemptSwitch += onUserPress;
        Set(initialState);
    }

    public void Set(bool state)
    {
        _state = state;
        _linkedToggle.SetState(state);
    }

    public void Switch()
    {
        Set(!_state);
    }
}
