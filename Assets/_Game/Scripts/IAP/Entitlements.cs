using System;
using UnityEngine;

/// <summary>
/// Full-game unlock. The store receipt is the source of truth after a successful
/// store init. Until then, the last confirmed receipt is cached so a paying player
/// is not locked out while offline.
/// </summary>
public static class Entitlements
{
    public const string FullUnlockProductId = "zflite_full";

    const string ReceiptCacheKey = "iap_zflite_full";
    const string EditorGrantKey = "iap_zflite_full_editor";

    static bool _receiptOwned;

    public static event Action Changed;

    public static bool HasFullUnlock
    {
        get
        {
            if (LevelTestMode.IsActive || AutopilotMode.IsActive)
                return true;

#if UNITY_EDITOR
            if (PlayerPrefs.GetInt(EditorGrantKey, 0) == 1)
                return true;
#endif
            return _receiptOwned;
        }
    }

    public static bool AllowsLevel(int level)
    {
        return HasFullUnlock || level == PlayerPrefsHolder.FirstRankedLevel;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadCache()
    {
        _receiptOwned = PlayerPrefs.GetInt(ReceiptCacheKey, 0) == 1;
    }

    public static void SetReceiptOwned(bool owned)
    {
        if (_receiptOwned == owned)
            return;

        _receiptOwned = owned;
        PlayerPrefs.SetInt(ReceiptCacheKey, owned ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

#if UNITY_EDITOR
    public static void SetEditorGrant(bool granted)
    {
        PlayerPrefs.SetInt(EditorGrantKey, granted ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
#endif
}
