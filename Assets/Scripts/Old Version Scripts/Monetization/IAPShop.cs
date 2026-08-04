using System;
using UnityEngine;
//using UnityEngine.Purchasing;
using UnityEngine.UI;

public class IAPShop : MonoBehaviour/*, IStoreListener*/
{
//    private static IStoreController storeController;
//    private static IExtensionProvider storeExtensionProvider;

//    [Header("Starter Pack")]
//    [SerializeField] Button starterBuyButton;
//    [SerializeField] Button boosterBuyButton;
//    [SerializeField] Button superBuyButton;

//    int gemCount, coinCount, swapCount, removeCount;

//    private const string STARTER_PACK = "starter_pack"; // Product ID for starter pack
//    private const string BOOSTER_PACK = "booster_pack"; // Product ID for booster pack
//    private const string SUPER_PACK = "super_pack";     // Product ID for super pack

//    private void Start()
//    {
//        InitializePurchasing();

//        gemCount = PlayerPrefs.GetInt("Gems", 2);
//        coinCount = PlayerPrefs.GetInt("TotalCoins", 0);
//        swapCount = PlayerPrefs.GetInt("Swap", 1);
//        removeCount = PlayerPrefs.GetInt("Remove", 1);

//        Debug.Log(" gemCount: " + gemCount + " coinCount :" + coinCount + " swapCount: " + swapCount + " removeCount :" + removeCount);

//        starterBuyButton.onClick.RemoveAllListeners();
//        starterBuyButton.onClick.AddListener(() => BuyProductID(STARTER_PACK));

//        boosterBuyButton.onClick.RemoveAllListeners();
//        boosterBuyButton.onClick.AddListener(() => BuyProductID(BOOSTER_PACK));

//        superBuyButton.onClick.RemoveAllListeners();
//        superBuyButton.onClick.AddListener(() => BuyProductID(SUPER_PACK));
//    }

//    public void InitializePurchasing()
//    {
//        if (IsInitialized())
//            return;

//        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
//        builder.AddProduct(STARTER_PACK, ProductType.Consumable);
//        builder.AddProduct(BOOSTER_PACK, ProductType.Consumable);
//        builder.AddProduct(SUPER_PACK, ProductType.Consumable);

//        UnityPurchasing.Initialize(this, builder);
//    }

//    private bool IsInitialized()
//    {
//        return storeController != null && storeExtensionProvider != null;
//    }

//    public void BuyProductID(string productId)
//    {
//        if (IsInitialized())
//        {
//            Product product = storeController.products.WithID(productId);
//            if (product != null && product.availableToPurchase)
//            {
//                Debug.Log($"Purchasing product asychronously: {productId}");
//                storeController.InitiatePurchase(product);
//            }
//            else
//            {
//                Debug.Log("BuyProductID: Product not found or not available for purchase");
//            }
//        }
//        else
//        {
//            Debug.Log("BuyProductID: Not initialized");
//        }
//    }

//    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
//    {
//        storeController = controller;
//        storeExtensionProvider = extensions;
//        Debug.Log("IAP Initialized");
//    }

//    public void OnInitializeFailed(InitializationFailureReason error)
//    {
//        Debug.LogError($"IAP Initialization Failed: {error}");
//    }

//    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
//    {
//        if (String.Equals(args.purchasedProduct.definition.id, STARTER_PACK, StringComparison.Ordinal))
//        {
//            Debug.Log("Starter Pack Purchased");
//            ExecuteStarterPackRewards();
//        }
//        else if (String.Equals(args.purchasedProduct.definition.id, BOOSTER_PACK, StringComparison.Ordinal))
//        {
//            Debug.Log("Booster Pack Purchased");
//            ExecuteBoosterPackRewards();
//        }
//        else if (String.Equals(args.purchasedProduct.definition.id, SUPER_PACK, StringComparison.Ordinal))
//        {
//            Debug.Log("Super Pack Purchased");
//            ExecuteSuperPackRewards();
//        }
//        else
//        {
//            Debug.Log("Purchase Failed or Unhandled");
//        }

//        return PurchaseProcessingResult.Complete;
//    }

//    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
//    {
//        Debug.LogError($"Purchase failed: {product.definition.id}, Reason: {failureReason}");
//    }

//    private void ExecuteStarterPackRewards()
//    {
//        gemCount += 50;
//        PlayerPrefs.SetInt("Gems", gemCount);

//        coinCount += 1000;
//        PlayerPrefs.SetInt("TotalCoins", coinCount);

//        SwapController.instance.SetSwapCount(3);
//        HammerManager.instance.SetHammerCount(3);

//        PlayerPrefs.Save();

//        GameDataManager.instance.LoadData();
//        UIManager.instance.UpdateUI();

//        starterBuyButton.interactable = false;
//    }

//    private void ExecuteBoosterPackRewards()
//    {
//        SwapController.instance.SetSwapCount(10);
//        HammerManager.instance.SetHammerCount(10);
//        WatchAD.instance.SetCleanerCount(10);

//        PlayerPrefs.Save();

//        GameDataManager.instance.LoadData();
//        UIManager.instance.UpdateUI();

//        boosterBuyButton.interactable = false;
//    }

//    private void ExecuteSuperPackRewards()
//    {
//        gemCount += 100;
//        PlayerPrefs.SetInt("Gems", gemCount);

//        coinCount += 2000;
//        PlayerPrefs.SetInt("TotalCoins", coinCount);

//        SwapController.instance.SetSwapCount(10);
//        HammerManager.instance.SetHammerCount(10);
//        WatchAD.instance.SetCleanerCount(10);

//        PlayerPrefs.Save();

//        GameDataManager.instance.LoadData();
//        UIManager.instance.UpdateUI();

//        superBuyButton.interactable = false;
//    }

//    void IStoreListener.OnInitializeFailed(InitializationFailureReason error, string message)
//    {
//    }
}
