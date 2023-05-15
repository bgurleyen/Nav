using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class ToggleButton3DLinker : MonoBehaviour, IPointerUpHandler, IPointerDownHandler
{
    [SerializeField] private Renderer indicatorMesh;
    [SerializeField] private Material onMat;
    [SerializeField] private Material offMat;
    [SerializeField] private AnimatorController meshAnimatorController;

    private Material[] _materials;
    private Animator _meshAnimator;

    private void Awake()
    {
        _materials = indicatorMesh.materials;
        var toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(SetState);
        _meshAnimator = indicatorMesh.gameObject.GetComponent<Animator>();
        if (_meshAnimator == null)
        {
            _meshAnimator = indicatorMesh.gameObject.AddComponent<Animator>();
            _meshAnimator.runtimeAnimatorController = meshAnimatorController;
        }
        
    }

    private void SetState(bool on)
    {
        _materials[1] = on ? onMat : offMat;
        indicatorMesh.materials = _materials;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _meshAnimator.SetTrigger("Pressed");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _meshAnimator.SetTrigger("Normal");
    }
}
