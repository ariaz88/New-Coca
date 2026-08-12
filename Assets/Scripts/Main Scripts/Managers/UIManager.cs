using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    public Toggle vibrationToggle;

    public static UIManager instance;
    [SerializeField] private TMPro.TextMeshProUGUI levelText;
    [SerializeField] private TMPro.TextMeshProUGUI gemsText;
    [SerializeField] private TMPro.TextMeshProUGUI coinText;
    [SerializeField] private TMPro.TextMeshProUGUI swapText;
    [SerializeField] private TMPro.TextMeshProUGUI removeText;
    //[SerializeField] private TMPro.TextMeshProUGUI gameplayCoinsText;
    [SerializeField] private TMPro.TextMeshProUGUI totalCoinsText;
    [SerializeField] private TMPro.TextMeshProUGUI boxCount;
    [SerializeField] private Image coinSlider;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private GameObject revivePanel;
    private int maxCoin=2500;
    private int currentCoin=0;
    private void OnEnable()
    {
        // Subscribe to the level won event


    }

    private void OnDisable()
    {
        // Unsubscribe to avoid memory leaks
        if (GameManager.instance != null)
        {
            GameManager.instance.OnLevelWon -= HandleLevelWon;
            GameManager.instance.OnLevelLose -= Instance_OnLevelLose;
        }

    }
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

        private void Start()
    {
        GameManager.instance.OnLevelWon += HandleLevelWon;
        GameManager.instance.OnLevelLose += Instance_OnLevelLose;
        Debug.Log("Active Scene: " + SceneManager.GetActiveScene().name);
        UpdateUI();
        boxCount.text = 0 + "/" + maxCoin;
        //Shadow shadow = boxCount.gameObject.GetComponent<Shadow>();
        //if (shadow == null)
        //{
        //    shadow = boxCount.gameObject.AddComponent<Shadow>();
        //}
        //shadow.effectColor = new Color(0, 0, 0, 0.5f); // Black with 50% transparency
        //shadow.effectDistance = new Vector2(6, -10);     // Shadow offset
        //shadow.useGraphicAlpha = true;

    }

    private void HandleLevelWon(object sender, System.EventArgs e)
    {
        Debug.Log("UI Manager: Level Won!");
        // Update UI or show a win screen
        ShowWinScreen();
    }
    private void Instance_OnLevelLose(object sender, System.EventArgs e)
    {
        RevivePanel.instance.isReviveButtonPressed = false;

        Debug.Log("UI Manager: Lost the Level !");
        // Update UI or show a win screen
        ShowLoseScreen();
    }
    private void ShowWinScreen()
    {
        StartCoroutine(WinPanel());
    }

    IEnumerator WinPanel()
    {
        //yield return new WaitForSeconds(2.5f);
        yield return new WaitForSeconds(1.2f);
        //if (Board.instance.InteractionFinished())
        //{
        //}
        AnimatePanel(winPanel);

    }
    private void AnimatePanel(GameObject panel)
    {
        panel.transform.localScale = Vector3.zero;

        // Activate the panel
        panel.gameObject.SetActive(true);
        GameManager.instance?.PauseForEndPanel();

        // Animate the scale from 0 to its default scale (1, 1, 1)
        panel.transform.DOScale(Vector3.one, 0.5f)
            .SetUpdate(true)
            .SetEase(Ease.OutBack).OnComplete(()=> {

                Transform firstChild = panel.transform.GetChild(0);
                if (firstChild != null)
                {
                    //Debug.Log($"Activating: {firstChild.name}");
                    firstChild.gameObject.SetActive(true);
                }

            });
    }

    private void ShowLoseScreen()
    {
        if (RevivePanel.instance.isReviveButtonPressed)
        {
            Debug.Log("Revive process already initiated. Skipping lose screen.");
            return;
        }

        RevivePanel.instance.isReviveActive = true;
        AnimatePanel(revivePanel);

        if (RevivePanel.instance.isReviveActive)
        {
            RevivePanel.instance.ShowCoroutine();
        }

        Debug.Log("You Lost");

    }
    /// <summary>
    /// Repaints the HUD. Pass includeCoins: false from gameplay paths that award
    /// coins with a fly animation, so the counter is left to CoinManager and does
    /// not jump ahead of the coins still travelling to it.
    /// </summary>
    public void UpdateUI(bool includeCoins = true)
    {
        //(int level, _, int gems) = GameDataManager.instance.LoadData();
        ////levelText.text = $"Level: {level}";
        //gemsText.text = $"Gems: {gems}";
        //if (totalCoinsText != null)
        //{
        //    //totalCoinsText.text = $"{TotalCoins}";
        //    UpdateTotalCoins(TotalCoins);
        //}
        (_, int TotalCoins, int gems ,int remove ,int swap ,_) = GameDataManager.instance.LoadData();

        if (gemsText != null)
        {
            UpdateGems(gems);
        }
        if (includeCoins && coinText!=null)
        {
            // Persisted total plus this level's pending coins. Showing the raw
            // GameDataManager.coin made the HUD reset to 0 on every level load,
            // while showing TotalCoins alone froze it until the level ended.
            RefreshCoins();

        }
        if (levelText!=null)
        {
            UpdateLevel(SceneManager.GetActiveScene().name);
        }
        //if (removeText != null)
        //{
        //    UpdateRemove(remove);
        //}
        //if (swapText != null)
        //{
        //    UpdateSwap(swap);
        //}



    }
    // After Wininng the Level

    public void OnLevelComplete()
    {
        // Rewards only. WinPanel owns level progression so the saved level can
        // never be incremented by two different completion paths.
        GameDataManager.instance.AddToTotalCoins();
        UIManager.instance.UpdateTotalCoins(GameDataManager.instance.TotalCoins);

        // Optionally save additional data (e.g., coins, gems)
        GameDataManager.instance.SaveData(
            PlayerPrefs.GetInt("Level"),
            PlayerPrefs.GetInt("TotalCoins"),
            PlayerPrefs.GetInt("Gems"),
            PlayerPrefs.GetInt("Remove"),
            PlayerPrefs.GetInt("Swap"),
            PlayerPrefs.GetInt("Clear")

        );
    }
    //public void UpdateGameplayCoins(int coins)
    //{
    //    if (gameplayCoinsText == null)
    //    {
    //        return;
    //    }

    //    float sliderValue = (float)coins / GameDataManager.instance.MaxCoin;
    //    coinSlider.value = sliderValue;
    //    gameplayCoinsText.text = $"{coins}";
    //}
    public void UpdateGameplayCoins(int coins)
    {
        if (/*gameplayCoinsText == null ||*/ coinSlider == null)
        {
            Debug.LogWarning("GameplayCoinsText or CoinSlider is not assigned!");
            return;
        }

        // Debug to verify coins and MaxCoin
        //Debug.Log($"Updating slider: Coins = {coins}, MaxCoin = {GameDataManager.instance.MaxCoin}");
        currentCoin += coins;
        // Calculate slider value directly
        coinSlider.fillAmount = (float)currentCoin / maxCoin;

        // Optional: Update gameplay coins text (if needed elsewhere)
        //boxCount.text = $"{coins}/{maxCoin}";
        boxCount.text = currentCoin + "/" + maxCoin;
    }


    // Update the total coins UI
    public void UpdateTotalCoins(int totalCoins)
    {
        totalCoinsText.text = $" {totalCoins}";
    }

    public void UpdateGems(int gemCount)
    {
        gemsText.text = $" {gemCount}";
        
    }
    public void UpdateCoins(int coinCount)
    {
        coinText.text = $" { coinCount}";

    }

    /// <summary>
    /// Repaints the coin counter from current data. Call after anything that
    /// awards coins mid-level, since UpdateUI only ran at level start and end.
    /// </summary>
    public void RefreshCoins()
    {
        if (coinText == null || GameDataManager.instance == null)
        {
            return;
        }

        UpdateCoins(GameDataManager.instance.DisplayCoins);
    }

    /// <summary>
    /// Repaints the gem counter from saved data. Called as gems land so the
    /// counter tracks the animation rather than jumping ahead of it.
    /// </summary>
    public void RefreshGems()
    {
        if (gemsText == null || GameDataManager.instance == null)
        {
            return;
        }

        UpdateGems(GameDataManager.instance.GetGems());
    }
    public void UpdateLevel(string leveltxt)
    {
        levelText.text = $" { leveltxt}";

    }

    //public void UpdateSwap(int swapCount)
    //{
    //    swapText.text = $" {swapCount}";
    //}
    //public void UpdateRemove(int removeCount)
    //{
    //    removeText.text = $" {removeCount}";
    //}

}



