using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drag on a turn knob changes the same value as its left and right presses.
/// Right or up increases, left or down decreases. A short tap still clicks.
/// </summary>
public class KnobDragInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    const int MaxStepsPerEvent = 24;

    LongPressEventTrigger negative;
    LongPressEventTrigger positive;
    LongPressEventTrigger press;
    Vector2 total;
    float bank;
    int axis;

    public static void Attach(LongPressEventTrigger host, LongPressEventTrigger negative, LongPressEventTrigger positive)
    {
        if (host == null)
            return;

        var drag = host.GetComponent<KnobDragInput>();
        if (drag == null)
            drag = host.gameObject.AddComponent<KnobDragInput>();
        drag.negative = negative;
        drag.positive = positive;
        drag.press = host;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        total = Vector2.zero;
        bank = 0f;
        axis = 0;
        press?.CancelPendingClick();
    }

    public void OnDrag(PointerEventData eventData)
    {
        press?.CancelPendingClick();
        total += eventData.delta;

        if (axis == 0)
        {
            if (total.magnitude < 8f)
                return;
            axis = Mathf.Abs(total.y) > Mathf.Abs(total.x) * 1.25f ? 2 : 1;
            bank = axis == 1 ? total.x : total.y;
        }
        else
        {
            bank += axis == 1 ? eventData.delta.x : eventData.delta.y;
        }

        var frame = axis == 1 ? eventData.delta.x : eventData.delta.y;
        Consume(frame);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        total = Vector2.zero;
        bank = 0f;
        axis = 0;
    }

    void Consume(float frameDelta)
    {
        var step = PixelsPerStep();
        if (Mathf.Abs(frameDelta) > step * 1.5f)
            step *= 0.55f;

        var guard = 0;
        while (bank >= step && guard < MaxStepsPerEvent)
        {
            bank -= step;
            positive?.onClick.Invoke();
            guard++;
        }

        while (bank <= -step && guard < MaxStepsPerEvent)
        {
            bank += step;
            negative?.onClick.Invoke();
            guard++;
        }
    }

    static float PixelsPerStep()
    {
        var dpi = Screen.dpi;
        if (dpi < 10f)
            dpi = 160f;
        return Mathf.Clamp(dpi * 0.11f, 14f, 70f);
    }
}
