using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;


[ExecuteInEditMode]
public class SetLetersToButton : MonoBehaviour
{
    void Awake()
    {
        SetAtoZData();
    }

    [ButtonInspector]
    void SetAtoZData()
    {
        var a = 'A';
        foreach (Transform item in transform)
        {
            var  _b = item.GetComponent<Button>();
            var  _t = item.GetComponentInChildren<TMP_Text>();

            if (a == 'Z' + 1)
            {
                _t.text = Keys.Sp;
                _b.name = "Button SP";
            }
            else if(a == 'Z' + 2)
            {
                _t.text = Keys.Del;
                _b.name = "Button DEL";
            }
            else if(a == 'Z' + 3)
            {
                _t.text = Keys.Slash;
                _b.name = "Button /";
            }
            else if(a == 'Z' + 4)
            {
                _t.text = Keys.Clr;
                _b.name = "Button CLR";
            }
            else
            {
                _t.text = a.ToString();
                _b.name = $"Button {a}";
            }
            
            a++;
        }
    }
}
