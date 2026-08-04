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
       GameObject cylinder = Instantiate(cylinderPref, Board.instance.grid[0,0].transform.position, Quaternion.identity);
        cylinder.transform.position = new Vector3(cylinder.transform.position.x,0.28f, cylinder.transform.position.z);
    }

 
}
