using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Capacity-independent box. Soda slots are explicit resources: a transfer
/// reserves one slot before changing either box, so two sodas can never claim
/// the same destination.
/// </summary>
[DisallowMultipleComponent]
public sealed class Box_version2 : MonoBehaviour
{
    [Header("Slots")]
    [Tooltip("Optional explicit slot list. If empty, children named SodaPosition<number> are discovered.")]
    [SerializeField] private List<Transform> sodaSlots = new List<Transform>();

    [Header("Dragging")]
    [SerializeField] private float dragHeight = 0.34f;
    [SerializeField] private bool disableLegacyBoxComponent = true;

    [Header("Transfer animation")]
    [SerializeField, Min(0.01f)] private float sodaMoveDuration = 0.28f;
    [SerializeField, Min(0f)] private float sodaMoveArcHeight = 0.5f;

    [Header("Removal animation")]
    [SerializeField, Min(0.01f)] private float removalDuration = 0.45f;
    [SerializeField, Min(0f)] private float packedRiseDistance = 0.45f;

    private readonly List<Soda> sodas = new List<Soda>();
    private readonly Dictionary<Soda, int> slotBySoda = new Dictionary<Soda, int>();
    private bool[] reservedSlots = Array.Empty<bool>();
    private Box legacyBox;
    private Collider inputCollider;
    private Vector3 dragStartPosition;
    private Plane dragPlane;
    private Vector3 dragOffset;
    private Node highlightedNode;
    private bool isDragging;
    private bool isRetired;
    private int stableId;
    private long placementOrder;

    public int Column { get; private set; } = -1;
    public int Row { get; private set; } = -1;
    public int Capacity => sodaSlots.Count;
    public int SodaCount => sodas.Count(soda => soda != null);
    public int FreeSlots => Capacity - SodaCount - ReservedSlotCount;
    public int DistinctColorCount => GetColorCounts().Count;
    public bool IsPlaced { get; private set; }
    public bool IsRetired => isRetired;
    public bool IsBusy { get; private set; }
    public bool IsEmpty => SodaCount == 0;
    public bool IsFull => Capacity > 0 && SodaCount == Capacity;
    public bool IsPacked => IsFull && DistinctColorCount == 1;
    public int StableId => stableId;
    public long PlacementOrder => placementOrder;
    public float SodaMoveDuration => sodaMoveDuration;
    public float RemovalDuration => removalDuration;
    public IReadOnlyList<Soda> Sodas => sodas;
    public IReadOnlyList<Transform> SodaSlots => sodaSlots;

