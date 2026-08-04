using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;



public class LoseGame : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] GameObject revivePanel;
    [SerializeField] Button playAgainButton;
    [SerializeField] Button mainMenuButton;
    [SerializeField] GameObject losePanel;
    [SerializeField] Image BGImage;
    [SerializeField] Image progressBar;
    [SerializeField] TextMeshProUGUI barText;
  
    int levelNumer = 2;
    int desieredLevel = 20;
    private void Start()
    {
        revivePanel.SetActive(false);
        progressBar.fillAmount = (float)levelNumer / desieredLevel;
        StartCoroutine(ShowProgress());
    }

    IEnumerator ShowProgress()
    {
        yield return new WaitForSeconds(0.5f);
        progressBar.fillAmount = (float)(levelNumer - 1) / desieredLevel;
        barText.text = levelNumer-1 + "/" + desieredLevel;
    }

    public void PlayAgain()
    {
        losePanel.transform.DOScale(Vector3.zero, 0.8f)
           .SetEase(Ease.OutBack).OnComplete(() =>
           {
               losePanel.gameObject.SetActive(false);
               if (BGImage != null)
               {
                   Color color = BGImage.color;
                   color.a = 0f; // Set alpha to 0
                   BGImage.color = color;
               }
        SceneManager.LoadScene("Level1");

           }
           );

    }

    public void MainMenu()
    {
        losePanel.transform.DOScale(Vector3.zero, 0.8f)
           .SetEase(Ease.OutBack).OnComplete(() =>
           {
               losePanel.gameObject.SetActive(false);
               if (BGImage != null)
               {
                   Color color = BGImage.color;
                   color.a = 0f; // Set alpha to 0
                   BGImage.color = color;
               }
               SceneManager.LoadScene("MainMenu");

           }
           );

    }


}
