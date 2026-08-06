﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using DG.Tweening;

/// <summary>
/// Immutable snapshot raised after the Board has accepted a box into a cell.
/// The accepted Box is still alive at this boundary, even when the resolver will
/// subsequently match and retire it.
/// </summary>
public struct BoardPlacementAcceptedEvent
{
    public readonly Box Box;
    public readonly int StableId;
    public readonly long PlacementOrder;
    public readonly int Column;
    public readonly int Row;

    public BoardPlacementAcceptedEvent(Box box, int stableId, long placementOrder, int column, int row)
    {
        Box = box;
        StableId = stableId;
        PlacementOrder = placementOrder;
        Column = column;
        Row = row;
    }
}

/// <summary>
/// Immutable snapshot raised after the complete asynchronous placement resolver
/// has finished soda transfers, matches, removals, and Board cleanup.
/// </summary>
public struct BoardPlacementResolutionEvent
{
    public readonly int TriggerStableId;
    public readonly long PlacementOrder;
    public readonly int PackedMatches;
    public readonly bool IsBoardEmpty;

    public BoardPlacementResolutionEvent(
        int triggerStableId,
        long placementOrder,
        int packedMatches,
        bool isBoardEmpty)
    {
        TriggerStableId = triggerStableId;
        PlacementOrder = placementOrder;
        PackedMatches = packedMatches;
        IsBoardEmpty = isBoardEmpty;
    }
}

/// <summary>
/// Sparse, per-scene level-design data for one Board cell that starts occupied.
/// A missing entry means the playable cell starts empty. Item order maps directly
/// to SodaPosition0, SodaPosition1, and so on.
/// </summary>
[System.Serializable]
public sealed class InitialBoardBoxData
{
    public Vector2Int coordinate;

    [Tooltip("Optional. When empty, the active SpawnContoller Box Prefab is reused.")]
    public GameObject boxPrefabOverride;

    public List<Soda.SodaColor> startingSodas = new List<Soda.SodaColor>();
}

