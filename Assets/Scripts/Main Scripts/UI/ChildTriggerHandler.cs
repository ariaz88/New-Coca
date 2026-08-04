using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChildTriggerHandler : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Box box = other.gameObject.GetComponent<Box>();
        if (other.transform.CompareTag("Carrier"))
        {
            Board.instance.grid[box.column, box.row].isOccupied = false;
            Destroy(other.gameObject);

        }

    }
}
