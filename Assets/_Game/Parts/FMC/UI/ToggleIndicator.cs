using Navigation;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ToggleIndicator : MonoBehaviour
{
    [FormerlySerializedAs("light")]
    public Toggle lightToggle;

    System.Collections.IEnumerator Start()
    {
        while (true)
        {
            lightToggle.isOn = Session.IsMod;
            yield return new WaitForSeconds(0.2f);
        }
    }
}
