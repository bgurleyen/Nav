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

    public void Init(string text, Color color, float backgroundAlpha = 1f)
    {
        if (label != null)
        {
            label.gameObject.SetActive(true);
            label.enabled = true;
            label.text = text;
            label.color = color;
            label.alpha = 1f;
            label.fontStyle = FontStyles.Bold;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableWordWrapping = false;
            label.outlineWidth = 0.25f;
            label.outlineColor = Color.white;
        }

        if (background == null)
        {
            return;
        }

        var bgColor = background.color;
        bgColor.a = Mathf.Clamp01(backgroundAlpha);
        background.color = bgColor;
    }
}
