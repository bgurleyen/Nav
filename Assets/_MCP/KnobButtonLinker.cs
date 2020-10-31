using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class KnobButtonLinker : MonoBehaviour
{
    [SerializeField] Transform rotatingMesh;
    [SerializeField] float stepDegrees = 5;
    [SerializeField] bool isPositive;

    Sequence seq;
    LongPressEventTrigger button;

    void Awake()
    {
        button = GetComponent<LongPressEventTrigger>();
        button.onClick.AddListener(ButtonClick);
    }


    void ButtonClick()
    {
        var _degrees = isPositive ? stepDegrees : -stepDegrees;
        seq?.Kill();
        seq = DOTween.Sequence()
            .Append(rotatingMesh.DOLocalRotate(Vector3.up * _degrees, 0.15f, RotateMode.LocalAxisAdd));
    }
    
}
