using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Live state of one order slot during a level.
/// Authored values are copied here at level start, so replaying a level always
/// restarts from the numbers in the Board inspector.
/// </summary>
public sealed class OrderRuntimeState
{
    /// <summary>Left-to-right position of this order in the panel.</summary>
    public int Index { get; }

    public OrderKind Kind { get; }

    /// <summary>Meaningless for a Blocks order.</summary>
    public Soda.SodaColor Color { get; }

    /// <summary>Authored target. Never changes during a level.</summary>
    public int RequiredCount { get; }

    /// <summary>How many packed boxes of this color, or locked blocks, are still needed.</summary>
    public int Remaining { get; private set; }

    public bool IsFulfilled => Remaining <= 0;
    public bool IsBlocks => Kind == OrderKind.Blocks;

    public OrderRuntimeState(int index, OrderKind kind, Soda.SodaColor color, int requiredCount)
    {
        Index = index;
        Kind = kind;
        Color = color;
        RequiredCount = Mathf.Max(1, requiredCount);
        Remaining = RequiredCount;
    }

    internal void Consume()
    {
        Remaining = Mathf.Max(0, Remaining - 1);
    }
}

/// <summary>
/// Snapshot handed to the presentation layer when a packed box is accepted by an
/// order. It carries everything the panel and the VFX need, so neither has to
/// query the manager back.
/// </summary>
public struct OrderConsumedEvent
{
    /// <summary>Which order slot accepted the box.</summary>
    public readonly int OrderIndex;

    public readonly Soda.SodaColor Color;

    /// <summary>The value the slot must display once the effect lands.</summary>
    public readonly int RemainingAfter;

    /// <summary>True when this box was the one that finished the order.</summary>
    public readonly bool CompletesOrder;

    /// <summary>Board-space position the packed box occupied, the VFX origin.</summary>
    public readonly Vector3 SourceWorldPosition;

    public OrderConsumedEvent(
        int orderIndex,
        Soda.SodaColor color,
        int remainingAfter,
        bool completesOrder,
        Vector3 sourceWorldPosition)
    {
        OrderIndex = orderIndex;
        Color = color;
        RemainingAfter = remainingAfter;
        CompletesOrder = completesOrder;
        SourceWorldPosition = sourceWorldPosition;
    }
}

/// <summary>
/// Owns the level goal: which sodas are wanted, how many of each are left, and
/// when the level is finished.
///
/// Two-step design, and the reason for it
/// --------------------------------------
/// Packing a box and *seeing* the order tick down are separated in time, because
/// a light streak has to fly from the board up to the panel first. So:
///
///   1. Board packs a box  -> TryConsumePackedBox decrements the logical count
///      immediately and raises OrderConsumed. Deciding early means two boxes
///      packed in the same frame can never both claim the last unit of an order.
///   2. The presentation plays its effect and calls NotifyImpactApplied when the
///      streak reaches the slot. Only once every impact has landed and every
///      order reads zero is AllOrdersFulfilled raised.
///
/// That ordering is what keeps the win panel from appearing over an order that
/// still visually shows "1".
///
/// A packed color that no order asks for is ignored entirely: TryConsumePackedBox
/// returns false and raises nothing, so no label, streak, or glow is produced.
/// </summary>
[DisallowMultipleComponent]
public sealed class OrderManager : MonoBehaviour
{
    public static OrderManager instance;

    [SerializeField, Tooltip("Optional. When empty, the active Board.instance is used.")]
    private Board board;

    [SerializeField, Tooltip("Logs every order transition. Useful while building levels.")]
    private bool verboseLogging;

    private readonly List<OrderRuntimeState> orders = new List<OrderRuntimeState>();
    private readonly Dictionary<Soda.SodaColor, OrderRuntimeState> ordersByColor =
        new Dictionary<Soda.SodaColor, OrderRuntimeState>();

    /// <summary>At most one per level, so it is held directly rather than keyed.</summary>
    private OrderRuntimeState blocksOrder;

    private int pendingImpacts;
    private bool completionRaised;
    private bool initialized;

    /// <summary>Ordered order list, left to right, for the panel to render.</summary>
    public IReadOnlyList<OrderRuntimeState> Orders => orders;

    public bool HasOrders => orders.Count > 0;

    /// <summary>True once every order reads zero, ignoring effects still in flight.</summary>
    public bool AllOrdersLogicallyFulfilled
    {
        get
        {
            for (int index = 0; index < orders.Count; index++)
            {
                if (!orders[index].IsFulfilled)
                {
                    return false;
                }
            }

            return orders.Count > 0;
        }
    }

    /// <summary>Raised after the runtime order list has been built for this level.</summary>
    public event System.Action OrdersInitialized;

    /// <summary>Raised the moment a packed box is accepted by an order.</summary>
    public event System.Action<OrderConsumedEvent> OrderConsumed;

