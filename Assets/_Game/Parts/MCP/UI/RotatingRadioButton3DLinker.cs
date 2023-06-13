using System;
using DG.Tweening;
using Navigation;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class RotatingRadioButton3DLinker : MonoBehaviour
{
    [SerializeField] private Transform _mesh3D;
    [SerializeField] private Vector3 _rotateDirection;
    [SerializeField] private int _minAngle;
    [SerializeField] private int _stepAngle;
    [SerializeField] private int _maxSteps = 3;
    [SerializeField] private float _stepDuration = 0.2f;
    [SerializeField] private bool _preserveInitialRotation;
    
    public Action ChangeCallback;

    private Button _button;
    private int _currentValue;
    private Sequence _rotationSeq;
    private Quaternion _initialRotation;

    public int CurrentValue => _currentValue;
    public int MaxSteps => _maxSteps;

    public void Set(int value)
    {
        _currentValue = value;

        _rotationSeq?.Kill();
        _rotationSeq = DOTween.Sequence()
            .Append(_mesh3D.DOLocalRotateQuaternion(GetRotation, _stepDuration).SetEase(Ease.Linear));
    }

    private Quaternion GetRotation =>
        (_preserveInitialRotation ? _initialRotation : Quaternion.identity) *
        Quaternion.Euler(_rotateDirection * (_minAngle + _stepAngle * _currentValue));
    
    public void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(ButtonPress);
        _initialRotation = _mesh3D.localRotation;
    }

    private void Start()
    {
        _currentValue = 0;
        _mesh3D.localRotation = GetRotation;
    }

    private void ButtonPress()
    {
        ChangeCallback?.Invoke();
    }
}
public class RotatingButtonLinkedValue  
{
    private readonly RotatingRadioButton3DLinker _linkedRotatingButton;

    public static implicit operator int(RotatingButtonLinkedValue i) => i._linkedRotatingButton.CurrentValue;

    public RotatingButtonLinkedValue(RotatingRadioButton3DLinker rotatingButton, int initialState)
    {
        _linkedRotatingButton = rotatingButton;
        rotatingButton.ChangeCallback += Increment;
        Set(initialState);
    }

    public RotatingButtonLinkedValue(RotatingRadioButton3DLinker rotatingButton, Action onUserPress, int initialState )
    {
        _linkedRotatingButton = rotatingButton;
        rotatingButton.ChangeCallback += onUserPress;
        Set(initialState);
    }

    public void Set(int value)
    {
        _linkedRotatingButton.Set(value);
    }

    public void Increment()
    {
        var newValue = (_linkedRotatingButton.CurrentValue + 1) % _linkedRotatingButton.MaxSteps;
        Set(newValue);
    }

}
