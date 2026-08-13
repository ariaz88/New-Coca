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
    /// <summary>
    /// Drops every rail slot whose occupant is finished with it.
    ///
    /// This used to ask "is it a Box, and is it on the board". A Defuser is not a
    /// Box, so it could never satisfy that and one unused Defuser would pin the
    /// rail forever: SpawnContoller only refills once spawnedBoxes is empty, and
    /// Board.CheckRailExhausted needs the same list empty before it will call the
    /// level lost. It now asks the occupant whether it is consumed, which Box
    /// answers with IsOnBoard and a Defuser answers for itself.
    ///
    /// The null guard matters too: a destroyed occupant leaves a null entry, and
    /// GetComponent on it would have thrown here rather than in the code that
    /// destroyed it.
    /// </summary>
    public void RemoveSpawnerList()
    {
        if (SpawnContoller.instance == null)
        {
            return;
        }

        SpawnContoller.instance.spawnedBoxes.RemoveAll(item =>
        {
            if (item == null)
            {
                return true;
            }

            IRailItem railItem = item.GetComponent<IRailItem>();
            return railItem != null && railItem.IsConsumed;
        });
    }


}
