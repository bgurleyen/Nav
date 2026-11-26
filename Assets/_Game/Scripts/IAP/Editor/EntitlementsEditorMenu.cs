using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only toggles for IAP unlock and saved display name (<see cref="PlayerPrefsHolder"/>).
/// </summary>
public static class EntitlementsEditorMenu
{
    const string PurchasedMenuPath = "Game/IAP/Satın alındı";
    const string UserNameMenuPath = "Game/IAP/İsim girildi";

    const string ReceiptCacheKey = "iap_descenttutor_full";
    const string EditorGrantKey = "iap_descenttutor_full_editor";
    const string DefaultEditorUserName = "Editor";

    [MenuItem(PurchasedMenuPath, false, 0)]
    static void TogglePurchased()
    {
        bool next = !ReadPurchased();
        ApplyPurchased(next);
    }

    [MenuItem(PurchasedMenuPath, true)]
    static bool TogglePurchasedValidate()
    {
        Menu.SetChecked(PurchasedMenuPath, ReadPurchased());
        return true;
    }

    [MenuItem(UserNameMenuPath, false, 1)]
    static void ToggleUserNameEntered()
    {
        bool next = !ReadUserNameEntered();
        ApplyUserNameEntered(next);
    }

    [MenuItem(UserNameMenuPath, true)]
    static bool ToggleUserNameEnteredValidate()
    {
        Menu.SetChecked(UserNameMenuPath, ReadUserNameEntered());
        return true;
    }

    static bool ReadPurchased()
    {
        if (Application.isPlaying)
            return Entitlements.HasFullUnlock;

        return PlayerPrefs.GetInt(EditorGrantKey, 0) == 1
            || PlayerPrefs.GetInt(ReceiptCacheKey, 0) == 1;
    }

    static void ApplyPurchased(bool purchased)
    {
        if (Application.isPlaying)
        {
            Entitlements.SetEditorGrant(purchased);
            Entitlements.SetReceiptOwned(purchased);
            return;
        }

        PlayerPrefs.SetInt(EditorGrantKey, purchased ? 1 : 0);
        PlayerPrefs.SetInt(ReceiptCacheKey, purchased ? 1 : 0);
        PlayerPrefs.Save();
    }

    static bool ReadUserNameEntered()
    {
        return !string.IsNullOrEmpty(PlayerPrefs.GetString("UserName", null));
    }

    static void ApplyUserNameEntered(bool entered)
    {
        if (entered)
        {
            if (string.IsNullOrEmpty(PlayerPrefs.GetString("UserName", null)))
                PlayerPrefs.SetString("UserName", DefaultEditorUserName);
        }
        else
        {
            PlayerPrefs.DeleteKey("UserName");
            PlayerPrefs.SetInt("Agree", 1);
        }

        PlayerPrefs.Save();
    }
}