using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class WinPanel : MonoBehaviour
{
    [SerializeField] Button threeXReward;
    [SerializeField] Button noThanksButton;
    [SerializeField] GameObject winPanel;
    [SerializeField] Image BGImage;
    [SerializeField] Image progressBar;
    [SerializeField] TextMeshProUGUI barText;
    [Header("Base Rewards (single source of truth)")]
    [Tooltip("Gems granted for finishing a level, before the x3 multiplier.")]
    [SerializeField] int gemCount = 5;
    [Tooltip("Coins granted for finishing a level, before the x3 multiplier.")]
    [SerializeField] int coinCount = 25;
    [Tooltip("Hammers granted for finishing a level, before the x3 multiplier.")]
    [SerializeField] int hammerCount = 1;

    [Header("Reward Labels (auto-found by name when left empty)")]
    [SerializeField] TextMeshProUGUI gemRewardText;
    [SerializeField] TextMeshProUGUI coinRewardText;
    [SerializeField] TextMeshProUGUI hammerRewardText;

    [Header("Reward Burst Sizes")]
    [Tooltip("How many gem sprites fly to the counter. Visual only.")]
    [SerializeField] int gemBurstSprites = 5;
    [Tooltip("How many coin sprites fly to the counter. Visual only.")]
    [SerializeField] int coinBurstSprites = 40;

    int muliplier = 1;
    int levelNumer=1;
    int desieredLevel=20;
    private bool completionClaimed;
    private bool rewardedAdRequestInProgress;
    private void Start()
    {
        progressBar.fillAmount = levelNumer-1 / desieredLevel;

        RefreshRewardLabels();

        StartCoroutine(ShowProgress());
    }

    /// <summary>
    /// Writes the configured reward values into the panel's labels, so what the
    /// player reads always matches what AddRewards actually grants. These labels
    /// used to be static text typed into the scene, which is why editing the
    /// reward values changed nothing on screen.
    /// </summary>
    private void RefreshRewardLabels()
    {
        ResolveRewardLabels();

        if (gemRewardText != null)
        {
            gemRewardText.text = gemCount.ToString();
        }

        if (coinRewardText != null)
        {
            coinRewardText.text = coinCount.ToString();
        }

        if (hammerRewardText != null)
        {
            hammerRewardText.text = hammerCount.ToString();
        }
    }

    private void ResolveRewardLabels()
    {
        if (winPanel == null)
        {
            return;
        }

        if (gemRewardText != null && coinRewardText != null && hammerRewardText != null)
        {
            return;
        }

        foreach (TextMeshProUGUI label in winPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            switch (label.gameObject.name)
            {
                case "GemText":
                    if (gemRewardText == null) gemRewardText = label;
                    break;
                case "CoinText":
                    if (coinRewardText == null) coinRewardText = label;
                    break;
                case "RemoveText":
                    if (hammerRewardText == null) hammerRewardText = label;
                    break;
            }
        }
    }
  
    IEnumerator ShowProgress()
    {
        yield return new WaitForSecondsRealtime(1f);
        progressBar.fillAmount = (float)levelNumer / desieredLevel;
        barText.text = levelNumer + "/" + desieredLevel;
    }
    private void AddRewards(int muliplier)
    {
        GameDataManager.instance.AddGems(gemCount * muliplier);
        HammerManager.instance.SetHammerCount(hammerCount * muliplier);
        GameDataManager.instance.AddCoins(coinCount * muliplier);
        GameDataManager.instance.AddToTotalCoins();
    }
    [Header("Reward Animation Timing")]
    [Tooltip("Pause after the gems fly out, before the coins start.")]
    [SerializeField] float gemToCoinGap = 0.6f;
    [Tooltip("Pause after the coins fly out, before the counters refresh.")]
    [SerializeField] float coinSettleTime = 0.6f;

    IEnumerator ShowAnimation(int multiplier)
    {
        yield return new WaitForSecondsRealtime(0.5f);

        // Gems first, then coins. Firing both in the same frame made the two
        // reward streams overlap into one indistinguishable burst.
        CoinManager.instance.AddGems(threeXReward.transform.position, gemBurstSprites * multiplier);
        yield return new WaitForSecondsRealtime(gemToCoinGap);

        CoinManager.instance.AddCoins(threeXReward.transform.position, coinBurstSprites * multiplier);
        yield return new WaitForSecondsRealtime(coinSettleTime);

        UIManager.instance.UpdateUI();
    }

    public void ClaimThreeX()
    {
        if (completionClaimed || rewardedAdRequestInProgress)
        {
            return;
        }

        rewardedAdRequestInProgress = true;
        SetClaimButtonsInteractable(false);

        if (AdManager.instance == null)
        {
            HandleRewardedAdUnavailable();
            return;
        }

        AdManager.instance.ShowRewardedAd(CompleteThreeXClaim, HandleRewardedAdUnavailable);
    }

    public void NoThanksClick()
    {
        if (!TryBeginClaim())
        {
            return;
        }

        CompleteClaim(1);
    }

    private void CompleteThreeXClaim()
    {
        if (completionClaimed)
        {
            return;
        }

        rewardedAdRequestInProgress = false;
        completionClaimed = true;
        CompleteClaim(3);
    }

    private void HandleRewardedAdUnavailable()
    {
        rewardedAdRequestInProgress = false;
        Debug.LogWarning("Rewarded ad is not ready. Please try again in a moment.");
        SetClaimButtonsInteractable(true);
    }

    private void CompleteClaim(int multiplier)
    {
        AddRewards(multiplier);
        winPanel.transform.DOScale(Vector3.zero, 0.7f)
           .SetUpdate(true)
           .SetEase(Ease.OutBack).OnComplete(() =>
           {
               winPanel.gameObject.SetActive(false);
               if (BGImage != null)
               {
                   Color color = BGImage.color;
                   color.a = 0f;
                   BGImage.color = color;
               }

               StartCoroutine(AdvanceAfterClaim(multiplier));
           });
    }

    private IEnumerator AdvanceAfterClaim(int multiplier)
    {
        yield return ShowAnimation(multiplier);
        yield return new WaitForSecondsRealtime(1f);
        GoToNextLevel();
    }

    private void GoToNextLevel()
    {
        GameManager.instance?.PrepareSceneTransition();

        string currentSceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(currentSceneName, "TUTORIAL", StringComparison.OrdinalIgnoreCase))
        {
            LoadConfiguredLevel("Level1", 1);
            return;
        }

        if (!TryGetLogicalLevel(currentSceneName, out int currentLogicalLevel))
        {
            Debug.LogWarning(
                $"Cannot determine the next level from scene '{currentSceneName}'. Returning to MainMenu.");
            LoadMainMenu();
            return;
        }

        int nextLogicalLevel = currentLogicalLevel + 1;
        string nextSceneName = $"Level{nextLogicalLevel}";
        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            LoadConfiguredLevel(nextSceneName, nextLogicalLevel);
            return;
        }

        // The current scene is the last configured level. Keep saved progress on
        // this valid level and finish the run through the existing Main Menu.
        Debug.Log($"Completed final configured level '{currentSceneName}'. Returning to MainMenu.");
        LoadMainMenu();
    }

    private bool TryBeginClaim()
    {
        if (completionClaimed)
        {
            return false;
        }

        completionClaimed = true;
        SetClaimButtonsInteractable(false);

        return true;
    }

    private void SetClaimButtonsInteractable(bool interactable)
    {
        if (threeXReward != null)
        {
            threeXReward.interactable = interactable;
        }

        if (noThanksButton != null)
        {
            noThanksButton.interactable = interactable;
        }
    }

    private static bool TryGetLogicalLevel(string sceneName, out int logicalLevel)
    {
        logicalLevel = 0;
        const string prefix = "Level";
        return !string.IsNullOrEmpty(sceneName) &&
               sceneName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(sceneName.Substring(prefix.Length), out logicalLevel) &&
               logicalLevel >= 1;
    }

    private static void LoadConfiguredLevel(string sceneName, int logicalLevel)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"Scene '{sceneName}' is not configured in Build Profiles.");
            LoadMainMenu();
            return;
        }

        GameDataManager.instance?.SetLevel(logicalLevel);
        SceneManager.LoadScene(sceneName);
    }

    private static void LoadMainMenu()
    {
        if (Application.CanStreamedLevelBeLoaded("MainMenu"))
        {
            SceneManager.LoadScene("MainMenu");
            return;
        }

        Debug.LogError("MainMenu is not configured in Build Profiles.");
    }

}
