using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;


public class RevivePanel : MonoBehaviour
{
    public static RevivePanel instance;

    [SerializeField] Button reviveButton;
    [SerializeField] Button noThanksButton;
    [SerializeField] GameObject revivePanel;
    [SerializeField] GameObject losePanel;
    [SerializeField] Image BGImage;
    [SerializeField] Image progressBar;
    [SerializeField] TextMeshProUGUI barText;
    [Tooltip("Gem cost for each successive revive within one level. The last entry repeats once the player runs past the end of the list.")]
    [SerializeField] int[] reviveCosts = { 5, 7, 10, 15, 20 };
    [Tooltip("Optional label showing the current revive price. Safe to leave empty.")]
    [SerializeField] TextMeshProUGUI costText;

    public bool isReviveButtonPressed;
    public bool isReviveActive;
    public float countdownDuration = 9f;

    // Counts revives used in this level only. RevivePanel lives in the scene, so
    // this resets naturally whenever a new level loads.
    private int reviveUseCount;
    private Coroutine countdownRoutine;

    /// <summary>
    /// Price of the next revive. Escalates through reviveCosts and then stays at
    /// the final entry so the cost never stops rising unexpectedly.
    /// </summary>
    public int CurrentReviveCost
    {
        get
        {
            if (reviveCosts == null || reviveCosts.Length == 0)
            {
                return 5;
            }

            return reviveCosts[Mathf.Min(reviveUseCount, reviveCosts.Length - 1)];
        }
    }
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }
    private void Start()
    {
        //progressBar.fillAmount = levelNumer - 1 / desieredLevel;

        //StartCoroutine(StartCountdown());
    }
 public void ShowCoroutine()
    {
        if (/*GameManager.instance.gameEnded*/  isReviveActive && !isReviveButtonPressed)
        {
            Debug.Log(" Revive is active");

            UpdateCostLabel();

            //revivePanel.gameObject.SetActive(true);
            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
            }

            countdownRoutine = StartCoroutine(StartCountdown());
        }
    }

    private void UpdateCostLabel()
    {
        if (costText != null)
        {
            costText.text = CurrentReviveCost.ToString();
        }
    }
    private IEnumerator StartCountdown()
    {

        float elapsedTime = 0f; // Time that has passed
        float remainingTime = countdownDuration; // Remaining time

        while (elapsedTime < countdownDuration)
        {
            // Update the progress bar fillAmount (0 to 1)
            progressBar.fillAmount = elapsedTime / countdownDuration;

            // Update the countdown text
            barText.text = Mathf.CeilToInt(remainingTime).ToString();

            // Wait for the next frame
            yield return null;

            // Update elapsed and remaining time
            elapsedTime += Time.unscaledDeltaTime;
            remainingTime = countdownDuration - elapsedTime;
        }

        // Ensure progress bar is fully filled and text shows 0 at the end
        progressBar.fillAmount = 1f;
        barText.text = "0";
        //******************************
        if (!isReviveButtonPressed && isReviveActive)
        {
            Debug.Log("Inside The  Start Coroutine In Revive Class");

            yield return new WaitForSecondsRealtime(1f);
            revivePanel.transform.DOScale(Vector3.zero, 0.7f)
              .SetUpdate(true)
              .SetEase(Ease.OutBack).OnComplete(() =>
              {
                  revivePanel.gameObject.SetActive(false);
                  if (BGImage != null)
                  {
                      Color color = BGImage.color;
                      color.a = 0f; // Set alpha to 0
                   BGImage.color = color;
                  }
                  ShowLosePanel();
                  isReviveActive = false;

              }
              );

        }
        
    }

    private void ShowLosePanel()
    {
        if (isReviveButtonPressed /*|| !isReviveActive*/)
        {
            return;
        }
        Debug.Log("Inside the Show Lose Panel");
        losePanel.transform.localScale = Vector3.zero;

        // Activate the panel
        losePanel.gameObject.SetActive(true);

        // Animate the scale from 0 to its default scale (1, 1, 1)
        losePanel.transform.DOScale(Vector3.one, 0.5f)
            .SetUpdate(true)
            .SetEase(Ease.OutBack).OnComplete(()=> {
                //Transform firstChild = losePanel.transform.GetChild(0);
                //if (firstChild != null)
                //{
                //    //Debug.Log($"Activating: {firstChild.name}");
                //    firstChild.gameObject.SetActive(true);
                //}
                Graphic mainPanelGraphic = gameObject.GetComponent<Graphic>(); // Check if it has Image or similar
                if (mainPanelGraphic != null)
                {
                    mainPanelGraphic.raycastTarget = false;
                    Debug.Log("Raycast Target of main panel is now disabled.");
                }

            });
    }
    /// <summary>
    /// Charges the current revive price. Returns false when the player cannot
    /// afford it, in which case nothing is spent and the revive must be aborted.
    /// </summary>
    private bool TryPayForRevive()
    {
        if (GameDataManager.instance == null)
        {
            return false;
        }

        if (!GameDataManager.instance.TrySpendGems(CurrentReviveCost))
        {
            return false;
        }

        reviveUseCount++;
        UpdateCostLabel();

        if (WatchAD.instance != null)
        {
            WatchAD.instance.CurrentState = WatchAD.ClearState.OnActive;
        }

        return true;
    }

    public void ReviveLevel()
    {
        if (isReviveButtonPressed)
        {
            return;
        }

        // Charge first: if the player cannot afford it, leave the panel and its
        // countdown running so they can still fall through to the lose flow.
        if (!TryPayForRevive())
        {
            Debug.Log($"Not enough gems to revive. Needs {CurrentReviveCost}, has {GameDataManager.instance?.GetGems() ?? 0}.");
            return;
        }

        isReviveButtonPressed = true;
        isReviveActive = false;

        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        revivePanel.transform.DOScale(Vector3.zero, 0.7f)
           .SetUpdate(true)
           .SetEase(Ease.OutBack).OnComplete(() =>
           {
               revivePanel.gameObject.SetActive(false);
               if (BGImage != null)
               {
                   Color color = BGImage.color;
                   color.a = 0f; // Set alpha to 0
                   BGImage.color = color;
               }
               //StartCoroutine(ShowAnimation(3));
               WatchAD.instance?.ActivateAd();
               GameManager.instance?.ResumeAfterRevive();

               // Allow another revive later in this same level, now at a higher price.
               isReviveButtonPressed = false;

           }
           );
    }

    public void NoThanksClick()
    {
      
        Debug.Log("Inside The  No Thanks Button");

        if (countdownRoutine != null)
        {
            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        revivePanel.transform.DOScale(Vector3.zero, 0.7f)
             .SetUpdate(true)
             .SetEase(Ease.OutBack).OnComplete(() =>
             {
                 revivePanel.gameObject.SetActive(false);
                 if (BGImage != null)
                 {
                     Color color = BGImage.color;
                     color.a = 0f; // Set alpha to 0
                      BGImage.color = color;
                 }
                 ShowLosePanel();
                 isReviveActive = false;
             }
             );

    }
}
