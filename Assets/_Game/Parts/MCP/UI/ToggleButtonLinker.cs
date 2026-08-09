using System;
using UnityEngine;

public class ToggleButtonLinker : MonoBehaviour
{
    public Action UserAttemptSwitch;

    private bool _state;
    private bool _interactable = true;

    public bool State => _state;
    public bool Interactable => _interactable;

    public virtual void SetState(bool on)
    {
         _state = on;
    }

    public virtual void SetInteractable(bool interactable)
    {
        _interactable = interactable;
    }

    protected void OnUserSwitch()
    {
        if (!_interactable)
            return;

        UserAttemptSwitch?.Invoke();
    }
}