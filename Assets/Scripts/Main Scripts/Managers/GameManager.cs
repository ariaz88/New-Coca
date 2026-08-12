using System.Collections;
using UnityEngine;
using System;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public event EventHandler OnLevelWon;
    public event EventHandler OnLevelLose;
   [SerializeField] GameObject cylinderPref;
    [SerializeField] Canvas canvas;
    public bool gameEnded;
    public bool gameOver;
    private bool endSequenceStarted;
    private bool subscribedToOrders;

    public bool EndSequenceStarted => endSequenceStarted;
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
        Time.timeScale = 1f;
        gameEnded = false;
        gameOver = false;
        endSequenceStarted = false;

        if (GameDataManager.instance != null)
        {
            GameDataManager.instance.BeginLevel(GetActiveLogicalLevel());
        }

        SoundManager.instance?.PlayLevelMusic();

        SubscribeToOrders();

        StartCoroutine(ActivateCanvas());
    }

    /// <summary>
    /// Hooks the Orders win condition when this scene uses one.
    ///
    /// Subscribed in Start rather than Awake because OrderManager builds its
    /// runtime list in its own Start. Awake would only see an empty manager and
    /// HasOrders would read false.
    /// </summary>
    private void SubscribeToOrders()
    {
        if (OrderManager.instance == null || subscribedToOrders)
        {
            return;
        }

        OrderManager.instance.AllOrdersFulfilled += HandleAllOrdersFulfilled;
        subscribedToOrders = true;
    }

    private void HandleAllOrdersFulfilled()
    {
        BeginWinSequence();
    }

    /// <summary>
    /// True when this scene's goal is its order list. Scenes without an
    /// OrderManager, or with an empty order list, keep the historical packed-box
    /// counter so the Tutorial and any unconverted level still finish.
    /// </summary>
    private bool UsesOrderWinCondition =>
        OrderManager.instance != null && OrderManager.instance.HasOrders;
    IEnumerator  ActivateCanvas()
    {
        yield return new  WaitForSeconds(1.8f);
        canvas.gameObject.SetActive(true);
    }
    public void CheckWinCondition()
    {
        //string levelName = "TUTORIAL";
        //if (SceneManager.GetActiveScene().name == levelName)
        //{
        //    return;
        //}
        // A level driven by orders is won by OrderManager.AllOrdersFulfilled,
        // which fires only after the last effect has visually landed. Falling
        // through to the packed-box counter here would end the level early and
        // cover an order slot that still reads a number.
        if (UsesOrderWinCondition)
        {
            return;
        }

        if (GameDataManager.instance == null)
        {
            return;
        }

        if (GameDataManager.instance.boxNum >= 10)
        {
            BeginWinSequence();
        }
    }

    /// <summary>
    /// The single entry point into the win flow. Both goal types funnel through
    /// it so the guards, spawn stop, and music stop can never diverge.
    /// </summary>
    private void BeginWinSequence()
    {
        if (endSequenceStarted || gameEnded || gameOver)
        {
            return;
        }

        endSequenceStarted = true;
        gameEnded = true;
        SetSpawningStopped(true);
        SoundManager.instance?.StopMusic();
        StartCoroutine(LevelWon());
    }
    public void CheckLoseCondition(bool isBoardFilled)
    {
        if (!isBoardFilled || endSequenceStarted || gameEnded || gameOver)
        {
            return;
        }

        endSequenceStarted = true;
        gameEnded = true;
        gameOver = true;
        SetSpawningStopped(true);
        SoundManager.instance?.StopMusic();
        Debug.Log("Lost");
        LevelLose();
    }

    IEnumerator  LevelWon()
    {
        gameOver = true;
        yield return new WaitForSeconds(0.2f);

        Debug.Log("Level Won!");
        // Fire the event to notify all subscribers
        OnLevelWon?.Invoke(this, EventArgs.Empty);
        //AdManager.instance.RewardAdFinal();

    }
    private void LevelLose()
    {

        OnLevelLose?.Invoke(this, EventArgs.Empty);
        //AdManager.instance.RewardAdFinal();

    }
    public void CreateCylinder()
    {
        if (cylinderPref == null || Board.instance == null)
        {
            Debug.LogError("Cannot create the clear cylinder: its prefab or the Board is missing.", this);
            return;
        }

        Node startNode = Board.instance.GetFirstPlayableNode();
        if (startNode == null)
        {
            Debug.LogError("Cannot create the clear cylinder: the Board has no playable cells.", this);
            return;
        }

        Vector3 startPosition = startNode.transform.position;
        startPosition.y = 0.28f;
        Instantiate(cylinderPref, startPosition, Quaternion.identity);
    }

    /// <summary>
    /// Freezes scaled gameplay only after an end panel becomes visible. UI end
    /// panels use unscaled DOTween/time so their buttons and animations still run.
    /// </summary>
    public void PauseForEndPanel()
    {
        Time.timeScale = 0f;
    }

    public void ResumeAfterRevive()
    {
        Time.timeScale = 1f;
        gameEnded = false;
        gameOver = false;
        endSequenceStarted = false;
        SetSpawningStopped(false);

        // Clearing the flags above is not enough: the spawn loop already exited
        // when the level was lost, so the rail needs its routine started again.
        SpawnContoller.instance?.RestartSpawning();

        SoundManager.instance?.PlayLevelMusic();
    }

    public void PrepareSceneTransition()
    {
        Time.timeScale = 1f;
    }

    private void SetSpawningStopped(bool stopped)
    {
        if (BoxSpawner.instance != null)
        {
            BoxSpawner.instance.stopSpawn = stopped;
        }

        if (SpawnContoller.instance != null)
        {
            SpawnContoller.instance.stopSpawn = stopped;
        }
    }

    private static int GetActiveLogicalLevel()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return LevelNaming.TryGetLevelNumber(sceneName, out int logicalLevel) ? logicalLevel : 0;
    }

    private void OnDestroy()
    {
        if (subscribedToOrders && OrderManager.instance != null)
        {
            OrderManager.instance.AllOrdersFulfilled -= HandleAllOrdersFulfilled;
        }

        subscribedToOrders = false;

        if (instance == this)
        {
            Time.timeScale = 1f;
            instance = null;
        }
    }

 
}
