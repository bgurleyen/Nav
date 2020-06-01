using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LabelText : MonoBehaviour
{
    public TMP_Text label;

    public void Init(string name)
    {
        label.text = name;
    }
}
