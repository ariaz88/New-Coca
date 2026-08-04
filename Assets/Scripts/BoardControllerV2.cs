using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using DG.Tweening;

public class BoardControllerV2 : MonoBehaviour
{
    BoardController boardController;
    float timer = 0;

    public List<Box> boardBoxes = new List<Box>();
    public Box lastPlacedBox;
    public bool IsBoxRemoved;
    public static BoardControllerV2 instance;
    [SerializeField] GameObject nodePref;
    [SerializeField] GameObject boxPref;
    public Node[,] grid;
    public Box[,] allBoxes;
    int height = 5;
    int width = 4;
    
    public int Height
    {
        get { return height; }
        private set { height = value; }
    }
    public int Width
    {
        get { return width; }
        private set { width = value; }
    }
    
    private Box currentBox;
    public bool isBoxFull;
    public int coins;
    bool isBoxInstantiated = false;
    bool isTransferInProgress = false;
    bool hasMatch = false;
    
    // New Universal Transfer System
    private UniversalSodaTransferSystem universalTransferSystem;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }
    
    void Start()
    {
        boardController = GetComponent<BoardController>();
        lastPlacedBox = null;
        grid = new Node[width, height];
        allBoxes = new Box[width, height];
        
        // Initialize the new universal transfer system
        universalTransferSystem = gameObject.AddComponent<UniversalSodaTransferSystem>();
        
        GenerateBoard();
        Debug.Log("BoardControllerV2 initialized with Universal Transfer System");
    }
    
    private void StartTransfer()
    {
        isTransferInProgress = true;
    }

    private void EndTransfer()
    {
        isTransferInProgress = false;
    }
    
    public void SetCurrentBox(Box box)
    {
        currentBox = box;
        currentBox.IsOnBoard = true;
    }
    
    public Box FindMostRecentBoxOnBoard()
    {
        Box mostRecentBox = null;
        float latestTimestamp = float.MinValue;

        foreach (var box in boardBoxes)
        {
            if (box != null && box.PlacementTimestamp > latestTimestamp)
            {
                mostRecentBox = box;
                latestTimestamp = box.PlacementTimestamp;
            }
        }

        return mostRecentBox;
    }
    
    private void GenerateBoard()
    {
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                Vector3 pos = new Vector3(i * 0.279f, 0.185f, j * 0.279f) + new Vector3(0.149f, 0.115f, 0.16f);
                GameObject cell = Instantiate(nodePref, pos, Quaternion.identity);
                cell.transform.parent = transform;
                Node node = cell.GetComponent<Node>();
                node.column = i;
                node.row = j;
                grid[i, j] = node;
            }
        }
    }
    
    public void UpdateBoxPosition(int i, int j)
    {
        if (i >= 0 && i < width && j >= 0 && j < height)
        {
            if (grid[i, j].isOccupied && allBoxes[i, j] == null)
            {
                if (currentBox != null && !boardBoxes.Contains(currentBox))
                {
                    boardBoxes.Add(currentBox);
                }
                
                allBoxes[i, j] = currentBox;
                currentBox.column = i;
                currentBox.row = j;
                IsBoxRemoved = false;

                // Use NEW universal transfer system instead of old CheckMatches
                CheckMatchesV2(i, j, currentBox);
                
                if (boardController != null)
                {
                    boardController.RemoveSpawnerList();
                }
            }
        }
    }
    
    // NEW: Simplified match checking using Universal Transfer System
    private void CheckMatchesV2(int column, int row, Box currentBox)
    {
        if (isTransferInProgress) return;

        // Get adjacent boxes
        List<Box> adjacentBoxes = new List<Box>();
        foreach (var (adjColumn, adjRow) in GetAdjacentPositions(column, row))
        {
            Box adjacentBox = allBoxes[adjColumn, adjRow];
            if (adjacentBox != null)
            {
                adjacentBoxes.Add(adjacentBox);
            }
        }

        if (adjacentBoxes.Count == 0)
        {
            CheckAndTransferFromFullBoxes();
            CheckBoardFill();
            return;
        }

        // Use the universal transfer system - handles ALL scenarios automatically!
        universalTransferSystem.ProcessAllTransfers(currentBox, adjacentBoxes);
        
        // Schedule cleanup after transfers complete
        StartCoroutine(ScheduleCleanup());
    }
    
    private IEnumerator ScheduleCleanup()
    {
        yield return new WaitForSeconds(2f); // Wait for all transfers to complete
        CheckAndTransferFromFullBoxes();
        CheckBoardFill();
    }
    
    public List<(int, int)> GetAdjacentPositions(int column, int row)
    {
        List<(int, int)> adjacentPositions = new List<(int, int)>();

        if (column > 0) adjacentPositions.Add((column - 1, row));
        if (column < width - 1) adjacentPositions.Add((column + 1, row));
        if (row > 0) adjacentPositions.Add((column, row - 1));
        if (row < height - 1) adjacentPositions.Add((column, row + 1));

        return adjacentPositions;
    }
    
    public HashSet<(int, int)> GetAllAdjacentPositionsForBoxes()
    {
        HashSet<(int, int)> adjacentPositions = new HashSet<(int, int)>();

        foreach (var box in boardBoxes)
        {
            (int column, int row) = (box.column, box.row);
            List<(int, int)> boxAdjacentPositions = GetAdjacentPositions(column, row);

            foreach (var pos in boxAdjacentPositions)
            {
                if (!IsPositionOccupied(pos.Item1, pos.Item2))
                {
                    adjacentPositions.Add(pos);
                }
            }
        }

        return adjacentPositions;
    }
    
    public HashSet<(int, int)> GetAdjacentPositionsForLastBox()
    {
        HashSet<(int, int)> adjacentPositions = new HashSet<(int, int)>();
        lastPlacedBox = FindMostRecentBoxOnBoard();
        
        if (lastPlacedBox != null)
        {
            (int column, int row) = (lastPlacedBox.column, lastPlacedBox.row);
            List<(int, int)> boxAdjacentPositions = GetAdjacentPositions(column, row);

            foreach (var pos in boxAdjacentPositions)
            {
                if (!IsPositionOccupied(pos.Item1, pos.Item2))
                {
                    adjacentPositions.Add(pos);
                }
            }
        }

        return adjacentPositions;
    }
    
    public Node GetNodeUnderMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Node node = hit.collider.GetComponent<Node>();
            if (node != null)
            {
                return node;
            }
        }
        return null;
    }

    public bool IsPositionOccupied(int column, int row)
    {
        return grid[column, row].isOccupied;
    }
    
    public bool IsBoardEmpty()
    {
        return boardBoxes.Count == 0;
    }
    
    public bool AreAdjacent(Box box1, Box box2)
    {
        if (box1 == null || box2 == null) return false;
        
        int columnDiff = Mathf.Abs(box1.column - box2.column);
        int rowDiff = Mathf.Abs(box1.row - box2.row);
        
        return (columnDiff == 1 && rowDiff == 0) || (columnDiff == 0 && rowDiff == 1);
    }
    
    public void DOSWAP(Box box1, Box box2)
    {
        if (!AreAdjacent(box1, box2)) return;
        
        Vector3 tempPos = box1.transform.position;
        box1.transform.position = box2.transform.position;
        box2.transform.position = tempPos;
        
        int tempCol = box1.column;
        int tempRow = box1.row;
        
        box1.column = box2.column;
        box1.row = box2.row;
        box2.column = tempCol;
        box2.row = tempRow;
        
        allBoxes[box1.column, box1.row] = box1;
        allBoxes[box2.column, box2.row] = box2;
    }
    
    // Keep essential methods from original Board.cs
    public void RemoveEmptyBoxes()
    {
        HandAnimation hand = GetComponent<HandAnimation>();

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                if (allBoxes[i, j] != null && allBoxes[i, j].Sodas.Count == 0)
                {
                    if (boardBoxes.Contains(allBoxes[i, j]))
                    {
                        boardBoxes.Remove(allBoxes[i, j]);
                    }
                    StartCoroutine(MakeInvisibleAfterDelay(allBoxes[i, j], 0.5f));
                    
                    if (SpawnContoller.instance != null && SpawnContoller.instance.isTutorialState)
                    {
                        HandAnimation.instance.TryRestartAnimation(0.8f);
                        Debug.Log("Show Hand is running From BOARD V2 - EMPTY");
                    }

                    grid[i, j].isOccupied = false;
                }

                if (allBoxes[i, j] != null && allBoxes[i, j].BoxFilled())
                {
                    if (boardBoxes.Contains(allBoxes[i, j]))
                    {
                        boardBoxes.Remove(allBoxes[i, j]);
                    }
                    
                    if (SpawnContoller.instance != null && SpawnContoller.instance.isTutorialState)
                    {
                        if (IsBoardEmpty())
                        {
                            HandAnimation.instance.TryRestartAnimation(0.1f);
                            Debug.Log("Show Hand is running From BOARD V2 - FULL");
                        }
                    }

                    var sodasInParent = allBoxes[i, j].transform.GetComponentsInChildren<Soda>();

                    if (sodasInParent.Length >= 4)
                    {
                        Debug.Log("Box Filled and soda count is: " + currentBox.Sodas.Count);
                        GameDataManager.instance.BoxCompletion(1);
                        GameDataManager.instance.AddCoins(100);
                        UIManager.instance.UpdateGameplayCoins(100);
                        GameManager.instance.CheckWinCondition();
                    }

                    Transform tempPos = allBoxes[i, j].transform;
                    grid[i, j].isOccupied = false;
                    allBoxes[i, j].CloseBox(tempPos);
                    
                    if (!allBoxes[i, j].IsInstantiated)
                    {
                        StartCoroutine(MoveToTruck(tempPos));
                        allBoxes[i, j].IsInstantiated = true;
                    }
                }
            }
        }
        
        if (UIManager.instance != null)
        {
            UIManager.instance.UpdateUI();
        }
    }
    
    private IEnumerator MakeInvisibleAfterDelay(Box box, float delay)
    {
        foreach (Transform child in box.transform)
        {
            MeshRenderer childRenderer = child.GetComponent<MeshRenderer>();
            if (childRenderer != null)
            {
                childRenderer.enabled = false;
            }
        }

        yield return new WaitForSeconds(delay);

        if (box != null)
        {
            Destroy(box.gameObject, 1.5f);
        }
    }
    
    private IEnumerator MoveToTruck(Transform boxTransform)
    {
        LiftTruck activeTruck = LiftTruckManager.instance.GetActiveTruck();

        if (activeTruck != null)
        {
            yield return new WaitForSeconds(0.3f);

            GameObject box = Instantiate(
                boxPref,
                new Vector3(boxTransform.position.x, boxTransform.position.y + 0.53f, boxTransform.position.z),
                Quaternion.Euler(0, 0, 0)
            );

            if (activeTruck.IsEnoughRoomLeft())
            {
                Vector3 truckTargetPosition = activeTruck.GetNextAvailablePosition();

                box.transform.DOMove(truckTargetPosition, 0.37f).SetEase(Ease.Linear).OnComplete(() =>
                {
                    activeTruck.AddBox(box);
                });
            }
            else
            {
                Debug.LogWarning("Active truck is full. Finding next truck...");
                LiftTruck nextTruck = LiftTruckManager.instance.GetActiveTruck();

                if (nextTruck != null)
                {
                    nextTruck.AddBox(box);
                }
                else
                {
                    Debug.LogError("No available trucks to add the box!");
                }
            }
        }
    }
    
    // Placeholder methods - implement if needed
    private void CheckAndTransferFromFullBoxes()
    {
        // This method can be simplified or removed since Universal Transfer System handles this
        Debug.Log("CheckAndTransferFromFullBoxes - handled by Universal Transfer System");
    }
    
    private void CheckBoardFill()
    {
        // Check if board is full and trigger lose condition
        bool isFull = true;
        for (int i = 0; i < width && isFull; i++)
        {
            for (int j = 0; j < height && isFull; j++)
            {
                if (!grid[i, j].isOccupied)
                {
                    isFull = false;
                }
            }
        }
        
        if (isFull && GameManager.instance != null)
        {
            GameManager.instance.CheckLoseCondition(true);
        }
    }
}