using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
public class LiftTruckManager : MonoBehaviour
{
    public static LiftTruckManager instance;

  
    public List<LiftTruck> trucks; // List of all trucks
    public Transform[] waypoints; // Global waypoints for movement
    public Transform[] waypointsIn; //way points to get the third position
    //public Transform unloadPosition; // The unload spot at the top
    public float truckMoveDelay = 1f; // Delay before moving trucks in sequence
    public Transform position1; // Position 1 (active truck)
    public Transform position2; // Position 2
    public Transform position3; // Position 3 (last truck)
    private Queue<LiftTruck> truckQueue = new Queue<LiftTruck>(); // Queue for truck order
    private LiftTruck activeTruck;
    private LiftTruck currentlyWasActiveTruck;

    [Header("Unload Position Variables")]
    public Transform unloadStartPosition; // Starting position for unloading
    public int columnLimit = 2; // Maximum boxes per column
    public int rowLimit = 4; // Maximum boxes per column
    private float boxHeight; // Box height (calculated dynamically)
    private float boxWidth; // Box width (calculated dynamically)
    private Vector3 currentUnloadPosition; // Tracks the next unload position
    private int currentColumn = 0; // Tracks the current column index
    private int boxesInColumn = 0; // Tracks the number of boxes in the current column


    private int activeTruckIndex = 0; // Index of the active truck
    private float[] columnHeights = new float[6];



    [SerializeField]private Transform middleTransform;
    private void OnEnable()
    {
        // Subscribe to events
        LiftTruck.OnTruckFull += HandleTruckFull;
        LiftTruck.OnTruckUnloaded += HandleTruckUnloaded;
    }

    private void Awake()
    {
        instance = this;
        foreach (LiftTruck truck in trucks)
        {
            truckQueue.Enqueue(truck);
        }

    }

    private void OnDisable()
    {
        // Unsubscribe from events
        LiftTruck.OnTruckFull -= HandleTruckFull;
        LiftTruck.OnTruckUnloaded -= HandleTruckUnloaded;
    }

    private void Start()
    {
        //SetActiveTruck(0); // Set the first truck as active at the start
        activeTruck = truckQueue.Peek(); // The next truck in the queue becomes active
        activeTruck.IsActive = true;
        currentlyWasActiveTruck = null;
        InitializeTrucks();
        //Unload Pos
        currentUnloadPosition = unloadStartPosition.position;
        for (int i = 0; i < columnHeights.Length; i++)
        {
            columnHeights[i] = unloadStartPosition.position.y;
        }
    }

    public void InitializeBoxDimensions(GameObject box)
    {
        var boxCollider = box.GetComponent<Collider>();
        if (boxCollider != null)
        {
            boxHeight = boxCollider.bounds.size.y;
            boxWidth = boxCollider.bounds.size.x;
        }
    }
    public Vector3 GetNextUnloadPosition1()
    {
        Vector3 nextPosition = currentUnloadPosition;

        // Update the unload position for the next box
        boxesInColumn++;
        if (boxesInColumn >= columnLimit)
        {
            Vector3 tempPos = new Vector3(currentUnloadPosition.x, unloadStartPosition.position.y, currentUnloadPosition.z);
            // Move to the next column
            boxesInColumn = 0;
            currentColumn++;
            currentUnloadPosition = tempPos + new Vector3(currentColumn * boxWidth, 0, 0) /*+ new Vector3(0.015f,0,0);*/ ;
            //waypoints[3].position += new Vector3(currentColumn * boxHeight*2/3, 0, 0);
            //waypointsIn[0].position += new Vector3(currentColumn * boxHeight * 2 / 3, 0, 0);
        }
        else
        {
            // Move up in the current column
            currentUnloadPosition += new Vector3(0, boxHeight*1.1f, 0);
        }

        return nextPosition;
    }

    // Array to track the current height of each column

    public Vector3 GetNextUnloadPosition2()
    {
        Vector3 nextPosition = currentUnloadPosition;

        // Update the unload position for the next box
        boxesInColumn++;

        if (boxesInColumn >= columnLimit)
        {
            // Save the current column's height before switching columns
            columnHeights[currentColumn] = currentUnloadPosition.y;

            // Move to the next column
            boxesInColumn = 0;
            currentColumn++;

            // Reset column index if we reach the 4th column
            if (currentColumn >= 4)
            {
                currentColumn = 0; // Restart at the first column

                // Resume spawning at the MAX height of column 0
                currentUnloadPosition = new Vector3(
                    unloadStartPosition.position.x + (currentColumn * boxWidth * 1.6f),
                    columnHeights[currentColumn],
                    unloadStartPosition.position.z // Keep Z consistent
                );
            }
            else
            {
                // Move to the next column and start at its current height
                currentUnloadPosition = new Vector3(
                    unloadStartPosition.position.x + (currentColumn * boxWidth * 1.6f),
                    columnHeights[currentColumn],
                    unloadStartPosition.position.z // Keep Z consistent
                );
            }
        }
        else
        {
            // Move up in the current column
            currentUnloadPosition += new Vector3(0, boxHeight * 1.1f, 0);
        }

        return nextPosition;
    }
    public Vector3 GetNextUnloadPosition3()
    {
        // Set the next position based on current unload position
        Vector3 nextPosition = currentUnloadPosition;

        // Update the unload position for the next box
        boxesInColumn++;

        if (boxesInColumn >= columnLimit)
        {
            // Update the current column's height to reflect the cumulative height
            columnHeights[currentColumn] = currentUnloadPosition.y;

            // Move to the next column
            boxesInColumn = 0;
            currentColumn++;

            // If we've cycled through all columns, reset to column 0
            if (currentColumn >= 4)
            {
                currentColumn = 0; // Restart at column 0
            }

            // Set the unload position for the new column
            currentUnloadPosition = new Vector3(
                unloadStartPosition.position.x + (currentColumn * boxWidth * 1.6f), // X position based on column
                columnHeights[currentColumn], // Continue from the last saved height of this column
                unloadStartPosition.position.z // Keep Z position consistent
            );
        }
        else
        {
            // Move up in the current column
            currentUnloadPosition += new Vector3(0, boxHeight * 1.1f, 0);

            // Update the cumulative height for the current column
            columnHeights[currentColumn] = currentUnloadPosition.y;
        }

        return nextPosition;
    }
    public Vector3 GetNextUnloadPosition()
    {
        // Calculate the position for the next box
        Vector3 nextPosition = new Vector3(
            unloadStartPosition.position.x + (currentColumn * boxWidth * 1.25f), // X position based on column
            columnHeights[currentColumn],                                      // Y position (last saved height)
            unloadStartPosition.position.z                                     // Z position
        );

        // Update the column height to account for the new box
        columnHeights[currentColumn] += boxHeight * 1.1f;

        // Move to the next position in the column
        boxesInColumn++;

        if (boxesInColumn >= columnLimit)
        {
            // Reset the current column's box count
            boxesInColumn = 0;

            // Move to the next column
            currentColumn++;

            // If all columns have been used, reset to column 0
            if (currentColumn >= 6)
            {
                currentColumn = 0;
            }
        }

        return nextPosition;
    }




