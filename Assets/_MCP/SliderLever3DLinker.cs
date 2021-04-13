using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SliderLever3DLinker : MonoBehaviour
{
    [SerializeField] Transform _lever3D;
    [SerializeField] Vector3 _rotateDirection;
    [SerializeField] int _minAngle;
    [SerializeField] int _maxAngle;
    [SerializeField] float _stepDuration = 0.2f;
    [SerializeField] FloatUnityEvent _sliderCallback;

    Sequence _leverSeq;
    Slider _slider;
    float _angleStep;
    Quaternion _initialRotation;
    float _cachedOldSliderValue;

    void Awake()
    {
        _slider = GetComponent<Slider>();
        _slider.onValueChanged.AddListener(SliderChanged);
        _angleStep = (_maxAngle - _minAngle) / _slider.maxValue;
        _initialRotation = _lever3D.localRotation;

        _lever3D.localRotation = _initialRotation *
                                 Quaternion.Euler(_rotateDirection * (_minAngle + _angleStep * _slider.value));
        _cachedOldSliderValue = _slider.value;
        _sliderCallback?.Invoke(_slider.value);
    }

    void SliderChanged(float newValue)
    {
        _leverSeq?.Kill();
        _leverSeq = DOTween.Sequence()
            .Append(_lever3D.DOLocalRotateQuaternion(_initialRotation * Quaternion.Euler( _rotateDirection * (_minAngle + _angleStep * newValue)), 
                _stepDuration * Mathf.Abs(_cachedOldSliderValue - newValue)).SetEase(Ease.Linear));
        _cachedOldSliderValue = newValue;
        
        _sliderCallback?.Invoke(_slider.value);
    }
}
