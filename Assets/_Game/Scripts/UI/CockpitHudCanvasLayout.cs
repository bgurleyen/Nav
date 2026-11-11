using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Groups canvas children under a 1920×1080 HUD root and stretches that root to the
/// game-camera viewport, including the shortened frame left by <see cref="AspectRatioLetterbox"/>.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class CockpitHudCanvasLayout : MonoBehaviour
{
    const string HudRootName = "HudLayoutRoot";

    [SerializeField] Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] float canvasScalerMatch = 0.5f;

    RectTransform _canvasRect;
    Canvas _canvas;
    RectTransform _hudRoot;
    void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _canvasRect = (RectTransform)transform;

        var scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.matchWidthOrHeight = canvasScalerMatch;
        }

        EnsureHudRoot();
        ApplyLayout();
    }

    void LateUpdate()
    {
        ApplyLayout();
    }

    void EnsureHudRoot()
    {
        var existing = _canvasRect.Find(HudRootName);
        if (existing != null)
        {
            _hudRoot = (RectTransform)existing;
            return;
        }

        var rootGo = new GameObject(HudRootName, typeof(RectTransform));
        _hudRoot = rootGo.GetComponent<RectTransform>();
        _hudRoot.SetParent(_canvasRect, false);
        _hudRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _hudRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _hudRoot.pivot = new Vector2(0.5f, 0.5f);
        _hudRoot.sizeDelta = referenceResolution;
        _hudRoot.localScale = Vector3.one;
        _hudRoot.anchoredPosition = Vector2.zero;

        var toMove = new Transform[_canvasRect.childCount];
        for (var i = 0; i < toMove.Length; i++)
        {
            toMove[i] = _canvasRect.GetChild(i);
        }

        foreach (var child in toMove)
        {
            if (child == _hudRoot)
            {
                continue;
            }

            child.SetParent(_hudRoot, false);
        }
    }

    void ApplyLayout()
    {
        if (_hudRoot == null)
        {
            return;
        }

        var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;
        _hudRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _hudRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _hudRoot.pivot = new Vector2(0.5f, 0.5f);
        _hudRoot.sizeDelta = referenceResolution;

        var viewportPixels = AspectRatioLetterbox.GetViewportPixelRect(cam);

        var letterbox = cam != null ? cam.GetComponent<AspectRatioLetterbox>() : null;
        if (letterbox != null)
        {
            _hudRoot.localScale = ScaleToPixels(viewportPixels.width, viewportPixels.height);
            _hudRoot.anchoredPosition = Vector2.zero;
            return;
        }

        var viewportWidth = viewportPixels.width;
        var viewportHeight = viewportPixels.height;
        var refAspect = referenceResolution.x / referenceResolution.y;
        var viewportAspect = viewportWidth / Mathf.Max(1f, viewportHeight);

        float fitWidth;
        float fitHeight;
        if (viewportAspect > refAspect)
        {
            fitHeight = viewportHeight;
            fitWidth = fitHeight * refAspect;
        }
        else
        {
            fitWidth = viewportWidth;
            fitHeight = fitWidth / refAspect;
        }

        var scale = ScaleToPixels(fitWidth, fitHeight);
        var uniformScale = Mathf.Min(scale.x, scale.y);
        _hudRoot.localScale = new Vector3(uniformScale, uniformScale, 1f);

        var viewportCenter = new Vector2(
            viewportPixels.x + viewportPixels.width * 0.5f,
            viewportPixels.y + viewportPixels.height * 0.5f);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, viewportCenter, cam, out var localCenter))
        {
            _hudRoot.anchoredPosition = localCenter;
        }
    }

    Vector3 ScaleToPixels(float pixelWidth, float pixelHeight)
    {
        var scaleFactor = _canvas != null ? _canvas.rootCanvas.scaleFactor : 1f;
        if (scaleFactor <= 0f)
        {
            scaleFactor = 1f;
        }

        var scaleX = pixelWidth / (referenceResolution.x * scaleFactor);
        var scaleY = pixelHeight / (referenceResolution.y * scaleFactor);
        return new Vector3(scaleX, scaleY, 1f);
    }
}
