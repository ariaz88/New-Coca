using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class LiftTruck : MonoBehaviour
{
    public static event Action<LiftTruck> OnTruckFull;     // Event for when truck is full
    public static event Action<LiftTruck> OnTruckUnloaded; // Event for when truck unloads

    [SerializeField] Transform[] boxPositions; // Positions for the two boxes on the truck
    private List<GameObject> loadedBoxes = new List<GameObject>();
    public float speed = 5f;        
    private int boxCount = 0;        // Number of boxes on the truck
    public bool IsActive = false;   // Whether this truck is currently active
    private Coroutine queueMovementCoroutine;

    public void SetActive(bool active)
    {
        IsActive = active;
    }

    public bool IsFull()
    {
        return boxPositions == null || boxCount >= boxPositions.Length;
    }

    public void AddBox(GameObject box)
    {
        if (box == null)
            return;

        if (boxPositions != null && boxCount < boxPositions.Length)
        {
            // Ignore duplicate completion callbacks for the same box.
            if (loadedBoxes.Contains(box))
                return;

            loadedBoxes.Add(box);

            // Place the box in the truck
            box.transform.position = boxPositions[boxCount].position;
            box.transform.parent = transform; // Attach to the truck
            boxCount++;

            // Check if truck is now full
            if (IsFull() && IsActive)
            {
                OnTruckFull?.Invoke(this);
            }
        }
        else
        {
            // Current truck is full, find the next active truck
            LiftTruckManager truckManager = FindObjectOfType<LiftTruckManager>();
            if (truckManager != null)
            {
                LiftTruck nextTruck = truckManager.GetActiveTruck();
                if (nextTruck != null)
                {
                    nextTruck.AddBox(box); // Add the box to the next active truck
                }
                else
                {
                    Debug.LogWarning("No available trucks to handle the box!");
                }
            }
            else
            {
                Debug.LogError("TruckManager not found in the scene!");
            }
        }
    }

    public IEnumerator MoveToWaypoints(Transform[] waypoints)
    {
        CancelQueueMovement();

        if (waypoints == null)
            yield break;

        foreach (var waypoint in waypoints)
        {
            if (waypoint == null)
                continue;

            while (Vector3.Distance(transform.position, waypoint.position) > 0.02f)
            {
                Vector3 direction = (waypoint.position - transform.position).normalized;

                transform.position = Vector3.MoveTowards(
                    transform.position,
                    waypoint.position,
                    Mathf.Max(0.01f, speed) * Time.deltaTime);
                //transform.rotation = Quaternion.LookRotation(direction);
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = targetRotation * Quaternion.Euler(0, -90, 0);

                yield return null;
            }

            transform.position = waypoint.position;
            yield return new WaitForSeconds(0.5f); // Optional pause at each waypoint
        }
    }

    public IEnumerator UnloadBoxes1(Transform unloadPosition)
    {
        foreach (var box in loadedBoxes)
        {
            // Simulate unloading boxes
            if (loadedBoxes.Count > 0)
            {
                box.transform.SetParent(null); // Detach from truck
                box.transform.position = unloadPosition.position; // Move to unload position
                loadedBoxes.Remove(box);
                yield return new WaitForSeconds(0.5f); // Delay for unloading
            }
        }

        boxCount = 0; // Reset box count after unloading
        OnTruckUnloaded?.Invoke(this); // Raise event
    }
    public IEnumerator UnloadBoxes()
    {
        if (loadedBoxes.Count > 0)
        {
            LiftTruckManager.instance.InitializeBoxDimensions(loadedBoxes[0]);
        }

        for (int i = loadedBoxes.Count - 1; i >= 0; i--)
        {
            var box = loadedBoxes[i];
            box.transform.SetParent(null); // Detach from truck

            Vector3 unloadPosition = LiftTruckManager.instance.GetNextUnloadPosition();

            box.transform.position = unloadPosition; // Move to unload position
            int[] angles = { 2, 5, 8 , 12, 15 };
            int randomAngle = angles[UnityEngine.Random.Range(0, angles.Length)];
            box.transform.rotation = Quaternion.Euler(0, randomAngle, 0);


            loadedBoxes.RemoveAt(i); // Remove the box from the list
            yield return new WaitForSeconds(0.5f); // Delay for unloading
        }

        boxCount = 0; // Reset box count after unloading
        //LiftTruckManager.instance.ResetUnloadArea(); // Reset the unload area
        OnTruckUnloaded?.Invoke(this); // Raise event
    }


    public void MoveToNextInQueue(Vector3 targetPosition, float delay)
    {
        CancelQueueMovement();
        queueMovementCoroutine = StartCoroutine(MoveAfterDelay(targetPosition, delay));
    }

    public IEnumerator MoveAfterDelay(Vector3 targetPosition, float delay)
    {
        yield return new WaitForSeconds(delay);
        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                Mathf.Max(0.01f, speed) * Time.deltaTime);
            yield return null;
        }

        transform.position = targetPosition;
        queueMovementCoroutine = null;
    }

    private void CancelQueueMovement()
    {
        if (queueMovementCoroutine == null)
            return;

        StopCoroutine(queueMovementCoroutine);
        queueMovementCoroutine = null;
    }
    //public Vector3 GetNextAvailablePosition()
    //{
    //    return boxPositions[boxCount].position;
    //}
    public Vector3 GetNextAvailablePosition()
    {
        if (boxCount < boxPositions.Length)
        {
            return boxPositions[boxCount].position;
        }
        else
        {
            Debug.LogWarning("No available position in this truck!");
            return transform.position; // Return the truck's position as a fallback
        }
    }
    public bool IsEnoughRoomLeft()
    {
        return boxCount < boxPositions.Length;
    }

}
