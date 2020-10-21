using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class LongPressEventTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("How long must pointer be down on this object to trigger a long press")]
    [SerializeField] float holdTime = 1f;

    public UnityEvent onClick = new UnityEvent();

    bool isDown;
    bool isHeld;
    float lastExecute;

    void Update()
    {
        if (isDown)
        {
            if (Time.time - lastExecute > holdTime)
            {
                isHeld = true;
                Execute();
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDown = true;
        lastExecute = Time.time;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDown = false;
        isHeld = false;
        if (!isHeld)
        {
            Execute();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isDown = false;
        isHeld = false;
    }

    void Execute()
    {
        onClick?.Invoke();
        lastExecute = Time.time;
    }
}