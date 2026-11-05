using UnityEngine;

/// <summary>
/// Letterboxes/pillarboxes the game camera to a fixed aspect (default 16:9), centered on screen.
/// A dedicated full-screen clear camera (child or assigned reference) paints black bars outside the viewport.
/// </summary>
[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class AspectRatioLetterbox : MonoBehaviour
{
    const string ClearCameraChildName = "Letterbox Clear Camera";
    const float ClearCameraDepthOffset = 100f;

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
        if (_lastScreenSize.x == Screen.width && _lastScreenSize.y == Screen.height)
        {
            return;
        }

        Apply();
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
        _gameCamera.rect = ComputeNormalizedViewportRect(
            Screen.width,
            Screen.height,
            targetAspect);

        ConfigureClearCamera();
    }

    /// <summary>
    /// Uses landscape dimensions so ultrawide / swapped Screen.width×height still pillarbox correctly.
    /// </summary>
    public static Rect ComputeNormalizedViewportRect(int screenWidth, int screenHeight, float targetAspect)
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
}
