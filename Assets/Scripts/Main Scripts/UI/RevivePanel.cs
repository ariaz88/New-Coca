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
    int gemCount = 5;
    public bool isReviveButtonPressed;
    public bool isReviveActive;
    public float countdownDuration = 9f;
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

            //revivePanel.gameObject.SetActive(true);
            StartCoroutine(StartCountdown());
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
            elapsedTime += Time.deltaTime;
            remainingTime = countdownDuration - elapsedTime;
        }

        // Ensure progress bar is fully filled and text shows 0 at the end
        progressBar.fillAmount = 1f;
        barText.text = "0";
        //******************************
        if (!isReviveButtonPressed && isReviveActive)
        {
            Debug.Log("Inside The  Start Coroutine In Revive Class");

            yield return  new WaitForSeconds(1f);
            revivePanel.transform.DOScale(Vector3.zero, 0.7f)
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
    private void RemoveGem()
    {
        GameDataManager.instance.AddGems(-5 );
        WatchAD.instance.CurrentState = WatchAD.ClearState.OnActive;
        
    }

    public void ReviveLevel()
    {
        isReviveButtonPressed = true;
        isReviveActive = false;
        RemoveGem();
        revivePanel.transform.DOScale(Vector3.zero, 0.7f)
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
               WatchAD.instance.ActivateAd();
               GameManager.instance.gameEnded = false;
               BoxSpawner.instance.stopSpawn = false;

           }
           );
    }

    public void NoThanksClick()
    {
      
        Debug.Log("Inside The  No Thanks Button");
        revivePanel.transform.DOScale(Vector3.zero, 0.7f)
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
