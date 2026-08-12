﻿using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using DG.Tweening;

/// <summary>
/// Active box implementation.
/// Capacity is discovered from SodaPosition children, so the same class works
/// for 4, 5, 6, or any other slot count without changing transfer cases.
/// </summary>
[DisallowMultipleComponent]
public class Box : MonoBehaviour
{
    [Header("Box")]
    [SerializeField] private GameObject topBox;

    [Header("Slots")]
    [Tooltip("Optional explicit slot list. If empty, children named SodaPosition<number> are discovered.")]
    [SerializeField] private List<Transform> sodaSlots = new List<Transform>();

    [Header("Dragging")]
    [SerializeField] private float dragHeight = 0.34f;

    [Header("Transfer animation")]
    [SerializeField, Min(0.01f)] private float sodaMoveDuration = 0.28f;
    [SerializeField, Min(0f)] private float sodaMoveArcHeight = 0.5f;

    [Header("Removal animation")]
    [SerializeField, Min(0.01f)] private float removalDuration = 0.45f;
    [SerializeField, Min(0f)] private float packedRiseDistance = 0.45f;

    private readonly Dictionary<Soda, int> slotBySoda = new Dictionary<Soda, int>();
    private readonly List<Transform> emptyPositions = new List<Transform>();
    private bool[] reservedSlots = new bool[0];
    private Camera mainCamera;
    private Collider inputCollider;
    private Vector3 startPos;
    private Plane dragPlane;
    private Vector3 dragOffset;
    private Node currentHighlightedNode;
    private bool isDragging;
    private bool isRetired;
    private bool doorCreated;
    private int stableId;
    private long placementOrder;
    private object dragConstraintOwner;
    private Func<Box, bool> dragConstraint;
    private const string DragOverlayShaderResourcePath = "DraggedBoxOverlay";
    private readonly List<DragRendererState> dragRendererStates =
        new List<DragRendererState>();
    private readonly List<Material> dragOverlayMaterials = new List<Material>();
    private Shader dragOverlayShader;

    private sealed class DragRendererState
    {
        public Renderer Renderer;
        public int SortingOrder;
        public Material[] SharedMaterials;
    }

    [Header("Board")]
    public int column = -1;
    public int row = -1;
    public bool IsDragged;

    [Header("Soda")]
    public List<Soda> Sodas = new List<Soda>();
    public bool IsRecursive;
    public bool IsInstantiated;
    public GameObject sodaPrefab;

    public float PlacementTimestamp { get; private set; }
    public bool IsOnBoard { get; set; }
    public bool IsBoxReleased;
    public Material OriginalMaterial { get; private set; }
    public Material highLightMaterialForHammer;
    public Material highLightMaterialForSwap;

    public int Capacity => sodaSlots.Count;
    public int DiscoverableCapacity
    {
        get
        {
            int explicitSlotCount = sodaSlots != null
                ? sodaSlots.Count(slot => slot != null)
                : 0;
            if (explicitSlotCount > 0)
            {
                return explicitSlotCount;
            }

            return GetComponentsInChildren<Transform>(true)
                .Count(child => child != transform && IsSodaSlotName(child.name));
        }
    }
    public int SodaCount => Sodas.Count(soda => soda != null);
    public int FreeSlots => Capacity - SodaCount - ReservedSlotCount;
    public int DistinctColorCount => GetSodaColorCounts().Count;
    public bool IsEmpty => SodaCount == 0;
    public bool IsFull => Capacity > 0 && SodaCount == Capacity;
    public bool IsPacked => BoxFilled();
    public bool IsRetired => isRetired;
    public bool IsBusy { get; private set; }
    public int StableId => stableId;
    public long PlacementOrder => placementOrder;
    public float SodaMoveDuration => sodaMoveDuration;
    public float RemovalDuration => removalDuration;

    /// <summary>Raised after this rail box has entered a valid drag operation.</summary>
    public event Action<Box> DragStarted;

    /// <summary>Raised when a started drag is cancelled or rejected by the Board.</summary>
    public event Action<Box> DropRejectedOrCancelled;

    /// <summary>
    /// Installs one optional owner-scoped rule that is checked before this Box
    /// enters a drag. Normal gameplay has no rule; Tutorials can temporarily
    /// restrict which rail Box is actionable.
    /// </summary>
    public bool TrySetDragConstraint(object owner, Func<Box, bool> constraint)
    {
        if (owner == null || constraint == null)
        {
            return false;
        }

        if (dragConstraintOwner != null && !ReferenceEquals(dragConstraintOwner, owner))
        {
            return false;
        }

        dragConstraintOwner = owner;
        dragConstraint = constraint;
        return true;
    }

    /// <summary>Clears the optional drag rule only when called by its owner.</summary>
    public void ClearDragConstraint(object owner)
    {
        if (owner == null || !ReferenceEquals(dragConstraintOwner, owner))
        {
            return;
        }

        dragConstraintOwner = null;
        dragConstraint = null;
    }

