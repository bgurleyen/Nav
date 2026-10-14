using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using MoreMountains.NiceVibrations;
using Navigation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SliderLever3DLinker : MonoBehaviour
{
    [SerializeField] private Transform _lever3D;
    [SerializeField] private Vector3 _rotateDirection;
    [SerializeField] private int _minAngle;
    [SerializeField] private int _maxAngle;
    [SerializeField] private float _stepDuration = 0.2f;
    [SerializeField] private bool _haptic = true;
    [SerializeField] private FloatUnityEvent _sliderCallback;

    private Sequence _leverSeq;
    private Slider _slider;
    private float _angleStep;
    private Quaternion _initialRotation;
    private float _cachedOldSliderValue;

    private void Awake()
    {
     
         _slider = GetComponent<Slider>();
        _slider.onValueChanged.AddListener(SliderChanged);
        if(_haptic) _slider.onValueChanged.AddListener(SliderChangedVibration);
        _angleStep = (_maxAngle - _minAngle) / _slider.maxValue;
        _initialRotation = _lever3D.localRotation;

        _lever3D.localRotation = _initialRotation *
                                 Quaternion.Euler(_rotateDirection * (_minAngle + _angleStep * -27));
        _cachedOldSliderValue = _slider.value;
    }

    private void Start()
    {
        _sliderCallback?.Invoke(_slider.value);
    }

    private void SliderChanged(float newValue)
    {
        _leverSeq?.Kill();
        _leverSeq = DOTween.Sequence()
            .Append(_lever3D.DOLocalRotateQuaternion(_initialRotation * Quaternion.Euler( _rotateDirection * (_minAngle + _angleStep * newValue)), 
                _stepDuration * Mathf.Abs(_cachedOldSliderValue - newValue)).SetEase(Ease.Linear))
            .SetLink(_lever3D.gameObject);
        _cachedOldSliderValue = newValue;
        
        _sliderCallback?.Invoke(_slider.value);
    }

    private void SliderChangedVibration(float newValue)
    {
        MMVibrationManager.Haptic(HapticTypes.HeavyImpact);
    }

    private void OnDestroy()
    {
        _leverSeq?.Kill();
    }
}
