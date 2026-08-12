using System;
using UnityEngine;


public class GameDataManager : MonoBehaviour
{
    public static GameDataManager instance { get; private set; }

    [Header("Development")]
    [Tooltip("Deletes every PlayerPrefs key on Start. Keep disabled outside deliberate reset testing.")]
    [SerializeField] private bool resetDataOnStartForDevelopment;

    private const string LevelKey = "Level";
    private const string GemsKey = "Gems";
    private const string TotalCoinKey = "TotalCoins";
    private const string RemoveKey = "Remove";
    private const string SwapKey = "Swap";
    private const string ClearKey = "Clear";
    public int TotalCoins { get; private set; }
    public int coin=0;
    public int MaxCoin = 20;
    public int boxNum=0;

    // Singleton pattern for global access

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject); // Persist across scenes
    }
    private void Start()
    {
        if (resetDataOnStartForDevelopment)
        {
            ResetData();
        }

        LoadData();
    }
    // Save data
    public void SaveData(int level, int coins, int gems , int remove , int swap , int clear)
    {
       SaveInt(LevelKey, level);
       SaveInt(TotalCoinKey, coins);
       SaveInt(GemsKey, gems);
       SaveInt(RemoveKey, remove);
       SaveInt(SwapKey, swap);
       SaveInt(ClearKey, clear);
     
    }

    // Load data
    public (int level, int coins, int gems , int remove , int swap, int clear) LoadData()
    {
        int level = PlayerPrefs.GetInt(LevelKey, 1); // Default level = 1
         TotalCoins = PlayerPrefs.GetInt(TotalCoinKey, 0); // Default coins = 0
        int gems = PlayerPrefs.GetInt(GemsKey, 0);   // Default gems = 0
        int remove = PlayerPrefs.GetInt(RemoveKey ,1);
        int swap = PlayerPrefs.GetInt(SwapKey ,1);
        int clear = PlayerPrefs.GetInt(ClearKey ,1);

        //Debug.Log($"Loaded Data -> Level: {level}, Coins: {TotalCoins }, Gems: {gems}");
        return (level, TotalCoins, gems ,remove , swap,clear);
    }   

    public void SaveInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value); // Saves an integer to PlayerPrefs
        PlayerPrefs.Save(); // Commits changes
    }
    public void AddCoins(int amount)
    {
        coin += amount;       
    }
    /// <summary>
    /// Balance to show in the HUD: the persisted total plus whatever the current
    /// level has earned but not yet committed. Lets the counter react to every
    /// packed box without writing to PlayerPrefs on each one.
    /// </summary>
    public int DisplayCoins => TotalCoins + coin;

    public void AddToTotalCoins()
    {
        TotalCoins += coin;
        coin = 0;           // Reset the gameplay coin counter, so a second call
                            // cannot bank the same pending coins twice.
        PlayerPrefs.SetInt("TotalCoins", TotalCoins);
        PlayerPrefs.Save();
    }

    public void BoxCompletion(int box)
    {
        boxNum += box;
    }

    /// <summary>
    /// Clears values that belong only to one gameplay scene. When the active
    /// scene has a logical Level number, it also repairs stale saved progress so
    /// scene state and PlayerPrefs cannot drift apart.
    /// </summary>
    public void BeginLevel(int logicalLevel)
    {
        boxNum = 0;
        coin = 0;

        if (logicalLevel >= 1)
        {
            SetLevel(logicalLevel);
        }
    }
    public int GetGameplayCoins()
    {
        return boxNum;
    }
  
    // Update gems
    public void AddGems(int amount)
    {
        int currentGems = PlayerPrefs.GetInt(GemsKey, 2);
        currentGems += amount;
        PlayerPrefs.SetInt(GemsKey, currentGems);
        PlayerPrefs.Save();
        //Debug.Log($"Gems Added: {amount}, Total Gems: {currentGems}");
    }

    public int GetGems()
    {
        return PlayerPrefs.GetInt(GemsKey, 2);
    }

    /// <summary>
    /// Spends gems only when the player can actually afford the cost, so a
    /// purchase can never push the balance negative. Returns false when the
    /// caller should cancel whatever it was about to grant.
    /// </summary>
    public bool TrySpendGems(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        int currentGems = GetGems();
        if (currentGems < amount)
        {
            return false;
        }

        PlayerPrefs.SetInt(GemsKey, currentGems - amount);
        PlayerPrefs.Save();
        return true;
    }

    // Update level
    public int IncrementLevel()
    {
        int currentLevel = PlayerPrefs.GetInt(LevelKey, 1);
        currentLevel++;
        PlayerPrefs.SetInt(LevelKey, currentLevel);
        PlayerPrefs.Save();
        Debug.Log($"Level Increased: {currentLevel}");
        return currentLevel;
    }

    public void SetLevel(int logicalLevel)
    {
        int safeLevel = Mathf.Max(1, logicalLevel);
        PlayerPrefs.SetInt(LevelKey, safeLevel);
        PlayerPrefs.Save();
    }

    // Reset all data
    public void ResetData()
    {
        PlayerPrefs.DeleteAll();
        Debug.Log("Game Data Reset");
    }
}
