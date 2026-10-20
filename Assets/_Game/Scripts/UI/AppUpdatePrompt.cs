using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// On each launch, asks the store if a newer build is published.
/// Android uses Play's update info. iOS reads the public App Store version.
/// </summary>
public class AppUpdatePrompt : MonoBehaviour
{
    const int UpdateAvailable = 2;
    const int UpdateInProgress = 3;

    static bool _started;

#if UNITY_ANDROID
    AndroidJavaObject _manager;
    AndroidJavaObject _infoTask;
    SuccessListener _success;
    FailureListener _failure;
#endif
    string _storeUrl;
    volatile bool _updateAvailable;
    bool _dialogShown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (_started)
            return;
        _started = true;

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        var host = new GameObject(nameof(AppUpdatePrompt));
        DontDestroyOnLoad(host);
        host.AddComponent<AppUpdatePrompt>().Begin();
#endif
    }

    void Begin()
    {
#if UNITY_ANDROID
        BeginPlay();
#elif UNITY_IOS
        StartCoroutine(LookupAppStore());
#endif
    }

#if UNITY_ANDROID
    void BeginPlay()
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var factory = new AndroidJavaClass("com.google.android.play.core.appupdate.AppUpdateManagerFactory"))
            {
                _manager = factory.CallStatic<AndroidJavaObject>("create", activity);
            }

            _success = new SuccessListener(this);
            _failure = new FailureListener();
            _infoTask = _manager.Call<AndroidJavaObject>("getAppUpdateInfo");
            _infoTask.Call<AndroidJavaObject>("addOnSuccessListener", _success);
            _infoTask.Call<AndroidJavaObject>("addOnFailureListener", _failure);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AppUpdate] " + e.Message);
        }
    }
#endif

#if UNITY_IOS
    IEnumerator LookupAppStore()
    {
        string country = DeviceCountry();
        bool found = false;
        yield return LookupAppStore(country, result => found = result);
        if (!found && country != "us")
            yield return LookupAppStore("us", _ => { });
    }

    IEnumerator LookupAppStore(string country, Action<bool> found)
    {
        string url = "https://itunes.apple.com/lookup?bundleId="
            + UnityWebRequest.EscapeURL(Application.identifier)
            + "&country=" + country;

        using (var request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[AppUpdate] App Store lookup failed: " + request.error);
                found(false);
                yield break;
            }

            var parsed = JsonUtility.FromJson<AppStoreLookup>(request.downloadHandler.text);
            if (parsed == null || parsed.resultCount < 1 || parsed.results == null || parsed.results.Length == 0)
            {
                found(false);
                yield break;
            }

            var listing = parsed.results[0];
            found(true);
            if (!IsStoreNewer(listing.version, Application.version))
                yield break;

            _storeUrl = AppleStoreUrl(listing);
            MarkUpdateAvailable();
        }
    }

    static string DeviceCountry()
    {
        try
        {
            return RegionInfo.CurrentRegion.TwoLetterISORegionName.ToLowerInvariant();
        }
        catch (Exception)
        {
            return "us";
        }
    }

    static string AppleStoreUrl(AppStoreListing listing)
    {
        if (!string.IsNullOrEmpty(listing.trackViewUrl))
            return listing.trackViewUrl.Replace("https://", "itms-apps://").Replace("http://", "itms-apps://");

        if (listing.trackId > 0)
            return "itms-apps://itunes.apple.com/app/id" + listing.trackId;

        return null;
    }
#endif

    void Update()
    {
        if (!_updateAvailable || _dialogShown)
            return;

        _dialogShown = true;
#if UNITY_IOS
        const string body = "A newer version is on the App Store.";
#else
        const string body = "A newer version is on the Play Store.";
#endif
        UpdateOfferDialog.Show(OpenStore, body);
    }

    internal void MarkUpdateAvailable()
    {
        _updateAvailable = true;
    }

    internal void ReadAvailability(AndroidJavaObject info)
    {
        if (info == null)
            return;

        int availability = info.Call<int>("updateAvailability");
        if (availability == UpdateAvailable || availability == UpdateInProgress)
            MarkUpdateAvailable();
    }

    void OpenStore()
    {
#if UNITY_IOS
        if (!string.IsNullOrEmpty(_storeUrl))
            Application.OpenURL(_storeUrl);
        return;
#else
        string id = Application.identifier;
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var uriClass = new AndroidJavaClass("android.net.Uri"))
            using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", "market://details?id=" + id))
            using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW", uri))
            {
                intent.Call<AndroidJavaObject>("setPackage", "com.android.vending");
                activity.Call("startActivity", intent);
                return;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AppUpdate] Play Store intent failed: " + e.Message);
        }

        Application.OpenURL("https://play.google.com/store/apps/details?id=" + id);
#endif
    }

