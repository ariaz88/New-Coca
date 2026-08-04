using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Version 2 board coordinator. It owns grid state and lifecycle only; transfer
/// policy lives in TransferAlgorithm_version2.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SodaTransferResolver_version2))]
public sealed class Board_version2 : MonoBehaviour
{
    [Serializable]
    public sealed class BoxEvent : UnityEvent<Box_version2> { }

    private static readonly Vector2Int[] DirectOffsets =
    {
        new Vector2Int(0, 1),  // up
        new Vector2Int(1, 0),  // right
        new Vector2Int(0, -1), // down
        new Vector2Int(-1, 0)  // left
    };

    [Header("Board")]
    [SerializeField, Min(1)] private int width = 4;
    [SerializeField, Min(1)] private int height = 5;
    [SerializeField] private Node nodePrefab;
    [SerializeField] private Vector3 localOrigin = new Vector3(0.149f, 0.185f, 0.16f);
    [SerializeField] private Vector2 cellSpacing = new Vector2(0.279f, 0.279f);
    [SerializeField] private float placedBoxYOffset = 0.155f;
    [SerializeField] private bool generateNodesOnStart = true;

    [Header("Completion")]
    [SerializeField] private bool awardLegacyGameRewards = true;
    [SerializeField, Min(0)] private int coinsPerPackedBox = 100;
    [SerializeField] private BoxEvent onBoxPacked = new BoxEvent();
    [SerializeField] private BoxEvent onBoxRemoved = new BoxEvent();
    [SerializeField] private UnityEvent onResolutionStarted = new UnityEvent();
    [SerializeField] private UnityEvent onResolutionFinished = new UnityEvent();

    private Node[,] nodes;
    private Box_version2[,] boxes;
    private SodaTransferResolver_version2 resolver;
    private readonly HashSet<Box_version2> retiredBoxes = new HashSet<Box_version2>();
    private long placementSequence;
    private int nextStableId = 1;

    public static Board_version2 Instance { get; private set; }
    public int Width => width;
    public int Height => height;
    public bool IsResolving { get; private set; }
    public bool CanInteract => !IsResolving;
    public bool IsEmpty => ActiveBoxes.Count == 0;
    public IReadOnlyList<Box_version2> ActiveBoxes
    {
        get
        {
            if (boxes == null) return Array.Empty<Box_version2>();
            return boxes.Cast<Box_version2>()
                        .Where(box => box != null && !box.IsRetired)
                        .ToList();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one Board_version2 may be active.", this);
            enabled = false;
            return;
        }

        Instance = this;
        resolver = GetComponent<SodaTransferResolver_version2>();
        nodes = new Node[width, height];
        boxes = new Box_version2[width, height];
    }

