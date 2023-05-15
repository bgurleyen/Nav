using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LabelText : MonoBehaviour
{
    public TMP_Text label;

    public void Init(string text, Color? color = null)
    {
        label.text = text;
        label.color = color ?? Color.white;
    }
}
