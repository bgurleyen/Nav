using Gamelogic.Extensions;
using TMPro;
using UnityEngine;

public class MainScreen : Singleton<MainScreen>
{
    [SerializeField] private Color modified;
    
    [Header("top header")]
    [SerializeField] private BgText leftInfo1;
    [SerializeField] private TMP_Text leftInfo2;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text pageNumber;

    

    [Header("scratch pad")]
    [SerializeField]
    public BgText scratchPadText;

    [Header("LCD swipe resize")]
    [SerializeField] float minSwipeDistancePixels = 60f;
    [SerializeField] CanvasUI canvasUI;

    public static BgText Pad => Instance != null ? Instance.scratchPadText : null;

    RectTransform _lcdRect;
    Camera _eventCamera;
    bool _swipeTracking;
    Vector2 _swipeStartScreenPos;
    int _swipeTrackingPointerId = -1;

    void Awake()
    {
        _lcdRect = transform as RectTransform;
        if (canvasUI == null)
            canvasUI = FindObjectOfType<CanvasUI>();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            _eventCamera = canvas.worldCamera;
    }

    void Update()
    {
        if (_lcdRect == null || canvasUI == null)
            return;

        if (Input.touchCount > 0)
        {
            for (var i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        TryBeginSwipe(touch.position, touch.fingerId);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        TryEndSwipe(touch.position, touch.fingerId);
                        break;
                }
            }

            return;
        }

        if (Input.GetMouseButtonDown(0))
            TryBeginSwipe(Input.mousePosition, -1);
        else if (Input.GetMouseButtonUp(0))
            TryEndSwipe(Input.mousePosition, -1);
    }

    void TryBeginSwipe(Vector2 screenPos, int pointerId)
    {
        if (_swipeTracking || !IsScreenPointInsideLcd(screenPos))
            return;

        _swipeTracking = true;
        _swipeTrackingPointerId = pointerId;
        _swipeStartScreenPos = screenPos;
    }

    void TryEndSwipe(Vector2 screenPos, int pointerId)
    {
        if (!_swipeTracking || pointerId != _swipeTrackingPointerId)
            return;

        _swipeTracking = false;
        _swipeTrackingPointerId = -1;

        var delta = screenPos - _swipeStartScreenPos;
        if (Mathf.Abs(delta.y) < minSwipeDistancePixels)
            return;
        if (Mathf.Abs(delta.y) <= Mathf.Abs(delta.x))
            return;

        if (delta.y > 0f)
            canvasUI.SwipeExpandFmc();
        else
            canvasUI.SwipeRetractFmc();
    }

    bool IsScreenPointInsideLcd(Vector2 screenPos)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(_lcdRect, screenPos, _eventCamera);
    }
    


    public void UpdatePageInfo( bool isMod = false, string firstInfo = "", string pageTitle = "", string secondInfo = "",int currentPage = -1, int totalPages = -1)
    {
        SetHeaderText(leftInfo2, secondInfo);
        SetHeaderText(pageNumber, currentPage == -1 ? "" : $"{currentPage + 1}/{totalPages}");

        if (leftInfo1 == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(firstInfo))
        {
            leftInfo1.Clear();
        }
        else if (isMod)
        {
            leftInfo1.SetAsModified(firstInfo);
        }
        else
        {
            leftInfo1.SetAsDefault(firstInfo);
        }

        SetHeaderText(title, pageTitle);
    }

    private static void SetHeaderText(TMP_Text tmp, string text)
    {
        if (tmp == null)
        {
            return;
        }

        tmp.text = text ?? "";
        tmp.ForceMeshUpdate(true);
    }

   

    
    public static class Keywords
    {
        public const string DELETE = "DELETE";
        public const string ERASE = "ERASE";
        public const string STEP = "STEP";
        public const string ResetMcpAltitude = "RESET MCP ALTITUDE";
        public const string DragRequired = "DRAG REQUIRED";
    }
}
