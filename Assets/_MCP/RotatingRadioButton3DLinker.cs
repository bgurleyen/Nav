using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class RotatingRadioButton3DLinker : MonoBehaviour
{
    [SerializeField] Transform _mesh3D;
    [SerializeField] Vector3 _rotateDirection;
    [SerializeField] int _minAngle;
    [SerializeField] int _stepAngle;
    [SerializeField] int _maxSteps = 3;
    [SerializeField] float _stepDuration = 0.2f;
    [SerializeField] bool _preserveInitialRotation;
    [SerializeField] FloatUnityEvent _changeCallback;

    Button _button;
    int _currentValue;
    Sequence _rotationSeq;
    Quaternion _initialRotation;

    Quaternion GetRotation =>
        (_preserveInitialRotation ? _initialRotation : Quaternion.identity) *
        Quaternion.Euler(_rotateDirection * (_minAngle + _stepAngle * _currentValue));
    
    public void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(ButtonPress);
        _initialRotation = _mesh3D.localRotation;
    }

    void Start()
    {
        _currentValue = 0;
        _mesh3D.localRotation = GetRotation;
    }

    void ButtonPress()
    {
        _currentValue = (_currentValue + 1) % _maxSteps;

        _rotationSeq?.Kill();
        _rotationSeq = DOTween.Sequence()
            .Append(_mesh3D.DOLocalRotateQuaternion(GetRotation, _stepDuration).SetEase(Ease.Linear));
        
        
        _changeCallback?.Invoke(_currentValue);
    }
}
