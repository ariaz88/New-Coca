using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardController : MonoBehaviour
{
    Board board;
    private void Awake()
    {
        
    }
    private void Start()
    {
        board = GetComponent<Board>();
    }

    public void RemoveSpawnerList1()
    {
        SpawnContoller.instance.spawnedBoxes.RemoveAll(item =>
        {
            Box box = item.GetComponent<Box>();
            return box != null && box.IsOnBoard;
        });
    }
    public void RemoveSpawnerList()
    {
        // Create a temporary list to store items to remove
        List<GameObject> itemsToRemove = new List<GameObject>();

        // Iterate over the items to identify which ones to remove
        foreach (var item in SpawnContoller.instance.spawnedBoxes)
        {
            Box box = item.GetComponent<Box>();
            if (box != null && box.IsOnBoard)
            {
                itemsToRemove.Add(item);
            }
        }

        // Remove the identified items from the original list
        foreach (var item in itemsToRemove)
        {
            SpawnContoller.instance.spawnedBoxes.Remove(item);
        }
    }


}