    /// <summary>Raised once, after the last effect has visually landed.</summary>
    public event System.Action AllOrdersFulfilled;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Copies the authored orders off the Board into runtime counters. Safe to
    /// call more than once; the second call rebuilds from the authored values,
    /// which is what a revive or a restart wants.
    /// </summary>
    public void Initialize()
    {
        Board activeBoard = board != null ? board : Board.instance;
        if (activeBoard == null)
        {
            Debug.LogError(
                "OrderManager found no Board, so this level has no orders. Assign one or place " +
                "OrderManager in a scene that contains a Board.",
                this);
            return;
        }

        orders.Clear();
        ordersByColor.Clear();
        blocksOrder = null;
        pendingImpacts = 0;
        completionRaised = false;

        IReadOnlyList<LevelOrderData> authored = activeBoard.LevelOrders;
        for (int index = 0; index < authored.Count; index++)
        {
            LevelOrderData data = authored[index];
            if (data == null)
            {
                continue;
            }

            if (data.IsBlocks)
            {
                if (blocksOrder != null)
                {
                    Debug.LogWarning(
                        $"Order {index} is a second Blocks order and was ignored. " +
                        "A level can only ask for locked blocks once.",
                        this);
                    continue;
                }

                blocksOrder = new OrderRuntimeState(
                    orders.Count, OrderKind.Blocks, data.color, data.requiredCount);
                orders.Add(blocksOrder);
                continue;
            }

            if (ordersByColor.ContainsKey(data.color))
            {
                // The Board inspector already flags duplicates as an error. If one
                // survives anyway, the extra row is dropped rather than creating a
                // second slot that could never be targeted unambiguously.
                Debug.LogWarning(
                    $"Order {index} repeats color {data.color} and was ignored. " +
                    "Merge duplicate rows in the Board inspector.",
                    this);
                continue;
            }

            OrderRuntimeState state = new OrderRuntimeState(
                orders.Count, OrderKind.Soda, data.color, data.requiredCount);
            orders.Add(state);
            ordersByColor.Add(state.Color, state);
        }

        initialized = true;

        if (orders.Count == 0)
        {
            Debug.LogWarning(
                "This level defines no orders. Add at least one in the Orders section of the " +
                "Board inspector, otherwise the level cannot be completed through orders.",
                this);
        }

        OrdersInitialized?.Invoke();
    }

    /// <summary>Returns the live state for a color, or null when unordered.</summary>
    public OrderRuntimeState GetOrder(Soda.SodaColor color)
    {
        return ordersByColor.TryGetValue(color, out OrderRuntimeState state) ? state : null;
    }

    /// <summary>True when an order still needs boxes of this color.</summary>
    public bool WantsColor(Soda.SodaColor color)
    {
        OrderRuntimeState state = GetOrder(color);
        return state != null && !state.IsFulfilled;
    }

    /// <summary>
    /// Called by Board when a box is packed. Returns false, and does nothing at
    /// all, when the color is not ordered or its order is already complete.
    /// </summary>
    public bool TryConsumePackedBox(Soda.SodaColor color, Vector3 sourceWorldPosition)
    {
        if (!initialized)
        {
            Initialize();
        }

        OrderRuntimeState state = GetOrder(color);
        if (state == null || state.IsFulfilled)
        {
            if (verboseLogging)
            {
                Debug.Log($"Packed {color} ignored: no open order for that color.", this);
            }

            return false;
        }

        state.Consume();
        pendingImpacts++;

        if (verboseLogging)
        {
            Debug.Log($"Order {state.Index} ({color}) now needs {state.Remaining}.", this);
        }

        OrderConsumed?.Invoke(new OrderConsumedEvent(
            state.Index,
            color,
            state.Remaining,
            state.IsFulfilled,
            sourceWorldPosition));

        return true;
    }

    /// <summary>True when the level asks for locked blocks and still needs some.</summary>
    public bool WantsBlocks => blocksOrder != null && !blocksOrder.IsFulfilled;

    /// <summary>
    /// Called by Board the moment a locked block actually opens. A frozen blocker
    /// that has only cracked has not opened and must not count.
    ///
    /// Unlike a packed box this raises no flying effect: there is nothing to
    /// deliver, the block simply ceases to exist on the board. The panel reacts
    /// with the same hop the soda slots use, applied immediately, so the tick and
    /// the slot update stay in step without a streak in between.
    /// </summary>
    public bool TryConsumeOpenedBlock(Vector3 sourceWorldPosition)
    {
        if (!initialized)
        {
            Initialize();
        }

        if (blocksOrder == null || blocksOrder.IsFulfilled)
        {
            return false;
        }

        blocksOrder.Consume();
        pendingImpacts++;

        if (verboseLogging)
        {
            Debug.Log($"Blocks order now needs {blocksOrder.Remaining}.", this);
        }

        OrderConsumed?.Invoke(new OrderConsumedEvent(
            blocksOrder.Index,
            blocksOrder.Color,
            blocksOrder.Remaining,
            blocksOrder.IsFulfilled,
            sourceWorldPosition));

        return true;
    }

    /// <summary>
    /// Reported by the presentation once an effect has reached its slot and the
    /// displayed number has been updated. When the last one lands and every
    /// order reads zero, the level is won.
    /// </summary>
    public void NotifyImpactApplied()
    {
        pendingImpacts = Mathf.Max(0, pendingImpacts - 1);
        TryRaiseCompletion();
    }

    private void TryRaiseCompletion()
    {
        if (completionRaised || pendingImpacts > 0 || !AllOrdersLogicallyFulfilled)
        {
            return;
        }

        completionRaised = true;
        AllOrdersFulfilled?.Invoke();
    }
}
