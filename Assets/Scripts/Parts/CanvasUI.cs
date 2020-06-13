using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasUI : MonoBehaviour
{
    [SerializeField] Animator animatorFMV;

    public void OnExpandFMVClick()
    {
        animatorFMV.SetTrigger("ExpandFMV");
    }

    public void OnRetractFMVClick()
    {
        animatorFMV.SetTrigger("RetractFMV");
    }
}
