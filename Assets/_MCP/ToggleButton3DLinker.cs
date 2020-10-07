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
    [SerializeField] Renderer indicatorMesh;
    [SerializeField] Material onMat;
    [SerializeField] Material offMat;
    [SerializeField] AnimatorController meshAnimatorController;

    Material[] materials;
    Animator meshAnimator;

    void Awake()
    {
        materials = indicatorMesh.materials;
        var _toggle = GetComponent<Toggle>();
        _toggle.onValueChanged.AddListener(SetState);
        meshAnimator = indicatorMesh.gameObject.GetComponent<Animator>();
        if (meshAnimator == null)
        {
            meshAnimator = indicatorMesh.gameObject.AddComponent<Animator>();
            meshAnimator.runtimeAnimatorController = meshAnimatorController;
        }
        
    }

    void SetState(bool on)
    {
        materials[1] = on ? onMat : offMat;
        indicatorMesh.materials = materials;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        meshAnimator.SetTrigger("Pressed");
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        meshAnimator.SetTrigger("Normal");
    }
}
