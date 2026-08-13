using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Restyles the Orders card at runtime.
///
/// The card was originally generated with Unity's built-in flat sprites as
/// placeholder art - a hard red rectangle around a cream fill - and that
/// placeholder shipped into all 25 level scenes. Rather than hand-editing 25
/// scenes, this component rebuilds the card's look in code from
/// OrderPanelTextures, exactly as OrderSlotUI already does for its own glow and
/// tick. Restyling therefore reaches every scene at once, and the panel builder
/// stays untouched.
///
/// It only ever changes appearance - sprites, colours, sizes, and a few
/// decorative children. Layout structure, the slot template and every serialized
/// reference OrderPanelUI and OrderVfxDirector rely on are left alone, so the
/// delivery VFX still lands pixel-exact on each slot icon.
/// </summary>
[DisallowMultipleComponent]
public sealed class OrderPanelSkin : MonoBehaviour
{
    [Header("Card")]
    [SerializeField] private Color cardTop = new Color(0.16f, 0.18f, 0.38f, 1f);
    [SerializeField] private Color cardBottom = new Color(0.09f, 0.10f, 0.24f, 1f);
    [SerializeField] private Color cardRim = new Color(1f, 0.78f, 0.32f, 1f);
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0.06f, 0.42f);

    [Header("Header ribbon")]
    [SerializeField] private Color ribbonTop = new Color(1f, 0.82f, 0.36f, 1f);
    [SerializeField] private Color ribbonBottom = new Color(0.98f, 0.60f, 0.15f, 1f);
    [SerializeField] private Color ribbonTextColor = new Color(0.24f, 0.13f, 0.02f, 1f);

    [Header("Slots")]
    [SerializeField, Tooltip("Recessed well behind each drink. Darker than the card, so the chip reads as cut into it.")]
    private Color chipColor = new Color(0.05f, 0.05f, 0.15f, 0.55f);
    [SerializeField, Tooltip("Thin lit edge around the chip.")]
    private Color chipRim = new Color(1f, 1f, 1f, 0.13f);
    [SerializeField] private Color badgeColor = new Color(0.05f, 0.05f, 0.14f, 0.9f);
    [SerializeField] private Color countColor = new Color(1f, 0.94f, 0.82f, 1f);

    [Header("Layout")]
    [SerializeField] private Vector2 cardSize = new Vector2(392f, 186f);

    [SerializeField, Tooltip("Distance from the top of the screen to the top of the card, in canvas units. Sits in the strip the hidden progress bar used to occupy, between the level label and the coin/gem row.")]
    private float cardTopMargin = 150f;

    [SerializeField, Min(0f), Tooltip("Gap between the outermost chip and the edge of the card, in canvas units. The card is sized from its contents plus twice this.")]
    private float slotEdgeMargin = 5f;
    [SerializeField] private Vector2 ribbonSize = new Vector2(196f, 56f);

    [SerializeField, Tooltip("Taller than it is wide: the drink stands upright, so a square chip left no room between the bottle and its count badge.")]
    private Vector2 chipSize = new Vector2(104f, 118f);

    private bool applied;

    private void Awake()
    {
        Apply();
    }

    /// <summary>
    /// Applies the skin. Safe to call more than once - every generated child is
    /// found by name and reused, so a second call restyles rather than duplicates.
    /// </summary>
    public void Apply()
    {
        RectTransform panel = transform as RectTransform;
        if (panel == null)
        {
            return;
        }

        panel.anchorMin = new Vector2(0.5f, 1f);
        panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -cardTopMargin);
        panel.sizeDelta = cardSize;

        StyleShadow(panel);
        StyleCard(panel);
        StyleRibbon(panel);
        StyleSlotContainer(panel);

        applied = true;
    }

    // ------------------------------------------------------------------ card

    private void StyleShadow(RectTransform panel)
    {
        // Sits behind everything, offset down, so the card reads as raised.
        RectTransform shadow = EnsureChild(panel, "CardShadow", 0);
        Image image = EnsureImage(shadow);
        image.sprite = OrderPanelTextures.Shadow;
        image.type = Image.Type.Sliced;
        image.color = shadowColor;
        image.raycastTarget = false;

        Stretch(shadow);
        shadow.offsetMin = new Vector2(-14f, -22f);
        shadow.offsetMax = new Vector2(14f, 6f);
    }

    private void StyleCard(RectTransform panel)
    {
        // The builder's "Border" and its "Fill" child are reused as the card body
        // and its gradient, so nothing that references them by path breaks.
        RectTransform border = panel.Find("Border") as RectTransform;
        if (border == null)
        {
            border = EnsureChild(panel, "Border", 1);
        }

        Image borderImage = EnsureImage(border);
        borderImage.sprite = OrderPanelTextures.Card;
        borderImage.type = Image.Type.Sliced;
        borderImage.color = cardBottom;
        borderImage.raycastTarget = false;
        Stretch(border);

        RectTransform fill = border.Find("Fill") as RectTransform;
        if (fill == null)
        {
            fill = EnsureChild(border, "Fill", 0);
        }

        // Inset by a few pixels so a sliver of the darker body shows all the way
        // round, which reads as thickness.
        Image fillImage = EnsureImage(fill);
        fillImage.sprite = OrderPanelTextures.Card;
        fillImage.type = Image.Type.Sliced;
        fillImage.color = cardTop;
        fillImage.raycastTarget = false;
        Stretch(fill);
        fill.offsetMin = new Vector2(4f, 4f);
        fill.offsetMax = new Vector2(-4f, -4f);

        // A thin lit line just inside the top edge.
        //
        // This replaced a large gloss panel. Any big translucent overlay has to end
        // somewhere, and a straight-edged gradient sprite ended in a clearly
        // visible rectangle across the card. A 3px rounded line has no flat area
        // for an edge to show in, and reads as a lit rim on a moulded surface,
        // which is the effect the gloss was reaching for anyway.
        RectTransform sheen = EnsureChild(border, "CardSheen", 1);
        Image sheenImage = EnsureImage(sheen);
        sheenImage.sprite = OrderPanelTextures.Pill;
        sheenImage.type = Image.Type.Sliced;
        sheenImage.color = new Color(1f, 1f, 1f, 0.16f);
        sheenImage.raycastTarget = false;
        sheen.anchorMin = new Vector2(0f, 1f);
        sheen.anchorMax = new Vector2(1f, 1f);
        sheen.pivot = new Vector2(0.5f, 1f);
        sheen.offsetMin = new Vector2(26f, 0f);
        sheen.offsetMax = new Vector2(-26f, 0f);
        sheen.sizeDelta = new Vector2(sheen.sizeDelta.x, 3f);
        sheen.anchoredPosition = new Vector2(0f, -9f);

        RectTransform rim = EnsureChild(border, "CardRim", 2);
        Image rimImage = EnsureImage(rim);
        rimImage.sprite = OrderPanelTextures.CardBorder;
        rimImage.type = Image.Type.Sliced;
        rimImage.color = cardRim;
        rimImage.raycastTarget = false;
        Stretch(rim);
    }

    private void StyleRibbon(RectTransform panel)
    {
        RectTransform ribbon = panel.Find("LabelTab") as RectTransform;
        if (ribbon == null)
        {
            return;
        }

        ribbon.SetAsLastSibling();
        ribbon.anchorMin = new Vector2(0.5f, 1f);
        ribbon.anchorMax = new Vector2(0.5f, 1f);
        ribbon.pivot = new Vector2(0.5f, 0.5f);
        ribbon.sizeDelta = ribbonSize;
        ribbon.anchoredPosition = new Vector2(0f, 2f);

        // Deeper amber outer pill with a brighter inner pill inset inside it: two
        // rounded shapes rather than a rounded shape with a square gradient laid
        // over it, so no straight edge is ever visible.
        Image ribbonImage = EnsureImage(ribbon);
        ribbonImage.sprite = OrderPanelTextures.Pill;
        ribbonImage.type = Image.Type.Sliced;
        ribbonImage.color = ribbonBottom;
        ribbonImage.raycastTarget = false;

        RectTransform ribbonShade = EnsureChild(ribbon, "RibbonShade", 0);
        Image shadeImage = EnsureImage(ribbonShade);
        shadeImage.sprite = OrderPanelTextures.Pill;
        shadeImage.type = Image.Type.Sliced;
        shadeImage.color = ribbonTop;
        shadeImage.raycastTarget = false;
        Stretch(ribbonShade);
        ribbonShade.offsetMin = new Vector2(4f, 4f);
        ribbonShade.offsetMax = new Vector2(-4f, -7f);

        // Highlight line along the ribbon's top, matching the card's.
        RectTransform ribbonSheen = EnsureChild(ribbon, "RibbonSheen", 1);
        Image ribbonSheenImage = EnsureImage(ribbonSheen);
        ribbonSheenImage.sprite = OrderPanelTextures.Pill;
        ribbonSheenImage.type = Image.Type.Sliced;
        ribbonSheenImage.color = new Color(1f, 1f, 1f, 0.38f);
        ribbonSheenImage.raycastTarget = false;
        ribbonSheen.anchorMin = new Vector2(0f, 1f);
        ribbonSheen.anchorMax = new Vector2(1f, 1f);
        ribbonSheen.pivot = new Vector2(0.5f, 1f);
        ribbonSheen.offsetMin = new Vector2(22f, 0f);
        ribbonSheen.offsetMax = new Vector2(-22f, 0f);
        ribbonSheen.sizeDelta = new Vector2(ribbonSheen.sizeDelta.x, 3f);
        ribbonSheen.anchoredPosition = new Vector2(0f, -9f);

        TextMeshProUGUI label = ribbon.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = "ORDERS";
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 6f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = ribbonTextColor;
            label.raycastTarget = false;

            // Auto-sized rather than fixed: the ribbon narrows with the card, and a
            // one-order level's card is small enough that a fixed 30pt "ORDERS"
            // spilled out past both ends of the pill.
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 30f;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;

            label.rectTransform.SetAsLastSibling();
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(16f, 4f);
            label.rectTransform.offsetMax = new Vector2(-16f, -4f);
        }
    }

    // ----------------------------------------------------------------- slots

    private void StyleSlotContainer(RectTransform panel)
    {
        RectTransform container = panel.Find("SlotContainer") as RectTransform;
        if (container == null)
        {
            return;
        }

        Stretch(container);
        container.offsetMin = new Vector2(slotEdgeMargin, 12f);
        container.offsetMax = new Vector2(-slotEdgeMargin, -26f);

        const float slotSpacing = 10f;
        HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = slotSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
        }

        // The card sizes itself to its contents. Levels carry between one and four
        // orders, and a card wide enough for four leaves a single order stranded in
        // the middle of an empty panel, while a card sized for two clips at four.
        // Counted from the panel's own list, not by walking the container.
        //
        // OrderPanelUI rebuilds its slots more than once during start-up, and
        // Destroy is deferred to the end of the frame - so a walk of the children
        // sees the outgoing slots alongside the incoming ones. That counted six
        // slots for a three-order level and produced a card twice as wide as it
        // should be. The panel's list only ever holds the live slots.
        OrderPanelUI panelUI = GetComponent<OrderPanelUI>();
        int slotCount;

        if (panelUI != null && panelUI.Slots != null)
        {
            slotCount = panelUI.Slots.Count;
        }
        else
        {
            slotCount = 0;
            foreach (Transform child in container)
            {
                if (child.gameObject.activeSelf && child.GetComponent<OrderSlotUI>() != null)
                {
                    slotCount++;
                }
            }
        }

        if (slotCount > 0)
        {
            // Hugs its contents: card width is exactly the chips plus one small
            // margin each side. The card previously carried a wide dead zone at
            // both ends that made the orders look stranded in the middle of it.
            float contentWidth = slotCount * chipSize.x + (slotCount - 1) * slotSpacing;

            // The floor exists only for the ribbon. A single-order level would
            // otherwise produce a card narrower than the word "ORDERS", and the
            // auto-sized label would shrink to something unreadable.
            float width = Mathf.Max(contentWidth + slotEdgeMargin * 2f, 200f);
            panel.sizeDelta = new Vector2(width, cardSize.y);

            RectTransform ribbon = panel.Find("LabelTab") as RectTransform;
            if (ribbon != null)
            {
                ribbon.sizeDelta = new Vector2(
                    Mathf.Min(ribbonSize.x, width - 40f), ribbonSize.y);
            }
        }

        foreach (Transform child in container)
        {
            OrderSlotUI slot = child.GetComponent<OrderSlotUI>();
            if (slot != null)
            {
                StyleSlot(child as RectTransform);
                continue;
            }

            // The builder's divider bars are removed rather than restyled: once each
            // order sits in its own chip the chips already separate them, and a
            // 3px-wide rounded pill squashed between them renders as a lens shape.
            //
            // Deactivated, not merely un-rendered. An invisible separator still
            // occupies its width in the HorizontalLayoutGroup, which pushed the
            // last chip out past the edge of the card.
            if (child.GetComponent<Image>() != null)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Gives each order its own recessed chip. The chip is inserted BEHIND the
    /// slot's existing children so the icon, count, tick and impact glow keep
    /// their exact positions - OrderVfxDirector aims at the icon's world position,
    /// and moving it would make every delivery land off-target.
    /// </summary>
    private void StyleSlot(RectTransform slot)
    {
        if (slot == null)
        {
            return;
        }

        slot.sizeDelta = chipSize;

        RectTransform chip = EnsureChild(slot, "ChipBackground", 0);
        Image chipImage = EnsureImage(chip);
        chipImage.sprite = OrderPanelTextures.Chip;
        chipImage.type = Image.Type.Sliced;
        chipImage.color = chipColor;
        chipImage.raycastTarget = false;
        Stretch(chip);
        chip.offsetMin = new Vector2(2f, 2f);
        chip.offsetMax = new Vector2(-2f, -2f);

        RectTransform chipEdge = EnsureChild(slot, "ChipRim", 1);
        Image edgeImage = EnsureImage(chipEdge);
        edgeImage.sprite = OrderPanelTextures.CardBorder;
        edgeImage.type = Image.Type.Sliced;
        edgeImage.color = chipRim;
        edgeImage.raycastTarget = false;
        Stretch(chipEdge);
        chipEdge.offsetMin = new Vector2(2f, 2f);
        chipEdge.offsetMax = new Vector2(-2f, -2f);

        LayOutSlotContents(slot);
    }

    /// <summary>
    /// Repositions the icon, count and tick inside the chip.
    ///
    /// The builder stacked the count directly over the drink, so the numeral sat
    /// on top of the bottle and both were hard to read. The icon moves up into the
    /// chip and the count drops into its own badge on the lower edge, which also
    /// gives the completed tick somewhere unambiguous to live.
    /// </summary>
    private void LayOutSlotContents(RectTransform slot)
    {
        OrderSlotUI slotUI = slot.GetComponent<OrderSlotUI>();

        RectTransform icon = null;
        RectTransform glow = null;
        RectTransform check = null;
        TextMeshProUGUI count = null;

        foreach (Transform child in slot)
        {
            string childName = child.name;
            if (childName == "ChipBackground" || childName == "ChipRim" || childName == "CountBadge")
            {
                continue;
            }

            // Name is tested before component type: in the shipped slot template
            // the tick is a plain container whose Image lives on a child, so
            // requiring an Image here would silently skip it and leave the tick
            // sitting at the builder's old position.
            if (childName.Contains("Check"))
            {
                check = child as RectTransform;
                continue;
            }

            if (childName.Contains("Glow"))
            {
                glow = child as RectTransform;
                continue;
            }

            if (count == null)
            {
                TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
                if (text != null)
                {
                    count = text;
                    continue;
                }
            }

            if (icon == null && child.GetComponent<Image>() != null)
            {
                icon = child as RectTransform;
            }
        }

        if (icon != null)
        {
            Centre(icon);
            icon.sizeDelta = new Vector2(76f, 76f);
            icon.anchoredPosition = new Vector2(0f, 20f);

            Image iconImage = icon.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.preserveAspect = true;
            }
        }

        if (glow != null)
        {
            Centre(glow);
            glow.anchoredPosition = new Vector2(0f, 14f);
            glow.SetSiblingIndex(2);
        }

        // The badge is drawn behind whichever of count/tick is showing, and hangs
        // off the chip's bottom edge so it reads as a separate token.
        RectTransform badge = EnsureChild(slot, "CountBadge", slot.childCount);
        Image badgeImage = EnsureImage(badge);
        badgeImage.sprite = OrderPanelTextures.Pill;
        badgeImage.type = Image.Type.Sliced;
        badgeImage.color = badgeColor;
        badgeImage.raycastTarget = false;
        Centre(badge);
        badge.sizeDelta = new Vector2(56f, 38f);
        badge.anchoredPosition = new Vector2(0f, -42f);

        if (count != null)
        {
            Centre(count.rectTransform);
            count.rectTransform.sizeDelta = new Vector2(56f, 38f);
            count.rectTransform.anchoredPosition = new Vector2(0f, -42f);
            count.rectTransform.SetAsLastSibling();
            count.color = countColor;
            count.fontStyle = FontStyles.Bold;
            count.fontSize = 30f;
            count.alignment = TextAlignmentOptions.Center;
            count.raycastTarget = false;
        }

        if (check != null)
        {
            Centre(check);
            check.sizeDelta = new Vector2(34f, 34f);
            check.anchoredPosition = new Vector2(0f, -42f);
            check.SetAsLastSibling();
        }

        // The rest pose the impact punch springs back to was captured before this
        // relayout, so it has to be re-read or the first delivery snaps the icon
        // back to the builder's old position.
        slotUI?.RefreshRestState();
    }

    private static void Centre(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    // ---------------------------------------------------------------- helpers

    private static RectTransform EnsureChild(Transform parent, string childName, int siblingIndex)
    {
        Transform existing = parent.Find(childName);
        RectTransform rect;

        if (existing != null)
        {
            rect = existing as RectTransform;
            if (rect == null)
            {
                return null;
            }
        }
        else
        {
            GameObject created = new GameObject(childName, typeof(RectTransform));
            rect = created.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
        }

        if (siblingIndex >= 0 && siblingIndex < parent.childCount)
        {
            rect.SetSiblingIndex(siblingIndex);
        }

        return rect;
    }

    private static Image EnsureImage(RectTransform target)
    {
        Image image = target.GetComponent<Image>();
        return image != null ? image : target.gameObject.AddComponent<Image>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>Re-applies after the panel rebuilds its slots on a retry.</summary>
    public void RefreshSlots()
    {
        if (!applied)
        {
            Apply();
            return;
        }

        RectTransform panel = transform as RectTransform;
        if (panel != null)
        {
            StyleSlotContainer(panel);
        }
    }
}
