using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopControllerVS : MonoBehaviour
{
    Vector3 mouseScreenPosition;
    Vector3 mouseWorldPosition;
     [Header("DailyRewards")]
    [SerializeField] Button claimButton;
    [SerializeField] Button watchAdClaimButton;
    [SerializeField] GameObject resetHolderClaim;
    [SerializeField] GameObject resetHolderWatchAd;
    public TextMeshProUGUI timerTextClaim; // Timer for the claim button
    public TextMeshProUGUI timerTextWatchAd; // Timer for the watch ad button
    public int rewardAmount = 100;
    private DateTime nextClaimTime;
    private DateTime nextAdClaimTime;
    private bool isClaimTimerActive;
    private bool isAdTimerActive;
    private const string ClaimTimerKey = "NextClaimTime";
    private const string AdTimerKey = "NextAdClaimTime";

    [Header("Starter Pack")]
    [SerializeField] Button starterBuyButton;
    [SerializeField] Button boosterBuyButton;
    [SerializeField] Button superBuyButton;

    int gemCount, coinCount, swapCount, removeCount , clearCount;

    private void Start()
    {
        //gemCount = PlayerPrefs.GetInt("Gems",2);
        //coinCount  = PlayerPrefs.GetInt("TotalCoins", 0);
        //swapCount = PlayerPrefs.GetInt("Swap",1);
        //removeCount = PlayerPrefs.GetInt("Remove",1);
        // Load data using GameDataManager

        var (level, coins, gems, remove, swap, clear) = GameDataManager.instance.LoadData();

        coinCount = coins;
        //gemCount = gems;
        //swapCount = swap;
        //removeCount = remove;
        //clearCount = clear;

        Debug.Log(" gemCount: " + gemCount + " coinCount :" + coinCount + " swapCount: " + swapCount + " removeCount :" + removeCount);
        //starterBuyButton.onClick.RemoveAllListeners();
        //starterBuyButton.onClick.AddListener(OnStarterBuyButtonClicked);

        //boosterBuyButton.onClick.RemoveAllListeners();
        //boosterBuyButton.onClick.AddListener(OnBoosterBuyButtonClicked); 

        //superBuyButton.onClick.RemoveAllListeners();
        //superBuyButton.onClick.AddListener(OnSuperBuyButtonClicked);

        //ClearTimerHistory();

        //claimButton.onClick.RemoveAllListeners();
        //claimButton.onClick.AddListener(OnClaimButtonClick);

        //watchAdClaimButton.onClick.RemoveAllListeners();
        //watchAdClaimButton.onClick.AddListener(OnWatchAdButtonClick);

        // Initialize timers for both buttons
        InitializeButtonTimer(ClaimTimerKey, ref nextClaimTime, ref isClaimTimerActive, claimButton, resetHolderClaim, timerTextClaim, UpdateClaimTimer);
        InitializeButtonTimer(AdTimerKey, ref nextAdClaimTime, ref isAdTimerActive, watchAdClaimButton, resetHolderWatchAd, timerTextWatchAd, UpdateAdTimer);
    }

    #region Daily Rewards

    private void InitializeButtonTimer(
        string timerKey,
        ref DateTime nextTime,
        ref bool isTimerActive,
        Button button,
        GameObject resetHolder,
        TextMeshProUGUI timerText,
        Func<IEnumerator> updateTimerCoroutine)
    {
        if (PlayerPrefs.HasKey(timerKey))
        {
            nextTime = DateTime.Parse(PlayerPrefs.GetString(timerKey));
            if (DateTime.Now < nextTime)
            {
                isTimerActive = true;
                button.interactable = false;
                button.gameObject.SetActive(false);
                resetHolder.gameObject.SetActive(true);
                StartCoroutine(updateTimerCoroutine());
            }
        }
        else
        {
            nextTime = DateTime.Now;
            SetButtonActiveState(button, resetHolder, timerText);
        }
    }

    public void OnClaimButtonClick()
    {

        // Reward the player
        int currentCoin = PlayerPrefs.GetInt("TotalCoins", 0);
        currentCoin += 80;
        PlayerPrefs.SetInt("TotalCoins", currentCoin);
        CoinManager.instance.AddCoins(mouseWorldPosition, 100);

        // Set the next claim time to 24 hours from now
        nextClaimTime = DateTime.Now.AddHours(1);
        PlayerPrefs.SetString(ClaimTimerKey, nextClaimTime.ToString());
        PlayerPrefs.Save();
        UIManager.instance.UpdateUI();

        // Start the timer
        isClaimTimerActive = true;
        claimButton.interactable = false;
        claimButton.gameObject.SetActive(false);
        resetHolderClaim.gameObject.SetActive(true);
        StartCoroutine(UpdateClaimTimer());
    }

    //private void OnWatchAd()
    //{
    //    UIManager.instance.UpdateUI();
    //    StartCoroutine(OnWatchAdButtonClick());
    //}
    public void  OnWatchAdButtonClick()
    {

        //yield return new WaitForSeconds(2.5f);
        // Reward for watching an ad

        //AdManager.instance.RewardAdFinal();
        int currentCoins = PlayerPrefs.GetInt("TotalCoins", 0);
        currentCoins += 200; // Example: Ad reward is double
        PlayerPrefs.SetInt("TotalCoins", currentCoins);
        CoinManager.instance.AddCoins(mouseWorldPosition, 100);

        // Set the next ad claim time to 1 hour from now
        nextAdClaimTime = DateTime.Now.AddHours(1);
        PlayerPrefs.SetString(AdTimerKey, nextAdClaimTime.ToString());
        PlayerPrefs.Save();
        UIManager.instance.UpdateUI();

        // Start the timer
        isAdTimerActive = true;
        watchAdClaimButton.interactable = false;
        watchAdClaimButton.gameObject.SetActive(false);
        resetHolderWatchAd.gameObject.SetActive(true);
        StartCoroutine(UpdateAdTimer());
    }

    public void ClearTimerHistory()
    {
        PlayerPrefs.DeleteKey(ClaimTimerKey);
        PlayerPrefs.DeleteKey(AdTimerKey);
        Debug.Log("All timer histories cleared.");
    }

    private IEnumerator UpdateClaimTimer()
    {
        while (isClaimTimerActive)
        {
            yield return UpdateTimer(nextClaimTime, claimButton, resetHolderClaim, timerTextClaim);
        }
    }

    private IEnumerator UpdateAdTimer()
    {
        while (isAdTimerActive)
        {
            yield return UpdateTimer(nextAdClaimTime, watchAdClaimButton, resetHolderWatchAd, timerTextWatchAd);
        }
    }

    private IEnumerator UpdateTimer(
       DateTime nextTime,
       Button button,
       GameObject resetHolder,
       TextMeshProUGUI timerText)
    {
        bool isTimerActive = true; // Use a local variable instead of ref
        TimeSpan remainingTime = nextTime - DateTime.Now;
        if (remainingTime.TotalSeconds > 0)
        {
            timerText.text = $"{remainingTime.Hours:D2}:{remainingTime.Minutes:D2}:{remainingTime.Seconds:D2}";
        }
        else
        {
            SetButtonActiveState(button, resetHolder, timerText);
            isTimerActive = false;
            yield break;
        }

        yield return new WaitForSeconds(1f);
    }


    private void SetButtonActiveState(Button button, GameObject resetHolder, TextMeshProUGUI timerText)
    {
        button.interactable = true;
        button.gameObject.SetActive(true);
        resetHolder.gameObject.SetActive(false);
        //timerText.text = "READY!";
    }

    #endregion


    // PACKS SHOPPING
    public void OnStarterBuyButtonClicked()
    {
        //gemCount, coinCount, swapCount, removeCount
        //Gem
        //int currentGem = gemCount;
        //gemCount += 50;

        CoinManager.instance.AddGems(mouseWorldPosition, 50);
        GameDataManager.instance.AddGems(50);

        //Coin
        CoinManager.instance.AddCoins(mouseWorldPosition, 100);
        GameDataManager.instance.AddCoins(1000);
        coinCount +=1000;
        GameDataManager.instance.SaveInt("TotalCoins", coinCount);

        //PlayerPrefs.SetInt("TotalCoins", coinCount);

        //GameDataManager.instance.SaveData(1, coinCount, gemCount, removeCount , swapCount , clearCount);
        //Swap
        //int currentSwap = swapCount;
        //currentSwap += 3;
        //PlayerPrefs.SetInt("Swap", currentSwap);
        SwapController.instance.SetSwapCount(3 );


        //Remove
        //int currentRemove = removeCount;
        //currentRemove += 3;
        //PlayerPrefs.SetInt("Remove", currentRemove);
        HammerManager.instance.SetHammerCount(3);

        //PlayerPrefs.Save();

       
        UIManager.instance.UpdateUI();
        starterBuyButton.interactable = false;
    }
    public void OnBoosterBuyButtonClicked()
    {        
        //Swap       
        SwapController.instance.SetSwapCount(10);

        //Remove        
        HammerManager.instance.SetHammerCount(10);

        //Clean
        WatchAD.instance.SetCleanerCount(10);

        //PlayerPrefs.Save();

        //GameDataManager.instance.LoadData();
        UIManager.instance.UpdateUI();
        boosterBuyButton.interactable = false;
    }
    public void OnSuperBuyButtonClicked()
    {

        //Gem
    
        CoinManager.instance.AddGems(mouseWorldPosition, 50);
        GameDataManager.instance.AddGems(100);

        //Coin

        CoinManager.instance.AddCoins(mouseWorldPosition, 100);
        GameDataManager.instance.AddCoins(2000);
        coinCount += 2000;
        GameDataManager.instance.SaveInt("TotalCoins", coinCount);



        //Swap       
        SwapController.instance.SetSwapCount(10);

        //Remove        
        HammerManager.instance.SetHammerCount(10);

        //Clean
        WatchAD.instance.SetCleanerCount(10);


        UIManager.instance.UpdateUI();
        superBuyButton.interactable = false;
    }

    public void OnRemoveAdds()
    {
        Debug.Log("Remove all Adds  in the game");
    }
    void Update()
    {
        // Get the mouse position in screen coordinates
        mouseScreenPosition = Input.mousePosition;

        // Set the desired z-distance from the camera
        mouseScreenPosition.z = Mathf.Abs(Camera.main.transform.position.z);

        // Convert screen coordinates to world position
         mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);

        // Log or use the world position
        //Debug.Log("Mouse World Position: " + mouseWorldPosition);
    }
}
