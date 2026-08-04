using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using TMPro;

public class IAPManager : MonoBehaviour
{
    private string removeAdd = "Remove_Adds";
    private string starterPack = "Starter_Pack";
    private string boosterPack = "Booster_Pack";
    private string superPack = "Super_Pack";
    [SerializeField]private ShopControllerVS shopController;
    [SerializeField] Button starterButton;
    [SerializeField] Button boosterButton;
    [SerializeField] Button superButton;
    [SerializeField] Button removeAddButton;

    void Start()
    {
        StartCoroutine(CheckInitialization());
    }

    IEnumerator CheckInitialization()
    {
        bool isInitialized = false;
        while (!isInitialized)
        {
            if (CodelessIAPStoreListener.Instance.HasProductInCatalog(removeAdd))
            {
                isInitialized = true;
            }
            yield return new WaitForSeconds(0.5f);
        }
        if (CodelessIAPStoreListener.Instance.GetProduct(removeAdd).hasReceipt)
        {
            shopController.OnRemoveAdds();
        }
    }
    public void OnPurchasedComplete(Product product) 
    {
        if (product.definition.id == starterPack)
        {
        shopController.OnStarterBuyButtonClicked();
        }
        else if (product.definition.id == boosterPack)
        {
            shopController.OnBoosterBuyButtonClicked();

        }
        else if (product.definition.id == superPack)
        {
            shopController.OnSuperBuyButtonClicked();
        }
        else if (product.definition.id == removeAdd)
        {
            shopController.OnRemoveAdds();
        }
    }
    
    public void OnPurchasedFailed(Product product , PurchaseFailureDescription failureDescription )
    {
        Debug.Log(product.definition.id + " purchase failure reason " + failureDescription);
    }
    
    // Update the button price , I mean any other country's currency rather than us dollar
    
    public void OnProductFetched(Product product)
    {
        if (product.definition.id == starterPack)
        {
            UpdateButtonPrice(starterButton , product);
        }
        else if (product.definition.id == boosterPack)
        {
            UpdateButtonPrice(boosterButton, product);

        }
        else if (product.definition.id == superPack)
        {
            UpdateButtonPrice(superButton, product);

        }
        else if (product.definition.id ==removeAdd)
        {
            UpdateButtonPrice(removeAddButton, product);
        }
    }
    private void UpdateButtonPrice(Button button , Product product)
    {
        TextMeshProUGUI buttonText = button.transform.GetChild(0).GetComponent<TextMeshProUGUI>();

        if (buttonText != null)
        {
            buttonText.text = product.metadata.localizedPrice + " " + product.metadata.isoCurrencyCode;
        }
    }
}


