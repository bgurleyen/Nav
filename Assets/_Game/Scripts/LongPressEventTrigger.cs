using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class LongPressEventTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("How long must pointer be down on this object to trigger a long press")]
    [SerializeField]
    private float holdTime = 0.7f;
    [SerializeField] private float repeatTime = 0.1f;

    public UnityEvent onClick = new UnityEvent();

    public void CopyTimingFrom(LongPressEventTrigger other)
    {
        if (other == null)
            return;
        holdTime = other.holdTime;
        repeatTime = other.repeatTime;
    }

    private bool isDown;
    private bool isHeld;
    private bool clickCancelled;
    private float lastExecute;

    /// <summary>
    /// Drop the press that is in progress so pointer-up does not also count as a click.
    /// Used when a drag on a knob takes over the gesture.
    /// </summary>
    public void CancelPendingClick()
    {
        clickCancelled = true;
        isDown = false;
        isHeld = false;
    }

    private void Update()
    {
        if (isDown)
        {
            if (!isHeld)
            {
                if (Time.time - lastExecute > holdTime)
                {
                    Execute();
                    isHeld = true;
                }
            }
            else
            {
                if (Time.time - lastExecute > repeatTime)
                {
                    Execute();
                }
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        clickCancelled = false;
        isDown = true;
        isHeld = false;
        lastExecute = Time.time;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isHeld && !clickCancelled)
            Execute();
        isDown = false;
        isHeld = false;
        clickCancelled = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isDown = false;
        isHeld = false;
    }

    private void Execute()
    {
        onClick?.Invoke();
        lastExecute = Time.time;
    }
}