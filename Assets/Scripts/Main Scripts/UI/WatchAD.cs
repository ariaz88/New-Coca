using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
public class WatchAD : MonoBehaviour
{
    public static WatchAD instance;

    [SerializeField] TextMeshProUGUI clearText;
    [SerializeField] TextMeshProUGUI timerClicked;
    [SerializeField] TextMeshProUGUI timerText;
    [SerializeField] Image loadBar; 

    //[SerializeField] Button claerButton;
    [SerializeField] private Button watchADButton; 
    [SerializeField] Button timerButton;



    [SerializeField] private float countdownTime = 10f; // Time before hammer is usable in seconds
    private float timeRemaining;

    private int clearAmount;
    private int remainingClearUse;

    public enum ClearState
    {
        OnTimer,
        OnActive
    }
    public ClearState CurrentState;

    private bool watchADActivated = false;
    private bool isCountdownOver = false;
    public bool isAdFinished = false; // Flag to track ad completion
    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        clearAmount = PlayerPrefs.GetInt("Clear", 1);

        watchADButton.interactable = false; // Initially disable the hammer button
        watchADActivated = false;
        isCountdownOver = false;
        InitializeState();
        //TryStartCountdown();
    }
    private void InitializeState()
    {     
        
            CurrentState = ClearState.OnTimer;
            StartCoroutine(StartCountdown());

        UpdateUIBasedOnState();
    }
    private void TryStartCountdown()
    {
        if (!isCountdownOver && !watchADActivated)
        {
            watchADButton.interactable = false; // Disable button during countdown
            StartCoroutine(StartCountdown());
        }
    }
    private void UpdateText()
    {
        clearText.text = remainingClearUse.ToString();

    }
    //private void Update()
    //{
    //    if ( !isCountdownOver && !watchADActivated)
    //    {
    //        StartCoroutine(StartCountdown());
    //    }
      
    //}

    private IEnumerator StartCountdown()
    {
        yield return new WaitForSeconds(countdownTime);
        CurrentState = ClearState.OnActive;
        UpdateUIBasedOnState();

        //isCountdownOver = true;
        //watchADButton.interactable = true;
        //remainingClearUse = PlayerPrefs.GetInt("Clear", 1); // Reset hammer uses
        //UpdateText();
    }

    private void UpdateUIBasedOnState()
    {
        switch (CurrentState)
        {           

            case ClearState.OnTimer:
                timeRemaining = countdownTime;
                timerButton.gameObject.SetActive(true);
                watchADButton.gameObject.SetActive(false);
                watchADButton.interactable = false; // Disable the swap button
                StartCoroutine(UpdateProgressBar());
                break;

            case ClearState.OnActive:
                timerButton.gameObject.SetActive(false);
                watchADButton.gameObject.SetActive(true);
                watchADButton.interactable = true;

                remainingClearUse = PlayerPrefs.GetInt("Clear", clearAmount);
                clearText.text = remainingClearUse.ToString();
                break;
        }
    }

    private IEnumerator UpdateProgressBar()
    {
        while (timeRemaining > 0)
        {
            // Update the progress bar
            loadBar.fillAmount = (countdownTime - timeRemaining) / countdownTime;

            // Format the time in "MM:SS"
            int minutes = Mathf.FloorToInt(timeRemaining / 60);
            int seconds = Mathf.FloorToInt(timeRemaining % 60);
            timerText.text = $"{minutes:D2}:{seconds:D2}";

            // Decrement the time
            timeRemaining -= Time.deltaTime;

            // Wait for the next frame
            yield return null;
        }

        // Countdown is complete
        //isCountdownActive = false;
        timeRemaining = 0;
        timerText.text = "00:00";
        loadBar.fillAmount = 0;

        // Perform swap activation or other actions here
        //ActivateSwapIcon();
    }
    private void ShakerAnim(TextMeshProUGUI text)
    {
        text.gameObject.SetActive(true);
        const float duration = 0.5f;
        const float strength = 4f;
        text.transform.DOShakePosition(duration, strength).OnComplete(() => {
            text.gameObject.SetActive(false);
        });
    }

    public void OnTimerStateClicked()
    {
        if (CurrentState != ClearState.OnTimer)
        {
            return;
        }
        ShakerAnim(timerClicked);

    }

    public void ActivateClear()
    {
        CurrentState = ClearState.OnActive;
            ActivateAd();
        if (isCountdownOver)
        {
            watchADActivated = true;
        }
    }
    private void RewardAdWithCallback(System.Action onComplete)
    {
        onComplete?.Invoke();

        isAdFinished = false; // Reset the flag
        //AdManager.instance.RewardAdFinal();

        // Start the coroutine and pass the callback
        //StartCoroutine(ImplementCallBack(onComplete));
    }

    private IEnumerator ImplementCallBack(System.Action onComplete)
    {
        // Simulate the ad duration
        yield return new WaitForSeconds(2.6f);

        // Set the flag to true
        isAdFinished = true;

        // Invoke the callback
        Debug.Log("Ad Finished, executing callback");
        onComplete?.Invoke();
    }


    public void ActivateAd()
    {
        if (SwapController.instance.IsSwapButtonPressed || HammerManager.instance.IsHammerButtonPressed)
        {
            return;
        }
        if (isCountdownOver || CurrentState == ClearState.OnActive)
        {
            watchADActivated = true;

            //AdManager.instance.RewardAdFinal();
            //Board.instance.ClearBoard();

            //watchADActivated = false; // Deactivate the hammer
            //isCountdownOver = false;
            //watchADButton.interactable = false;
            RewardAdWithCallback(() =>
            {
                // Logic to run after the ad finishes
                watchADActivated = false;
                Board.instance.ClearBoard();
                remainingClearUse--;
                SaveSwapData();

                if (remainingClearUse<=0)
                {
                //isCountdownOver = false;
                //watchADButton.interactable = false;
                    CurrentState = ClearState.OnTimer;
                    StartCoroutine(StartCountdown());
                    Debug.Log("Inside The Watch");
                clearAmount = 1;
                    SaveSwapData();

                }
            });
            //UpdateText();
            UpdateUIBasedOnState();

        }
    }
    private void SaveSwapData()
    {
        if (remainingClearUse > 1)
        {
            GameDataManager.instance.SaveInt("Clear", remainingClearUse);
        }
        else
        {
            GameDataManager.instance.SaveInt("Clear", 1);

        }
    }

    public void SetCleanerCount(int amount)
    {
        clearAmount += amount;
        remainingClearUse = clearAmount;
        SaveSwapData();
        UpdateText();

    }
    public void WatchAddAndRemoveBox()
    {
        //AdManager.instance.RewardAdFinal();


    }

}
