using UnityEngine;

/// <summary>
/// Letterboxes/pillarboxes the game camera to a fixed aspect (default 16:9).
/// A black strip half an FMC key tall stays under the frame so the bottom keys are not on the screen edge.
/// The 16:9 picture keeps its width and is squeezed vertically into the area above that strip.
/// Camera.aspect follows that shorter rect; a locked 16:9 aspect makes Unity narrow the
/// rendered width and clip a few millimeters off each side.
/// A full-screen clear camera paints the bars black.
/// </summary>
[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class AspectRatioLetterbox : MonoBehaviour
{
    const string ClearCameraChildName = "Letterbox Clear Camera";
    const float ClearCameraDepthOffset = 100f;

    /// <summary>FMC keypad key height inside the 1920×1080 HUD (small-key cell).</summary>
    const float FmsButtonReferenceHeight = 63.6f;
    const float HudReferenceHeight = 1080f;

    [SerializeField] float aspectWidth = 16f;
    [SerializeField] float aspectHeight = 9f;
    [SerializeField] Camera _clearCamera;

    Camera _gameCamera;
    Vector2Int _lastScreenSize;

    public float TargetAspect => aspectWidth / aspectHeight;

    public static Rect GetViewportPixelRect(Camera camera)
    {
        if (camera == null)
        {
            return new Rect(0f, 0f, Screen.width, Screen.height);
        }

        var pixelW = camera.pixelWidth;
        var pixelH = camera.pixelHeight;
        if (pixelW <= 0 || pixelH <= 0)
        {
            var normalized = camera.rect;
            pixelW = Mathf.Max(1, Mathf.RoundToInt(normalized.width * Screen.width));
            pixelH = Mathf.Max(1, Mathf.RoundToInt(normalized.height * Screen.height));
            return new Rect(
                normalized.x * Screen.width,
                normalized.y * Screen.height,
                pixelW,
                pixelH);
        }

        return new Rect(
            camera.rect.x * Screen.width,
            camera.rect.y * Screen.height,
            pixelW,
            pixelH);
    }

    void Awake()
    {
        _gameCamera = GetComponent<Camera>();
        ResolveClearCamera();
        Apply();
    }

    void Update()
    {
        if (_lastScreenSize.x != Screen.width || _lastScreenSize.y != Screen.height)
        {
            Apply();
            return;
        }

        if (_gameCamera != null)
        {
            _gameCamera.aspect = ViewportPixelAspect(_gameCamera.rect, Screen.width, Screen.height);
        }
    }

    void ResolveClearCamera()
    {
        if (_clearCamera == null)
        {
            var child = transform.Find(ClearCameraChildName);
            if (child != null)
            {
                _clearCamera = child.GetComponent<Camera>();
            }
        }

        if (_clearCamera == null)
        {
            var go = new GameObject(ClearCameraChildName);
            go.transform.SetParent(transform, false);
            _clearCamera = go.AddComponent<Camera>();
        }

        ConfigureClearCamera();
    }

    void ConfigureClearCamera()
    {
        if (_clearCamera == null)
        {
            return;
        }

        _clearCamera.transform.localPosition = Vector3.zero;
        _clearCamera.transform.localRotation = Quaternion.identity;
        _clearCamera.depth = _gameCamera.depth - ClearCameraDepthOffset;
        _clearCamera.clearFlags = CameraClearFlags.SolidColor;
        _clearCamera.backgroundColor = Color.black;
        _clearCamera.cullingMask = 0;
        _clearCamera.rect = new Rect(0f, 0f, 1f, 1f);
        _clearCamera.usePhysicalProperties = false;
        _clearCamera.allowHDR = false;
        _clearCamera.allowMSAA = false;
        _clearCamera.targetDisplay = _gameCamera.targetDisplay;
    }

    void Apply()
    {
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        var targetAspect = aspectWidth / aspectHeight;
        var viewport = ComputeNormalizedViewportRect(
            Screen.width,
            Screen.height,
            targetAspect);
        _gameCamera.rect = viewport;
        _gameCamera.usePhysicalProperties = false;
        _gameCamera.ResetProjectionMatrix();
        // Match the shortened rect. Locking 16:9 here shrinks the drawn width and crops the sides.
        _gameCamera.aspect = ViewportPixelAspect(viewport, Screen.width, Screen.height);

        ConfigureClearCamera();
    }

    /// <summary>
    /// Half an FMC key, as a fraction of the 16:9 frame height. The frame is scaled down until the
    /// black strip under it matches this fraction of the frame that is actually on screen.
    /// </summary>
    public static float BottomGapOverContentHeight =>
        0.5f * FmsButtonReferenceHeight / HudReferenceHeight;

    /// <summary>
    /// Uses landscape dimensions so ultrawide / swapped Screen.width×height still pillarbox correctly.
    /// The frame is then pinned to the top, leaving a black strip at the bottom.
    /// </summary>
    public static Rect ComputeNormalizedViewportRect(int screenWidth, int screenHeight, float targetAspect)
    {
        var fitted = FitLandscapeAspect(screenWidth, screenHeight, targetAspect);
        return InsetBottomGap(fitted, screenWidth, screenHeight, BottomGapOverContentHeight);
    }

    static float ViewportPixelAspect(Rect normalized, int screenWidth, int screenHeight)
    {
        var width = normalized.width * screenWidth;
        var height = normalized.height * screenHeight;
        if (width <= 0f || height <= 0f)
        {
            return 16f / 9f;
        }

        return width / height;
    }

    static Rect FitLandscapeAspect(int screenWidth, int screenHeight, float targetAspect)
    {
        if (screenWidth <= 0 || screenHeight <= 0 || targetAspect <= 0f)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        var landscapeWidth = Mathf.Max(screenWidth, screenHeight);
        var landscapeHeight = Mathf.Min(screenWidth, screenHeight);
        var windowAspect = (float)landscapeWidth / landscapeHeight;

        if (windowAspect > targetAspect)
        {
            var width = targetAspect / windowAspect;
            return new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }

        var height = windowAspect / targetAspect;
        return new Rect(0f, (1f - height) * 0.5f, 1f, height);
    }

    static Rect InsetBottomGap(Rect fitted, int screenWidth, int screenHeight, float gapOverContent)
    {
        if (screenWidth <= 0 || screenHeight <= 0 || gapOverContent <= 0f)
        {
            return fitted;
        }

        var contentW = fitted.width * screenWidth;
        var contentH = fitted.height * screenHeight;
        if (contentW <= 0f || contentH <= 0f)
        {
            return fitted;
        }

        var maxContentH = screenHeight / (1f + gapOverContent);
        if (contentH > maxContentH)
        {
            contentH = maxContentH;
        }

        var x = fitted.x * screenWidth;
        var y = screenHeight - contentH;
        return new Rect(
            x / screenWidth,
            y / screenHeight,
            contentW / screenWidth,
            contentH / screenHeight);
    }
}
