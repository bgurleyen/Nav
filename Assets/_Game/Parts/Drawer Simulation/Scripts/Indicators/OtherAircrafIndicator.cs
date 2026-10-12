using TMPro;
using UnityEngine;

public class OtherAircrafIndicator : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private SpriteRenderer background;

    private void Awake()
    {
        if (background != null)
        {
            return;
        }

        background = GetComponentInChildren<SpriteRenderer>(true);
    }

    public void Init(string text, Color color, float backgroundAlpha = 1f, float fontSize = 36f)
    {
        if (label != null)
        {
            label.gameObject.SetActive(true);
            label.enabled = true;
            label.text = text;
            label.color = color;
            label.alpha = 1f;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableWordWrapping = false;
            label.outlineWidth = 0.2f;
            label.outlineColor = Color.black;
        }

        if (background == null)
            return;

        background.enabled = backgroundAlpha > 0.01f;
        var bgColor = background.color;
        bgColor.a = Mathf.Clamp01(backgroundAlpha);
        background.color = bgColor;
    }
}
