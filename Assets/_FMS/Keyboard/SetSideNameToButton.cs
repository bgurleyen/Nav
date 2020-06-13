using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;


[ExecuteInEditMode]
public class SetSideNameToButton : MonoBehaviour
{
    [SerializeField] bool isLeft;
    
    void Awake()
    {
        SetData();
    }

    [ButtonInspector]
    void SetData()
    {
        var _i = 1;
        foreach (var _item in GetComponentsInChildren<SideButton>())
        {
            _item.name = $"{_i * (isLeft ? 1 : -1)}";
            _i++;
        }
    }
}