/// <summary>
/// Active board implementation.
/// This version is data-driven: it resolves the connected orthogonal component
/// around the placed box and asks UniversalSodaTransferSystem for one safe,
/// deterministic transfer at a time.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UniversalSodaTransferSystem))]
public class Board : MonoBehaviour
{
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    private static readonly Vector2Int[] DirectOffsets =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0)
    };

    [Header("Board")]
    [SerializeField] private GameObject nodePref;
    [SerializeField] private GameObject boxPref;
    [SerializeField, Min(1)] private int width = 4;
    [SerializeField, Min(1)] private int height = 5;
    [SerializeField, Tooltip("Cells omitted from this scene's Board layout. Coordinates are zero-based.")]
    private List<Vector2Int> removedCells = new List<Vector2Int>();
    [SerializeField] private Vector3 localOrigin = new Vector3(0.149f, 0.185f, 0.16f);
    [SerializeField] private Vector2 cellSpacing = new Vector2(0.279f, 0.279f);
    [SerializeField] private float placedBoxYOffset = 0.155f;

    [Header("Per-Level Initial Boxes")]
    [SerializeField, Tooltip("Sparse list: only cells that start with a box are stored.")]
    private List<InitialBoardBoxData> initialBoxes = new List<InitialBoardBoxData>();

    [Header("Scene View Layout Preview")]
    [SerializeField] private bool drawLayoutGizmos = true;
    [SerializeField] private Color playableCellGizmoColor = new Color(0.2f, 0.9f, 0.35f, 0.9f);
    [SerializeField] private Color removedCellGizmoColor = new Color(0.38f, 0.18f, 0.07f, 0.75f);
    [SerializeField, Min(0.001f)] private float gizmoCellHeight = 0.025f;
    [SerializeField, Range(0.1f, 1f)] private float gizmoCellFill = 0.82f;
    [SerializeField, Min(0f), Tooltip("Raises the Scene-view preview above the physical board surface.")]
    private float gizmoVerticalOffset = 0.17f;

    [Header("Breakable Blocked Cell Visual")]
    [SerializeField, Tooltip("Optional. When empty, Board creates a Cube automatically.")]
    private GameObject removedCellVisualPrefab;
    [SerializeField, Tooltip("Optional material override for generated blocked-cell visuals.")]
    private Material removedCellVisualMaterial;
    [SerializeField] private Color removedCellVisualColor = new Color(0.38f, 0.18f, 0.07f, 1f);
    [SerializeField, Range(0.1f, 1f)] private float removedCellVisualFill = 0.78f;
    [SerializeField, Min(0.01f)] private float removedCellVisualHeight = 0.06f;
    [SerializeField, Tooltip("Raises the Play-mode blocked-cell cube above the physical board surface.")]
    private float removedCellVisualYOffset = 0.15f;
    [SerializeField, Min(0f), Tooltip("Seconds used to shrink and break an adjacent brown cell after a packed match.")]
    private float blockedCellBreakDuration = 0.25f;

    [Header("Rewards")]
    [SerializeField] private bool awardPackedBoxRewards = true;
    [SerializeField, Min(0)] private int coinsPerPackedBox = 100;

    [Header("Packed Box Animation")]
    [SerializeField, Min(0.01f)] private float packedBoxMoveDuration = 0.37f;

    public List<Box> boardBoxes = new List<Box>();
    public Box lastPlacedBox;
    public bool IsBoxRemoved;
    public static Board instance;
    public Node[,] grid;
    public Box[,] allBoxes;
    public bool isBoxFull;
    public int coins;

    private BoardController boardController;
    private Box currentBox;
    private UniversalSodaTransferSystem universalTransferSystem;
    private readonly HashSet<Box> retiredBoxes = new HashSet<Box>();
    private long placementSequence;
    private long packedMatchSequence;
    private int nextStableId = 1;
    private readonly HashSet<Vector2Int> removedCellLookup = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> breakingBlockedCells = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, GameObject> removedCellVisuals =
        new Dictionary<Vector2Int, GameObject>();
    private Material generatedRemovedCellMaterial;
    private object placementConstraintOwner;
    private System.Func<Box, int, int, bool> placementConstraint;

    /// <summary>Raised exactly once for every Board-accepted placement.</summary>
    public event System.Action<BoardPlacementAcceptedEvent> PlacementAccepted;

    /// <summary>Raised exactly once when that placement's full resolver is complete.</summary>
    public event System.Action<BoardPlacementResolutionEvent> PlacementResolutionCompleted;

    public int Height => height;
    public int Width => width;
    public int PlayableCellCount => width * height - removedCellLookup.Count;
    public bool IsResolving { get; private set; }
    public bool CanInteract => !IsResolving;
    public bool HasPlacementConstraint => placementConstraintOwner != null && placementConstraint != null;

    private void Awake()
    {
        RebuildLayoutCache();

        if (instance != null && instance != this)
        {
            Debug.LogError("Only one Board may be active.", this);
            enabled = false;
            return;
        }

        instance = this;
        universalTransferSystem = GetComponent<UniversalSodaTransferSystem>();
        if (universalTransferSystem == null)
        {
            universalTransferSystem = gameObject.AddComponent<UniversalSodaTransferSystem>();
        }
    }

    private void Start()
    {
        boardController = GetComponent<BoardController>();
        grid = new Node[width, height];
        allBoxes = new Box[width, height];
        boardBoxes.Clear();
        lastPlacedBox = null;
        retiredBoxes.Clear();
        GenerateBoard();
        GenerateInitialBoxes();
    }

    private void OnDestroy()
    {
        if (generatedRemovedCellMaterial != null)
        {
            Destroy(generatedRemovedCellMaterial);
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    public bool IsInside(int column, int row)
    {
        return column >= 0 && column < width && row >= 0 && row < height;
    }

    /// <summary>
    /// Returns true only when the coordinate is inside the rectangular bounds and
    /// has not been removed by this scene's custom Board layout.
    /// </summary>
    public bool IsPlayableCell(int column, int row)
    {
        return IsInside(column, row) &&
               !removedCellLookup.Contains(new Vector2Int(column, row));
    }

    public bool CanPlaceAt(int column, int row)
    {
        return CanInteract &&
               IsInside(column, row) &&
               grid != null &&
               grid[column, row] != null &&
               allBoxes != null &&
               allBoxes[column, row] == null &&
               !grid[column, row].isOccupied;
    }

    /// <summary>
    /// Installs one optional owner-scoped rule on top of the normal Board rules.
    /// This is used by feature Tutorials without changing normal-level placement.
    /// </summary>
    public bool TrySetPlacementConstraint(
        object owner,
        System.Func<Box, int, int, bool> constraint)
    {
        if (owner == null || constraint == null)
        {
            return false;
        }

        if (placementConstraintOwner != null &&
            !ReferenceEquals(placementConstraintOwner, owner))
        {
            return false;
        }

        placementConstraintOwner = owner;
        placementConstraint = constraint;
        return true;
    }

    /// <summary>Clears the optional rule only when called by its owner.</summary>
    public void ClearPlacementConstraint(object owner)
    {
        if (owner == null || !ReferenceEquals(placementConstraintOwner, owner))
        {
            return;
        }

        placementConstraintOwner = null;
        placementConstraint = null;
    }

    public bool TryPlaceBox(Box box, int column, int row)
    {
        if (box == null || box.IsRetired || !CanPlaceAt(column, row) ||
            (placementConstraint != null && !placementConstraint(box, column, row)))
        {
            return false;
        }

        SetCurrentBox(box);
        UpdateBoxPosition(column, row);
        return true;
    }

    public void SetCurrentBox(Box box)
    {
        currentBox = box;
        if (currentBox != null)
        {
            currentBox.IsOnBoard = true;
        }
    }

    public void UpdateBoxPosition(int column, int row)
    {
        if (currentBox == null || !IsInside(column, row) || allBoxes == null || grid == null)
        {
            return;
        }

        if (allBoxes[column, row] != null && allBoxes[column, row] != currentBox)
        {
            return;
        }

        Box placedBox = currentBox;
        placedBox.RefreshContents();

        if (placedBox.Capacity <= 0)
        {
            Debug.LogError($"{placedBox.name} has no SodaPosition slots. Add SodaPosition children to the prefab.", placedBox);
            return;
        }

        if (placedBox.GetSodasCount() > placedBox.Capacity)
        {
            Debug.LogError($"{placedBox.name} has more sodas than its discovered capacity.", placedBox);
            return;
        }

        allBoxes[column, row] = placedBox;
        grid[column, row].isOccupied = true;
        placedBox.transform.position = GetPlacementWorldPosition(column, row);
        placedBox.MarkPlaced(column, row, nextStableId++, ++placementSequence);

        if (!boardBoxes.Contains(placedBox))
        {
            boardBoxes.Add(placedBox);
        }

        lastPlacedBox = placedBox;
        IsBoxRemoved = false;

        PlacementAccepted?.Invoke(new BoardPlacementAcceptedEvent(
            placedBox,
            placedBox.StableId,
            placedBox.PlacementOrder,
            column,
            row));

        if (SpawnContoller.instance != null)
        {
            SpawnContoller.instance.spawnedBoxes.Remove(placedBox.gameObject);
        }

        if (boardController != null)
        {
            boardController.RemoveSpawnerList();
        }

        currentBox = null;
        StartCoroutine(ResolvePlacement(placedBox));
    }

    public Box FindMostRecentBoxOnBoard()
    {
        Box mostRecentBox = null;
        float latestTimestamp = float.MinValue;

        foreach (Box box in boardBoxes)
        {
            if (box != null && !box.IsRetired && box.PlacementTimestamp > latestTimestamp)
            {
                mostRecentBox = box;
                latestTimestamp = box.PlacementTimestamp;
            }
        }

        return mostRecentBox;
    }

    public Vector3 GetPlacementWorldPosition(int column, int row)
    {
        if (IsInside(column, row) && grid != null && grid[column, row] != null)
        {
            return grid[column, row].transform.position + Vector3.up * placedBoxYOffset;
        }

        return transform.TransformPoint(
            localOrigin + new Vector3(
                column * cellSpacing.x,
                placedBoxYOffset,
                row * cellSpacing.y));
    }

    public Box GetBox(int column, int row)
    {
        return IsInside(column, row) && allBoxes != null ? allBoxes[column, row] : null;
    }

    public IEnumerable<Box> GetDirectNeighbours(Box box)
    {
        if (box == null || box.IsRetired || allBoxes == null)
        {
            yield break;
        }

        foreach (Vector2Int offset in DirectOffsets)
        {
            int column = box.column + offset.x;
            int row = box.row + offset.y;
            if (!IsPlayableCell(column, row))
            {
                continue;
            }

            Box neighbour = allBoxes[column, row];
            if (neighbour != null && !neighbour.IsRetired)
            {
                yield return neighbour;
            }
        }
    }

    public bool AreDirectlyAdjacent(Box first, Box second)
    {
        if (first == null || second == null)
        {
            return false;
        }

        return Mathf.Abs(first.column - second.column) +
               Mathf.Abs(first.row - second.row) == 1;
    }

    public bool AreAdjacent(Box box1, Box box2)
    {
        return AreDirectlyAdjacent(box1, box2);
    }

    public List<Box> GetConnectedComponent(Box origin)
    {
        List<Box> result = new List<Box>();
        if (origin == null || origin.IsRetired)
        {
            return result;
        }

        HashSet<Box> visited = new HashSet<Box>();
        Queue<Box> queue = new Queue<Box>();
        visited.Add(origin);
        queue.Enqueue(origin);

        while (queue.Count > 0)
        {
            Box current = queue.Dequeue();
            result.Add(current);

            foreach (Box neighbour in GetDirectNeighbours(current))
            {
                if (visited.Add(neighbour))
                {
                    queue.Enqueue(neighbour);
                }
            }
        }

        return result;
    }

    public List<(int, int)> GetAdjacentPositions(int column, int row)
    {
        List<(int, int)> adjacentPositions = new List<(int, int)>();
        foreach (Vector2Int offset in DirectOffsets)
        {
            int nextColumn = column + offset.x;
            int nextRow = row + offset.y;
            if (IsPlayableCell(nextColumn, nextRow))
            {
                adjacentPositions.Add((nextColumn, nextRow));
            }
        }

        return adjacentPositions;
    }

    public HashSet<(int, int)> GetAdjacentPositionsForLastBox()
    {
        HashSet<(int, int)> adjacentPositions = new HashSet<(int, int)>();
        lastPlacedBox = FindMostRecentBoxOnBoard();

        if (lastPlacedBox == null)
        {
            return adjacentPositions;
        }

        foreach ((int column, int row) in GetAdjacentPositions(lastPlacedBox.column, lastPlacedBox.row))
        {
            if (!IsPositionOccupied(column, row))
            {
                adjacentPositions.Add((column, row));
            }
        }

        return adjacentPositions;
    }

    public HashSet<(int, int)> GetAllAdjacentPositionsForBoxes()
    {
        HashSet<(int, int)> adjacentPositions = new HashSet<(int, int)>();

        foreach (Box box in boardBoxes)
        {
            if (box == null || box.IsRetired)
            {
                continue;
            }

            foreach ((int column, int row) in GetAdjacentPositions(box.column, box.row))
            {
                if (!IsPositionOccupied(column, row))
                {
                    adjacentPositions.Add((column, row));
                }
            }
        }

        return adjacentPositions;
    }

    public Node GetNodeUnderMouse()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return null;
        }

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            Node node = hit.collider.GetComponentInParent<Node>();
            if (node != null)
            {
                return node;
            }
        }

        return null;
    }

    public Node GetDropTargetNode(Vector3 worldPosition)
    {
        if (grid == null ||
            Mathf.Approximately(cellSpacing.x, 0f) ||
            Mathf.Approximately(cellSpacing.y, 0f))
        {
            return null;
        }

        Vector3 localPosition = transform.InverseTransformPoint(worldPosition) - localOrigin;
        float columnPosition = localPosition.x / cellSpacing.x;
        float rowPosition = localPosition.z / cellSpacing.y;

        if (columnPosition <= -0.5f || columnPosition >= width - 0.5f ||
            rowPosition <= -0.5f || rowPosition >= height - 0.5f)
        {
            return null;
        }

        int column = Mathf.RoundToInt(columnPosition);
        int row = Mathf.RoundToInt(rowPosition);
        return IsInside(column, row) ? grid[column, row] : null;
    }

    public bool IsPositionOccupied(int column, int row)
    {
        return !IsInside(column, row) ||
               grid == null ||
               grid[column, row] == null ||
               grid[column, row].isOccupied;
    }

    public bool IsBoardEmpty()
    {
        CleanupBoardList();
        return boardBoxes.Count == 0;
    }

    public void DOSWAP(Box box1, Box box2)
    {
        if (!AreAdjacent(box1, box2) || allBoxes == null)
        {
            return;
        }

        Vector3 tempPosition = box1.transform.position;
        box1.transform.position = box2.transform.position;
        box2.transform.position = tempPosition;

        int firstColumn = box1.column;
        int firstRow = box1.row;
        int secondColumn = box2.column;
        int secondRow = box2.row;

        box1.SetBoardCoordinates(secondColumn, secondRow);
        box2.SetBoardCoordinates(firstColumn, firstRow);

        allBoxes[firstColumn, firstRow] = box2;
        allBoxes[secondColumn, secondRow] = box1;

        if (!IsResolving)
        {
            StartCoroutine(ResolvePlacement(box1));
        }
    }

    public void RemoveEmptyBoxes()
    {
        List<Box> activeBoxes = GetAllActiveBoxes();
        StartCoroutine(RetireTerminalBoxesRoutine(activeBoxes));
    }

    public void NotifyBoxRemoved(Box box, bool destroyObject)
    {
        if (box == null)
        {
            return;
        }

        RemoveBoxFromGrid(box);
        boardBoxes.Remove(box);
        box.MarkRetired();

        if (destroyObject)
        {
            Destroy(box.gameObject, 0.3f);
        }
    }

    public void UpdateBoard()
    {
        CleanupBoardList();
        CheckBoardFill();
    }

    public bool InteractionFinished()
    {
        return !IsResolving;
    }

    public void ClearBoard()
    {
        if (allBoxes == null)
        {
            return;
        }

        for (int column = 0; column < width; column++)
        {
            for (int row = 0; row < height; row++)
            {
                Box box = allBoxes[column, row];
                allBoxes[column, row] = null;
                if (grid[column, row] != null)
                {
                    grid[column, row].isOccupied = false;
                }

                if (box != null)
                {
                    Destroy(box.gameObject);
                }
            }
        }

        boardBoxes.Clear();
        retiredBoxes.Clear();
        currentBox = null;
        lastPlacedBox = null;
        IsResolving = false;
        placementSequence = 0;
        packedMatchSequence = 0;
        nextStableId = 1;
    }

    public void HighLightHammerNode()
    {
        if (grid == null || allBoxes == null)
        {
            return;
        }

        for (int column = 0; column < width; column++)
        {
            for (int row = 0; row < height; row++)
            {
                if (allBoxes[column, row] != null && grid[column, row] != null)
                {
                    grid[column, row].HighLightForHammer();
                }
            }
        }
    }

    public void UnHighLightHammerNode()
    {
        if (grid == null)
        {
            return;
        }

        foreach (Node node in grid)
        {
            if (node != null)
            {
                node.Unhighlight();
            }
        }
    }

    public (int column, int row) GetNodePositionFromWorld(Vector3 worldPosition)
    {
        if (grid != null)
        {
            float bestDistance = float.MaxValue;
            int bestColumn = 0;
            int bestRow = 0;

            for (int column = 0; column < width; column++)
            {
                for (int row = 0; row < height; row++)
                {
                    if (grid[column, row] == null)
                    {
                        continue;
                    }

                    float distance = (grid[column, row].transform.position - worldPosition).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestColumn = column;
                        bestRow = row;
                    }
                }
            }

            return (bestColumn, bestRow);
        }

        Vector3 adjustedPosition = transform.InverseTransformPoint(worldPosition) - localOrigin;
        int fallbackColumn = Mathf.Clamp(Mathf.RoundToInt(adjustedPosition.x / cellSpacing.x), 0, width - 1);
        int fallbackRow = Mathf.Clamp(Mathf.RoundToInt(adjustedPosition.z / cellSpacing.y), 0, height - 1);
        return (fallbackColumn, fallbackRow);
    }

    /// <summary>
    /// Returns active Nodes in a row-by-row serpentine path. Presentation systems
    /// such as the clear cylinder can use this without assuming a fixed Board size.
    /// </summary>
    public List<Node> GetPlayableNodesSerpentine()
    {
        List<Node> nodes = new List<Node>();
        if (grid == null)
        {
            return nodes;
        }

        for (int row = 0; row < height; row++)
        {
            if (row % 2 == 0)
            {
                for (int column = 0; column < width; column++)
                {
                    AddPlayableNode(nodes, column, row);
                }
            }
            else
            {
                for (int column = width - 1; column >= 0; column--)
                {
                    AddPlayableNode(nodes, column, row);
                }
            }
        }

        return nodes;
    }

    public Node GetFirstPlayableNode()
    {
        if (grid == null)
        {
            return null;
        }

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                if (grid[column, row] != null)
                {
                    return grid[column, row];
                }
            }
        }

        return null;
    }

    internal float RetireTerminalBoxes(ICollection<Box> component)
    {
        if (component == null || component.Count == 0)
        {
            return 0f;
        }

        float maxDelay = 0f;
        foreach (Box box in component.ToList())
        {
            if (box == null || box.IsRetired)
            {
                continue;
            }

            box.RefreshContents();
            if (!box.BoxFilled() && !box.IsEmpty)
            {
                continue;
            }

            bool packed = box.BoxFilled();
            float animationDuration = packed ? packedBoxMoveDuration : box.RemovalDuration;
            RetireBox(box, packed);
            maxDelay = Mathf.Max(maxDelay, animationDuration);
        }

        return maxDelay;
    }

    private IEnumerator RetireTerminalBoxesRoutine(ICollection<Box> boxesToCheck)
    {
        float delay = RetireTerminalBoxes(boxesToCheck);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        CheckBoardFill();
    }

    private IEnumerator ResolvePlacement(Box trigger)
    {
        if (trigger == null)
        {
            yield break;
        }

        int triggerStableId = trigger.StableId;
        long triggerPlacementOrder = trigger.PlacementOrder;
        long packedMatchesBeforeResolution = packedMatchSequence;

        IsResolving = true;
        if (universalTransferSystem != null)
        {
            yield return universalTransferSystem.Resolve(this, trigger);
        }
        else
        {
            Debug.LogWarning("Board cannot run soda transfers because UniversalSodaTransferSystem is missing.", this);
        }

        // A placement is not fully resolved until every blocker touched by its
        // packed matches has finished breaking and become a real empty Node.
        while (breakingBlockedCells.Count > 0)
        {
            yield return null;
        }

        CleanupBoardList();
        CheckBoardFill();
        UIManager.instance?.UpdateUI();
        IsResolving = false;

        int packedMatches = (int)System.Math.Max(0L, packedMatchSequence - packedMatchesBeforeResolution);
        PlacementResolutionCompleted?.Invoke(new BoardPlacementResolutionEvent(
            triggerStableId,
            triggerPlacementOrder,
            packedMatches,
            IsBoardEmpty()));
    }

    private void RetireBox(Box box, bool packed)
    {
        if (box == null || !retiredBoxes.Add(box))
        {
            return;
        }

        RemoveBoxFromGrid(box);
        boardBoxes.Remove(box);
        box.MarkRetired();

        if (packed)
        {
            BeginUnlockAdjacentBlockedCells(box.column, box.row);
            packedMatchSequence++;
            Vector3 sourcePosition = box.transform.position;
            AwardPackedBox(box);
            MovePackedCopyToTruck(sourcePosition);

            box.gameObject.SetActive(false);
            Destroy(box.gameObject);
            return;
        }

        StartCoroutine(box.PlayRemovalAnimation(false));
    }

    private void RemoveBoxFromGrid(Box box)
    {
        if (box == null || allBoxes == null || grid == null)
        {
            return;
        }

        int column = box.column;
        int row = box.row;
        if (IsInside(column, row) && allBoxes[column, row] == box)
        {
            allBoxes[column, row] = null;
            if (grid[column, row] != null)
            {
                grid[column, row].isOccupied = false;
            }
        }
    }

    private List<Box> GetAllActiveBoxes()
    {
        List<Box> result = new List<Box>();
        if (allBoxes == null)
        {
            return result;
        }

        for (int column = 0; column < width; column++)
        {
            for (int row = 0; row < height; row++)
            {
                Box box = allBoxes[column, row];
                if (box != null && !box.IsRetired)
                {
                    result.Add(box);
                }
            }
        }

        return result;
    }

    private void CleanupBoardList()
    {
        boardBoxes.RemoveAll(box => box == null || box.IsRetired || !box.IsOnBoard);
    }

    private void AwardPackedBox(Box box)
    {
        if (!awardPackedBoxRewards)
        {
            return;
        }

        if (GameDataManager.instance != null)
        {
            GameDataManager.instance.BoxCompletion(1);
            GameDataManager.instance.AddCoins(coinsPerPackedBox);
        }

        if (UIManager.instance != null)
        {
            UIManager.instance.UpdateGameplayCoins(coinsPerPackedBox);
        }

        if (CoinManager.instance != null)
        {
            CoinManager.instance.AddCoins(box.transform.position, coinsPerPackedBox);
        }

        if (GameManager.instance != null)
        {
            GameManager.instance.CheckWinCondition();
        }
    }

    private void MovePackedCopyToTruck(Vector3 sourcePosition)
    {
        LiftTruckManager truckManager = LiftTruckManager.instance;
        LiftTruck activeTruck = truckManager != null
            ? truckManager.GetActiveTruck()
            : null;

        if (activeTruck == null || boxPref == null)
        {
            return;
        }

        GameObject movingBox = Instantiate(boxPref, sourcePosition, Quaternion.identity);

        if (activeTruck.IsEnoughRoomLeft())
        {
            Vector3 target = activeTruck.GetNextAvailablePosition();
            movingBox.transform.DOMove(target, packedBoxMoveDuration).OnComplete(() =>
            {
                if (movingBox == null)
                {
                    return;
                }

                if (activeTruck != null)
                {
                    activeTruck.AddBox(movingBox);
                }
                else
                {
                    Destroy(movingBox);
                }
            });
        }
        else
        {
            LiftTruck nextTruck = truckManager.GetActiveTruck();
            if (nextTruck != null && nextTruck.IsEnoughRoomLeft())
            {
                Vector3 target = nextTruck.GetNextAvailablePosition();
                movingBox.transform.DOMove(target, packedBoxMoveDuration)
                    .OnComplete(() =>
                    {
                        if (movingBox == null)
                        {
                            return;
                        }

                        if (nextTruck != null)
                        {
                            nextTruck.AddBox(movingBox);
                        }
                        else
                        {
                            Destroy(movingBox);
                        }
                    });
            }
            else
            {
                Destroy(movingBox);
            }
        }
    }

    private void CheckBoardFill()
    {
        if (grid == null || allBoxes == null)
        {
            return;
        }

        bool foundPlayableCell = false;
        for (int column = 0; column < width; column++)
        {
            for (int row = 0; row < height; row++)
            {
                if (grid[column, row] == null)
                {
                    continue;
                }

                foundPlayableCell = true;
                if (allBoxes[column, row] == null)
                {
                    return;
                }
            }
        }

        if (foundPlayableCell)
        {
            GameManager.instance?.CheckLoseCondition(true);
        }
    }

    private void GenerateBoard()
    {
        if (nodePref == null)
        {
            Debug.LogError("Board needs a node prefab.", this);
            return;
        }

        for (int column = 0; column < width; column++)
        {
            for (int row = 0; row < height; row++)
            {
                if (!IsPlayableCell(column, row))
                {
                    GenerateRemovedCellVisual(column, row);
                    continue;
                }

                CreatePlayableNode(column, row);
            }
        }
    }

    private Node CreatePlayableNode(int column, int row)
    {
        if (nodePref == null || grid == null || !IsInside(column, row))
        {
            return null;
        }

        Node existingNode = grid[column, row];
        if (existingNode != null)
        {
            return existingNode;
        }

        GameObject cell = Instantiate(
            nodePref,
            transform.TransformPoint(GetCellLocalPosition(column, row)),
            transform.rotation,
            transform);
        Node node = cell.GetComponent<Node>();
        if (node == null)
        {
            Debug.LogError("Node prefab must contain a Node component.", cell);
            Destroy(cell);
            return null;
        }

        node.column = column;
        node.row = row;
        node.isOccupied = false;
        grid[column, row] = node;
        return node;
    }

    private void GenerateInitialBoxes()
    {
        if (initialBoxes == null || initialBoxes.Count == 0)
        {
            return;
        }

        HashSet<Vector2Int> spawnedCoordinates = new HashSet<Vector2Int>();
        foreach (InitialBoardBoxData data in initialBoxes)
        {
            if (data == null || !IsPlayableCell(data.coordinate.x, data.coordinate.y) ||
                !spawnedCoordinates.Add(data.coordinate))
            {
                Debug.LogWarning("An invalid, removed, or duplicate initial Box cell was skipped.", this);
                continue;
            }

            GameObject prefab = ResolveInitialBoxPrefab(data);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"Initial Box {data.coordinate} needs a prefab override or an assigned SpawnContoller Box Prefab.",
                    this);
                continue;
            }

            Vector3 position = GetPlacementWorldPosition(data.coordinate.x, data.coordinate.y);
            GameObject boxObject = Instantiate(prefab, position, Quaternion.Euler(-90f, 0f, 0f));
            Box box = boxObject.GetComponent<Box>();
            if (box == null)
            {
                Debug.LogError($"Initial Box prefab '{prefab.name}' does not contain Box.", boxObject);
                Destroy(boxObject);
                continue;
            }

            box.name = $"Initial Box ({data.coordinate.x}, {data.coordinate.y})";
            box.DiscoverSlots();
            if (box.Capacity <= 0)
            {
                Debug.LogError($"'{prefab.name}' has no SodaPosition slots.", boxObject);
                Destroy(boxObject);
                continue;
            }

            PopulateInitialSodas(box, data);
            box.RefreshContents();
            RegisterInitialBox(box, data.coordinate.x, data.coordinate.y);
        }
    }

    private static GameObject ResolveInitialBoxPrefab(InitialBoardBoxData data)
    {
        if (data != null && data.boxPrefabOverride != null)
        {
            return data.boxPrefabOverride;
        }

        return SpawnContoller.instance != null ? SpawnContoller.instance.boxPrefab : null;
    }

    private void PopulateInitialSodas(Box box, InitialBoardBoxData data)
    {
        if (box == null || data == null || data.startingSodas == null ||
            data.startingSodas.Count == 0)
        {
            return;
        }

        GameObject sodaPrefab = box.sodaPrefab != null
            ? box.sodaPrefab
            : SpawnContoller.instance != null ? SpawnContoller.instance.sodaPrefab : null;
        if (sodaPrefab == null)
        {
            Debug.LogWarning($"'{box.name}' has initial items but no Soda Prefab.", box);
            return;
        }

        int itemCount = Mathf.Min(data.startingSodas.Count, box.Capacity);
        if (data.startingSodas.Count > box.Capacity)
        {
            Debug.LogWarning(
                $"'{box.name}' defines {data.startingSodas.Count} sodas but its real capacity is {box.Capacity}. " +
                "Extra items were ignored.",
                box);
        }

        for (int slotIndex = 0; slotIndex < itemCount; slotIndex++)
        {
            Transform slot = box.GetSlot(slotIndex);
            if (slot == null)
            {
                continue;
            }

            GameObject sodaObject = Instantiate(
                sodaPrefab,
                slot.position,
                Quaternion.Euler(-90f, 0f, 0f),
                box.transform);
            Soda soda = sodaObject.GetComponent<Soda>();
            if (soda == null)
            {
                Debug.LogError($"Soda Prefab '{sodaPrefab.name}' does not contain Soda.", sodaObject);
                Destroy(sodaObject);
                continue;
            }

            soda.SetColor(data.startingSodas[slotIndex]);
        }
    }

    private void RegisterInitialBox(Box box, int column, int row)
    {
        if (box == null || grid == null || allBoxes == null || !IsPlayableCell(column, row) ||
            grid[column, row] == null || allBoxes[column, row] != null)
        {
            if (box != null)
            {
                Destroy(box.gameObject);
            }

            return;
        }

        allBoxes[column, row] = box;
        grid[column, row].isOccupied = true;
        box.transform.position = GetPlacementWorldPosition(column, row);
        box.MarkPlaced(column, row, nextStableId++, ++placementSequence);
        boardBoxes.Add(box);
    }

    private void GenerateRemovedCellVisual(int column, int row)
    {
        // Scene editing uses Gizmos only. Real blocker objects must exist only in Play Mode.
        if (!Application.isPlaying)
        {
            return;
        }

        Vector3 localPosition = GetCellLocalPosition(column, row);
        localPosition.y += removedCellVisualYOffset;

        GameObject visual;
        bool createdFallbackCube = false;
        if (removedCellVisualPrefab != null)
        {
            visual = Instantiate(removedCellVisualPrefab, transform);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = Quaternion.identity;
        }
        else
        {
            createdFallbackCube = true;
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(
                Mathf.Abs(cellSpacing.x) * removedCellVisualFill,
                removedCellVisualHeight,
                Mathf.Abs(cellSpacing.y) * removedCellVisualFill);
        }

        visual.name = $"Blocked Cell ({column}, {row})";
        removedCellVisuals[new Vector2Int(column, row)] = visual;

        foreach (Collider visualCollider in visual.GetComponentsInChildren<Collider>(true))
        {
            visualCollider.enabled = false;
            Destroy(visualCollider);
        }

        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        properties.SetColor(BaseColorProperty, removedCellVisualColor);
        properties.SetColor(ColorProperty, removedCellVisualColor);
        foreach (Renderer visualRenderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Material visualMaterial = removedCellVisualMaterial;
            if (visualMaterial == null && createdFallbackCube)
            {
                visualMaterial = GetOrCreateRemovedCellMaterial();
            }

            if (visualMaterial != null)
            {
                visualRenderer.sharedMaterial = visualMaterial;
            }

            visualRenderer.SetPropertyBlock(properties);
        }
    }

    /// <summary>
    /// Starts one non-chaining break for each blocked orthogonal neighbour of a
    /// packed Box. The lookup remains blocked until its own animation completes.
    /// </summary>
    private void BeginUnlockAdjacentBlockedCells(int column, int row)
    {
        foreach (Vector2Int offset in DirectOffsets)
        {
            Vector2Int coordinate = new Vector2Int(column + offset.x, row + offset.y);
            if (!IsInside(coordinate.x, coordinate.y) ||
                !removedCellLookup.Contains(coordinate) ||
                !breakingBlockedCells.Add(coordinate))
            {
                continue;
            }

            StartCoroutine(BreakBlockedCellRoutine(coordinate));
        }
    }

    private IEnumerator BreakBlockedCellRoutine(Vector2Int coordinate)
    {
        removedCellVisuals.TryGetValue(coordinate, out GameObject visual);
        if (visual != null && blockedCellBreakDuration > 0f)
        {
            Transform visualTransform = visual.transform;
            Vector3 startingScale = visualTransform.localScale;
            float elapsed = 0f;

            while (visual != null && elapsed < blockedCellBreakDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / blockedCellBreakDuration);
                float breakScale = 1f - Mathf.SmoothStep(0f, 1f, progress);
                visualTransform.localScale = startingScale * breakScale;
                yield return null;
            }
        }

        UnlockBlockedCell(coordinate, visual);
    }

    private void UnlockBlockedCell(Vector2Int coordinate, GameObject visual)
    {
        removedCellLookup.Remove(coordinate);
        removedCells?.Remove(coordinate);
        removedCellVisuals.Remove(coordinate);

        if (visual != null)
        {
            Destroy(visual);
        }

        // Creating the real Node refreshes occupancy and drop/highlight lookup.
        // All neighbour queries read grid/removedCellLookup live, so the new cell
        // participates immediately without rebuilding the whole Board.
        if (allBoxes != null)
        {
            allBoxes[coordinate.x, coordinate.y] = null;
        }

        Node node = CreatePlayableNode(coordinate.x, coordinate.y);
        if (node != null)
        {
            node.isOccupied = false;
            node.Unhighlight();
        }

        breakingBlockedCells.Remove(coordinate);
    }

    private Material GetOrCreateRemovedCellMaterial()
    {
        if (generatedRemovedCellMaterial != null)
        {
            return generatedRemovedCellMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return null;
        }

        generatedRemovedCellMaterial = new Material(shader)
        {
            name = "Generated Blocked Cell Material"
        };
        generatedRemovedCellMaterial.SetColor(BaseColorProperty, removedCellVisualColor);
        generatedRemovedCellMaterial.SetColor(ColorProperty, removedCellVisualColor);
        return generatedRemovedCellMaterial;
    }

    private Vector3 GetCellLocalPosition(int column, int row)
    {
        return localOrigin + new Vector3(
            column * cellSpacing.x,
            0f,
            row * cellSpacing.y);
    }

    private void AddPlayableNode(List<Node> nodes, int column, int row)
    {
        Node node = grid[column, row];
        if (node != null)
        {
            nodes.Add(node);
        }
    }

    private void RebuildLayoutCache()
    {
        removedCellLookup.Clear();
        if (removedCells == null)
        {
            removedCells = new List<Vector2Int>();
            return;
        }

        foreach (Vector2Int cell in removedCells)
        {
            if (IsInside(cell.x, cell.y))
            {
                removedCellLookup.Add(cell);
            }
        }
    }

    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        blockedCellBreakDuration = Mathf.Max(0f, blockedCellBreakDuration);

        if (removedCells == null)
        {
            removedCells = new List<Vector2Int>();
        }

        HashSet<Vector2Int> validUniqueCells = new HashSet<Vector2Int>();
        for (int index = removedCells.Count - 1; index >= 0; index--)
        {
            Vector2Int cell = removedCells[index];
            if (!IsInside(cell.x, cell.y) || !validUniqueCells.Add(cell))
            {
                removedCells.RemoveAt(index);
            }
        }

        // A Board with no playable cell cannot support spawning or placement.
        if (validUniqueCells.Count >= width * height)
        {
            removedCells.Remove(new Vector2Int(0, 0));
        }

        if (initialBoxes == null)
        {
            initialBoxes = new List<InitialBoardBoxData>();
        }

        HashSet<Vector2Int> uniqueInitialCells = new HashSet<Vector2Int>();
        for (int index = initialBoxes.Count - 1; index >= 0; index--)
        {
            InitialBoardBoxData data = initialBoxes[index];
            if (data == null || !IsInside(data.coordinate.x, data.coordinate.y) ||
                removedCells.Contains(data.coordinate) ||
                !uniqueInitialCells.Add(data.coordinate))
            {
                initialBoxes.RemoveAt(index);
                continue;
            }

            if (data.startingSodas == null)
            {
                data.startingSodas = new List<Soda.SodaColor>();
            }
        }

        RebuildLayoutCache();
    }

    /// <summary>
    /// Called by BoardEditor through Unity's official DrawGizmo registration.
    /// This keeps the complete board preview visible in the Scene window and
    /// gives it a dedicated Board entry in the Scene Gizmos menu.
    /// </summary>
    public void DrawLayoutGizmos()
    {
        if (!drawLayoutGizmos || width <= 0 || height <= 0)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 cellSize = new Vector3(
            Mathf.Abs(cellSpacing.x) * gizmoCellFill,
            gizmoCellHeight,
            Mathf.Abs(cellSpacing.y) * gizmoCellFill);

        for (int column = 0; column < width; column++)
        {
            for (int row = 0; row < height; row++)
            {
                Vector3 center = GetCellLocalPosition(column, row);
                center.y += gizmoVerticalOffset + gizmoCellHeight * 0.5f;

                if (IsConfiguredRemovedCell(column, row))
                {
                    Gizmos.color = removedCellGizmoColor;
                    Gizmos.DrawCube(center, cellSize);
                    Gizmos.color = new Color(
                        removedCellGizmoColor.r,
                        removedCellGizmoColor.g,
                        removedCellGizmoColor.b,
                        1f);
                    Gizmos.DrawWireCube(center, cellSize);
                }
                else
                {
                    Gizmos.color = new Color(
                        playableCellGizmoColor.r,
                        playableCellGizmoColor.g,
                        playableCellGizmoColor.b,
                        Mathf.Min(playableCellGizmoColor.a, 0.18f));
                    Gizmos.DrawCube(center, cellSize);
                    Gizmos.color = playableCellGizmoColor;
                    Gizmos.DrawWireCube(center, cellSize);
                }
            }
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private bool IsConfiguredRemovedCell(int column, int row)
    {
        return removedCells != null &&
               removedCells.Contains(new Vector2Int(column, row));
    }
}

#if false
public class Board_OldVersion : MonoBehaviour
{
    BoardController boardController;
    public List<Box> boardBoxes = new List<Box>();
    public Box lastPlacedBox;
    public bool IsBoxRemoved;
    public static Board instance;
    [SerializeField] GameObject nodePref;
    [SerializeField] GameObject boxPref;
    public Node[,] grid;
    public Box[,] allBoxes;
    int height = 5;
    int width = 4;
    
    public int Height => height;
    public int Width => width;
    
    private Box currentBox;
    public bool isBoxFull;
    public int coins;
    bool isBoxInstantiated = false;
    bool isTransferInProgress = false;
    
    // NEW V2 SYSTEM: Universal Transfer System replaces 25+ scenario methods
    private UniversalSodaTransferSystem universalTransferSystem;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        Debug.Log("🚀 Board V2 System Initialized!");
    }
    
    void Start()
    {
        boardController = GetComponent<BoardController>();
        lastPlacedBox = null;
        grid = new Node[width, height];
        allBoxes = new Box[width, height];
        
        // Initialize the V2 Universal Transfer System
        universalTransferSystem = gameObject.AddComponent<UniversalSodaTransferSystem>();
        
        GenerateBoard();
        Debug.Log("✅ Board V2 with Universal Transfer System ready!");
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

                // NEW V2: Use Universal Transfer System instead of 25+ scenario methods
                CheckMatchesV2(i, j, currentBox);
                
                if (boardController != null)
                {
                    boardController.RemoveSpawnerList();
                }
            }
        }
    }
    
    // NEW V2: Simplified - replaces all complex transfer logic
    private void CheckMatchesV2(int column, int row, Box currentBox)
    {
        if (isTransferInProgress) return;

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
            RemoveEmptyBoxes();
            CheckBoardFill();
            return;
        }

        // Universal Transfer System handles ALL scenarios automatically
        universalTransferSystem.ProcessAllTransfers(currentBox, adjacentBoxes);
        StartCoroutine(ScheduleCleanup());
    }
    
    private IEnumerator ScheduleCleanup()
    {
        yield return new WaitForSeconds(2f);
        RemoveEmptyBoxes();
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
    
    public HashSet<(int, int)> GetAdjacentPositionsForLastBox()
    {
        HashSet<(int, int)> adjacentPositions = new HashSet<(int, int)>();
        lastPlacedBox = FindMostRecentBoxOnBoard();
        
        if (lastPlacedBox != null)
        {
            List<(int, int)> boxAdjacentPositions = GetAdjacentPositions(lastPlacedBox.column, lastPlacedBox.row);
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
            return hit.collider.GetComponent<Node>();
        }
        return null;
    }

    public bool IsPositionOccupied(int column, int row) => grid[column, row].isOccupied;
    public bool IsBoardEmpty() => boardBoxes.Count == 0;
    
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
        
        // Swap positions
        Vector3 tempPos = box1.transform.position;
        box1.transform.position = box2.transform.position;
        box2.transform.position = tempPos;
        
        // Swap grid data
        int tempCol = box1.column; int tempRow = box1.row;
        box1.column = box2.column; box1.row = box2.row;
        box2.column = tempCol; box2.row = tempRow;
        
        allBoxes[box1.column, box1.row] = box1;
        allBoxes[box2.column, box2.row] = box2;
    }
    
    public void RemoveEmptyBoxes()
    {
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                if (allBoxes[i, j] != null && allBoxes[i, j].Sodas.Count == 0)
                {
                    if (boardBoxes.Contains(allBoxes[i, j]))
                        boardBoxes.Remove(allBoxes[i, j]);
                    StartCoroutine(MakeInvisibleAfterDelay(allBoxes[i, j], 0.5f));
                    grid[i, j].isOccupied = false;
                }

                if (allBoxes[i, j] != null && allBoxes[i, j].BoxFilled())
                {
                    if (boardBoxes.Contains(allBoxes[i, j]))
                        boardBoxes.Remove(allBoxes[i, j]);

                    var sodasInParent = allBoxes[i, j].transform.GetComponentsInChildren<Soda>();
                    if (sodasInParent.Length >= 4)
                    {
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
        UIManager.instance?.UpdateUI();
    }
    
    private IEnumerator MakeInvisibleAfterDelay(Box box, float delay)
    {
        foreach (Transform child in box.transform)
        {
            MeshRenderer childRenderer = child.GetComponent<MeshRenderer>();
            if (childRenderer != null) childRenderer.enabled = false;
        }
        yield return new WaitForSeconds(delay);
        if (box != null) Destroy(box.gameObject, 1.5f);
    }
    
    private IEnumerator MoveToTruck(Transform boxTransform)
    {
        LiftTruck activeTruck = LiftTruckManager.instance.GetActiveTruck();
        if (activeTruck != null)
        {
            yield return new WaitForSeconds(0.3f);
            GameObject box = Instantiate(boxPref, boxTransform.position + Vector3.up * 0.53f, Quaternion.identity);
            
            if (activeTruck.IsEnoughRoomLeft())
            {
                Vector3 target = activeTruck.GetNextAvailablePosition();
                box.transform.DOMove(target, 0.37f).OnComplete(() => activeTruck.AddBox(box));
            }
            else
            {
                LiftTruck nextTruck = LiftTruckManager.instance.GetActiveTruck();
                nextTruck?.AddBox(box);
            }
        }
    }
    
    private void CheckBoardFill()
    {
        bool isFull = true;
        for (int i = 0; i < width && isFull; i++)
        {
            for (int j = 0; j < height && isFull; j++)
            {
                if (!grid[i, j].isOccupied) isFull = false;
            }
        }
        if (isFull) GameManager.instance?.CheckLoseCondition(true);
    }
    
    // Compatibility methods for other scripts
    public void HighLightHammerNode()
    {
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
                if (allBoxes[i, j] != null) grid[i, j].HighLightForHammer();
    }
    
    public void UnHighLightHammerNode()
    {
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
                grid[i, j].Unhighlight();
    }
    
    // Missing methods that other scripts depend on
    public (int column, int row) GetNodePositionFromWorld(Vector3 worldPosition)
    {
        // Convert world position to grid coordinates
        Vector3 offset = new Vector3(0.149f, 0.115f, 0.16f);
        Vector3 adjustedPos = worldPosition - offset;
        int column = Mathf.RoundToInt(adjustedPos.x / 0.279f);
        int row = Mathf.RoundToInt(adjustedPos.z / 0.279f);
        
        // Clamp to valid grid bounds
        column = Mathf.Clamp(column, 0, width - 1);
        row = Mathf.Clamp(row, 0, height - 1);
        
        return (column, row);
    }
    
    public HashSet<(int, int)> GetAllAdjacentPositionsForBoxes()
    {
        HashSet<(int, int)> adjacentPositions = new HashSet<(int, int)>();

        foreach (var box in boardBoxes)
        {
            List<(int, int)> boxAdjacentPositions = GetAdjacentPositions(box.column, box.row);
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
    
    public void ClearBoard()
    {
        // Clear all boxes from the board
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                if (allBoxes[i, j] != null)
                {
                    if (boardBoxes.Contains(allBoxes[i, j]))
                        boardBoxes.Remove(allBoxes[i, j]);
                    
                    Destroy(allBoxes[i, j].gameObject);
                    allBoxes[i, j] = null;
                    grid[i, j].isOccupied = false;
                }
            }
        }
        
        // Clear the board boxes list
        boardBoxes.Clear();
        currentBox = null;
        lastPlacedBox = null;
        
        Debug.Log("Board cleared!");
    }
}
#endif
