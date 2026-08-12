using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The object supply for the order effects: pooled UI Images, pooled TMP labels and
/// pooled trail ribbons, all parented to one full-screen layer.
///
/// Why pooling matters here specifically
/// -------------------------------------
/// A single delivery spawns a head made of three Images, roughly fifty short-lived
/// particle Images, and a ribbon. Packing several boxes in a row would allocate and
/// destroy hundreds of GameObjects a second on a phone. Renting from a stack means
/// the second delivery costs nothing at all, and the steady state is a few dozen
/// inactive objects.
///
/// This was previously two private stacks inside OrderVfxDirector. It is a separate
/// class now because the ribbon needs pooling too, and because the flight logic in
/// ObjectiveDeliveryVfx rents its own objects rather than going through the director.
/// </summary>
public sealed class OrderVfxPool
{
    private readonly RectTransform root;
    private readonly Material glowMaterial;

    private readonly Stack<Image> imagePool = new Stack<Image>();
    private readonly Stack<TextMeshProUGUI> labelPool = new Stack<TextMeshProUGUI>();
    private readonly Stack<OrderTrailRibbon> ribbonPool = new Stack<OrderTrailRibbon>();

    /// <summary>The layer every pooled object is parented to.</summary>
    public RectTransform Root => root;

    public OrderVfxPool(RectTransform root, Material glowMaterial)
    {
        this.root = root;
        this.glowMaterial = glowMaterial;
    }

    // ---------------------------------------------------------------- images

    /// <summary>
    /// An active, centre-anchored Image on the effect layer.
    ///
    /// <paramref name="additive"/> selects the glow material. It is off for the
    /// "+1" icon, which is real artwork and must render normally, and on for every
    /// light element, which must not look like a flat sticker.
    /// </summary>
    public Image RentImage(bool additive = true)
    {
        Image image = imagePool.Count > 0 ? imagePool.Pop() : CreateImage();

        image.material = additive ? glowMaterial : null;

        // Newest effect draws on top of older ones still fading out.
        image.transform.SetAsLastSibling();
        return image;
    }

    public void ReturnImage(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.gameObject.SetActive(false);
        image.rectTransform.localScale = Vector3.one;
        image.rectTransform.localRotation = Quaternion.identity;

        // Reset on return, not on rent. The same pooled Image serves as a head
        // core, a star, and a "+1" icon, and only the icon wants aspect preserved.
        // Leaving it on would squash the next head into its sprite's ratio.
        image.preserveAspect = false;
        image.sprite = null;
        image.material = null;
        image.color = Color.white;

        imagePool.Push(image);
    }

    private Image CreateImage()
    {
        GameObject created = new GameObject("OrderVfxImage", typeof(RectTransform));
        created.transform.SetParent(root, false);

        Image image = created.AddComponent<Image>();
        image.raycastTarget = false;

        // Centre-anchored, so anchoredPosition maps straight onto the canvas
        // coordinates the director's conversion helpers produce.
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        created.SetActive(false);
        return image;
    }

    // ---------------------------------------------------------------- labels

    public TextMeshProUGUI RentLabel()
    {
        TextMeshProUGUI label = labelPool.Count > 0 ? labelPool.Pop() : CreateLabel();
        label.transform.SetAsLastSibling();
        return label;
    }

    public void ReturnLabel(TextMeshProUGUI label)
    {
        if (label == null)
        {
            return;
        }

        label.gameObject.SetActive(false);
        label.rectTransform.localScale = Vector3.one;
        labelPool.Push(label);
    }

    private TextMeshProUGUI CreateLabel()
    {
        GameObject created = new GameObject("OrderVfxLabel", typeof(RectTransform));
        created.transform.SetParent(root, false);

        TextMeshProUGUI label = created.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        label.fontStyle = FontStyles.Bold;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(140f, 60f);

        created.SetActive(false);
        return label;
    }

    // --------------------------------------------------------------- ribbons

    public OrderTrailRibbon RentRibbon()
    {
        OrderTrailRibbon ribbon = ribbonPool.Count > 0 ? ribbonPool.Pop() : CreateRibbon();
        ribbon.transform.SetAsLastSibling();
        return ribbon;
    }

    public void ReturnRibbon(OrderTrailRibbon ribbon)
    {
        if (ribbon == null)
        {
            return;
        }

        // Cleared before deactivating: a ribbon that kept its samples would flash
        // the previous delivery's shape for one frame when it is next rented. Its
        // segments are separate pooled Images and go back too, or they would stay
        // checked out forever and every delivery would rent a fresh set.
        ribbon.Clear();
        ribbon.ReleaseSegments();
        ribbon.gameObject.SetActive(false);
        ribbonPool.Push(ribbon);
    }

    private OrderTrailRibbon CreateRibbon()
    {
        GameObject created = new GameObject("OrderVfxRibbon", typeof(RectTransform));
        created.transform.SetParent(root, false);

        // The ribbon is a controller, not a renderer: it positions pooled Image
        // segments that live directly on the effect layer, so this object only
        // needs to exist and tick.
        OrderTrailRibbon ribbon = created.AddComponent<OrderTrailRibbon>();

        created.SetActive(false);
        return ribbon;
    }
}
