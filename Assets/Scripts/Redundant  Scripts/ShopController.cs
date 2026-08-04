using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopController : MonoBehaviour
{
    [SerializeField] Button claimButton; 
    [SerializeField] Button watchAdClaimButton; 
    [SerializeField] GameObject ResetHolder;
    public TextMeshProUGUI timerText; // Text to display the remaining time
    public int rewardAmount = 100; 
    private DateTime nextClaimTime; // Time when the button becomes active again
    private bool isTimerActive; // Tracks if the timer is running
    private const string TimerKey = "NextClaimTime";



    private void Start()
    {
        claimButton.onClick.RemoveAllListeners();
        claimButton.onClick.AddListener(OnClaimButtonClick);

        watchAdClaimButton.onClick.RemoveAllListeners();
        watchAdClaimButton.onClick.AddListener(OnWatchAdButtonClick);

        //ClearTimerHistory();
        // Load the nextClaimTime from PlayerPrefs
        if (PlayerPrefs.HasKey("NextClaimTime"))
        {
            nextClaimTime = DateTime.Parse(PlayerPrefs.GetString("NextClaimTime"));
            if (DateTime.Now < nextClaimTime)
            {
                isTimerActive = true;
                claimButton.interactable = false; // Disable the button if timer is active
                claimButton.gameObject.SetActive(false);
                ResetHolder.gameObject.SetActive(true);
                StartCoroutine(UpdateTimer());

            }
        }
        else
        {
            // If no saved time, set the button to be available
            nextClaimTime = DateTime.Now;
            ActiveState();
        }

    }

    public void OnClaimButtonClick()
    {
        // Reward the player
        int currentCoins = PlayerPrefs.GetInt("TotalCoins", 0);
        currentCoins += rewardAmount;
        PlayerPrefs.SetInt("TotalCoins", currentCoins);

        // Set the next claim time to 24 hours from now
        nextClaimTime = DateTime.Now.AddHours(1);
        PlayerPrefs.SetString("NextClaimTime", nextClaimTime.ToString());
        PlayerPrefs.Save();

        // Start the timer
        isTimerActive = true;
        claimButton.interactable = false;
        claimButton.gameObject.SetActive(false);
        ResetHolder.gameObject.SetActive(true);
        StartCoroutine(UpdateTimer());

    }
    public void OnWatchAdButtonClick()
    {
        

    }

    public void ClearTimerHistory()
    {
        if (PlayerPrefs.HasKey(TimerKey))
        {
            PlayerPrefs.DeleteKey(TimerKey);
            Debug.Log("Timer history cleared.");
        }
        else
        {
            Debug.Log("No timer history found to clear.");
        }
    }
    private IEnumerator UpdateTimer()
    {
        while (isTimerActive)
        {
            claimButton.gameObject.SetActive(false);
            ResetHolder.gameObject.SetActive(true);
            TimeSpan remainingTime = nextClaimTime - DateTime.Now;
            if (remainingTime.TotalSeconds > 0)
            {
                // Update the timer text
                timerText.text = $"{remainingTime.Hours:D2}:{remainingTime.Minutes:D2}:{remainingTime.Seconds:D2}";
            }
            else
            {
                 //timerText.text = "READY!";
                ActiveState();
                 yield break; // Exit the coroutine when done
            }

            // Wait for 1 second before updating again
            yield return new WaitForSeconds(1f);
        }
    }
    private void ActiveState()
    {
        isTimerActive = false;
        claimButton.interactable = true;
        claimButton.gameObject.SetActive(true);
        ResetHolder.gameObject.SetActive(false);
    }
    //private void Update()
    //{
    //    //// If the timer is active, update the countdown
    //    //if (isTimerActive)
    //    //{
    //    //    claimButton.gameObject.SetActive(false);
    //    //    ResetHolder.gameObject.SetActive(true);

    //    //    TimeSpan remainingTime = nextClaimTime - DateTime.Now;
    //    //    Debug.Log(" nextClaimTime is : " + nextClaimTime + " DateTime.Now is : " + DateTime.Now);
    //    //    if (remainingTime.TotalSeconds > 0)
    //    //    {
    //    //        // Update the timer text
    //    //        timerText.text = $" {remainingTime.Hours:D2}:{remainingTime.Minutes:D2}:{remainingTime.Seconds:D2}";
    //    //    }
    //    //    else
    //    //    {
    //    //        // Timer finished, enable the button
    //    //        ActiveState();
    //    //        //timerText.text = "READY!";
    //    //    }
    //    //}
    //}
}
