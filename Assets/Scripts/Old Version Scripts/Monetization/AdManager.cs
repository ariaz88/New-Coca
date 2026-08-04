using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using GoogleMobileAds.Api;
using TMPro;
using UnityEngine.UI;
using System;


public class AdManager : MonoBehaviour
{
//    public static AdManager instance;
//    public static Action OnaddFinished;
//    //public TextMeshProUGUI totalCoinTxt;

//    public string appID = "ca-app-pub-3940256099942544~3347511713";  //test

//#if UNITY_ANDROID

//    //real for tennisStar
//    //string bannerId = "ca-app-pub-8595026401548617/4963110117";
//    //string interId = "ca-app-pub-8595026401548617/6176065533";
//    //string rewardedId = "ca-app-pub-8595026401548617/6694940289";
//    //string nativeId = "ca-app-pub-8595026401548617/1497619772";


//    //test
//    string bannerId = "ca-app-pub-3940256099942544/6300978111";
//    string interId = "ca-app-pub-3940256099942544/1033173712";
//    string rewardedId = "ca-app-pub-3940256099942544/5224354917";
//    string nativeId = "ca-app-pub-3940256099942544/2247696110";


//    //#elif UNITY_IPHONE
//    //    string bannerId = "ca-app-pub-3940256099942544/2934735716";
//    //    string interId = "ca-app-pub-3940256099942544/4411468910";
//    //    string rewardedId = "ca-app-pub-3940256099942544/1712485313";
//    //    string nativeId = "ca-app-pub-3940256099942544/3986624511";

//#endif

//    BannerView bannerView;
//    InterstitialAd interstitialAd;
//    RewardedAd rewardedAd;
//    NativeAd nativeAd;

//    void Awake()
//    {
//        CheckInstance();
//        //Advertisement.Initialize(appID);
//        InitializeMyAdd();
//    }
//    void CheckInstance()
//    {
//        if (instance == null)
//        {
//            instance = this;
//        }
//        else
//        {
//            Destroy(gameObject);
//        }

//        DontDestroyOnLoad(this.gameObject);
//    }
//    //private void Start() {

//    //}

//    public void InitializeMyAdd()
//    {
//        //ShowCoins();
//        MobileAds.RaiseAdEventsOnUnityMainThread = true;
//        MobileAds.Initialize(initStatus => {
//            //  print("Ads Initialiased !!");
//        });
//        LoadRewardedAd();
//    }
//    #region Banner

//    public void LoadBannerAd()
//    {
//        //creat banner
//        CreatebannerView();

//        //listeb to banner events
//        ListenToBannerEvents();

//        //load the banner
//        if (bannerView == null)
//        {
//            CreatebannerView();
//        }

//        var adrequest = new AdRequest();
//        adrequest.Keywords.Add("unity-admob-sample");

//        //print("Loading Banner Ad!!");
//        bannerView.LoadAd(adrequest);   // show the banner add on screen
//    }
//    void CreatebannerView()
//    {
//        if (bannerView == null)
//        {
//            DestroyBannerAd();
//        }
//        bannerView = new BannerView(bannerId, AdSize.Banner, AdPosition.Top); //small
//        //bannerView = new BannerView(bannerId, AdSize.MediumRectangle, AdPosition.Top);  // medium

//    }

//    void ListenToBannerEvents()
//    {
//        // Raised when an ad is loaded into the banner view.
//        bannerView.OnBannerAdLoaded += () => {
//            //Debug.Log("Banner view loaded an ad with response : "
//            //    + bannerView.GetResponseInfo());
//        };
//        // Raised when an ad fails to load into the banner view.
//        bannerView.OnBannerAdLoadFailed += (LoadAdError error) => {
//            Debug.LogError("Banner view failed to load an ad with error : "
//                + error);
//        };
//        // Raised when the ad is estimated to have earned money.
//        bannerView.OnAdPaid += (AdValue adValue) => {
//            Debug.Log(("Banner view paid {0} {1}.",
//                adValue.Value,
//                adValue.CurrencyCode));
//        };
//        // Raised when an impression is recorded for an ad.
//        bannerView.OnAdImpressionRecorded += () => {
//            // Debug.Log("Banner view recorded an impression.");
//        };
//        // Raised when a click is recorded for an ad.
//        bannerView.OnAdClicked += () => {
//            //Debug.Log("Banner view was clicked.");
//        };
//        // Raised when an ad opened full screen content.
//        bannerView.OnAdFullScreenContentOpened += () => {
//            //Debug.Log("Banner view full screen content opened.");
//        };
//        // Raised when the ad closed full screen content.
//        bannerView.OnAdFullScreenContentClosed += () => {
//            //Debug.Log("Banner view full screen content closed.");
//        };
//    }
//    public void DestroyBannerAd()
//    {
//        if (bannerView != null)
//        {
//            //print("Destroying banner Ad");
//            bannerView.Destroy();
//            bannerView = null;
//        }
//    }

//    #endregion

//    #region Interstitial

//    public void LoadInterstitalAd()
//    {

//        if (interstitialAd != null)
//        {
//            interstitialAd.Destroy();
//            interstitialAd = null;
//        }

//        var adrequest = new AdRequest();
//        adrequest.Keywords.Add("unity-admob-sample");

//        InterstitialAd.Load(interId, adrequest, (InterstitialAd ad, LoadAdError error) => {

//            if (error != null || ad == null)
//            {
//                print("interstitial ad failed to load " + error);
//                return;
//            }

//            print("interstitial ad loaded " + ad.GetResponseInfo());

//            interstitialAd = ad;
//            InterstitialEvent(interstitialAd);

//        });

//    }
//    public void ShowInterstitialAD()
//    {

//        if (interstitialAd != null && interstitialAd.CanShowAd())
//        {
//            interstitialAd.Show();
//        }
//        else
//        {
//            print("Interstitial ad not ready !!");
//        }
//    }

//    public void InterstitialEvent(InterstitialAd ad)
//    {
//        // Raised when the ad is estimated to have earned money.
//        interstitialAd.OnAdPaid += (AdValue adValue) => {
//            Debug.Log(String.Format("Interstitial ad paid {0} {1}.",
//                adValue.Value,
//                adValue.CurrencyCode));
//        };
//        // Raised when an impression is recorded for an ad.
//        interstitialAd.OnAdImpressionRecorded += () => {
//            Debug.Log("Interstitial ad recorded an impression.");
//        };
//        // Raised when a click is recorded for an ad.
//        interstitialAd.OnAdClicked += () => {
//            Debug.Log("Interstitial ad was clicked.");
//        };
//        // Raised when an ad opened full screen content.
//        interstitialAd.OnAdFullScreenContentOpened += () => {
//            Debug.Log("Interstitial ad full screen content opened.");
//        };
//        // Raised when the ad closed full screen content.
//        interstitialAd.OnAdFullScreenContentClosed += () => {
//            Debug.Log("Interstitial ad full screen content closed.");
//        };
//        // Raised when the ad failed to open full screen content.
//        interstitialAd.OnAdFullScreenContentFailed += (AdError error) => {
//            Debug.LogError("Interstitial ad failed to open full screen content " +
//                           "with error : " + error);
//        };
//    }

//    #endregion

//    #region Rewarded

//    public void LoadRewardedAd()
//    {

//        if (rewardedAd != null)
//        {
//            rewardedAd.Destroy();
//            rewardedAd = null;
//        }

//        var adrequest = new AdRequest();
//        adrequest.Keywords.Add("unity-admob-sample");

//        RewardedAd.Load(rewardedId, adrequest, (RewardedAd ad, LoadAdError error) => {

//            if (error != null || ad == null)
//            {
//                print("Reward Faild to load " + error);
//                return;
//            }

//            print("Reward ad Loaded !!");
//            rewardedAd = ad;
//            RewardedAdEvents(rewardedAd);
//        });
//    }

//    public void ShowrewardedAd()
//    {

//        if (rewardedAd != null && rewardedAd.CanShowAd())
//        {

//            rewardedAd.Show((Reward reward) => {
//                print("Give Reward to player !!");

//                GrantCoins(1);
//            });
//        }

//        else
//        {
//            print("Reward ad not Ready !! ");
//        }
//    }

//    public void RewardedAdEvents(RewardedAd ad)
//    {
//        // Raised when the ad is estimated to have earned money.
//        ad.OnAdPaid += (AdValue adValue) => {
//            Debug.Log(String.Format("Rewarded ad paid {0} {1}.",
//                adValue.Value,
//                adValue.CurrencyCode));
//        };
//        // Raised when an impression is recorded for an ad.
//        ad.OnAdImpressionRecorded += () => {
//            Debug.Log("Rewarded ad recorded an impression.");
//        };
//        // Raised when a click is recorded for an ad.
//        ad.OnAdClicked += () => {
//            Debug.Log("Rewarded ad was clicked.");
//        };
//        // Raised when an ad opened full screen content.
//        ad.OnAdFullScreenContentOpened += () => {
//            Debug.Log("Rewarded ad full screen content opened.");
//        };
//        // Raised when the ad closed full screen content.
//        ad.OnAdFullScreenContentClosed += () => {
//            Debug.Log("Rewarded ad full screen content closed.");
//        };
//        // Raised when the ad failed to open full screen content.
//        ad.OnAdFullScreenContentFailed += (AdError error) => {
//            Debug.LogError("Rewarded ad failed to open full screen content " +
//                           "with error : " + error);
//        };
//    }

//    #endregion

//    #region Native

//    public Image img;

//    public void RequestNativeAd()
//    {
//        AdLoader adloader = new AdLoader.Builder(nativeId).ForNativeAd().Build();

//        adloader.OnNativeAdLoaded += this.HandleNativeAdLoaded;
//        adloader.OnAdFailedToLoad += this.HandleNativeAdfailedToLoad;

//        //adloader.LoadAd(new AdRequest.Builder().Build());
//    }

//    private void HandleNativeAdLoaded(object sender, NativeAdEventArgs e)
//    {
//        print("Native ad Loaded");
//        this.nativeAd = e.nativeAd;

//        if (img != null)
//        {
//            Texture2D iconTexture = this.nativeAd.GetIconTexture();
//            Sprite sprite = Sprite.Create(iconTexture, new Rect(0, 0, iconTexture.width, iconTexture.height), Vector2.one * 0.5f);

//            img.sprite = sprite;
//        }

//    }
//    private void HandleNativeAdfailedToLoad(object sender, AdFailedToLoadEventArgs e)
//    {
//        print("native ad Failed to Load !!");
//    }


//    #endregion


//    #region extra

//    void GrantCoins(int coins)
//    {
//        if (PlayerPrefs.HasKey("totalCoins"))
//        {
//            int crrCoins = PlayerPrefs.GetInt("totalCoins");
//            crrCoins += coins;
//            PlayerPrefs.SetInt("totalCoins", crrCoins);
//        }
//        else
//        {
//            PlayerPrefs.SetInt("totalCoins", 1);
//        }
//        OnaddFinished?.Invoke();
//        //  ShowCoins();
//    }

//    public void InterAdFinal()
//    {
//        StartCoroutine(FinalInterstitialAd());
//    }
//    IEnumerator FinalInterstitialAd()
//    {
//        LoadInterstitalAd();

//        yield return new WaitForSecondsRealtime(2.5f);
//        ShowInterstitialAD();
//    }

//    public void RewardAdFinal()
//    {
//        StartCoroutine(FinalRewardAd());
//    }
//    IEnumerator FinalRewardAd()
//    {
//        LoadRewardedAd();

//        yield return new WaitForSecondsRealtime(2.5f);
//        ShowrewardedAd();
//        WatchAD.instance.isAdFinished = true;
//    }

//    #endregion
}