    private int ReservedSlotCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < reservedSlots.Length; i++)
            {
                if (reservedSlots[i]) count++;
            }
            return count;
        }
    }

    private void Awake()
    {
        legacyBox = GetComponent<Box>();
        if (legacyBox != null && disableLegacyBoxComponent)
        {
            legacyBox.enabled = false;
        }

        inputCollider = GetComponent<Collider>();
        DiscoverSlots();
        RefreshContents();
        SyncLegacyBox();
    }

    private void Start()
    {
        // Sodas are commonly instantiated by a spawner immediately after the
        // box prefab, so scan once more on Start.
        RefreshContents();
    }

    public void DiscoverSlots()
    {
        sodaSlots.RemoveAll(slot => slot == null);
        if (sodaSlots.Count == 0)
        {
            sodaSlots = GetComponentsInChildren<Transform>(true)
                .Where(child => child != transform && IsSodaSlotName(child.name))
                .OrderBy(child => GetSlotNumber(child.name))
                .ThenBy(child => child.name, StringComparer.Ordinal)
                .ToList();
        }

        reservedSlots = new bool[sodaSlots.Count];
    }

    public void RefreshContents()
    {
        sodas.Clear();
        slotBySoda.Clear();
        if (reservedSlots.Length != sodaSlots.Count)
        {
            reservedSlots = new bool[sodaSlots.Count];
        }
        else
        {
            Array.Clear(reservedSlots, 0, reservedSlots.Length);
        }

        Soda[] found = GetComponentsInChildren<Soda>(true);
        var available = new HashSet<int>(Enumerable.Range(0, sodaSlots.Count));

        foreach (Soda soda in found.Where(item => item != null))
        {
            if (sodas.Count >= Capacity)
            {
                Debug.LogError(
                    $"{name} contains more sodas than its {Capacity} discovered slots.",
                    this);
                break;
            }

            int slot = FindNearestAvailableSlot(soda.transform.position, available);
            if (slot < 0) break;

            sodas.Add(soda);
            slotBySoda[soda] = slot;
            available.Remove(slot);
        }

        SyncLegacyBox();
    }

    public Dictionary<Soda.SodaColor, int> GetColorCounts()
    {
        return sodas
            .Where(soda => soda != null)
            .GroupBy(soda => soda.sodaColor)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    public int GetColorCount(Soda.SodaColor color)
    {
        int count = 0;
        foreach (Soda soda in sodas)
        {
            if (soda != null && soda.sodaColor == color) count++;
        }
        return count;
    }

    public Soda FindSoda(Soda.SodaColor color)
    {
        // A stable slot order keeps repeated runs visually deterministic.
        return sodas
            .Where(soda => soda != null && soda.sodaColor == color)
            .OrderByDescending(soda => slotBySoda.TryGetValue(soda, out int slot) ? slot : -1)
            .FirstOrDefault();
    }

    public Transform GetSlot(int index)
    {
        return index >= 0 && index < sodaSlots.Count ? sodaSlots[index] : null;
    }

    public bool TryReserveEmptySlot(out int slotIndex)
    {
        slotIndex = -1;
        if (isRetired || IsBusy || Capacity == 0) return false;

        bool[] occupied = new bool[Capacity];
        foreach (int index in slotBySoda.Values)
        {
            if (index >= 0 && index < occupied.Length) occupied[index] = true;
        }

        for (int i = 0; i < Capacity; i++)
        {
            if (!occupied[i] && !reservedSlots[i])
            {
                reservedSlots[i] = true;
                slotIndex = i;
                return true;
            }
        }

        return false;
    }

    public void ReleaseReservation(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < reservedSlots.Length)
        {
            reservedSlots[slotIndex] = false;
        }
    }

    public bool TryRemoveForTransfer(Soda soda, out int previousSlot)
    {
        previousSlot = -1;
        if (isRetired || IsBusy || soda == null || !sodas.Contains(soda))
        {
            return false;
        }

        slotBySoda.TryGetValue(soda, out previousSlot);
        sodas.Remove(soda);
        slotBySoda.Remove(soda);
        IsBusy = true;
        SyncLegacyBox();
        return true;
    }

    public bool TryAcceptReserved(Soda soda, int slotIndex)
    {
        if (isRetired || IsBusy || soda == null ||
            slotIndex < 0 || slotIndex >= Capacity ||
            !reservedSlots[slotIndex])
        {
            return false;
        }

        reservedSlots[slotIndex] = false;
        sodas.Add(soda);
        slotBySoda[soda] = slotIndex;
        IsBusy = true;
        SyncLegacyBox();
        return true;
    }

    public void RollbackRemovedSoda(Soda soda, int previousSlot)
    {
        if (soda != null && !sodas.Contains(soda))
        {
            sodas.Add(soda);
            slotBySoda[soda] = Mathf.Clamp(previousSlot, 0, Mathf.Max(0, Capacity - 1));
            soda.transform.SetParent(transform, true);
        }

        IsBusy = false;
        SyncLegacyBox();
    }

    public void FinishTransfer()
    {
        IsBusy = false;
        SyncLegacyBox();
    }

    public IEnumerator AnimateSodaToSlot(Soda soda, int slotIndex)
    {
        Transform slot = GetSlot(slotIndex);
        if (soda == null || slot == null) yield break;

        Transform sodaTransform = soda.transform;
        Vector3 start = sodaTransform.position;
        Vector3 end = slot.position;
        Vector3 control = (start + end) * 0.5f + Vector3.up * sodaMoveArcHeight;
        Collider sodaCollider = soda.GetComponent<Collider>();
        bool colliderWasEnabled = sodaCollider != null && sodaCollider.enabled;
        if (sodaCollider != null) sodaCollider.enabled = false;

        sodaTransform.SetParent(null, true);
        float elapsed = 0f;
        while (elapsed < sodaMoveDuration && soda != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / sodaMoveDuration);
            float oneMinusT = 1f - t;
            sodaTransform.position =
                oneMinusT * oneMinusT * start +
                2f * oneMinusT * t * control +
                t * t * end;
            yield return null;
        }

        if (soda != null)
        {
            sodaTransform.position = end;
            sodaTransform.SetParent(transform, true);
            if (sodaCollider != null) sodaCollider.enabled = colliderWasEnabled;
        }
    }

    internal void MarkPlaced(int column, int row, int id, long order)
    {
        Column = column;
        Row = row;
        stableId = id;
        placementOrder = order;
        IsPlaced = true;
        isDragging = false;

        if (legacyBox != null)
        {
            legacyBox.column = column;
            legacyBox.row = row;
            legacyBox.IsDragged = true;
            legacyBox.IsOnBoard = true;
        }
    }

    internal void MarkRetired()
    {
        if (isRetired) return;
        isRetired = true;
        IsPlaced = false;
        IsBusy = true;
        if (inputCollider != null) inputCollider.enabled = false;

        if (legacyBox != null)
        {
            legacyBox.IsOnBoard = false;
        }
    }

    internal IEnumerator PlayRemovalAnimation(bool packed)
    {
        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition +
                              (packed ? Vector3.up * packedRiseDistance : Vector3.zero);
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < removalDuration && this != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / removalDuration));
            transform.position = Vector3.Lerp(startPosition, endPosition, t);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        if (this != null)
        {
            Destroy(gameObject);
        }
    }

    private void OnMouseDown()
    {
        Board_version2 board = Board_version2.Instance;
        if (board == null || IsPlaced || isRetired || !board.CanInteract) return;

        dragStartPosition = transform.position;
        dragPlane = new Plane(Vector3.up, new Vector3(0f, dragHeight, 0f));
        if (!TryGetPointerOnPlane(dragPlane, out Vector3 pointer)) return;

        dragOffset = transform.position - pointer;
        isDragging = true;
    }

    private void OnMouseDrag()
    {
        if (!isDragging) return;
        Board_version2 board = Board_version2.Instance;
        if (board == null || !board.CanInteract)
        {
            CancelDrag();
            return;
        }

        if (TryGetPointerOnPlane(dragPlane, out Vector3 pointer))
        {
            transform.position = pointer + dragOffset;
        }

        Node node = board.GetNodeUnderPointer();
        if (node != highlightedNode)
        {
            ClearHighlight();
            if (node != null && board.CanPlaceAt(node.column, node.row))
            {
                highlightedNode = node;
                highlightedNode.Highlight();
            }
        }
    }

    private void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;

        Board_version2 board = Board_version2.Instance;
        Node node = board != null ? board.GetNodeUnderPointer() : null;
        ClearHighlight();

        if (board == null || node == null || !board.TryPlaceBox(this, node.column, node.row))
        {
            transform.position = dragStartPosition;
        }
    }

    private void CancelDrag()
    {
        isDragging = false;
        transform.position = dragStartPosition;
        ClearHighlight();
    }

    private void ClearHighlight()
    {
        if (highlightedNode != null)
        {
            highlightedNode.Unhighlight();
            highlightedNode = null;
        }
    }

    private void SyncLegacyBox()
    {
        if (legacyBox == null) return;

        // The disabled legacy component remains a compatibility facade for the
        // existing rail spawner, which reads IsDragged and Sodas.
        legacyBox.Sodas = sodas;
        legacyBox.IsDragged = IsPlaced;
        legacyBox.IsOnBoard = IsPlaced && !isRetired;
    }

    private int FindNearestAvailableSlot(Vector3 worldPosition, HashSet<int> available)
    {
        int bestIndex = -1;
        float bestDistance = float.MaxValue;

        foreach (int index in available)
        {
            float distance = (sodaSlots[index].position - worldPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static bool IsSodaSlotName(string value)
    {
        return value.StartsWith("SodaPosition", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetSlotNumber(string value)
    {
        string suffix = value.Substring("SodaPosition".Length);
        return int.TryParse(suffix, out int number) ? number : int.MaxValue;
    }

    private static bool TryGetPointerOnPlane(Plane plane, out Vector3 point)
    {
        point = default;
        Camera camera = Camera.main;
        if (camera == null) return false;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (!plane.Raycast(ray, out float distance)) return false;
        point = ray.GetPoint(distance);
        return true;
    }
}
