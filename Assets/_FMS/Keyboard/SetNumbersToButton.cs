using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class SetNumbersToButton : MonoBehaviour
{

    [ButtonInspector]
    void Set1to0Data()
    {
        foreach (Transform item in transform)
        {
            Button b = item.GetComponent<Button>();
            Text t = item.GetComponentInChildren<Text>();
            string s = b.gameObject.name;
            t.text = s[s.Length - 1].ToString();
        }
    }
}
