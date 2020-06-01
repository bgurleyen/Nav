using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class LongPressEventTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField]
    [Tooltip("How long must pointer be down on this object to trigger a long press")]
    private float holdTime = 1f;

    // Remove all comment tags (except this one) to handle the onClick event!
    public static bool held2 = false;
    private bool held = false;
    public UnityEvent onClick = new UnityEvent();

    public UnityEvent onLongPress = new UnityEvent();

    public void OnPointerDown(PointerEventData eventData)
    {
        held = false;
        held2 = held;
        Invoke("OnLongPress", holdTime);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        CancelInvoke("OnLongPress");
        if (!held) onClick.Invoke();
        held2 = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CancelInvoke("OnLongPress");
        held2 = false;
    }

    private void OnLongPress()
    {
        held = true;
        held2 = held;
        onLongPress.Invoke();
    }

}