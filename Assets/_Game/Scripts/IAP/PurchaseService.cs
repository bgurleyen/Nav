using System;
using System.Collections;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

/// <summary>
/// Non-consumable unlock (<see cref="Entitlements.FullUnlockProductId"/>).
/// Create that product id in Play Console and App Store Connect before release.
/// </summary>
public class PurchaseService : MonoBehaviour, IDetailedStoreListener
{
    public static event Action Updated;

    static PurchaseService _instance;
    static Action<string> _pending;

    IStoreController _controller;
    IExtensionProvider _extensions;
    bool _refreshing;

    public static bool IsReady => _instance != null && _instance._controller != null;

    public static bool IsRefreshing => _instance != null && _instance._refreshing;

    /// <summary>Editor Fake Store. It always reports $0.01 and never calls Play or the App Store.</summary>
    public static bool IsUsingFakeStore =>
        StandardPurchasingModule.Instance().appStore == AppStore.fake;

    public static bool TryGetLocalizedPrice(out string price)
    {
        price = null;
        var product = _instance?._controller?.products.WithID(Entitlements.FullUnlockProductId);
        if (product == null || !product.availableToPurchase || product.metadata == null)
            return false;

        if (IsFakePlaceholder(product.metadata))
            return false;

        if (string.IsNullOrEmpty(product.metadata.localizedPriceString))
            return false;

        price = product.metadata.localizedPriceString;
        return true;
    }

    static bool IsFakePlaceholder(ProductMetadata metadata)
    {
        return IsUsingFakeStore
            && metadata.isoCurrencyCode == "USD"
            && metadata.localizedPrice == 0.01m;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (_instance != null)
            return;

        var host = new GameObject(nameof(PurchaseService));
        DontDestroyOnLoad(host);
        _instance = host.AddComponent<PurchaseService>();
        _instance.StartCoroutine(_instance.InitializeWhenReady());
    }

    IEnumerator InitializeWhenReady()
    {
        _refreshing = true;

        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            var servicesTask = UnityServices.InitializeAsync();
            while (!servicesTask.IsCompleted)
                yield return null;

            if (servicesTask.Exception != null)
            {
                _refreshing = false;
                Debug.LogWarning("[IAP] Unity Gaming Services init failed: " + servicesTask.Exception);
                Updated?.Invoke();
                yield break;
            }
        }

        var module = StandardPurchasingModule.Instance();
        var builder = ConfigurationBuilder.Instance(module);
        builder.AddProduct(Entitlements.FullUnlockProductId, ProductType.NonConsumable);
        UnityPurchasing.Initialize(this, builder);
    }

    /// <summary>
    /// Ask the active store for product details again. Play and the App Store
    /// return a new formatted price; the editor fake store does not.
    /// </summary>
    public static void RefreshFromStore()
    {
        if (_instance == null || _instance._refreshing || _pending != null)
            return;

        _instance.StartCoroutine(_instance.InitializeWhenReady());
    }

    public static void Buy(Action<string> onFinished)
    {
        if (_instance == null || _instance._controller == null)
        {
            onFinished?.Invoke("Store is not ready yet.");
            return;
        }

        _pending = onFinished;
        _instance._controller.InitiatePurchase(Entitlements.FullUnlockProductId);
    }

    public static void Restore(Action<string> onFinished)
    {
        if (_instance == null || _instance._controller == null || _instance._extensions == null)
        {
            onFinished?.Invoke("Store is not ready yet.");
            return;
        }

#if UNITY_IOS && !UNITY_EDITOR
        _instance._extensions.GetExtension<IAppleExtensions>().RestoreTransactions((ok, message) =>
        {
            _instance.SyncReceipt(allowClear: false);
            onFinished?.Invoke(RestoreMessage(ok, message));
        });
#elif UNITY_ANDROID && !UNITY_EDITOR
        _instance._extensions.GetExtension<IGooglePlayStoreExtensions>().RestoreTransactions((ok, message) =>
        {
            _instance.SyncReceipt(allowClear: false);
            onFinished?.Invoke(RestoreMessage(ok, message));
        });
#else
        _instance.SyncReceipt(allowClear: false);
        onFinished?.Invoke(Entitlements.HasFullUnlock ? "Restored." : "No purchase found.");
#endif
    }

    static string RestoreMessage(bool ok, string message)
    {
        if (!ok)
            return string.IsNullOrEmpty(message) ? "Restore failed." : message;

        return Entitlements.HasFullUnlock ? "Restored." : "No purchase found.";
    }

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        _controller = controller;
        _extensions = extensions;
        _refreshing = false;
        SyncReceipt(allowClear: true);
        Updated?.Invoke();
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        _refreshing = false;
        Debug.LogWarning("[IAP] Store init failed: " + error);
        Updated?.Invoke();
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        _refreshing = false;
        Debug.LogWarning("[IAP] Store init failed: " + error + " " + message);
        Updated?.Invoke();
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs purchaseEvent)
    {
        var id = purchaseEvent.purchasedProduct.definition.id;
        if (id == Entitlements.FullUnlockProductId)
            Entitlements.SetReceiptOwned(true);

        FinishPending(null);
        Updated?.Invoke();
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        FinishPending(FailureText(failureDescription.reason, failureDescription.message));
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        FinishPending(FailureText(failureReason, null));
    }

    void SyncReceipt(bool allowClear)
    {
        var product = _controller?.products.WithID(Entitlements.FullUnlockProductId);
        if (product != null && product.hasReceipt)
            Entitlements.SetReceiptOwned(true);
        else if (allowClear)
            Entitlements.SetReceiptOwned(false);
    }

    static void FinishPending(string message)
    {
        var callback = _pending;
        _pending = null;
        callback?.Invoke(message);
    }

    static string FailureText(PurchaseFailureReason reason, string message)
    {
        if (reason == PurchaseFailureReason.UserCancelled)
            return "Purchase cancelled.";

        if (reason == PurchaseFailureReason.DuplicateTransaction)
            return "This purchase is already active.";

        if (!string.IsNullOrEmpty(message))
            return message;

        return reason.ToString();
    }
}
