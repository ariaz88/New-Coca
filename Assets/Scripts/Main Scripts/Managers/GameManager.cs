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
        StartCoroutine(ActivateCanvas());
        gameOver = false;
    }
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
        if (GameDataManager.instance.boxNum >= 10  )
        {
            gameEnded = true;
            BoxSpawner.instance.stopSpawn = true;
            //BoxSpawner.instance.enabled = false;
            StartCoroutine(LevelWon());
        }
    }
    public void CheckLoseCondition(bool isBoardFilled)
    {
        if (isBoardFilled)
        {
            Debug.Log("Lost");
            //gameEnded = true;
            LevelLose();
            BoxSpawner.instance.stopSpawn = true;

        }
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

 
}
