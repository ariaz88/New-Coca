using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class SwapController : MonoBehaviour
{
    public static SwapController instance;
    public bool IsSwapButtonPressed { get; private set; }
    private bool buttonActivated;
    [SerializeField] TextMeshProUGUI swapText;
    [SerializeField] TextMeshProUGUI timerClicked;
    [SerializeField] TextMeshProUGUI levelClicked;
    [SerializeField] TextMeshProUGUI timerText;
    [SerializeField] Image loadBar;
    [SerializeField] Image activeImage;
    [SerializeField] Image deactiveImage;
    [SerializeField] Image glowImage;

    [SerializeField] Button levelImage;
    [SerializeField] Button swapButton;
    [SerializeField] Button timerButton;

    private Box firstSelectedBox;
    private Box secondSelectedBox;
    private bool isSwapActive;
  
    private enum SwapState
    {
        LevelNotReached,
        OnTimer,
        OnActive
    }
    private SwapState currentState;

    private int defaultSwapAmount = 1;
    private int remainingSwapUses;
    [SerializeField] private bool isLevel3Reached = false; // Change to check for level 3
    [SerializeField] private float countdownTime = 10f;
    [SerializeField] private float countdownGlow = 5f;
    private float timeRemaining;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        timeRemaining = countdownTime;
        InitializeState();
    }

    private void InitializeState()
    {
        if (!isLevel3Reached)
        {
            currentState = SwapState.LevelNotReached;
        }
        else
        {
            currentState = SwapState.OnTimer;
            StartCoroutine(StartCountdown());
        }
        UpdateUIBasedOnState();
    }

    private void UpdateUIBasedOnState()
    {
        switch (currentState)
        {
            case SwapState.LevelNotReached:
                deactiveImage.gameObject.SetActive(true);
                levelImage.gameObject.SetActive(true);
                timerButton.gameObject.SetActive(false);
                swapButton.gameObject.SetActive(false);
                isSwapActive = false;
                break;

            case SwapState.OnTimer:
                glowImage.gameObject.SetActive(false);
                timeRemaining = countdownTime;
                deactiveImage.gameObject.SetActive(false);
                levelImage.gameObject.SetActive(false);
                timerButton.gameObject.SetActive(true);
                swapButton.gameObject.SetActive(false);
                swapButton.interactable = false; // Disable the swap button            
                isSwapActive = false;
                StartCoroutine(UpdateProgressBar());


                break;

            case SwapState.OnActive:
                isSwapActive = true;

                deactiveImage.gameObject.SetActive(false);
                levelImage.gameObject.SetActive(false);
                timerButton.gameObject.SetActive(false);
                swapButton.gameObject.SetActive(true);
                swapButton.interactable = true;             
                remainingSwapUses = PlayerPrefs.GetInt("Swap", defaultSwapAmount);
                swapText.text = remainingSwapUses.ToString();
                break;
        }
    }
    private IEnumerator UpdateProgressBar()
    {
        while (timeRemaining > 0)
        {
            // Update the progress bar
            loadBar.fillAmount = (countdownTime-timeRemaining) / countdownTime;

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

    private IEnumerator StartCountdown()
    {
        yield return new WaitForSeconds(countdownTime);
        currentState = SwapState.OnActive;
        UpdateUIBasedOnState();
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

    public void OnActivateSwap()
    {
        if (currentState != SwapState.OnActive || HammerManager.instance.IsHammerActive())
        {
            return;
        }
        ActivateSwapFeature();
        //ActivateSwapIcon();
    }
    public void OnTimerStateClicked()
    {
        if (currentState != SwapState.OnTimer)
        {
            return;
        }
        ShakerAnim(timerClicked);

    }
   

    public void OnLevelStateClicked()
    {
        if (currentState != SwapState.LevelNotReached)
        {
            return;
        }
        ShakerAnim(levelClicked);

    }

    private void ActivateSwapFeature()
    {
        currentState = SwapState.OnActive;
        //isSwapActive = true;
        firstSelectedBox = null;
        secondSelectedBox = null;
    }

    private void DeactivateSwapFeature()
    {
        //isSwapActive = false;
        firstSelectedBox = null;
        secondSelectedBox = null;
    }

    public void OnBoxClicked(Box clickedBox)
    {
        if (currentState != SwapState.OnActive || !clickedBox.IsOnBoard || HammerManager.instance.IsHammerButtonPressed)
        {
            return;
        }
        if (buttonActivated)
        {
            return;

        }
        Debug.Log("Swaping Boxes");

        if (firstSelectedBox == null)
        {
            firstSelectedBox = clickedBox;
            HighlightBox(firstSelectedBox, true);
        }
        else if (secondSelectedBox == null && Board.instance.AreAdjacent(firstSelectedBox, clickedBox))
        {
            secondSelectedBox = clickedBox;
            if (secondSelectedBox != firstSelectedBox)
            {
            HighlightBox(secondSelectedBox, true);
            Board.instance.DOSWAP(firstSelectedBox, secondSelectedBox);
            remainingSwapUses--;
             buttonActivated = true;
            DeactivateSwapFeature();

            }

            SaveSwapData();

            if (remainingSwapUses <= 0)
            {
                SaveSwapData();
                ResetSwapTimer();
            }

            swapText.text = remainingSwapUses.ToString();
        }
     
        else
        {
            // Reset selection if invalid or not adjacent
            HighlightBox(firstSelectedBox, false);
            HighlightBox(secondSelectedBox, false);
            firstSelectedBox = clickedBox;
            secondSelectedBox = null;
            HighlightBox(firstSelectedBox, true);
        }
    }

    private void ResetSwapTimer()
    {
        currentState = SwapState.OnTimer;
        //isSwapActive = false;
        UpdateUIBasedOnState();
        StartCoroutine(StartCountdown());
    }

    private void ActivateSwapIcon()
    {
        if (activeImage != null)
        {
            activeImage.gameObject.SetActive(true);
        }
    }

    private void DeactivateSwapIcon()
    {
        if (activeImage != null)
        {
            activeImage.gameObject.SetActive(false);
        }
    }

    public void HighlightBox(Box box, bool highlight)
    {
        if (box != null)
        {
            box.GetComponentInChildren<MeshRenderer>().material = highlight ? box.highLightMaterialForSwap : box.OriginalMaterial;
        }
    }
    public bool IsSwapActive()
    {
        return currentState == SwapState.OnActive;
    }

    public void OnSwapButtonPressed()
    {
        IsSwapButtonPressed = true;
        glowImage.gameObject.SetActive(true);

        Debug.Log("Swap button pressed");
        StartCoroutine(ResetAfterDelay());
    }

    private IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(countdownGlow); // Reset after 5 seconds
        IsSwapButtonPressed = false;
        glowImage.gameObject.SetActive(false);
        HighlightBox(firstSelectedBox, false);
        DeactivateSwapFeature();
        buttonActivated = false;
        Debug.Log("Swap button auto-reset");
    }

    public void OnClickSwap()
    {
        //if (SwapController.instance != null)
        //{
        //    SwapController.instance.OnSwapButtonPressed();
        //}
        OnSwapButtonPressed();
    }
    private void SaveSwapData()
    {
        if (remainingSwapUses>1)
        {
        GameDataManager.instance.SaveInt("Swap", remainingSwapUses);
        }
        else
        {
            GameDataManager.instance.SaveInt("Swap", 1);

        }
    }

    public void SetSwapCount(int amount)
    {
        defaultSwapAmount += amount;
        remainingSwapUses = defaultSwapAmount;
        SaveSwapData();
        swapText.text = remainingSwapUses.ToString();
    }
}
