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
        label.text = text;
        label.color = color;

        if (background == null)
        {
            return;
        }

        var bgColor = background.color;
        bgColor.a = Mathf.Clamp01(backgroundAlpha);
        background.color = bgColor;
    }
}
