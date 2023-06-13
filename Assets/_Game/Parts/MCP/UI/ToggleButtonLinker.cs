using System;
using UnityEngine;

public class ToggleButtonLinker : MonoBehaviour
{
    public Action UserAttemptSwitch;

    private bool _state;

    public bool State => _state;

    public virtual void SetState(bool on)
    {
         _state = on;
    }

    protected void OnUserSwitch()
    {
        UserAttemptSwitch?.Invoke();
    }
}