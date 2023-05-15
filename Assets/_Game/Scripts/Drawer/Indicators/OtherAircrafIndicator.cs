using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class OtherAircrafIndicator : MonoBehaviour
{
    [SerializeField] private TMP_Text label;


    public void Init(string text, Color color)
    {
        label.text = text;
        label.color = color;
    }
}
