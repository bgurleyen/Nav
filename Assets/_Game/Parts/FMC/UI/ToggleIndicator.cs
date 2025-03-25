using Navigation;
using UnityEngine;
using UnityEngine.UI;

public class ToggleIndicator : MonoBehaviour
{
    public Toggle light;

    System.Collections.IEnumerator Start()
    {
        while (true)
        {
            light.isOn = Session.IsMod;
            yield return new WaitForSeconds(0.2f);
        }
    }
}
