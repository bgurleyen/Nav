using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class DebriefLayoutScaler : MonoBehaviour
{
    [SerializeField] RectTransform _layoutRoot;

    RectTransform _parentRect;

    void Awake()
    {
        _parentRect = transform as RectTransform;
        if (_layoutRoot == null && transform.childCount > 0)
            _layoutRoot = transform.GetChild(0) as RectTransform;
    }

    void OnEnable()
    {
        StartCoroutine(ApplyScaleWhenReady());
    }

    void OnRectTransformDimensionsChange()
    {
        ApplyScale();
    }

    public void SetLayoutRoot(RectTransform layoutRoot)
    {
        _layoutRoot = layoutRoot;
        StartCoroutine(ApplyScaleWhenReady());
    }

    IEnumerator ApplyScaleWhenReady()
    {
        for (int i = 0; i < 8; i++)
        {
            ApplyScale();
            if (_layoutRoot != null && _layoutRoot.localScale.sqrMagnitude > 0.0001f)
                yield break;

            Canvas.ForceUpdateCanvases();
            yield return null;
        }
    }

    void ApplyScale()
    {
        if (_layoutRoot == null)
            return;

        if (_parentRect == null)
            _parentRect = transform as RectTransform;

        float parentW = _parentRect.rect.width;
        float parentH = _parentRect.rect.height;
        if (parentW <= 1f || parentH <= 1f)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Rect pixelRect = canvas.pixelRect;
                parentW = pixelRect.width;
                parentH = pixelRect.height;
            }
        }

        if (parentW <= 1f || parentH <= 1f)
            return;

        float scale = Mathf.Min(
            parentW / DebriefLayoutSpec.RefWidth,
            parentH / DebriefLayoutSpec.RefHeight);

        _layoutRoot.localScale = new Vector3(scale, scale, 1f);
        _layoutRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _layoutRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _layoutRoot.pivot = new Vector2(0.5f, 0.5f);
        _layoutRoot.anchoredPosition = Vector2.zero;
        _layoutRoot.sizeDelta = new Vector2(DebriefLayoutSpec.RefWidth, DebriefLayoutSpec.RefHeight);
    }
}
