using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class ToggleButton3DLinker : ToggleButtonLinker, IPointerUpHandler, IPointerDownHandler
{
    [SerializeField] private Renderer indicatorMesh;
    [SerializeField] private Material onMat;
    [SerializeField] private Material offMat;
    [SerializeField] private UnityEvent OnPressed;

    private Material[] _materials;
    private Animator _meshAnimator;

    
    /// <summary>
    /// [ newState, return ]
    /// </summary>
    private void Awake()
    {
        _materials = indicatorMesh.materials;
        _meshAnimator = indicatorMesh.gameObject.GetComponent<Animator>();
    }

    public override void SetState(bool on)
    {
        base.SetState(on);
       
        _materials[1] = on ? onMat : offMat;
        indicatorMesh.materials = _materials;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _meshAnimator.SetTrigger("Pressed");
        OnPressed?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _meshAnimator.SetTrigger("Normal");
        
        OnUserSwitch();
    }
}

public class ToggleLinkedBool
{
    private readonly ToggleButtonLinker _linkedToggle;

    public static implicit operator bool(ToggleLinkedBool i) => i._linkedToggle.State;

    public ToggleLinkedBool(ToggleButtonLinker toggle, bool initialState = false)
    {
        _linkedToggle = toggle;
        toggle.UserAttemptSwitch += Switch;
        Set(initialState);
    }

    public ToggleLinkedBool(ToggleButtonLinker toggle, Action onUserPress, bool initialState = false)
    {
        _linkedToggle = toggle;
        toggle.UserAttemptSwitch += onUserPress;
        Set(initialState);
    }

    public void Set(bool state)
    {
        _linkedToggle.SetState(state);
    }

    public void Switch()
    {
        Set(!_linkedToggle.State);
    }
    public void Switch_Oneway()
    {
        if (!_linkedToggle.State) Set(!_linkedToggle.State);
    }
}
