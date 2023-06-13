using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ToggleButtonToggleLinker : ToggleButtonLinker, IPointerUpHandler, IPointerDownHandler
{
    [SerializeField] private GameObject _tick;

    public override void SetState(bool on)
    {
        base.SetState(on);

        _tick.SetActive(on); 
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        OnUserSwitch();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
    }
}