    public void ResetUnloadArea()
    {
        // Reset the unload area to the starting position
        currentUnloadPosition = unloadStartPosition.position;
        currentColumn = 0;
        boxesInColumn = 0;
    }
    private void InitializeTrucks()
    {
        // Enqueue all trucks and set the initial positions
      
        UpdateTruckPositions();
    }
    private void UpdateTruckPositions()
    {
        int positionIndex = 0;

        // Rearrange all trucks except the one currently unloading
        foreach (LiftTruck truck in truckQueue)
        {
            if (truck != currentlyWasActiveTruck)
            {
                Vector3 targetPosition = GetPositionByIndex(positionIndex);
                truck.MoveToNextInQueue(targetPosition,0.1f);
                positionIndex++;
                truck.transform.rotation = Quaternion.Euler(transform.forward);
            }
        }
    }
    private Vector3 GetPositionByIndex(int index)
    {
        switch (index)
        {
            case 0: return position1.position; // Position 1
            case 1: return position2.position; // Position 2
            case 2: return position3.position; // Position 3
            default: return Vector3.zero; // Should never happen
        }
    }



    private void HandleTruckFull(LiftTruck truck)
    {
        activeTruckIndex = trucks.IndexOf(activeTruck);

        // If the active truck is full, start its movement
        if (truck == trucks[activeTruckIndex])
        {
            StartCoroutine(MoveActiveTruck(truck));
        }
    }

    private IEnumerator MoveActiveTruck(LiftTruck truck)
    {
        activeTruck.IsActive = false;
        currentlyWasActiveTruck = activeTruck;
        truckQueue.Dequeue();
        activeTruck = truckQueue.Peek();
        activeTruck.IsActive = true;
        UpdateTruckPositions();

        //if (truckQueue.Count > 0)
        //{
        //    activeTruck = truckQueue.Peek(); // Get the next truck in the queue
        //    activeTruck.MoveAfterDelay(position1.position, 0.1f);
        //}
        //else
        //{
        //    activeTruck = null; // No trucks left in the queue
        //}

        // Move the active truck through the waypoints
        yield return truck.MoveToWaypoints(waypoints);

        // Unload the boxes at the unload position
        yield return truck.UnloadBoxes();

        truckQueue.Enqueue(currentlyWasActiveTruck);

        // The next truck in the queue becomes active


        //***************************************************
        //// Move to the end of the queue
        //trucks.Remove(truck);
        //trucks.Add(truck);

        // Set the next truck in the queue as active
        //SetActiveTruck(0);
    }

    private void HandleTruckUnloaded(LiftTruck truck)
    {
        if (currentlyWasActiveTruck!=null)
        {
            StartCoroutine(MoveToTheThirdPosition(truck));
        }
    }
    private IEnumerator MoveToTheThirdPosition(LiftTruck truck)
    {
        yield return truck.MoveToWaypoints(waypointsIn);

    }
    public LiftTruck GetActiveTruck()
    {
        // Logic to return the currently active truck
        // Assuming you have a list of trucks to choose from
        return FindObjectsOfType<LiftTruck>().FirstOrDefault(truck => truck.IsActive);
    }


    private void UpdateTruckPositions2()
    {
        // Update truck positions based on the queue
        LiftTruck[] queueArray = truckQueue.ToArray();

        // Position 1: Active truck
        if (queueArray.Length > 0)
        {
            activeTruck = queueArray[0];
            activeTruck.MoveAfterDelay(position1.position, 0.1f);
        }

        // Position 2: Second truck in queue
        if (queueArray.Length > 1)
        {
            queueArray[1].MoveAfterDelay(position2.position, 0.1f);
        }

        // Position 3: Last truck in queue
        if (queueArray.Length > 2)
        {
            queueArray[2].MoveAfterDelay(position3.position, 0.1f);
        }
    }
    private void SetActiveTruck(int index)
    {
        // Deactivate all trucks
        for (int i = 0; i < trucks.Count; i++)
        {
            trucks[i].SetActive(i == index); // Only activate the truck at the given index
        }
        activeTruckIndex = index;
    }

}
