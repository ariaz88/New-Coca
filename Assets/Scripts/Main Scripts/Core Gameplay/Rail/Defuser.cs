using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A rail item that neutralises one bomb.
///
/// A sibling of Box, not a subclass. Subclassing would have dragged in
/// allBoxes, boardBoxes, StableIds, packing, retirement and the whole soda
/// transfer surface, none of which a Defuser has any use for - and a Defuser
/// that accidentally registered as a Box would take part in transfers, count
/// toward the board-full check, and be picked up by the hammer and swap
/// powerups. What the two genuinely share - pointer maths and the dragged-item
/// display lift - lives in RailDragSupport and is used by both.
///
/// The commit path is Board.TryDefuse, which deliberately does not go through
/// UpdateBoxPosition: that registers the mover in allBoxes, assigns a StableId,
/// raises PlacementAccepted with a hard Box payload and starts the resolver.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class Defuser : MonoBehaviour, IRailItem
{
    [SerializeField, Tooltip("Height of the plane the pointer is projected onto while dragging. Matches Box.")]
    private float dragHeight = 0.35f;

    [SerializeField, Min(0f), Tooltip("Seconds for the item to slide back to the rail after a rejected drop.")]
    private float returnDuration = 0.18f;

    private readonly RailDragDisplay dragDisplay = new RailDragDisplay();

    private bool isDragging;
    private bool isConsumed;
    private Vector3 startPosition;
    private Vector3 dragOffset;
    private Plane dragPlane;
    private Node currentHighlightedNode;
    private Coroutine returnRoutine;

    private Func<Defuser, bool> dragConstraint;
    private object dragConstraintOwner;

    /// <summary>
    /// Raised when this Defuser is spent. The bool says whether the charge should
    /// be refunded - true when it was wasted on a cell with no bomb on a level
    /// that forgives that.
    /// </summary>
    public event Action<Defuser, bool> Consumed;

    public bool IsConsumed => isConsumed;

    /// <summary>
    /// Whether missing costs the charge. When false the item still disappears -
    /// it never flies back to the rail - but the director grants a replacement.
    /// Set from the level's bomb settings when the item is created.
    /// </summary>
    public bool ConsumeOnWrongCell { get; set; }

    private bool refundOnConsume;

    /// <summary>
    /// Installs one owner-scoped rule that can block dragging, mirroring
    /// Box.TrySetDragConstraint so the preview and scanner freezes can gate both
    /// item types the same way.
    /// </summary>
    public bool TrySetDragConstraint(object owner, Func<Defuser, bool> constraint)
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

    public void ClearDragConstraint(object owner)
    {
        if (owner == null || !ReferenceEquals(dragConstraintOwner, owner))
        {
            return;
        }

        dragConstraintOwner = null;
        dragConstraint = null;
    }

    private void OnMouseDown()
    {
        if (isDragging || isConsumed)
        {
            return;
        }

        if (dragConstraint != null && !dragConstraint(this))
        {
            return;
        }

        if (GameManager.instance != null &&
            (GameManager.instance.gameOver || GameManager.instance.gameEnded))
        {
            return;
        }

        Board board = Board.instance;
        if (board == null || !board.CanInteract)
        {
            return;
        }

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        startPosition = transform.position;
        dragPlane = new Plane(Vector3.up, new Vector3(0f, dragHeight, 0f));
        if (!RailDragSupport.TryGetPointerOnPlane(dragPlane, out Vector3 pointer))
        {
            return;
        }

        dragOffset = transform.position - pointer;
        isDragging = true;
        dragDisplay.Enable(gameObject);
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

        if (RailDragSupport.TryGetPointerOnPlane(dragPlane, out Vector3 pointer))
        {
            transform.position = pointer + dragOffset;
        }

        // Deliberately NO cell highlight, in any state.
        //
        // Highlighting the valid target told the player exactly where a bomb was
        // the moment they picked the Defuser up - dragging it slowly across the
        // board was a free scan of every cell, which defeats the entire memory
        // mechanic and makes the scanner charges worthless. The Defuser is aimed
        // from memory or not at all.
    }

    private void OnMouseUp()
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        dragDisplay.Restore();

        Board board = Board.instance;
        Node node = board != null
            ? board.GetDropTargetNode(GetPlacementReferencePosition())
            : null;

        // The Defuser never highlights anything, but a Box dragged earlier might
        // have left one behind, and this is a cheap place to be sure.
        RailDragSupport.ClearAllHighlights();

        if (board != null && node != null && board.TryDefuse(node.column, node.row))
        {
            Consume();
            return;
        }

        if (node == null)
        {
            // Released off the board entirely - it never committed to a cell, so
            // it goes back to its slot. Only a drop ONTO the board is a decision.
            ReturnToRail();
            return;
        }

        // Dropped on the board and there was no bomb there. It is spent either
        // way: a Defuser that flew home after a miss let the player sweep the
        // whole board with a single charge, one cell at a time, for free.
        //
        // On the easier levels the charge itself is refunded - a fresh Defuser
        // arrives - so the mistake costs a turn rather than a resource. On the
        // harder ones it is simply gone.
        StartCoroutine(FailAndConsumeRoutine());
    }

    /// <summary>
    /// A Defuser targets any cell that still hides a live bomb - including one
    /// with a box already standing on it, because in Countdown mode the bomb the
    /// player most needs to defuse is exactly the one they just dropped a box on.
    /// </summary>
    private bool CanTarget(Board board, Node node)
    {
        return board.HasLiveBombAt(node.column, node.row);
    }

    /// <summary>
    /// The point the Board maps to a cell while this item is dragged.
    ///
    /// Must be transform.position, for the same reason Box documents at length:
    /// the collider sits well above the pivot, and under the rail item's rotation
    /// using the collider centre puts the hover test more than a full cell away
    /// from where the item actually is.
    /// </summary>
    private Vector3 GetPlacementReferencePosition()
    {
        return transform.position;
    }

    private void CancelDrag()
    {
        isDragging = false;
        dragDisplay.Restore();
        ClearCurrentHighlight();
        ReturnToRail();
    }

    private void ReturnToRail()
    {
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
        }

        returnRoutine = StartCoroutine(ReturnRoutine());
    }

    private IEnumerator ReturnRoutine()
    {
        Vector3 from = transform.position;
        float elapsed = 0f;

        while (elapsed < returnDuration && returnDuration > 0f)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(from, startPosition, Mathf.Clamp01(elapsed / returnDuration));
            yield return null;
        }

        transform.position = startPosition;
        returnRoutine = null;
    }

    /// <summary>
    /// Shakes where it was dropped and fades out. It stays on the cell it failed
    /// on rather than snapping home, so the failure reads as happening there.
    /// </summary>
    private IEnumerator FailAndConsumeRoutine()
    {
        refundOnConsume = !ConsumeOnWrongCell;

        Vector3 basePosition = transform.position;
        float elapsed = 0f;
        const float duration = 0.35f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float remaining = 1f - elapsed / duration;
            float offset = Mathf.Sin(elapsed * 55f) * 0.022f * remaining;
            transform.position = basePosition + new Vector3(offset, 0f, 0f);
            transform.localScale = Vector3.one * Mathf.Max(0.01f, remaining);
            yield return null;
        }

        Consume();
    }

    private void Consume()
    {
        if (isConsumed)
        {
            return;
        }

        isConsumed = true;

        // Leaving the rail list is this item's own job. RemoveSpawnerList prunes
        // by IsConsumed, but the rail only refills when the list is empty, and
        // waiting for the next placement to prune it would stall the refill by a
        // whole turn.
        if (SpawnContoller.instance != null)
        {
            SpawnContoller.instance.spawnedBoxes.Remove(gameObject);
        }

        Consumed?.Invoke(this, refundOnConsume);
        Destroy(gameObject, 0.05f);
    }

    private void ClearCurrentHighlight()
    {
        if (currentHighlightedNode != null)
        {
            currentHighlightedNode.Unhighlight();
            currentHighlightedNode = null;
        }
    }

    private void OnDisable()
    {
        dragDisplay.Restore();
    }
}
