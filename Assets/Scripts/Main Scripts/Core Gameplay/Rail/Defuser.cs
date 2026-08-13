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

    /// <summary>Raised when this Defuser is spent, so the pool can count it.</summary>
    public event Action<Defuser> Consumed;

    public bool IsConsumed => isConsumed;

    /// <summary>
    /// Whether a Defuser dropped on a cell with no bomb is spent anyway. Set from
    /// the level's bomb settings when the item is created.
    /// </summary>
    public bool ConsumeOnWrongCell { get; set; }

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

        Node node = board.GetDropTargetNode(GetPlacementReferencePosition());
        if (node == currentHighlightedNode)
        {
            return;
        }

        ClearCurrentHighlight();
        if (node != null && CanTarget(board, node))
        {
            currentHighlightedNode = node;
            currentHighlightedNode.Highlight();
        }
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

        ClearCurrentHighlight();
        RailDragSupport.ClearAllHighlights();

        if (board != null && node != null && CanTarget(board, node) &&
            board.TryDefuse(node.column, node.row))
        {
            Consume();
            return;
        }

        // A wrong drop is a real event, not a no-op: on the harder levels it costs
        // the charge, which is what makes guessing expensive.
        if (ConsumeOnWrongCell && node != null)
        {
            StartCoroutine(FailAndConsumeRoutine());
            return;
        }

        ReturnToRail();
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

    private IEnumerator FailAndConsumeRoutine()
    {
        // Shake in place, then vanish. Consuming immediately would read as the
        // item never having existed.
        Vector3 basePosition = startPosition;
        transform.position = basePosition;

        float elapsed = 0f;
        while (elapsed < 0.25f)
        {
            elapsed += Time.deltaTime;
            float offset = Mathf.Sin(elapsed * 60f) * 0.02f * (1f - elapsed / 0.25f);
            transform.position = basePosition + new Vector3(offset, 0f, 0f);
            yield return null;
        }

        transform.position = basePosition;
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

        Consumed?.Invoke(this);
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