#if UNITY_IOS
    static bool IsStoreNewer(string storeVersion, string localVersion)
    {
        int[] store = VersionParts(storeVersion);
        int[] local = VersionParts(localVersion);
        int count = Math.Max(store.Length, local.Length);
        for (int i = 0; i < count; i++)
        {
            int storePart = i < store.Length ? store[i] : 0;
            int localPart = i < local.Length ? local[i] : 0;
            if (storePart != localPart)
                return storePart > localPart;
        }

        return false;
    }

    static int[] VersionParts(string version)
    {
        if (string.IsNullOrEmpty(version))
            return Array.Empty<int>();

        string[] tokens = version.Split('.');
        var parts = new int[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            int value = 0;
            int length = 0;
            string token = tokens[i];
            while (length < token.Length && char.IsDigit(token[length]))
                length++;
            if (length > 0)
                int.TryParse(token.Substring(0, length), out value);
            parts[i] = value;
        }

        return parts;
    }
#endif

    void OnDestroy()
    {
#if UNITY_ANDROID
        _infoTask?.Dispose();
        _manager?.Dispose();
        _infoTask = null;
        _manager = null;
#endif
    }

    sealed class SuccessListener : AndroidJavaProxy
    {
        readonly AppUpdatePrompt _owner;

        public SuccessListener(AppUpdatePrompt owner)
            : base("com.google.android.gms.tasks.OnSuccessListener")
        {
            _owner = owner;
        }

        public void onSuccess(AndroidJavaObject info)
        {
            _owner.ReadAvailability(info);
        }
    }

    sealed class FailureListener : AndroidJavaProxy
    {
        public FailureListener()
            : base("com.google.android.gms.tasks.OnFailureListener")
        {
        }

        public void onFailure(AndroidJavaObject exception)
        {
            Debug.Log("[AppUpdate] Play did not report an update.");
        }
    }
}

[Serializable]
class AppStoreLookup
{
    public int resultCount;
    public AppStoreListing[] results;
}

[Serializable]
class AppStoreListing
{
    public string version;
    public string trackViewUrl;
    public long trackId;
}

public class UpdateOfferDialog : MonoBehaviour
{
    static UpdateOfferDialog _instance;

    Action _onUpdate;
    TMP_Text _title;
    TMP_Text _body;

    public static void Show(Action onUpdate, string body)
    {
        if (_instance == null)
            _instance = Create();

        _instance._onUpdate = onUpdate;
        _instance._title.text = "Update available";
        _instance._body.text = body;
        _instance.gameObject.SetActive(true);
    }

    static UpdateOfferDialog Create()
    {
        var root = new GameObject("Update Offer Dialog", typeof(RectTransform), typeof(UpdateOfferDialog));
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;
        root.AddComponent<GraphicRaycaster>();

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var dialog = root.GetComponent<UpdateOfferDialog>();
        dialog.Build();
        return dialog;
    }

    void Build()
    {
        var dimGo = new GameObject("Dimmer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dimGo.transform.SetParent(transform, false);
        Stretch(dimGo.GetComponent<RectTransform>());
        var dim = dimGo.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.65f);
        dim.raycastTarget = true;

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGo.transform.SetParent(transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(760f, 420f);
        panelGo.GetComponent<Image>().color = new Color32(28, 36, 48, 245);

        var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        accentGo.transform.SetParent(panelGo.transform, false);
        var accentRt = accentGo.GetComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 1f);
        accentRt.anchorMax = new Vector2(1f, 1f);
        accentRt.pivot = new Vector2(0.5f, 1f);
        accentRt.sizeDelta = new Vector2(0f, 4f);
        accentGo.GetComponent<Image>().color = new Color32(56, 110, 130, 255);

        _title = Label(panelGo.transform, "Title", 42f, FontStyles.Bold,
            new Vector2(0f, -36f), new Vector2(680f, 56f));
        _body = Label(panelGo.transform, "Body", 28f, FontStyles.Normal,
            new Vector2(0f, -130f), new Vector2(640f, 120f));
        _body.enableWordWrapping = true;

        Button(panelGo.transform, "Update", new Vector2(-160f, 48f), new Color32(56, 110, 130, 255), Accept);
        Button(panelGo.transform, "Not now", new Vector2(160f, 48f), new Color32(48, 56, 68, 255), Decline);
    }

    void Accept()
    {
        gameObject.SetActive(false);
        _onUpdate?.Invoke();
    }

    void Decline()
    {
        gameObject.SetActive(false);
    }

    static void Button(Transform parent, string label, Vector2 position, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(280f, 72f);
        go.GetComponent<Image>().color = color;

        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        var text = Label(go.transform, "Label", 28f, FontStyles.Bold, Vector2.zero, new Vector2(260f, 64f));
        text.text = label;
        var textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        textRt.anchoredPosition = Vector2.zero;
    }

    static TMP_Text Label(Transform parent, string name, float size, FontStyles style, Vector2 position, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = position;
        rt.sizeDelta = box;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.font = ResolveFont();
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color32(245, 232, 214, 255);
        tmp.raycastTarget = false;
        return tmp;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static TMP_FontAsset ResolveFont()
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
        if (font == null && TMP_Settings.instance != null)
            font = TMP_Settings.defaultFontAsset;
        return font;
    }
}
