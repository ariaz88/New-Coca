using System;
using GoogleMobileAds;
using GoogleMobileAds.Api;
using UnityEngine;

/// <summary>
/// Central Google Mobile Ads manager. This version intentionally uses Google's
/// Android/iOS test ad unit IDs so it is safe for development and device testing.
/// Replace these IDs only when a verified production AdMob setup is ready.
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager instance;
    public static Action OnaddFinished;

    private BannerView bannerView;
    private InterstitialAd interstitialAd;
    private RewardedAd rewardedAd;
    private bool isInitialized;

#if UNITY_ANDROID || UNITY_EDITOR
    private const string BannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
    private const string InterstitialAdUnitId = "ca-app-pub-3940256099942544/1033173712";
    private const string RewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IOS
    private const string BannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
    private const string InterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";
    private const string RewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
#else
    private const string BannerAdUnitId = "unused";
    private const string InterstitialAdUnitId = "unused";
    private const string RewardedAdUnitId = "unused";
#endif

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializeMyAdd();
    }

    public void InitializeMyAdd()
    {
        if (isInitialized)
        {
            return;
        }

        MobileAds.Initialize(initializationStatus =>
        {
            if (initializationStatus == null)
            {
                Debug.LogError("Google Mobile Ads initialization failed.");
                return;
            }

            isInitialized = true;
            Debug.Log("Google Mobile Ads initialized with test ad unit IDs.");
            LoadInterstitalAd();
            LoadRewardedAd();
        });
    }

    #region Banner

    public void LoadBannerAd()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("Ads are not initialized yet.");
            return;
        }

        DestroyBannerAd();
        bannerView = new BannerView(BannerAdUnitId, AdSize.Banner, AdPosition.Top);
        ListenToBannerEvents();
        bannerView.LoadAd(new AdRequest());
    }

    private void ListenToBannerEvents()
    {
        bannerView.OnBannerAdLoaded += () => Debug.Log("Test banner ad loaded.");
        bannerView.OnBannerAdLoadFailed += error => Debug.LogError("Banner ad failed to load: " + error);
        bannerView.OnAdFullScreenContentClosed += () => Debug.Log("Banner full-screen content closed.");
    }

    public void DestroyBannerAd()
    {
        if (bannerView == null)
        {
            return;
        }

        bannerView.Destroy();
        bannerView = null;
    }

    #endregion

    #region Interstitial

    public void LoadInterstitalAd()
    {
        if (!isInitialized)
        {
            return;
        }

        interstitialAd?.Destroy();
        interstitialAd = null;

        InterstitialAd.Load(InterstitialAdUnitId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError("Interstitial ad failed to load: " + error);
                return;
            }

            interstitialAd = ad;
            RegisterInterstitialEvents(ad);
            Debug.Log("Test interstitial ad loaded.");
        });
    }

    public void ShowInterstitialAD()
    {
        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            interstitialAd.Show();
            return;
        }

        Debug.LogWarning("Interstitial ad is not ready yet.");
        LoadInterstitalAd();
    }

    private void RegisterInterstitialEvents(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            ad.Destroy();
            if (interstitialAd == ad)
            {
                interstitialAd = null;
            }

            LoadInterstitalAd();
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogError("Interstitial ad failed to show: " + error);
            ad.Destroy();
            if (interstitialAd == ad)
            {
                interstitialAd = null;
            }

            LoadInterstitalAd();
        };
    }

    #endregion

    #region Rewarded

    public void LoadRewardedAd()
    {
        if (!isInitialized)
        {
            return;
        }

        rewardedAd?.Destroy();
        rewardedAd = null;

        RewardedAd.Load(RewardedAdUnitId, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogError("Rewarded ad failed to load: " + error);
                return;
            }

            rewardedAd = ad;
            RegisterRewardedEvents(ad);
            Debug.Log("Test rewarded ad loaded.");
        });
    }

    public bool IsRewardedAdReady => rewardedAd != null && rewardedAd.CanShowAd();

    /// <summary>
    /// Shows a rewarded ad and invokes the reward callback only after Google
    /// confirms that the user earned the reward.
    /// </summary>
    public void ShowRewardedAd(Action onRewardEarned, Action onAdUnavailable = null)
    {
        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            rewardedAd.Show(_ => onRewardEarned?.Invoke());
            return;
        }

        Debug.LogWarning("Rewarded ad is not ready yet.");
        LoadRewardedAd();
        onAdUnavailable?.Invoke();
    }

    // Kept for legacy UnityEvents that may still call this method directly.
    public void ShowrewardedAd()
    {
        ShowRewardedAd(() => GrantCoins(1));
    }

    private void RegisterRewardedEvents(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            ad.Destroy();
            if (rewardedAd == ad)
            {
                rewardedAd = null;
            }

            LoadRewardedAd();
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogError("Rewarded ad failed to show: " + error);
            ad.Destroy();
            if (rewardedAd == ad)
            {
                rewardedAd = null;
            }

            LoadRewardedAd();
        };
    }

    #endregion

    private void GrantCoins(int coins)
    {
        int currentCoins = PlayerPrefs.GetInt("totalCoins", 0);
        PlayerPrefs.SetInt("totalCoins", currentCoins + coins);
        PlayerPrefs.Save();
        OnaddFinished?.Invoke();
    }

    private void OnDestroy()
    {
        DestroyBannerAd();
        interstitialAd?.Destroy();
        rewardedAd?.Destroy();
    }

    // Legacy test helpers kept intentionally. They use a fixed 2.5-second wait,
    // so the active implementation above now loads ads ahead of time instead.
    // public void InterAdFinal()
    // {
    //     StartCoroutine(FinalInterstitialAd());
    // }
    // private IEnumerator FinalInterstitialAd()
    // {
    //     LoadInterstitalAd();
    //     yield return new WaitForSecondsRealtime(2.5f);
    //     ShowInterstitialAD();
    // }
    // public void RewardAdFinal()
    // {
    //     StartCoroutine(FinalRewardAd());
    // }
    // private IEnumerator FinalRewardAd()
    // {
    //     LoadRewardedAd();
    //     yield return new WaitForSecondsRealtime(2.5f);
    //     ShowrewardedAd();
    //     // WatchAD.instance.isAdFinished = true;
    // }
}
