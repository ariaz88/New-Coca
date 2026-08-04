using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;
public class HammerManager : MonoBehaviour
{
    public static HammerManager instance;
    public bool IsHammerButtonPressed { get; private set; }

    [SerializeField] TextMeshProUGUI hammerText;
    [SerializeField] TextMeshProUGUI timerClicked;
    [SerializeField] TextMeshProUGUI levelClicked;
    [SerializeField] TextMeshProUGUI timerText;
    [SerializeField] Image loadBar;
    [SerializeField] Image hammerIcon;
    [SerializeField] Image deactiveImage;

    [SerializeField] Button levelImage;
    [SerializeField] Button hammerButton;
    [SerializeField] Button timerButton;

    private enum HammerState
    {
        LevelNotReached,
        OnTimer,
        OnActive
    }
    private HammerState currentState;


    [SerializeField] private float countdownTime = 120f; // Time before hammer is usable in seconds
    private float timeRemaining;

    private int hammerAmount;
    private int remainingHammerUses; // Current number of hammer uses remaining

    //private bool hammerActivated = false;
    //private bool isCountdownOver = false;
    bool isLevel5Reached = true;

    int boardColumn, boardRow;
    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        timeRemaining = countdownTime;
        InitializeState();

        hammerAmount = PlayerPrefs.GetInt("Remove", 1);
    }

    //private void Update()
    //{
    //    if (Input.GetKey(KeyCode.B))
    //    {
    //        isLevel3Reached = true;
    //    }

    //    if (/*SceneManager.GetActiveScene().buildIndex == 3*/ isLevel3Reached && !isCountdownOver)
    //    {
    //        hammerButton.interactable = false; 
    //        StartCoroutine(StartCountdown());
    //    }
    //    if (!isCountdownOver)
    //    {
    //        DeactivateHammerIcon();
    //    }
    //}
    private void InitializeState()
    {
        if (!isLevel5Reached)
        {
            currentState = HammerState.LevelNotReached;
        }
        else
        {
            currentState = HammerState.OnTimer;
            StartCoroutine(StartCountdown());
        }
        UpdateUIBasedOnState();
    }
    private void TryStartCountdown()
    {
        //if (isLevel5Reached && !isCountdownOver && !hammerActivated)
        //{
        //    hammerButton.interactable = false; // Disable button during countdown
        //    StartCoroutine(StartCountdown());
        //}
    }

    private IEnumerator StartCountdown()
    {
        yield return new WaitForSeconds(countdownTime);
        currentState = HammerState.OnActive;
        UpdateUIBasedOnState();
        //**************************************************
        //yield return new WaitForSeconds(countdownTime);
        //isCountdownOver = true;
        //hammerButton.interactable = true;
        ////ActivateHammerIcon();
        //Debug.Log("Restart Coroutine");

        //remainingHammerUses = PlayerPrefs.GetInt("Remove", 1); // Reset hammer uses
        //UpdateText();

    }
    private void UpdateUIBasedOnState()
    {
        switch (currentState)
        {
            case HammerState.LevelNotReached:
                deactiveImage.gameObject.SetActive(true);
                levelImage.gameObject.SetActive(true);
                timerButton.gameObject.SetActive(false);
                hammerButton.gameObject.SetActive(false);
                break;

            case HammerState.OnTimer:
                timeRemaining = countdownTime;
                deactiveImage.gameObject.SetActive(false);
                levelImage.gameObject.SetActive(false);
                timerButton.gameObject.SetActive(true);
                hammerButton.gameObject.SetActive(false);
                hammerButton.interactable = false; // Disable the swap button
              
                StartCoroutine(UpdateProgressBar());


                break;

            case HammerState.OnActive:
                deactiveImage.gameObject.SetActive(false);
                levelImage.gameObject.SetActive(false);
                timerButton.gameObject.SetActive(false);
                hammerButton.gameObject.SetActive(true);
                hammerButton.interactable = true;
              
                remainingHammerUses = PlayerPrefs.GetInt("Remove", hammerAmount);
                hammerText.text = remainingHammerUses.ToString();
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

    private void UpdateText()
    {
        hammerText.text = remainingHammerUses.ToString();

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
        if (currentState != HammerState.OnTimer)
        {
            return;
        }
        ShakerAnim(timerClicked);

    }


    public void OnLevelStateClicked()
    {
        if (currentState != HammerState.LevelNotReached)
        {
            return;
        }
        ShakerAnim(levelClicked);

    }

    public void OnActivateHammer()
    {
        if (currentState != HammerState.OnActive /*|| SwapController.instance.IsSwapActive()*/ )
        {
            return;
        }
        currentState = HammerState.OnActive;
        HighlightAllBoxes(); 
        //****************************************************
        //if (SwapController.instance.IsSwapActive())
        //{
        //    return;
        //}
        //if (isCountdownOver)
        //{
        //    //hammerActivated = true;
        //    hammerButton.GetComponent<Image>().color = Color.blue;

        //   // *******************************************
        //    //hammerActivated = !hammerActivated;


        //    //hammerActivated = true;
        //    //hammerCount = hammerFinalCountDown;
        //    //hammerButton.GetComponent<Image>().color = hammerActivated ? Color.blue : Color.gray;

        //    //// Highlight all box positions if activated
        //    //if (hammerActivated)
        //    //{
        //    //    HighlightAllBoxes();
        //    //}
        //    //else
        //    //{
        //    //    ClearAllHighlights();
        //    //}
        //}
    }

    private void HighlightAllBoxes()
    {
        Board.instance.HighLightHammerNode();
    }

    private void ClearAllHighlights()
    {
        Board.instance.UnHighLightHammerNode();

    }

    //public void OnBoxClicked(Box box)
    //{
    //    if (hammerActivated && box.IsOnBoard)
    //    {
    //        box.DestroyBox();
    //        //box.CloseBox(); // Removes the box
    //        ClearAllHighlights(); // Remove highlights
    //        if (hammerCount>0)
    //        {
    //            hammerCount--;
    //        }
    //        else
    //        {
    //            hammerActivated = false; // Deactivate the hammer
    //            isCountdownOver = false;
    //            hammerButton.GetComponent<Image>().color = Color.white;

    //        }

    //    }
    //}
    public void OnBoxClicked(Box box)
    {
        if (SwapController.instance.IsSwapButtonPressed)
        {
            return;
        }
        if (currentState == HammerState.OnActive && box.IsOnBoard)
        {
            Debug.Log(" Removing  with Hammer");

            // ** setting the column and row of box, because after deleting box, isOccupied remained true
            boardColumn = box.column;
            boardRow = box.row;
            box.DestroyBox(); 

            ClearAllHighlights();
            //hammerActivated = false;
            //hammerButton.GetComponent<Image>().color = Color.white; // Reset button color
            remainingHammerUses--;
            SaveSwapData();
            StartCoroutine(SetIsOccupiedFalse());
            if (remainingHammerUses <= 0)
            {
                Debug.Log("Deactivate the hammer");
                DeactivateHammer();
                hammerAmount = 1;
                SaveSwapData();

            }
            //UpdateText();
            UpdateUIBasedOnState();

        }
    }

    IEnumerator SetIsOccupiedFalse()
    {
        yield return new WaitForSeconds(0.2f);
        //Debug.Log("SetIsOccupiedFalse");

        Board.instance.grid[boardColumn, boardRow].isOccupied = false;

    }
    private void DeactivateHammer()
    {
        currentState = HammerState.OnTimer;
        UpdateUIBasedOnState();
        StartCoroutine(StartCountdown());

        //hammerActivated = false; // Deactivate hammer
        //isCountdownOver = false; // Reset countdown
        ////hammerButton.GetComponent<Image>().color = Color.white; // Reset button color
        //hammerButton.interactable = false; // Disable the hammer button

        //// Restart the countdown if needed
        //StartCoroutine(StartCountdown());
    }

    private void ActivateHammerIcon()
    {
        levelImage.gameObject.SetActive(false); // Set the icon to active mode
        //if (hammerIcon != null)
        //{
        //    hammerIcon.gameObject.SetActive(true); // Set the icon to active mode
        //}
    }

    private void DeactivateHammerIcon()
    {
        if (hammerIcon != null)
        {
            //hammerIcon.gameObject.SetActive(false); // Set the icon to active mode
        }
    }
    public bool IsHammerActive()
    {
        return  currentState== HammerState.OnActive ;
    }

    public void OnHammerButtonPressed()
    {
        IsHammerButtonPressed = true;
        Debug.Log("Hammer button pressed");
        StartCoroutine(ResetHammerAfterDelay());
    }

    private IEnumerator ResetHammerAfterDelay()
    {
        yield return new WaitForSeconds(5f); // Reset after 5 seconds
        IsHammerButtonPressed = false;
        Debug.Log("Hammer button auto-reset");
    }

    public void OnClickHammer()
    {
        //if (SwapController.instance != null)
        //{
        //    SwapController.instance.OnSwapButtonPressed();
        //}
        OnHammerButtonPressed();
    }

    private void SaveSwapData()
    {
        if (remainingHammerUses > 1)
        {
            GameDataManager.instance.SaveInt("Remove", remainingHammerUses);
        }
        else
        {
            GameDataManager.instance.SaveInt("Remove", 1);

        }
    }
    public void SetHammerCount(int amount )
    {
        hammerAmount += amount;
        remainingHammerUses = hammerAmount;
        SaveSwapData();
        UpdateText();

    }
}
