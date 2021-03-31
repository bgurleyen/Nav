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
    [SerializeField] Vector3 _rotatingAxis = Vector3.up;

    Sequence seq;
    LongPressEventTrigger button;

    void Awake()
    {
        button = GetComponent<LongPressEventTrigger>();
        button.onClick.AddListener(ButtonClick);
    }


    void ButtonClick()
    {
        var degrees = isPositive ? stepDegrees : -stepDegrees;
        seq?.Kill();
        seq = DOTween.Sequence()
            .Append(rotatingMesh.DOLocalRotate(_rotatingAxis * degrees, 0.15f, RotateMode.LocalAxisAdd));
    }
    
}
