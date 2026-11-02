using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Opens TMP &lt;link="..."&gt; URLs on click.</summary>
public class TmpLinkOpener : MonoBehaviour, IPointerClickHandler
{
    TextMeshProUGUI _text;

    void Awake()
    {
        _text = GetComponent<TextMeshProUGUI>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_text == null)
            return;

        var camera = eventData.pressEventCamera;
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(_text, eventData.position, camera);
        if (linkIndex == -1)
            return;

        TMP_LinkInfo linkInfo = _text.textInfo.linkInfo[linkIndex];
        Application.OpenURL(linkInfo.GetLinkID());
    }
}
