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
    [SerializeField] private FloatUnityEvent _changeCallback;

    private Button _button;
    private int _currentValue;
    private Sequence _rotationSeq;
    private Quaternion _initialRotation;

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
        _currentValue = (_currentValue + 1) % _maxSteps;

        _rotationSeq?.Kill();
        _rotationSeq = DOTween.Sequence()
            .Append(_mesh3D.DOLocalRotateQuaternion(GetRotation, _stepDuration).SetEase(Ease.Linear));
        
        
        _changeCallback?.Invoke(_currentValue);
    }
}
