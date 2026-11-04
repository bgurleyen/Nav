using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wraps all canvas children in a centered 16:9 HUD rect inside the iOS safe area
/// so side panels do not stick to ultra-wide phone edges (e.g. iPhone 12 mini landscape).
/// Keeps a fixed 1920×1080 layout space and scales it to fit — does not shrink the layout rect.
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
    Rect _lastSafeArea;
    Vector2Int _lastScreenSize;

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

    void Update()
    {
        if (_lastSafeArea == Screen.safeArea &&
            _lastScreenSize.x == Screen.width &&
            _lastScreenSize.y == Screen.height)
        {
            return;
        }

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

        _lastSafeArea = Screen.safeArea;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        var safe = Screen.safeArea;
        var refAspect = referenceResolution.x / referenceResolution.y;
        var safeAspect = safe.width / safe.height;

        float fitWidth;
        float fitHeight;
        if (safeAspect > refAspect)
        {
            fitHeight = safe.height;
            fitWidth = fitHeight * refAspect;
        }
        else
        {
            fitWidth = safe.width;
            fitHeight = fitWidth / refAspect;
        }

        _hudRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _hudRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _hudRoot.pivot = new Vector2(0.5f, 0.5f);
        _hudRoot.sizeDelta = referenceResolution;

        var scaleFactor = _canvas != null ? _canvas.rootCanvas.scaleFactor : 1f;
        if (scaleFactor <= 0f)
        {
            scaleFactor = 1f;
        }

        var scaleX = fitWidth / (referenceResolution.x * scaleFactor);
        var scaleY = fitHeight / (referenceResolution.y * scaleFactor);
        var uniformScale = Mathf.Min(scaleX, scaleY);
        _hudRoot.localScale = new Vector3(uniformScale, uniformScale, 1f);

        var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;
        var safeCenter = new Vector2(safe.x + safe.width * 0.5f, safe.y + safe.height * 0.5f);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, safeCenter, cam, out var localCenter))
        {
            _hudRoot.anchoredPosition = localCenter;
        }
    }
}
