using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class LongPressEventTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("How long must pointer be down on this object to trigger a long press")]
    [SerializeField] float holdTime = 0.7f;
    [SerializeField] float repeatTime = 0.1f;

    public UnityEvent onClick = new UnityEvent();

    bool isDown;
    bool isHeld;
    float lastExecute;

    void Update()
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
        isDown = true;
        lastExecute = Time.time;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isHeld)
        {
            Execute();
        }
        isDown = false;
        isHeld = false;
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