using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

[ExecuteInEditMode]
public class SetNumbersToButton : MonoBehaviour
{
    void Awake()
    {
        Set1to0Data();
    }

    [ButtonInspector]
    void Set1to0Data()
    {
        int _i = 1;
        foreach (Transform item in transform)
        {
            Button b = item.GetComponent<Button>();
            
            var t = item.GetComponentInChildren<TMP_Text>();
            if (_i == 10)
            {
                b.name = $"Button .";
                t.text = Keys.Decimal;
            }
            else if (_i == 11)
            {
                b.name = $"Button 0";
                t.text = "0";
            }
            else if (_i == 12)
            {
                b.name = $"Button +-";
                t.text = Keys.Sign;
            }
            else
            {
                b.name = $"Button {_i}";
                t.text = _i.ToString();
            }

            _i++;
        }
    }
}

