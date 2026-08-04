using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingManager : MonoBehaviour
{
    [SerializeField] Button closeButton;
    [SerializeField] Button soundButton;
    [SerializeField] Button musicButton;
    [SerializeField] Button vibrationButton;
    [SerializeField] Button contacUsButton;
    [SerializeField] Button rateUsButton;
    [SerializeField] Button termsOfUseButton;
    [SerializeField] Button privacyPolicyButton;
    [SerializeField] Button restorePurchaseButton;

    [SerializeField] GameObject settingPanel;

    private Image musicOn, musicOff, soundOn, soundOff, vibrationOn, vibrationOff;

    private bool toggleMusicOn = true;
    private bool toggleSoundOn = false;
    private bool toggleVibrationOn = false;
    private void OnEnable()
    {
        closeButton.onClick.AddListener(HideSettingsPanel);
        musicButton.onClick.AddListener(ToggleONOFFMusics);
        soundButton.onClick.AddListener(ToggleONOFFSounds);
        vibrationButton.onClick.AddListener(ToggleONOFFVibration);
        contacUsButton.onClick.AddListener(ContactUs);
        rateUsButton.onClick.AddListener(RateUs);
        termsOfUseButton.onClick.AddListener(TermsOfServices);
        privacyPolicyButton.onClick.AddListener(PrivacyPolicy);
        restorePurchaseButton.onClick.AddListener(RestorePurchases);

    }

    private void OnDisable()
    {
        closeButton.onClick.RemoveListener(HideSettingsPanel);
        musicButton.onClick.RemoveListener(ToggleONOFFMusics);
        soundButton.onClick.RemoveListener(ToggleONOFFSounds);
        vibrationButton.onClick.RemoveListener(ToggleONOFFVibration);
        contacUsButton.onClick.RemoveListener(ContactUs);
        rateUsButton.onClick.RemoveListener(RateUs);
        termsOfUseButton.onClick.RemoveListener(TermsOfServices);
        privacyPolicyButton.onClick.RemoveListener(PrivacyPolicy);
        restorePurchaseButton.onClick.RemoveListener(RestorePurchases);

    }

    private void Start()
    {
        HideSettingsPanel();
        InitializeONOFFImages();
        toggleMusicOn = false;
        ToggleONOFFMusics();
    }
    private void InitializeONOFFImages()
    {
         musicOn = musicButton.transform.GetChild(0).GetComponent<Image>();
         musicOff = musicButton.transform.GetChild(1).GetComponent<Image>();

         soundOn = soundButton.transform.GetChild(0).GetComponent<Image>();
         soundOff = soundButton.transform.GetChild(1).GetComponent<Image>();

         vibrationOn = vibrationButton.transform.GetChild(0).GetComponent<Image>();
         vibrationOff = vibrationButton.transform.GetChild(1).GetComponent<Image>();
    }
    public void ToggleONOFFMusics()
    {
        toggleMusicOn = !toggleMusicOn;
        if (toggleMusicOn)
        {
            musicOn.gameObject.SetActive(true);
            musicOff.gameObject.SetActive(false);
        }
        else
        {
            musicOn.gameObject.SetActive(false);
            musicOff.gameObject.SetActive(true);
        }
        SoundManager.instance.ToggleMusicMute(toggleMusicOn);

    }
    public void ToggleONOFFSounds()
    {
        toggleSoundOn = !toggleSoundOn;
        if (toggleSoundOn)
        {
            soundOn.gameObject.SetActive(true);
            soundOff.gameObject.SetActive(false);
        }
        else
        {
            soundOn.gameObject.SetActive(false);
            soundOff.gameObject.SetActive(true);
        }
        SoundManager.instance.ToggleSoundMute(toggleSoundOn);


    }
    public void ToggleONOFFVibration()
    {
        toggleVibrationOn = !toggleVibrationOn;
        if (toggleVibrationOn)
        {
            vibrationOn.gameObject.SetActive(true);
            vibrationOff.gameObject.SetActive(false);
        }
        else
        {
            vibrationOn.gameObject.SetActive(false);
            vibrationOff.gameObject.SetActive(true);

        }
        //VibrationSettings.Instance.ToggleVibration(toggleVibrationOn);
    }

    public void ContactUs()
    {
        Application.OpenURL("https://www.arashaziizpour@gmail.com");
        Debug.Log("ContactUs");


    }
    public void RateUs()
    {
        Debug.Log("RateUs");

    }
    public void TermsOfServices()
    {
        Debug.Log("TermsOfServices");
    }
    public void PrivacyPolicy()
    {
        Debug.Log("PrivacyPolicy");
    }
    public void RestorePurchases()
    {
        Debug.Log("RestorePurchases");

    }

    public void ShowSettingsPanel()
    {
        settingPanel.gameObject.SetActive(true);
    }
    public void HideSettingsPanel()
    {
        settingPanel.gameObject.SetActive(false);
    }

}