    private int ReservedSlotCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < reservedSlots.Length; i++)
            {
                if (reservedSlots[i])
                {
                    count++;
                }
            }

            return count;
        }
    }

    private void Awake()
    {
        mainCamera = Camera.main;
        inputCollider = GetComponent<Collider>();
        if (Sodas == null)
        {
            Sodas = new List<Soda>();
        }

        MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null)
        {
            OriginalMaterial = meshRenderer.material;
        }

        DiscoverSlots();
        RefreshContents();
        IsOnBoard = false;
    }

    private void Start()
    {
        mainCamera = Camera.main;
        RefreshContents();
        UpdateEmptyPositions();
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
        UpdateEmptyPositions();
    }

    public void RefreshContents()
    {
        if (Sodas == null)
        {
            Sodas = new List<Soda>();
        }

        Sodas.Clear();
        slotBySoda.Clear();

        if (reservedSlots == null || reservedSlots.Length != sodaSlots.Count)
        {
            reservedSlots = new bool[sodaSlots.Count];
        }
        else
        {
            Array.Clear(reservedSlots, 0, reservedSlots.Length);
        }

        HashSet<int> availableSlots = new HashSet<int>(Enumerable.Range(0, sodaSlots.Count));
        Soda[] found = GetComponentsInChildren<Soda>(true);

        foreach (Soda soda in found.Where(item => item != null))
        {
            if (Sodas.Count >= Capacity)
            {
                Debug.LogError($"{name} contains more sodas than its {Capacity} discovered slots.", this);
                break;
            }

            int slot = FindNearestAvailableSlot(soda.transform.position, availableSlots);
            if (slot < 0)
            {
                break;
            }

            Sodas.Add(soda);
            slotBySoda[soda] = slot;
            availableSlots.Remove(slot);
        }

        UpdateEmptyPositions();
    }

    public List<Soda> GetSodaList()
    {
        RefreshContents();
        return Sodas;
    }

    public Dictionary<Soda.SodaColor, int> GetSodaColorCounts()
    {
        return Sodas
            .Where(soda => soda != null)
            .GroupBy(soda => soda.sodaColor)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    public int GetSodasCount()
    {
        return SodaCount;
    }

    public int GetColorCount(Soda.SodaColor color)
    {
        int count = 0;
        foreach (Soda soda in Sodas)
        {
            if (soda != null && soda.sodaColor == color)
            {
                count++;
            }
        }

        return count;
    }

    public int GetAvailableSpaces()
    {
        return Mathf.Max(0, FreeSlots);
    }

    public bool HasCapacity()
    {
        return GetAvailableSpaces() > 0;
    }

    public bool HasSodaOfColor(Soda.SodaColor color)
    {
        return GetColorCount(color) > 0;
    }

    public bool HasSingleColorSoda()
    {
        return GetSodaColorCounts().Count == 1;
    }

    public bool CanParticipateInTransfer()
    {
        return IsOnBoard && !IsRetired && !IsBusy && Capacity > 0;
    }

    public IEnumerable<Soda.SodaColor> GetDistinctColors()
    {
        return GetSodaColorCounts().Keys;
    }

    public bool HasSameColorSoda(Box other)
    {
        if (other == null)
        {
            return false;
        }

        HashSet<Soda.SodaColor> colors = new HashSet<Soda.SodaColor>(GetDistinctColors());
        return other.GetDistinctColors().Any(color => colors.Contains(color));
    }

    public bool HasColorSlotAvailable(Soda.SodaColor color)
    {
        return HasCapacity() && HasSodaOfColor(color);
    }

    public Soda FindSoda(Soda.SodaColor color)
    {
        return Sodas
            .Where(soda => soda != null && soda.sodaColor == color)
            .OrderByDescending(soda => slotBySoda.TryGetValue(soda, out int slot) ? slot : -1)
            .FirstOrDefault();
    }

    public Transform GetSlot(int index)
    {
        return index >= 0 && index < sodaSlots.Count ? sodaSlots[index] : null;
    }

    public Transform[] GetSodaPositions()
    {
        DiscoverSlots();
        return sodaSlots.ToArray();
    }

    public List<Transform> GetEmptySodaPositions()
    {
        UpdateEmptyPositions();
        return emptyPositions;
    }

    public List<Transform> GetEmptySodaPositions1()
    {
        return GetEmptySodaPositions();
    }

    public List<Transform> GetEmptySodaPositions2()
    {
        return GetEmptySodaPositions();
    }

    public List<Soda> GetReversedSodas()
    {
        return Sodas.Where(soda => soda != null).Reverse().ToList();
    }

    public void UpdateEmptyPositions()
    {
        emptyPositions.Clear();
        if (Capacity == 0)
        {
            return;
        }

        bool[] occupied = BuildOccupiedSlots();
        for (int i = 0; i < Capacity; i++)
        {
            bool reserved = reservedSlots != null && i < reservedSlots.Length && reservedSlots[i];
            if (!occupied[i] && !reserved && sodaSlots[i] != null)
            {
                emptyPositions.Add(sodaSlots[i]);
            }
        }
    }

    public void RearrangeSodas()
    {
        Sodas.RemoveAll(soda => soda == null);
        for (int i = 0; i < Sodas.Count && i < sodaSlots.Count; i++)
        {
            Soda soda = Sodas[i];
            if (soda == null || sodaSlots[i] == null)
            {
                continue;
            }

            slotBySoda[soda] = i;
            soda.transform.SetParent(transform, true);
            soda.transform.position = sodaSlots[i].position;
        }

        UpdateEmptyPositions();
    }

    public void AddSoda(Soda soda)
    {
        if (soda == null || Sodas.Contains(soda))
        {
            return;
        }

        if (!TryFindOpenSlot(out int slotIndex))
        {
            Debug.LogWarning($"{name} has no free slot for {soda.name}.", this);
            return;
        }

        Sodas.Add(soda);
        slotBySoda[soda] = slotIndex;
        soda.transform.SetParent(transform, true);
        Transform slot = GetSlot(slotIndex);
        if (slot != null)
        {
            soda.transform.position = slot.position;
        }

        UpdateEmptyPositions();
    }

    public void AddSoda1(Soda sodaToAdd, Box targetBox)
    {
        if (targetBox != null)
        {
            targetBox.AddSoda(sodaToAdd);
        }
    }

    public void RemoveSoda(Soda soda)
    {
        if (soda == null)
        {
            return;
        }

        Sodas.Remove(soda);
        slotBySoda.Remove(soda);
        UpdateEmptyPositions();
    }

    public bool BoxFilled()
    {
        return Capacity > 0 && SodaCount == Capacity && DistinctColorCount == 1;
    }

    public void DestroyBox()
    {
        if (Board.instance != null)
        {
            Board.instance.NotifyBoxRemoved(this, true);
        }
        else
        {
            Destroy(gameObject, 0.3f);
        }
    }

    public void CloseBox(Transform tempPos)
    {
        if (!doorCreated)
        {
            doorCreated = true;
            StartCoroutine(PlayRemovalAnimation(true));
        }
    }

    public void TransferSodasTo4(Box targetBox, int count)
    {
        StartCoroutine(TransferSodasToRoutine(targetBox, count));
    }

    public void TransferSodasTo1(Box targetBox, int count)
    {
        StartCoroutine(TransferSodasToRoutine(targetBox, count));
    }

    public void TransferSodasTo3(Box targetBox, int count)
    {
        StartCoroutine(TransferSodasToRoutine(targetBox, count));
    }

    public bool TryReserveEmptySlot(out int slotIndex)
    {
        slotIndex = -1;
        if (isRetired || IsBusy || Capacity == 0)
        {
            return false;
        }

        if (!TryFindOpenSlot(out slotIndex))
        {
            return false;
        }

        reservedSlots[slotIndex] = true;
        UpdateEmptyPositions();
        return true;
    }

    public void ReleaseReservation(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < reservedSlots.Length)
        {
            reservedSlots[slotIndex] = false;
            UpdateEmptyPositions();
        }
    }

    public bool TryRemoveForTransfer(Soda soda, out int previousSlot)
    {
        previousSlot = -1;
        if (isRetired || IsBusy || soda == null || !Sodas.Contains(soda))
        {
            return false;
        }

        slotBySoda.TryGetValue(soda, out previousSlot);
        Sodas.Remove(soda);
        slotBySoda.Remove(soda);
        IsBusy = true;
        UpdateEmptyPositions();
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
        Sodas.Add(soda);
        slotBySoda[soda] = slotIndex;
        IsBusy = true;
        UpdateEmptyPositions();
        return true;
    }

    public void RollbackRemovedSoda(Soda soda, int previousSlot)
    {
        if (soda != null && !Sodas.Contains(soda))
        {
            Sodas.Add(soda);
            int slot = Capacity > 0 ? Mathf.Clamp(previousSlot, 0, Capacity - 1) : 0;
            slotBySoda[soda] = slot;
            soda.transform.SetParent(transform, true);
        }

        IsBusy = false;
        UpdateEmptyPositions();
    }

    public void FinishTransfer()
    {
        IsBusy = false;
        UpdateEmptyPositions();
    }

    public IEnumerator AnimateSodaToSlot(Soda soda, int slotIndex)
    {
        Transform slot = GetSlot(slotIndex);
        if (soda == null || slot == null)
        {
            yield break;
        }

        Transform sodaTransform = soda.transform;
        Vector3 start = sodaTransform.position;
        Vector3 end = slot.position;
        Vector3 control = (start + end) * 0.5f + Vector3.up * sodaMoveArcHeight;
        Collider sodaCollider = soda.GetComponent<Collider>();
        bool colliderWasEnabled = sodaCollider != null && sodaCollider.enabled;
        if (sodaCollider != null)
        {
            sodaCollider.enabled = false;
        }

        sodaTransform.SetParent(null, true);
        if (SoundManager.instance != null)
        {
            SoundManager.instance.PlaySodaMove();
        }

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
            if (sodaCollider != null)
            {
                sodaCollider.enabled = colliderWasEnabled;
            }
        }
    }

    internal void MarkPlaced(int boardColumn, int boardRow, int id, long order)
    {
        SetBoardCoordinates(boardColumn, boardRow);
        stableId = id;
        placementOrder = order;
        PlacementTimestamp = Time.time;
        IsDragged = true;
        IsOnBoard = true;
        IsBoxReleased = true;
        isDragging = false;
    }

    internal void SetBoardCoordinates(int boardColumn, int boardRow)
    {
        column = boardColumn;
        row = boardRow;
    }

    internal void MarkRetired()
    {
        if (isRetired)
        {
            return;
        }

        isRetired = true;
        IsBusy = true;
        IsOnBoard = false;
        IsDragged = false;

        Collider boxCollider = GetComponent<Collider>();
        if (boxCollider != null)
        {
            boxCollider.enabled = false;
        }
    }

    internal IEnumerator PlayRemovalAnimation(bool packed)
    {
        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + (packed ? Vector3.up * packedRiseDistance : Vector3.zero);
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

    private IEnumerator TransferSodasToRoutine(Box targetBox, int count)
    {
        if (targetBox == null || count <= 0)
        {
            yield break;
        }

        int moved = 0;
        while (moved < count && Sodas.Count > 0 && targetBox.HasCapacity())
        {
            Soda soda = Sodas[Sodas.Count - 1];
            RemoveSoda(soda);
            targetBox.AddSoda(soda);
            Transform slot = targetBox.slotBySoda.TryGetValue(soda, out int slotIndex)
                ? targetBox.GetSlot(slotIndex)
                : null;

            if (slot != null)
            {
                yield return targetBox.AnimateSodaToSlot(soda, slotIndex);
            }

            moved++;
        }
    }

    private void OnMouseDown()
    {
        if (IsOnBoard)
        {
            if (HammerManager.instance != null &&
                HammerManager.instance.IsHammerButtonPressed &&
                HammerManager.instance.IsHammerActive())
            {
                HammerManager.instance.OnBoxClicked(this);
                return;
            }

            if (SwapController.instance != null &&
                SwapController.instance.IsSwapButtonPressed &&
                SwapController.instance.IsSwapActive())
            {
                SwapController.instance.OnBoxClicked(this);
                return;
            }
        }

        if (IsOnBoard || IsDragged || isRetired)
        {
            return;
        }

        // Check before capturing the pointer or changing isDragging so a blocked
        // Tutorial Box remains completely stationary.
        if (dragConstraint != null && !dragConstraint(this))
        {
            return;
        }

        if (GameManager.instance != null && (GameManager.instance.gameOver || GameManager.instance.gameEnded))
        {
            return;
        }

        Board board = Board.instance;
        if (board == null || !board.CanInteract)
        {
            return;
        }

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        startPos = transform.position;
        dragPlane = new Plane(Vector3.up, new Vector3(0f, dragHeight, 0f));
        if (!TryGetPointerOnPlane(dragPlane, out Vector3 pointer))
        {
            return;
        }

        dragOffset = transform.position - pointer;
        isDragging = true;
        EnableDraggedDisplayPriority();
        DragStarted?.Invoke(this);
    }

    private void OnMouseDrag()
    {
        if (!isDragging)
        {
            return;
        }

        Board board = Board.instance;
        if (board == null || !board.CanInteract)
        {
            CancelDrag();
            return;
        }

        if (TryGetPointerOnPlane(dragPlane, out Vector3 pointer))
        {
            transform.position = pointer + dragOffset;
        }

        Node node = board.GetDropTargetNode(GetPlacementReferencePosition());
        if (board.HasPlacementConstraint)
        {
            // A feature Tutorial owns the persistent set of visible valid cells.
            // Keep only the hover reference; do not replace its shared highlights.
            currentHighlightedNode = node;
            return;
        }

        if (node != currentHighlightedNode)
        {
            ClearCurrentHighlight();
            if (node != null && board.CanPlaceAt(node.column, node.row))
            {
                currentHighlightedNode = node;
                currentHighlightedNode.Highlight();
            }
        }
    }

    private void OnMouseUp()
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        RestoreNormalDisplayPriority();
        Board board = Board.instance;
        Node node = board != null
            ? board.GetDropTargetNode(GetPlacementReferencePosition())
            : null;
        bool preservePersistentHighlights = board != null && board.HasPlacementConstraint;
        if (preservePersistentHighlights)
        {
            currentHighlightedNode = null;
        }
        else
        {
            ClearCurrentHighlight();
            ClearAllHighlights();
        }

        if (board == null || node == null || !board.TryPlaceBox(this, node.column, node.row))
        {
            transform.position = startPos;
            DropRejectedOrCancelled?.Invoke(this);
        }
    }

    private Vector3 GetPlacementReferencePosition()
    {
        return inputCollider != null ? inputCollider.bounds.center : transform.position;
    }

    private void CancelDrag()
    {
        bool wasDragging = isDragging;
        isDragging = false;
        RestoreNormalDisplayPriority();
        transform.position = startPos;
        if (Board.instance != null && Board.instance.HasPlacementConstraint)
        {
            currentHighlightedNode = null;
        }
        else
        {
            ClearCurrentHighlight();
        }

        if (wasDragging)
        {
            DropRejectedOrCancelled?.Invoke(this);
        }
    }

    /// <summary>
    /// Gives this Box and its Soda renderers visual priority without changing
    /// the transform used by dragging or placement. ToyGloss moves only their
    /// rendered depth close to the camera; the original materials and sorting
    /// orders are restored as soon as dragging ends.
    /// </summary>
    private void EnableDraggedDisplayPriority()
    {
        RestoreNormalDisplayPriority();

        if (dragOverlayShader == null)
        {
            dragOverlayShader = Resources.Load<Shader>(DragOverlayShaderResourcePath);
        }

        foreach (Renderer childRenderer in GetComponentsInChildren<Renderer>(true))
        {
            if (childRenderer == null)
            {
                continue;
            }

            Material[] originalMaterials = childRenderer.sharedMaterials;
            dragRendererStates.Add(new DragRendererState
            {
                Renderer = childRenderer,
                SortingOrder = childRenderer.sortingOrder,
                SharedMaterials = originalMaterials
            });

            if (dragOverlayShader != null)
            {
                Material[] overlayMaterials = new Material[originalMaterials.Length];
                for (int i = 0; i < originalMaterials.Length; i++)
                {
                    Material originalMaterial = originalMaterials[i];
                    if (originalMaterial == null)
                    {
                        continue;
                    }

                    Material overlayMaterial = new Material(dragOverlayShader)
                    {
                        name = originalMaterial.name + " (Drag Overlay)",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    int overlayRenderQueue = overlayMaterial.renderQueue;
                    overlayMaterial.CopyPropertiesFromMaterial(originalMaterial);
                    overlayMaterial.renderQueue = overlayRenderQueue;
                    overlayMaterials[i] = overlayMaterial;
                    dragOverlayMaterials.Add(overlayMaterial);
                }

                childRenderer.sharedMaterials = overlayMaterials;
            }

            childRenderer.sortingOrder = short.MaxValue;
        }
    }

    private void RestoreNormalDisplayPriority()
    {
        foreach (DragRendererState state in dragRendererStates)
        {
            if (state?.Renderer == null)
            {
                continue;
            }

            state.Renderer.sharedMaterials = state.SharedMaterials;
            state.Renderer.sortingOrder = state.SortingOrder;
        }

        dragRendererStates.Clear();

        foreach (Material overlayMaterial in dragOverlayMaterials)
        {
            if (overlayMaterial != null)
            {
                Destroy(overlayMaterial);
            }
        }

        dragOverlayMaterials.Clear();
    }

    private void OnDisable()
    {
        RestoreNormalDisplayPriority();
    }

    private void ClearCurrentHighlight()
    {
        if (currentHighlightedNode != null)
        {
            currentHighlightedNode.Unhighlight();
            currentHighlightedNode = null;
        }
    }

    private void ClearAllHighlights()
    {
        if (Board.instance == null || Board.instance.grid == null)
        {
            return;
        }

        foreach (Node node in Board.instance.grid)
        {
            if (node != null)
            {
                node.Unhighlight();
            }
        }
    }

    private bool TryFindOpenSlot(out int slotIndex)
    {
        slotIndex = -1;
        if (Capacity == 0)
        {
            return false;
        }

        bool[] occupied = BuildOccupiedSlots();
        for (int i = 0; i < Capacity; i++)
        {
            bool reserved = reservedSlots != null && i < reservedSlots.Length && reservedSlots[i];
            if (!occupied[i] && !reserved)
            {
                slotIndex = i;
                return true;
            }
        }

        return false;
    }

    private bool[] BuildOccupiedSlots()
    {
        bool[] occupied = new bool[Capacity];
        foreach (int index in slotBySoda.Values)
        {
            if (index >= 0 && index < occupied.Length)
            {
                occupied[index] = true;
            }
        }

        return occupied;
    }

    private int FindNearestAvailableSlot(Vector3 worldPosition, HashSet<int> available)
    {
        int bestIndex = -1;
        float bestDistance = float.MaxValue;

        foreach (int index in available)
        {
            if (index < 0 || index >= sodaSlots.Count || sodaSlots[index] == null)
            {
                continue;
            }

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
        Camera camera = GetPointerCamera();
        if (camera == null)
        {
            return false;
        }

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (!plane.Raycast(ray, out float distance))
        {
            return false;
        }

        point = ray.GetPoint(distance);
        return true;
    }

    /// <summary>
    /// Uses the topmost camera viewport under the pointer. In a normal scene this
    /// resolves to Camera.main; in a split-view scene it lets rail boxes use the
    /// dedicated lower camera and board boxes use the upper camera.
    /// </summary>
    private static Camera GetPointerCamera()
    {
        Vector3 pointer = Input.mousePosition;
        Camera selected = null;
        float selectedDepth = float.NegativeInfinity;

        foreach (Camera camera in Camera.allCameras)
        {
            if (camera == null || !camera.isActiveAndEnabled ||
                !camera.pixelRect.Contains(new Vector2(pointer.x, pointer.y)))
            {
                continue;
            }

            if (selected == null || camera.depth > selectedDepth)
            {
                selected = camera;
                selectedDepth = camera.depth;
            }
        }

        return selected != null ? selected : Camera.main;
    }
}

#if false
public class Box_OldVersion : MonoBehaviour
{
    [Header("Box")]
    [SerializeField] private GameObject topBox;
    List<Transform> emptyPositions = new List<Transform>();
    public float PlacementTimestamp { get; private set; }
    private Camera mainCamera;
    private Vector3 startPos;
    private Vector3 offset;
    private Node currentHighlightedNode;
    Node lastHighlightedNode;
    private bool isDragging = false;
    private bool doorCreated = false; // Flag to track if the door has already been created

    [Header("Board")]
    public int column;
    public int row;
    public bool IsDragged;

    [Header("Soda")]
    public List<Soda> Sodas;
    public bool IsRecursive;
    public bool IsInstantiated;
    public GameObject sodaPrefab;
    private int maxCapacity = 4;

    public bool IsOnBoard { get; set; } // Flag to indicate if the box is on the board
    public bool IsBoxReleased;
    
    // Anti ping-pong system
    private float lastTransferTime = 0f;
    private const float TRANSFER_COOLDOWN = 1f; // 1 second cooldown between transfers

    public Material OriginalMaterial { get; private set; }
    public Material highLightMaterialForHammer;
    public Material highLightMaterialForSwap;
    void Start()
    {
        mainCamera = Camera.main;
        Sodas = new List<Soda>();
        GetSodaList();
        MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null)
        {
            OriginalMaterial = meshRenderer.material;
        }
        IsOnBoard = false;
        UpdateEmptyPositions();

    }

    public Dictionary<Soda.SodaColor, int> GetSodaColorCounts()
        {
            return Sodas.GroupBy(soda => soda.sodaColor)
                        .ToDictionary(group => group.Key, group => group.Count());
        }

    public void DestroyBox()
    {
        Board.instance.grid[column, row].isOccupied = false;
        Destroy(gameObject , 0.3f);
    }
    public bool HasSingleColorSoda()
    {
        return GetSodaColorCounts().Count == 1;
    }
    public List<Soda> GetSodaList()
    {
        foreach (Transform child in this.transform)
        {
            Soda sodaChild = child.gameObject.GetComponent<Soda>();
            if (sodaChild != null && !Sodas.Contains(sodaChild))
            {
                Sodas.Add(sodaChild);
            }
        }
        return Sodas;
    }
    public int GetSodasCount()
    {
        return Sodas.Count;
    }
    public int GetColorCount(Soda.SodaColor color)
    {
        return Sodas.Count(soda => soda.sodaColor == color);
    }
    public int GetAvailableSpaces()
    {
        return maxCapacity - Sodas.Count;
    }
    public bool HasCapacity()
    {
        return GetAvailableSpaces() > 0;
    }
    public bool HasSodaOfColor(Soda.SodaColor color)
    {
        foreach (var soda in Sodas)
        {
            if (soda.sodaColor == color)
            {
                return true;
            }
        }
        return false;
    }

    //public void AddSodaInTransit(Soda soda)
    //{
    //    inTransitSodas.Add(soda);
    //}

    public bool CanParticipateInTransfer()
    {
        return Time.time > lastTransferTime + TRANSFER_COOLDOWN;
    }
    
    public void MarkTransferTime()
    {
        lastTransferTime = Time.time;
    }
    
    public void AddSoda(Soda soda)
    {
        if (Sodas.Count < maxCapacity)
        {
            Sodas.Add(soda);
            MarkTransferTime(); // Mark transfer time when soda is added
            if (this != null)
            {
                StartCoroutine(UpdateEmptyPositionsWithDelay());
            }
        }
    }
    public void RemoveSoda(Soda soda)
    {
        if (Sodas.Contains(soda) && soda!=null)
        {
            Sodas.Remove(soda);
            MarkTransferTime(); // Mark transfer time when soda is removed
            if (this != null)
            {
                StartCoroutine(UpdateEmptyPositionsWithDelay());
            }
        }
    }   
    public Transform[] GetSodaPositions()
    {
        List<Transform> sodaPositions = new List<Transform>();

        // Find and add each of the child positions named "SodaPosition0", "SodaPosition1", etc.
        for (int i = 0; i < 4; i++)
        {
            Transform pos = transform.Find($"SodaPosition{i}");
            if (pos != null && !sodaPositions.Contains(pos))
            {
                sodaPositions.Add(pos); // Add position if it exists
            }
        }

        // Convert the list to an array and return it
        return sodaPositions.ToArray();
    }
    public void UpdateEmptyPositions()
    {
        //emptyPositions = new List<Transform>();
        emptyPositions.Clear();
        Transform[] allPositions = GetSodaPositions();
        //float tolerance = 0.1f;
        float tolerance = 0.02f;

        foreach (Transform pos in allPositions)
        {
            bool isOccupied = false;

            // Check if any soda in Sodas or inTransitSodas is close to this position
            foreach (Soda soda in Sodas)
            {
                if (Vector3.Distance(soda.transform.position, pos.position) < tolerance)
                {
                    isOccupied = true;
                    break;
                }
            }

            if (!isOccupied)
            {
                emptyPositions.Add(pos);
            }
        }

        //return emptyPositions;
    }
    private IEnumerator UpdateEmptyPositionsWithDelay()
    {
        yield return new WaitForSeconds(0.2f); // Wait for the next frame
        UpdateEmptyPositions();
    }

    public void RearrangeSodas3()
    {
        // Get soda color counts and sort by count descending, then by SodaColor enum order
        var sortedColors = GetSodaColorCounts()
            .OrderByDescending(pair => pair.Value) // Most frequent colors first
            .ThenBy(pair => pair.Key)             // Then by SodaColor enum order
            .SelectMany(pair => Enumerable.Repeat(pair.Key, pair.Value)) // Expand into a list of colors
            .ToList();

        // Get all soda positions
        var sodaPositions = GetSodaPositions();
        if (sortedColors.Count > sodaPositions.Length)
        {
            Debug.LogWarning("More sodas than positions available!");
            return;
        }

        // Rearrange sodas in the sorted order
        for (int i = 0; i < sortedColors.Count; i++)
        {
            Soda soda = Sodas.FirstOrDefault(s => s.sodaColor == sortedColors[i]);
            if (soda != null)
            {
                Transform targetPosition = sodaPositions[i];
                soda.transform.DOMove(targetPosition.position, 0.3f); // Animate to target position
            }
        }

        // Update empty positions after rearranging
        StartCoroutine(UpdateEmptyPositionsWithDelay());
    }
    public void RearrangeSodas2()
    {
        // Get soda positions
        var sodaPositions = GetSodaPositions();
        if (Sodas.Count > sodaPositions.Length)
        {
            Debug.LogWarning("More sodas than positions available!");
            return;
        }

        // Create a new list to maintain the updated order of sodas
        var updatedSodas = new List<Soda>();

        // Create a dictionary to track the sorted colors and their counts
        var colorCounts = new Dictionary<Soda.SodaColor, Queue<Soda>>();

        // Populate the colorCounts dictionary with sodas, maintaining their existing order
        foreach (var soda in Sodas)
        {
            if (!colorCounts.ContainsKey(soda.sodaColor))
            {
                colorCounts[soda.sodaColor] = new Queue<Soda>();
            }
            colorCounts[soda.sodaColor].Enqueue(soda);
        }

        // Rearrange sodas based on positions
        for (int i = 0; i < sodaPositions.Length; i++)
        {
            foreach (var colorQueue in colorCounts.Values)
            {
                if (colorQueue.Count > 0)
                {
                    var soda = colorQueue.Dequeue();
                    updatedSodas.Add(soda);
                    soda.transform.DOMove(sodaPositions[i].position, 0.3f); // Move soda to target position
                    break; // Move to the next position
                }
            }
        }

        // Update the sodas list to reflect the rearranged order
        Sodas = updatedSodas;

        // Update empty positions after rearranging
        StartCoroutine(UpdateEmptyPositionsWithDelay());
    }

    public void RearrangeSodas11()
    {
        // Get soda positions
        var sodaPositions = GetSodaPositions();
        if (Sodas.Count > sodaPositions.Length)
        {
            Debug.LogWarning("More sodas than positions available!");
            return;
        }

        // Create a new list to maintain the updated order of sodas
        var updatedSodas = new List<Soda>();

        // Get soda color counts sorted by count descending, then by SodaColor enum order
        var sortedColors = GetSodaColorCounts()
            .OrderByDescending(pair => pair.Value) // Highest count first
            .ThenBy(pair => pair.Key)             // Tie-breaker: SodaColor enum order
            .ToList();

        // Separate existing sodas (already aligned) and new sodas (not yet aligned)
        var sodaPositionsSet = new HashSet<Vector3>(sodaPositions.Select(pos => pos.position));
        var existingSodas = Sodas.Where(soda => sodaPositionsSet.Contains(soda.transform.position)).ToList();
        var newSodas = Sodas.Except(existingSodas).ToList();

        // Create a dictionary to store queues of sodas for each color, maintaining their existing order
        var colorQueues = new Dictionary<Soda.SodaColor, Queue<Soda>>();
        foreach (var soda in existingSodas)
        {
            if (!colorQueues.ContainsKey(soda.sodaColor))
            {
                colorQueues[soda.sodaColor] = new Queue<Soda>();
            }
            colorQueues[soda.sodaColor].Enqueue(soda);
        }

        // Phase 1: Rearrange existing sodas based on color priority
        int positionIndex = 0;
        foreach (var colorPair in sortedColors)
        {
            var color = colorPair.Key;
            if (!colorQueues.ContainsKey(color)) continue;

            var queue = colorQueues[color];
            while (queue.Count > 0 && positionIndex < sodaPositions.Length)
            {
                var soda = queue.Dequeue();
                updatedSodas.Add(soda);
                soda.transform.DOMove(sodaPositions[positionIndex].position, 0.3f); // Move soda to target position
                positionIndex++;
            }
        }

        // Phase 2: Arrange newly added sodas
        foreach (var soda in newSodas)
        {
            if (positionIndex >= sodaPositions.Length) break;

            updatedSodas.Add(soda);
            soda.transform.DOMove(sodaPositions[positionIndex].position, 0.3f); // Move soda to target position
            positionIndex++;
        }

        // Update the sodas list to reflect the rearranged order
        Sodas = updatedSodas;

        // Update empty positions after rearranging
        StartCoroutine(UpdateEmptyPositionsWithDelay());
    }
    public void RearrangeSodas()
    {
        // Get soda positions
        var sodaPositions = GetSodaPositions();
        if (Sodas.Count > sodaPositions.Length)
        {
            Debug.LogWarning("More sodas than positions available!");
            return;
        }

        // Create a new list to maintain the updated order of sodas
        var updatedSodas = new List<Soda>();

        // Get soda color counts sorted by count descending, then by SodaColor enum order
        var sortedColors = GetSodaColorCounts()
            .OrderByDescending(pair => pair.Value) // Highest count first
            .ThenBy(pair => pair.Key)             // Tie-breaker: SodaColor enum order
            .ToList();

        // Create a dictionary to store queues of sodas for each color, maintaining their existing order
        var colorQueues = new Dictionary<Soda.SodaColor, Queue<Soda>>();
        foreach (var soda in Sodas)
        {
            if (!colorQueues.ContainsKey(soda.sodaColor))
            {
                colorQueues[soda.sodaColor] = new Queue<Soda>();
            }
            colorQueues[soda.sodaColor].Enqueue(soda);
        }

        // Rearrange sodas in sorted order by placing sodas with the highest count first
        int positionIndex = 0;
        foreach (var colorPair in sortedColors)
        {
            var color = colorPair.Key;
            var queue = colorQueues[color];

            while (queue.Count > 0 && positionIndex < sodaPositions.Length)
            {
                var soda = queue.Dequeue();
                updatedSodas.Add(soda);
                soda.transform.DOMove(sodaPositions[positionIndex].position, 0.3f); // Move soda to target position
                positionIndex++;
            }
        }

        // Update the sodas list to reflect the rearranged order
        Sodas = updatedSodas;

        // Update empty positions after rearranging
        StartCoroutine(UpdateEmptyPositionsWithDelay());
    }


    private void UpdateEmptyPositions1()
    {
        emptyPositions.Clear(); // Clear the list before recalculating

        Transform[] allPositions = GetSodaPositions();
        int currentSodaCount = Sodas.Count; // Use Sodas count for the number of occupied slots

        // Populate the emptyPositions list based on unoccupied positions
        for (int i = currentSodaCount; i < maxCapacity; i++)
        {
            if (i < allPositions.Length)
            {
                emptyPositions.Add(allPositions[i]);
            }
        }
        if (emptyPositions.Count == 0 && currentSodaCount < maxCapacity)
        {
            Debug.LogWarning("UpdateEmptyPositions: Box has capacity, but no positions are marked as empty.");
        }
    }

    public List<Transform> GetEmptySodaPositions()
    {
        //return emptyPositions;
        if (emptyPositions == null || emptyPositions.Count == 0)
        {
            Debug.LogWarning("No empty positions available in the box.");
        }
        return emptyPositions;
    }
    public bool BoxFilled()
    {
        if (Sodas.Count == maxCapacity)
        {
            // Get the color of the first soda to use as a comparison
            Soda.SodaColor firstColor = Sodas[0].sodaColor;

            // Check if all sodas in the box have the same color as the first one
            for (int i = 1; i < maxCapacity; i++)
            {
                if (Sodas[i].sodaColor != firstColor)
                {
                    return false; 
                }
            }
            //Debug.Log("Box Filled");
            //GameDataManager.instance.CoinForBoxCopletion(100);
            //UIManager.instance.UpdateGameplayCoins(100);
            //GameManager.instance.CheckWinCondition(GameDataManager.instance.GetGameplayCoins());

            // Update the UI with the current gameplay coins
            //UIManager.instance.UpdateGameplayCoins(GameDataManager.instance.GetGameplayCoins());


            // All sodas are the same color and box is full
            return true;
        }

        return false; 
    }
    public void CloseBox(Transform tempPos)
    {
        StartCoroutine(SetBoxClose(tempPos));

    }
    private IEnumerator SetBoxClose(Transform tempPos)
    {
        yield return new WaitForSeconds(0.4f);
        foreach (Transform child in transform)
        {
            MeshRenderer childRenderer = child.GetComponentInChildren<MeshRenderer>();
            if (childRenderer != null)
            {
                childRenderer.enabled = false;
            }
        }
        if (!doorCreated)
        {
            doorCreated = true;

            // Create and attach the door to the box
            yield return new WaitForSeconds(0.8f);
            //GameObject door = Instantiate(topBox, new Vector3(transform.position.x, transform.position.y + 0.53f, transform.position.z), Quaternion.Euler(90, 0, 0));
            //door.transform.parent = transform;
            CoinManager.instance.AddCoins(tempPos.position, 100);

            IsDragged = false;
            IsOnBoard = false;

            //MoveToTruck(tempPos);


            Destroy(this.gameObject, 1.1f);

            #region oldMovememt
            //// Move the box up
            //Vector3 targetUpPosition = transform.position + new Vector3(0, 0.4f, 0);
            //transform.position = targetUpPosition;

            //yield return new WaitForSeconds(0.2f);

            //// Move the box to the right smoothly over 1 second
            //float elapsedTime = 0f;
            //Vector3 startPosition = transform.position;
            ////Vector3 targetRightPosition = startPosition + Vector3.right * 5f; // Move 1 unit to the right
            //Vector3 targetSidePosition;


            //if (column < 2)
            //{
            //    // Move to the right if on the right half
            //    targetSidePosition = startPosition + Vector3.left * 2f;
            //}
            //else
            //{
            //    // Move to the left if on the left half
            //    targetSidePosition = startPosition + Vector3.right * 2f;
            //}

            //while (elapsedTime < 0.3f)
            //{
            //    transform.position = Vector3.Lerp(startPosition, targetSidePosition, elapsedTime / 1f);
            //    elapsedTime += Time.deltaTime;
            //    yield return null;
            //}

            //// Ensure it reaches the exact right position
            //transform.position = targetSidePosition;
            #endregion



            // Destroy the box after a delay
            //currentHighlightedNode.Unhighlight();
            //currentHighlightedNode = null;
        }
    }
    private void MoveToTruck(Transform boxTransform)
    {
        LiftTruck activeTruck = LiftTruckManager.instance.GetActiveTruck();

        if (activeTruck != null)
        {
            // Add the box to the truck's list


            GameObject box = Instantiate(topBox, new Vector3(boxTransform.position.x, boxTransform.position.y + 0.53f, boxTransform.position.z), Quaternion.Euler(90, 0, 0));
            box.SetActive(false);
            Vector3 truckTargetPosition = activeTruck.GetNextAvailablePosition();

            box.transform.DOMove(truckTargetPosition, 0.7f).SetEase(Ease.Linear).OnComplete(() =>
            {
                activeTruck.AddBox(box);

            });

        }
    }

    IEnumerator SwapClick()
    {
        yield return new WaitForSeconds(0.8f);
        if (HammerManager.instance != null && HammerManager.instance.gameObject != null)
        {
            if (HammerManager.instance.IsHammerActive() && IsOnBoard)
            {
                Debug.Log("Hammer Destroyed the Box");
                HammerManager.instance.OnBoxClicked(this);

                yield break;
    }

}

    }

     void OnMouseDown()
    {
        //if (GameManager.instance.gameEnded || SpawnContoller.instance.stopSpawn)
        //{
        //    return;
        //}
        if (GameManager.instance.gameOver || Board.instance.grid[column , row].isOccupied)
        {
            return;
        }


        //if (SwapController.instance != null && SwapController.instance.gameObject != null)
        //{
        //    if (SwapController.instance.IsSwapButtonPressed && IsOnBoard)
        //    {
        //        Debug.Log("Inside  Swap Button");

        //        SwapController.instance.OnBoxClicked(this);
        //        return;
        //    }
        //}

        //if (HammerManager.instance != null && HammerManager.instance.gameObject != null)
        //{
        //    if (HammerManager.instance.IsHammerButtonPressed &&  IsOnBoard)
        //    {
        //        Debug.Log(" IsHammerbuttonPressed is : " + HammerManager.instance.IsHammerButtonPressed);
        //        Debug.Log("Inside Hammer Button");
        //        HammerManager.instance.OnBoxClicked(this);

        //        return;
        //    }

        //}


        if (lastHighlightedNode)
        {
            return;
        }
        startPos = transform.position;

        isDragging = true;
        offset = transform.position - GetMouseWorldPosition();
    }
    void OnMouseDrag()
    {
        if (isDragging )
        {
            // Update box position as you drag it
            transform.position = GetMouseWorldPosition() + offset;
            if (SpawnContoller.instance.isTutorialState)
            {

                FindObjectOfType<HandAnimation>().HideHande();

                if (Board.instance.IsBoardEmpty())
                {
                    // Highlight any node the mouse is over
                    Node nodeUnderMouse = Board.instance.GetNodeUnderMouse();
                    if (nodeUnderMouse != null && !nodeUnderMouse.isOccupied)
                    {
                        //nodeUnderMouse.Highlight();
                        HighlightNode(nodeUnderMouse);
                    }
                }
                else
                {
                    // Highlight only adjacent nodes
                    HashSet<(int, int)> adjacentPositions = Board.instance.GetAdjacentPositionsForLastBox();

                    foreach (var adjacentPos in adjacentPositions)
                    {
                        Board.instance.grid[adjacentPos.Item1, adjacentPos.Item2].Highlight();
                    }

                    Node nodeUnderMouse = Board.instance.GetNodeUnderMouse();

                    if (nodeUnderMouse != null &&
                        !nodeUnderMouse.isOccupied &&
                        adjacentPositions.Contains((nodeUnderMouse.column, nodeUnderMouse.row)))
                    {
                        //nodeUnderMouse.Highlight();
                        HighlightNode(nodeUnderMouse);

                    }
                    



                }
            }
            else
            {

                HighlightNodeUnderMouse();
            }

        }
    }
    void OnMouseUp()
    {
        isDragging = false;
        if (SpawnContoller.instance.isTutorialState)
        {
            TutorialMouseUp();
        }
        else
        {
            UpdateEmptyPositions();
            if (currentHighlightedNode != null && !currentHighlightedNode.isOccupied)
            {
                currentHighlightedNode.Unhighlight();
                currentHighlightedNode.isOccupied = true;

                transform.position = currentHighlightedNode.transform.position;

                // Reset the y position
                Vector3 tempPos = transform.position;
                tempPos.y = 0.34f;
                transform.position = tempPos;

                IsDragged = true;
                Board.instance.SetCurrentBox(this);
                Board.instance.UpdateBoxPosition(currentHighlightedNode.column, currentHighlightedNode.row);
                lastHighlightedNode = currentHighlightedNode;

                PlacementTimestamp = Time.time; // Use Unity's Time.time to get the current game time
            }
            else if (currentHighlightedNode == null)
            {
                transform.position = startPos;
            }
        }

        // Always unhighlight the current node after the drag is finished
        //**** THIS CODE SHOULD NOT BE USED!!! BECAUSE MOVE THE BOXES FROM THE BOARD TO SPAWN POSITIONS!! ***
        //if (currentHighlightedNode != null)
        //{
        //    currentHighlightedNode.Unhighlight();
        //    currentHighlightedNode = null;
        //}
    }

    private void TutorialMouseUp()
    {
        
        

            UpdateEmptyPositions();

            if (currentHighlightedNode != null && !currentHighlightedNode.isOccupied)
            {
                bool canPlaceBox = false;

                // Check if the board is empty
                if (Board.instance.IsBoardEmpty())
                {
                    // Allow placing the box anywhere on an empty board
                    canPlaceBox = true;
                }
                else
                {
                    // Get all adjacent positions of existing boxes
                    HashSet<(int, int)> adjacentPositions = Board.instance.GetAdjacentPositionsForLastBox();

                    // Check if the highlighted node's position is adjacent to any existing box
                    canPlaceBox = adjacentPositions.Contains((currentHighlightedNode.column, currentHighlightedNode.row));
                }

                if (canPlaceBox)
                {
                    currentHighlightedNode.Unhighlight();
                    currentHighlightedNode.isOccupied = true;

                    transform.position = currentHighlightedNode.transform.position;

                    // Reset the y position
                    Vector3 tempPos = transform.position;
                    tempPos.y = 0.34f;
                    transform.position = tempPos;

                    IsDragged = true;
                    Board.instance.SetCurrentBox(this);
                    Board.instance.UpdateBoxPosition(currentHighlightedNode.column, currentHighlightedNode.row);

                    lastHighlightedNode = currentHighlightedNode;

                    PlacementTimestamp = Time.time; // Use Unity's Time.time to get the current game time
                    ToolTipTutorial.instance.CompleteAnim();
                    ClearAllHighlights();
                }
                else
                {

                    HandAnimation.instance.TryRestartAnimation(0.5f);
                    Debug.Log("Show Hand is running From - BOX");

                    // Unhighlight the current node if placement fails
                    currentHighlightedNode.Unhighlight();

                    ClearAllHighlights();
                    transform.position = startPos;
                }
            }
            else if (currentHighlightedNode == null)
            {
                if (!IsOnBoard)
                {
                    HandAnimation.instance.TryRestartAnimation(0.5f);
                    Debug.Log("Show Hand is running From - BOX");

                    //HandAnimation.instance.ShowHand();
                }


                // If no node is highlighted, return to the starting position
                ClearAllHighlights();

                transform.position = startPos;
            }
        

    }
    private void ResetHighlights()
    {
        // Unhighlight the current node
        if (currentHighlightedNode != null)
        {
            currentHighlightedNode.Unhighlight();
            currentHighlightedNode = null;
        }

        // Unhighlight all adjacent nodes
        HashSet<(int, int)> adjacentPositions = Board.instance.GetAdjacentPositionsForLastBox();

        foreach (var adjacentPos in adjacentPositions)
        {
            Board.instance.grid[adjacentPos.Item1, adjacentPos.Item2].Unhighlight();
        }
    }

    private void ClearAllHighlights()
    {
        foreach (Node node in Board.instance.grid)
        {
            node.Unhighlight();
        }
    }
    private void HighlightNode(Node node)
    {
        // Unhighlight previous node if different from the current one
        if (currentHighlightedNode != null && currentHighlightedNode != node)
        {
            currentHighlightedNode.Unhighlight();

            // Unhighlight adjacent nodes only if the board is not empty
            if (!Board.instance.IsBoardEmpty())
            {
                HashSet<(int, int)> adjacentPositions = Board.instance.GetAdjacentPositionsForLastBox();

                foreach (var adjacentPos in adjacentPositions)
                {
                    Board.instance.grid[adjacentPos.Item1, adjacentPos.Item2].Unhighlight();
                }
            }
        }

        // Update the currently highlighted node
        currentHighlightedNode = node;

        // Highlight the new node
        if (currentHighlightedNode != null)
        {
            currentHighlightedNode.Highlight();
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = 1.5f; // Set some distance from the camera
        //mousePos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);

        return mainCamera.ScreenToWorldPoint(mousePos);
    }
    private void HighlightNodeUnderMouse()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Node node = hit.collider.GetComponent<Node>();

            if (node != null)
            {
                // Unhighlight the previous node if it's different
                if (currentHighlightedNode != null && currentHighlightedNode != node)
                {
                    currentHighlightedNode.Unhighlight();
                }

                // Highlight the new node
                node.Highlight();
                currentHighlightedNode = node;
            }
         
        }
        else if (currentHighlightedNode != null)
        {
            // Unhighlight the node if nothing is hit
            currentHighlightedNode.Unhighlight();
            currentHighlightedNode = null;
        }
    }

    #region Old Code
    //private void Update()
    //{
    //    GetSodaList();
    //    GetEmptySodaPositions();

    //}

    public int GetUniqueColorCount()
    {
        var sodaCounts = GetSodaColorCounts();
        return sodaCounts.Count;
    }
    public HashSet<Soda.SodaColor> GetDistinctColors()
    {
        var sodaCounts = GetSodaColorCounts();
        return new HashSet<Soda.SodaColor>(sodaCounts.Keys);
    }
  
    public bool HasSameColorSoda(Box otherBox)
    {
        return Sodas.Count > 0 && otherBox.Sodas.Count > 0 &&
            Sodas[0].sodaColor == otherBox.Sodas[0].sodaColor;
    }
    public bool HasColorSlotAvailable(Soda.SodaColor color)
    {
        int currentColorCount = Sodas.Count(s => s.sodaColor == color);
        return currentColorCount < GetAvailableSpaces();
    }
    public List<Soda> GetReversedSodas()
    {
        List<Soda> reversedSodas = new List<Soda>();
        for (int i = reversedSodas.Count - 1; i >= 0; i--)
        {
            if (!reversedSodas.Contains(reversedSodas[i]))
            {
                reversedSodas.Add(reversedSodas[i]);
            }
        }

        return reversedSodas;
    }
    public List<Transform> GetEmptySodaPositions2()
    {
        emptyPositions = new List<Transform>();
        Transform[] allPositions = GetSodaPositions();

        float tolerance = 0.1f; // Tolerance value for position matching

        foreach (Transform pos in allPositions)
        {
            bool isOccupied = false;

            // Check if any soda's position is close to this position within tolerance
            foreach (Soda soda in Sodas)
            {
                if (Vector3.Distance(soda.transform.position, pos.position) < tolerance)
                {
                    isOccupied = true;
                    break;
                }
            }

            if (!isOccupied)
            {
                emptyPositions.Add(pos);
            }
        }

        return emptyPositions;
    }

    public void TransferSodasTo4(Box targetBox, int count)
    {
        GetSodaList();
        StartCoroutine(TransferSodasOneByOne4(targetBox, count));
    }

    private IEnumerator TransferSodasOneByOne4(Box targetBox, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (Sodas.Count > 0 && targetBox.GetAvailableSpaces() > 0)
            {
                Soda sodaToAdd = Sodas[Sodas.Count - 1];
                Sodas.Remove(sodaToAdd);
                GetEmptySodaPositions();


                StartCoroutine(MoveSodaToTarget4(sodaToAdd, targetBox));

                // Wait for 0.3 seconds before moving the next soda
                yield return new WaitForSeconds(0.5f);
            }
            else
            {
                // Stop transferring if there are no more sodas or no more space in target box
                break;
            }
        }
    }

    private IEnumerator MoveSodaToTarget4(Soda soda, Box targetBox)
    {
        Vector3 startPos = soda.transform.position;
        Vector3 endPos = targetBox.GetEmptySodaPositions()[0].position; // Target position
        Vector3 controlPoint = (startPos + endPos) / 2 + Vector3.up * 0.5f; // Midpoint control for parabola

        float duration = 0.5f;
        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            // Calculate position along parabolic path
            soda.transform.position = CalculateParabola4(startPos, endPos, controlPoint, t);
            yield return null;
        }

        // Ensure final position is set correctly and add soda to target box
        soda.transform.position = endPos;
        soda.transform.parent = targetBox.transform; // Make the target box the new parent
        targetBox.AddSoda(soda);
    }

    private Vector3 CalculateParabola4(Vector3 start, Vector3 end, Vector3 control, float t)
    {
        // Quadratic Bezier formula for parabolic motion
        return (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
    }
    public void TransferSodasTo1(Box targetBox, int count)
    {
        GetSodaList();

        for (int i = 0; i < count; i++)
        {
            if (Sodas.Count > 0 && targetBox.GetAvailableSpaces() > 0)
            {
                Soda sodaToAdd = Sodas[Sodas.Count - 1];
                //Sodas[Sodas.Count - 1] = null;
                //Sodas.RemoveAt(Sodas.Count - 1);
                Sodas.Remove(sodaToAdd);

                //Sodas = new List<Soda>(Sodas); 
                GetEmptySodaPositions();

                //targetBox.AddSoda(sodaToAdd ,  targetBox);

                Destroy(sodaToAdd.gameObject);
            }
            else
            {
                // Stop transferring if there are no more sodas or no more space in target box
                break;
            }
        }
    }
    public void AddSoda1(Soda sodaToAdd, Box targetBox)
    {
        if (Sodas.Count < maxCapacity)
        {
            List<Transform> targetPos = targetBox.GetEmptySodaPositions();
            GameObject sodaGo = Instantiate(sodaPrefab, targetPos[targetPos.Count - 1].position, Quaternion.Euler(-90, 0, 0));
            Vector3 tempPos = sodaGo.transform.position;
            tempPos.y = 0.3f;
            sodaGo.transform.position = tempPos;
            Soda soda = sodaGo.GetComponent<Soda>();
            soda.SetColor(sodaToAdd.sodaColor);
            targetBox.Sodas.Add(soda);
            //Sodas.Add(sodaToAdd);
            //sodaToAdd.transform.parent = this.transform;
            soda.transform.parent = this.transform;
        }
    }

    public void TransferSodasTo3(Box targetBox, int count)
    {
        GetSodaList();

        for (int i = 0; i < count; i++)
        {
            if (Sodas.Count > 0 && targetBox.GetAvailableSpaces() > 0)
            {
                Soda sodaToTransfer = Sodas[Sodas.Count - 1];
                Sodas.Remove(sodaToTransfer); // Remove soda from the current box list

                // Move the soda to the target box
                StartCoroutine(MoveSodaToTarget4(sodaToTransfer, targetBox));
            }
            else
            {
                // Stop transferring if there are no more sodas or no more space in target box
                break;
            }
        }
    }

    public List<Transform> GetEmptySodaPositions1()
    {
        emptyPositions = new List<Transform>();
        Transform[] allPositions = GetSodaPositions();

        foreach (Transform pos in allPositions)
        {
            bool isOccupied = false;

            // Check if any soda's position matches this position
            foreach (Soda soda in Sodas)
            {
                if (soda.transform.position == pos.position)
                {
                    isOccupied = true;
                    break;
                }
            }

            if (!isOccupied)
            {
                emptyPositions.Add(pos);
            }
        }

        return emptyPositions;
    }
    void OnMouseUp1()
    {
        isDragging = false;
        if (SpawnContoller.instance.isTutorialState)
        {
            if (!IsOnBoard)
            {
                ////hand.ShowHand();

                HandAnimation.instance.TryRestartAnimation(0.1f);
            }

        }

        UpdateEmptyPositions();
        if (currentHighlightedNode != null && !currentHighlightedNode.isOccupied)
        {
            currentHighlightedNode.Unhighlight();
            currentHighlightedNode.isOccupied = true;
            //currentHighlightedNode = null;

            transform.position = currentHighlightedNode.transform.position;
            // reset the y pos to 0
            Vector3 temoPos = transform.position;
            temoPos.y = 0.34f;
            transform.position = temoPos;
            IsDragged = true;
            Board.instance.SetCurrentBox(this);
            Board.instance.UpdateBoxPosition(currentHighlightedNode.column, currentHighlightedNode.row);
            lastHighlightedNode = currentHighlightedNode;

            PlacementTimestamp = Time.time; // Use Unity's Time.time to get the current game time

        }
        else if (currentHighlightedNode == null)
        {
            transform.position = startPos;
        }

    }
    void OnMouseUp11()
    {
        isDragging = false;

        if (SpawnContoller.instance.isTutorialState)
        {

            UpdateEmptyPositions();

            if (currentHighlightedNode != null && !currentHighlightedNode.isOccupied)
            {
                bool canPlaceBox = false;

                // Check if the board is empty
                if (Board.instance.IsBoardEmpty())
                {
                    // Allow placing the box anywhere on an empty board
                    canPlaceBox = true;
                }
                else
                {
                    // Get all adjacent positions of existing boxes
                    HashSet<(int, int)> adjacentPositions = Board.instance.GetAllAdjacentPositionsForBoxes();

                    // Check if the highlighted node's position is adjacent to any existing box
                    canPlaceBox = adjacentPositions.Contains((currentHighlightedNode.column, currentHighlightedNode.row));
                }

                if (canPlaceBox)
                {
                    currentHighlightedNode.Unhighlight();
                    currentHighlightedNode.isOccupied = true;

                    transform.position = currentHighlightedNode.transform.position;

                    // Reset the y position
                    Vector3 tempPos = transform.position;
                    tempPos.y = 0.34f;
                    transform.position = tempPos;

                    IsDragged = true;
                    Board.instance.SetCurrentBox(this);
                    Board.instance.UpdateBoxPosition(currentHighlightedNode.column, currentHighlightedNode.row);
                    lastHighlightedNode = currentHighlightedNode;

                    PlacementTimestamp = Time.time; // Use Unity's Time.time to get the current game time
                }
                else
                {

                    HandAnimation.instance.TryRestartAnimation(0.5f);
                    Debug.Log("Show Hand is running From - BOX");

                    // Unhighlight the current node if placement fails
                    currentHighlightedNode.Unhighlight();
                    transform.position = startPos;
                }
            }
            else if (currentHighlightedNode == null)
            {
                if (!IsOnBoard)
                {
                    HandAnimation.instance.TryRestartAnimation(0.5f);
                    Debug.Log("Show Hand is running From - BOX");

                    //HandAnimation.instance.ShowHand();
                }


                // If no node is highlighted, return to the starting position
                transform.position = startPos;
            }
        }
        else
        {
            UpdateEmptyPositions();
            if (currentHighlightedNode != null && !currentHighlightedNode.isOccupied)
            {
                currentHighlightedNode.Unhighlight();
                currentHighlightedNode.isOccupied = true;

                transform.position = currentHighlightedNode.transform.position;

                // Reset the y position
                Vector3 tempPos = transform.position;
                tempPos.y = 0.34f;
                transform.position = tempPos;

                IsDragged = true;
                Board.instance.SetCurrentBox(this);
                Board.instance.UpdateBoxPosition(currentHighlightedNode.column, currentHighlightedNode.row);
                lastHighlightedNode = currentHighlightedNode;

                PlacementTimestamp = Time.time; // Use Unity's Time.time to get the current game time
            }
            else if (currentHighlightedNode == null)
            {
                transform.position = startPos;
            }
        }

        // Always unhighlight the current node after the drag is finished
        if (currentHighlightedNode != null)
        {
            currentHighlightedNode.Unhighlight();
            currentHighlightedNode = null;
        }
    }
    void OnMouseUp2()
    {
        isDragging = false;

        if (currentHighlightedNode != null)
        {
            currentHighlightedNode.Unhighlight();
            currentHighlightedNode.isOccupied = true;
            currentHighlightedNode = null;
            Destroy(gameObject);
            //Board.instance.UpdateBoard();           
          
        }
        else
        {
            transform.position = startPos;

        }
    }


    #endregion


}
#endif
