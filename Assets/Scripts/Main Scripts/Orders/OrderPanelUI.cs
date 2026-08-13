using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anything that can play the travelling effect between a packed box and an
/// order slot. Implemented in a later phase by OrderVfxDirector.
///
/// The contract is deliberately small: play whatever you like, then call
/// <c>onImpact</c> at the exact frame the slot should tick down. OrderPanelUI
/// works with or without an implementation assigned, so the panel can be built
/// and tested before any VFX exist.
/// </summary>
public interface IOrderImpactPresenter
{
    void PlayImpact(OrderConsumedEvent consumedEvent, OrderSlotUI targetSlot, System.Action onImpact);
}

/// <summary>
/// Builds and drives the on-screen Orders card.
///
/// It creates one <see cref="OrderSlotUI"/> per order at level start, listens to
/// OrderManager, and updates the matching slot when a packed box is accepted.
/// If an <see cref="IOrderImpactPresenter"/> is present it hands the moment over
/// and waits for the callback, so the number ticks down when the streak lands
/// rather than the instant the box was packed. Without one, the update is
/// immediate, which is the useful state while the panel is being laid out.
/// </summary>
[DisallowMultipleComponent]
public sealed class OrderPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Template slot. May be an inactive child of this panel or a prefab asset.")]
    private OrderSlotUI slotPrefab;

    [SerializeField, Tooltip("Parent for the generated slots. Usually carries a HorizontalLayoutGroup.")]
    private RectTransform slotContainer;

    [SerializeField, Tooltip("Optional thin divider cloned between slots, matching the reference card.")]
    private GameObject separatorPrefab;

    [SerializeField, Tooltip("Maps a soda color to its baked icon and effect tint.")]
    private SodaVisualLibrary visualLibrary;

    [Header("Behaviour")]
    [SerializeField, Tooltip("Hides the whole card when the level defines no orders.")]
    private bool hideWhenEmpty = true;

    private readonly List<OrderSlotUI> slots = new List<OrderSlotUI>();
    private readonly List<GameObject> separators = new List<GameObject>();
    private OrderPanelSkin skin;
    private IOrderImpactPresenter impactPresenter;
    private OrderManager subscribedManager;

    /// <summary>Live slots in panel order. Used by the VFX director to aim.</summary>
    public IReadOnlyList<OrderSlotUI> Slots => slots;

    /// <summary>
    /// The assigned library, or one resolved automatically when the inspector
    /// reference was never set. Slots would otherwise silently render no drink.
    /// </summary>
    public SodaVisualLibrary VisualLibrary
    {
        get
        {
            if (visualLibrary == null)
            {
                visualLibrary = SodaVisualLibrary.Resolve();
            }

            return visualLibrary;
        }
    }

    private void Awake()
    {
        // Resolved from this object's hierarchy so a designer only has to drop
        // the director component onto the panel, with no extra wiring.
        impactPresenter = GetComponentInChildren<IOrderImpactPresenter>(true);

        // Added in code rather than serialized into each scene: the card's look is
        // generated at runtime, so adding the skin here restyles all 25 level
        // scenes without any of them having to be opened and re-saved.
        skin = GetComponent<OrderPanelSkin>();
        if (skin == null)
        {
            skin = gameObject.AddComponent<OrderPanelSkin>();
        }
    }

    private void OnEnable()
    {
        Subscribe(OrderManager.instance);
    }

    private void Start()
    {
        // OrderManager builds its list in Start too. Subscribing in OnEnable can
        // therefore happen before the manager exists, so the panel resolves it
        // again here and builds from whatever state is ready.
        Subscribe(OrderManager.instance);
        Rebuild();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>
    /// Lets a later-phase director register itself explicitly instead of relying
    /// on the hierarchy search.
    /// </summary>
    public void SetImpactPresenter(IOrderImpactPresenter presenter)
    {
        impactPresenter = presenter;
    }

    /// <summary>Finds the slot rendering a color, or null when unordered.</summary>
    public OrderSlotUI GetSlot(Soda.SodaColor color)
    {
        for (int index = 0; index < slots.Count; index++)
        {
            if (slots[index] != null && slots[index].Color == color)
            {
                return slots[index];
            }
        }

        return null;
    }

    private void Subscribe(OrderManager manager)
    {
        if (manager == null || manager == subscribedManager)
        {
            return;
        }

        Unsubscribe();
        subscribedManager = manager;
        subscribedManager.OrdersInitialized += Rebuild;
        subscribedManager.OrderConsumed += HandleOrderConsumed;
    }

    private void Unsubscribe()
    {
        if (subscribedManager == null)
        {
            return;
        }

        subscribedManager.OrdersInitialized -= Rebuild;
        subscribedManager.OrderConsumed -= HandleOrderConsumed;
        subscribedManager = null;
    }

    /// <summary>Destroys the current slots and recreates them from the manager.</summary>
    private void Rebuild()
    {
        ClearSlots();

        OrderManager manager = subscribedManager != null ? subscribedManager : OrderManager.instance;
        if (manager == null || slotPrefab == null || slotContainer == null)
        {
            if (slotPrefab == null || slotContainer == null)
            {
                Debug.LogError(
                    "OrderPanelUI needs both a slot template and a slot container. " +
                    "Use Tools > Coca Sorting > Create Orders Panel to generate a valid one.",
                    this);
            }

            return;
        }

        IReadOnlyList<OrderRuntimeState> orders = manager.Orders;
        for (int index = 0; index < orders.Count; index++)
        {
            OrderRuntimeState state = orders[index];

            if (index > 0 && separatorPrefab != null)
            {
                GameObject separator = Instantiate(separatorPrefab, slotContainer);
                separator.SetActive(true);
                separators.Add(separator);
            }

            SodaVisualLibrary library = VisualLibrary;

            OrderSlotUI slot = Instantiate(slotPrefab, slotContainer);
            slot.gameObject.SetActive(true);
            slot.Bind(
                state.Index,
                state.Color,
                library != null ? library.GetIcon(state.Color) : null,
                state.Remaining);
            slots.Add(slot);
        }

        // Slots are instantiated fresh here, so the chip art has to be applied
        // after the rebuild rather than only once at Awake.
        skin?.RefreshSlots();

        if (hideWhenEmpty)
        {
            // Only the card's own children are toggled; disabling this component's
            // GameObject would stop it from ever rebuilding on a retry.
            SetCardVisible(orders.Count > 0);
        }
    }

    private void HandleOrderConsumed(OrderConsumedEvent consumedEvent)
    {
        OrderSlotUI slot = FindSlotByIndex(consumedEvent.OrderIndex);
        if (slot == null)
        {
            // The order exists but has no visible slot. The count still has to be
            // acknowledged or the level would never register as complete.
            OrderManager.instance?.NotifyImpactApplied();
            return;
        }

        if (impactPresenter == null)
        {
            ApplyImpact(slot, consumedEvent);
            return;
        }

        impactPresenter.PlayImpact(consumedEvent, slot, () => ApplyImpact(slot, consumedEvent));
    }

    /// <summary>
    /// The single place where a slot's number actually changes. Both the direct
    /// path and the VFX callback funnel through here so the two can never drift.
    /// </summary>
    private void ApplyImpact(OrderSlotUI slot, OrderConsumedEvent consumedEvent)
    {
        if (slot != null)
        {
            slot.PlayImpact();
            slot.SetRemaining(consumedEvent.RemainingAfter);
        }

        OrderManager.instance?.NotifyImpactApplied();
    }

    private OrderSlotUI FindSlotByIndex(int orderIndex)
    {
        for (int index = 0; index < slots.Count; index++)
        {
            if (slots[index] != null && slots[index].OrderIndex == orderIndex)
            {
                return slots[index];
            }
        }

        return null;
    }

    private void ClearSlots()
    {
        for (int index = 0; index < slots.Count; index++)
        {
            if (slots[index] != null)
            {
                Destroy(slots[index].gameObject);
            }
        }

        for (int index = 0; index < separators.Count; index++)
        {
            if (separators[index] != null)
            {
                Destroy(separators[index]);
            }
        }

        slots.Clear();
        separators.Clear();
    }

    private void SetCardVisible(bool visible)
    {
        for (int index = 0; index < transform.childCount; index++)
        {
            GameObject child = transform.GetChild(index).gameObject;

            // The templates live in the hierarchy but must stay inactive, or the
            // panel would show an empty placeholder slot next to the real ones.
            if (IsTemplateObject(child))
            {
                continue;
            }

            child.SetActive(visible);
        }
    }

    private bool IsTemplateObject(GameObject candidate)
    {
        if (slotPrefab != null && candidate == slotPrefab.gameObject)
        {
            return true;
        }

        return separatorPrefab != null && candidate == separatorPrefab;
    }
}
