using UnityEngine;
using UnityEngine.EventSystems;

public class ToggleButtonToggleLinker : ToggleButtonLinker, IPointerUpHandler, IPointerDownHandler
{
    [SerializeField] private GameObject _tick;
    [SerializeField] private GameObject _tick_normal;

    public override void SetState(bool on)
    {
        base.SetState(on);

        _tick.SetActive(on);
        _tick_normal.SetActive(!on); 
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        OnUserSwitch();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
    }
}
