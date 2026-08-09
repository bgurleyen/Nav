using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class KnobButtonLinker : MonoBehaviour
{
    [SerializeField] private Transform rotatingMesh;
    [SerializeField] private float stepDegrees = 5;
    [SerializeField] private bool isPositive;
    [SerializeField] private Vector3 _rotatingAxis = Vector3.up;

    private Sequence seq;
    private LongPressEventTrigger button;

    private void Awake()
    {
        button = GetComponent<LongPressEventTrigger>();
        button.onClick.AddListener(ButtonClick);
    }


    private void ButtonClick()
    {
        var degrees = isPositive ? stepDegrees : -stepDegrees;
        seq?.Kill();
        seq = DOTween.Sequence()
            .Append(rotatingMesh.DOLocalRotate(_rotatingAxis * degrees, 0.15f, RotateMode.LocalAxisAdd))
            .SetLink(rotatingMesh.gameObject);
    }

    private void OnDestroy()
    {
        seq?.Kill();
    }
}
