using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;


public class SetLetersToButton : MonoBehaviour
{

    [ButtonInspector]
    void SetAtoZData()
    {
        var a = 'A';
        foreach (Transform item in transform)
        {
            Button b = item.GetComponent<Button>();
            Text t = item.GetComponentInChildren<Text>();
            t.text = a.ToString();
            a++;
        }
    }
}
