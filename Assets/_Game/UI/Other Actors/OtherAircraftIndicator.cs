using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Navigation
{

    public class OtherAircraftIndicator : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        public void Init(string text, Color color)
        {
            _label.text = text;
            _label.color = color;
        }
    }
}