    private void Start()
    {
        if (generateNodesOnStart)
        {
            GenerateBoard();
        }
        else
        {
            DiscoverExistingNodes();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsInside(int column, int row)
    {
        return column >= 0 && column < width && row >= 0 && row < height;
    }

    public bool CanPlaceAt(int column, int row)
    {
        return CanInteract &&
               IsInside(column, row) &&
               nodes[column, row] != null &&
               boxes[column, row] == null &&
               !nodes[column, row].isOccupied;
    }

    public bool TryPlaceBox(Box_version2 box, int column, int row)
    {
        if (box == null || box.IsPlaced || box.IsRetired ||
            box.Capacity <= 0 || !CanPlaceAt(column, row))
        {
            return false;
        }

        box.RefreshContents();
        if (box.SodaCount > box.Capacity)
        {
            Debug.LogError($"{box.name} has more sodas than slots.", box);
            return false;
        }

        boxes[column, row] = box;
        nodes[column, row].isOccupied = true;
        box.transform.position = GetPlacementWorldPosition(column, row);
        box.MarkPlaced(column, row, nextStableId++, ++placementSequence);

        if (SpawnContoller.instance != null)
        {
            SpawnContoller.instance.spawnedBoxes.Remove(box.gameObject);
        }

        StartCoroutine(ResolvePlacement(box));
        return true;
    }

    public Node GetNodeUnderPointer()
    {
        Camera camera = Camera.main;
        if (camera == null) return null;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            Node node = hit.collider.GetComponentInParent<Node>();
            if (node != null) return node;
        }

        return null;
    }

    public Vector3 GetPlacementWorldPosition(int column, int row)
    {
        if (!IsInside(column, row) || nodes[column, row] == null)
        {
            return transform.TransformPoint(
                localOrigin + new Vector3(
                    column * cellSpacing.x,
                    placedBoxYOffset,
                    row * cellSpacing.y));
        }

        return nodes[column, row].transform.position + Vector3.up * placedBoxYOffset;
    }

    public Box_version2 GetBox(int column, int row)
    {
        return IsInside(column, row) ? boxes[column, row] : null;
    }

    public IEnumerable<Box_version2> GetDirectNeighbours(Box_version2 box)
    {
        if (box == null || box.IsRetired) yield break;

        foreach (Vector2Int offset in DirectOffsets)
        {
            int column = box.Column + offset.x;
            int row = box.Row + offset.y;
            if (!IsInside(column, row)) continue;

            Box_version2 neighbour = boxes[column, row];
            if (neighbour != null && !neighbour.IsRetired)
            {
                yield return neighbour;
            }
        }
    }

    public bool AreDirectlyAdjacent(Box_version2 first, Box_version2 second)
    {
        if (first == null || second == null) return false;
        int columnDistance = Mathf.Abs(first.Column - second.Column);
        int rowDistance = Mathf.Abs(first.Row - second.Row);
        return columnDistance + rowDistance == 1;
    }

    public List<Box_version2> GetConnectedComponent(Box_version2 origin)
    {
        var result = new List<Box_version2>();
        if (origin == null || origin.IsRetired) return result;

        var visited = new HashSet<Box_version2>();
        var queue = new Queue<Box_version2>();
        visited.Add(origin);
        queue.Enqueue(origin);

        while (queue.Count > 0)
        {
            Box_version2 current = queue.Dequeue();
            result.Add(current);

            foreach (Box_version2 neighbour in GetDirectNeighbours(current))
            {
                if (visited.Add(neighbour))
                {
                    queue.Enqueue(neighbour);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Removes completed/empty boxes from the logical grid immediately. Visual
    /// removal happens afterwards while input remains locked.
    /// </summary>
    internal float RetireTerminalBoxes(ICollection<Box_version2> component)
    {
        if (component == null || component.Count == 0) return 0f;

        float maxDelay = 0f;
        foreach (Box_version2 box in component.ToList())
        {
            if (box == null || box.IsRetired) continue;
            if (!box.IsPacked && !box.IsEmpty) continue;

            bool packed = box.IsPacked;
            RetireBox(box, packed);
            maxDelay = Mathf.Max(maxDelay, box.RemovalDuration);
        }

        return maxDelay;
    }

    public void ClearBoard()
    {
        if (boxes == null) return;

        for (int column = 0; column < width; column++)
        {
            for (int row = 0; row < height; row++)
            {
                Box_version2 box = boxes[column, row];
                boxes[column, row] = null;
                if (nodes[column, row] != null)
                {
                    nodes[column, row].isOccupied = false;
                }

                if (box != null)
                {
                    Destroy(box.gameObject);
                }
            }
        }

        retiredBoxes.Clear();
        IsResolving = false;
    }

    private IEnumerator ResolvePlacement(Box_version2 trigger)
    {
        IsResolving = true;
        onResolutionStarted.Invoke();

        yield return resolver.Resolve(this, trigger);

        IsResolving = false;
        onResolutionFinished.Invoke();
        CheckLoseCondition();
    }

    private void RetireBox(Box_version2 box, bool packed)
    {
        if (box == null || !retiredBoxes.Add(box)) return;

        int column = box.Column;
        int row = box.Row;
        if (IsInside(column, row) && boxes[column, row] == box)
        {
            boxes[column, row] = null;
            if (nodes[column, row] != null)
            {
                nodes[column, row].isOccupied = false;
            }
        }

        box.MarkRetired();
        if (packed)
        {
            AwardPackedBox(box);
            onBoxPacked.Invoke(box);
        }
        else
        {
            onBoxRemoved.Invoke(box);
        }

        StartCoroutine(box.PlayRemovalAnimation(packed));
    }

    private void AwardPackedBox(Box_version2 box)
    {
        if (!awardLegacyGameRewards) return;

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

    private void CheckLoseCondition()
    {
        if (boxes == null) return;

        for (int column = 0; column < width; column++)
        {
            for (int row = 0; row < height; row++)
            {
                if (boxes[column, row] == null) return;
            }
        }

        if (GameManager.instance != null)
        {
            GameManager.instance.CheckLoseCondition(true);
        }
    }

    private void GenerateBoard()
    {
        if (nodePrefab == null)
        {
            Debug.LogError("Board_version2 requires a node prefab.", this);
            return;
        }

        for (int column = 0; column < width; column++)
        {
            for (int row = 0; row < height; row++)
            {
                Vector3 localPosition = localOrigin +
                                        new Vector3(
                                            column * cellSpacing.x,
                                            0f,
                                            row * cellSpacing.y);
                Node node = Instantiate(
                    nodePrefab,
                    transform.TransformPoint(localPosition),
                    transform.rotation,
                    transform);
                node.column = column;
                node.row = row;
                node.isOccupied = false;
                nodes[column, row] = node;
            }
        }
    }

    private void DiscoverExistingNodes()
    {
        foreach (Node node in GetComponentsInChildren<Node>(true))
        {
            if (!IsInside(node.column, node.row))
            {
                Debug.LogWarning(
                    $"Ignoring out-of-range node ({node.column}, {node.row}).",
                    node);
                continue;
            }

            if (nodes[node.column, node.row] != null)
            {
                Debug.LogError(
                    $"Duplicate node at ({node.column}, {node.row}).",
                    node);
                continue;
            }

            nodes[node.column, node.row] = node;
            node.isOccupied = false;
        }
    }
}
