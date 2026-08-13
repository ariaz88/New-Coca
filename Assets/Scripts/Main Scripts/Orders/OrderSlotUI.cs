using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One slot inside the Orders panel: a soda icon, the remaining count, and the
/// green tick that replaces the count once the order is filled.
///
/// The slot owns only its own presentation. It never decides whether an order is
/// complete; OrderManager does that and the slot is told what to display. That
/// keeps the visual delay of the flying effect from ever changing game state.
/// </summary>
[DisallowMultipleComponent]
public sealed class OrderSlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Draws the baked soda sprite for this order's color.")]
    private Image iconImage;

    [SerializeField, Tooltip("Moved and scaled by the impact punch. Usually the icon's own RectTransform.")]
    private RectTransform iconTransform;

    [SerializeField, Tooltip("Remaining count. Hidden and replaced by the tick when the order completes.")]
    private TextMeshProUGUI countText;

    [SerializeField, Tooltip("Green tick shown instead of the count once the order is filled.")]
    private GameObject checkmarkRoot;

    [SerializeField, Tooltip("Soft radial glow flashed on impact. Starts fully transparent.")]
    private Image glowImage;

    [Header("Impact Punch")]
    [SerializeField, Min(0f), Tooltip("How far the icon hops upward on impact, in canvas units.")]
    private float punchHeight = 14f;

    [SerializeField, Min(0.01f), Tooltip("Seconds for the icon to hop up and settle back.")]
    private float punchDuration = 0.3f;

    [SerializeField, Range(0f, 0.5f), Tooltip("Extra scale added at the top of the hop.")]
    private float punchScale = 0.18f;

    [Header("Impact Glow")]
    [SerializeField, Tooltip("Optional override. When empty a soft radial halo is generated at runtime.")]
    private Sprite glowSpriteOverride;

    [SerializeField, Min(0f), Tooltip("Halo diameter in canvas units. Kept inside the card so it cannot spill past the border.")]
    private float glowSize = 118f;

    [SerializeField, Range(0f, 1f), Tooltip("Peak opacity of the warm glow behind the icon.")]
    private float glowPeakAlpha = 0.8f;

    [SerializeField, Min(0.01f), Tooltip("Seconds from transparent to peak. Kept short so the hit reads as sharp.")]
    private float glowAttack = 0.08f;

    [SerializeField, Min(0.01f), Tooltip("Seconds from peak back to transparent. Longer than the attack so it fades softly.")]
    private float glowDecay = 0.45f;

    [SerializeField, Tooltip("Fallback halo color, used only when no OrderVfxSettings asset can be found.")]
    private Color glowColor = new Color(0.867f, 0.984f, 1f, 1f);

    [Header("Completion")]
    [SerializeField, Min(0.01f), Tooltip("Seconds for the green tick to scale up from zero.")]
    private float checkmarkPopDuration = 0.28f;

    [SerializeField, Range(0f, 1f), Tooltip("Icon opacity after its order is filled.")]
    private float fulfilledIconAlpha = 0.88f;

    [SerializeField, Tooltip("Optional override. When empty a real tick shape is generated at runtime.")]
    private Sprite checkmarkSpriteOverride;

    [SerializeField, Tooltip("Color of the generated tick.")]
    private Color checkmarkColor = new Color(0.24f, 0.72f, 0.26f, 1f);

    private Vector2 iconRestPosition;
    private Vector3 iconRestScale;
    private bool restCaptured;
    private Sequence punchSequence;
    private Sequence glowSequence;

    /// <summary>
    /// The shared palette, or null while none can be found.
    ///
    /// The halo colour and the punch shape are read from here rather than from the
    /// serialized fields above, because the panels in the five level scenes already
    /// have the old warm-gold values baked into their YAML. Reading from the asset
    /// means retuning the palette fixes every scene at once, and no scene file has
    /// to be hand-edited. The serialized fields remain as the fallback.
    /// </summary>
    private OrderVfxSettings Settings => OrderVfxSettings.Resolve();

    private Color ActiveGlowColor
    {
        get
        {
            OrderVfxSettings resolved = Settings;
            return resolved != null ? resolved.SlotGlowColor : glowColor;
        }
    }

    /// <summary>Which order this slot renders. Assigned by OrderPanelUI.</summary>
    public int OrderIndex { get; private set; } = -1;

    public Soda.SodaColor Color { get; private set; }

    /// <summary>
    /// World-space point the flying streak should aim at. The icon rather than
    /// the slot root, so the streak lands on the drink and not on empty card.
    /// </summary>
    public Vector3 ImpactWorldPosition =>
        iconTransform != null ? iconTransform.position : transform.position;

    private void Awake()
    {
        CaptureRestState();
    }

    private void OnDestroy()
    {
        punchSequence?.Kill();
        glowSequence?.Kill();
    }

    /// <summary>Fills the slot for one order at level start.</summary>
    public void Bind(int orderIndex, Soda.SodaColor color, Sprite icon, int remaining)
    {
        CaptureRestState();

        OrderIndex = orderIndex;
        Color = color;
        name = $"OrderSlot_{orderIndex}_{color}";

        if (iconImage != null)
        {
            iconImage.sprite = icon;

            // A missing baked sprite hides the graphic instead of drawing a white
            // box, so an unfinished SodaVisualLibrary is obvious but not ugly.
            iconImage.enabled = icon != null;
            iconImage.color = Color32Opaque(iconImage.color);

            // Set here rather than only in the builder, so panels created before
            // this existed are corrected without being rebuilt. A soda is taller
            // than it is wide; without this the square slot stretches it into a
            // rectangle.
            iconImage.preserveAspect = true;
            iconImage.type = Image.Type.Simple;
        }

        ApplyGeneratedVisuals();
        SetRemaining(remaining, false);
    }

    /// <summary>
    /// Replaces the placeholder art the panel builder created with the generated
    /// shapes.
    ///
    /// Applied at runtime rather than only in the builder so panels created by an
    /// earlier version of the tool are corrected without being rebuilt. Two
    /// things were wrong with the placeholders: the halo was Unity's rounded
    /// rectangle, which reads as a yellow box and had a hard edge that spilled
    /// past the card border, and the tick was a plain green square.
    /// </summary>
    private void ApplyGeneratedVisuals()
    {
        if (glowImage != null)
        {
            glowImage.sprite = glowSpriteOverride != null
                ? glowSpriteOverride
                : OrderVfxTextures.Glow;

            // Simple, not Sliced: a radial texture must scale as a whole, and a
            // sliced draw would stretch its middle row and column into bars.
            glowImage.type = Image.Type.Simple;
            glowImage.raycastTarget = false;
            glowImage.color = WithAlpha(ActiveGlowColor, 0f);
            glowImage.rectTransform.sizeDelta = new Vector2(glowSize, glowSize);
        }

        if (checkmarkRoot == null)
        {
            return;
        }

        Image checkImage = checkmarkRoot.GetComponent<Image>();
        if (checkImage != null)
        {
            checkImage.sprite = checkmarkSpriteOverride != null
                ? checkmarkSpriteOverride
                : OrderVfxTextures.Checkmark;
            checkImage.type = Image.Type.Simple;
            checkImage.preserveAspect = true;
            checkImage.raycastTarget = false;
            checkImage.color = checkmarkColor;
        }
    }

    /// <summary>
    /// Re-reads the icon's rest position and scale.
    ///
    /// The impact punch springs the icon back to a pose captured once, at Bind.
    /// Anything that moves the icon afterwards - OrderPanelSkin's relayout does -
    /// has to call this, or the first delivery will snap the icon back to where it
    /// used to be.
    /// </summary>
    public void RefreshRestState()
    {
        restCaptured = false;
        CaptureRestState();
    }

    /// <summary>
    /// Updates what the slot displays. <paramref name="animated"/> is false at
    /// level start so a level does not open with six tick animations.
    /// </summary>
    public void SetRemaining(int remaining, bool animated = true)
    {
        bool fulfilled = remaining <= 0;

        if (countText != null)
        {
            countText.text = remaining.ToString();
            countText.gameObject.SetActive(!fulfilled);
        }

        if (iconImage != null)
        {
            iconImage.color = WithAlpha(iconImage.color, fulfilled ? fulfilledIconAlpha : 1f);
        }

        if (checkmarkRoot == null)
        {
            return;
        }

        checkmarkRoot.SetActive(fulfilled);
        if (!fulfilled || !animated)
        {
            if (fulfilled)
            {
                checkmarkRoot.transform.localScale = Vector3.one;
            }

            return;
        }

        // Scale-from-zero pop, using unscaled time so the tick still animates if
        // an end panel has already frozen gameplay.
        checkmarkRoot.transform.localScale = Vector3.zero;
        checkmarkRoot.transform
            .DOScale(Vector3.one, checkmarkPopDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
    }

    /// <summary>
    /// Plays the arrival: a warm halo flashes behind the icon while the icon
    /// hops up and settles. Called the instant the streak reaches this slot.
    /// </summary>
    public void PlayImpact()
    {
        CaptureRestState();
        PlayGlow();
        PlayPunch();
    }

    private void PlayGlow()
    {
        if (glowImage == null)
        {
            return;
        }

        glowSequence?.Kill();
        glowImage.color = WithAlpha(glowColor, 0f);

        glowSequence = DOTween.Sequence().SetUpdate(true);
        glowSequence.Append(glowImage
            .DOFade(glowPeakAlpha, glowAttack)
            .SetEase(Ease.OutQuad));
        glowSequence.Append(glowImage
            .DOFade(0f, glowDecay)
            .SetEase(Ease.InQuad));
    }

    private void PlayPunch()
    {
        if (iconTransform == null)
        {
            return;
        }

        punchSequence?.Kill();

        // Reset before replaying so rapid consecutive impacts cannot accumulate
        // offset and leave the icon drifting off the card.
        iconTransform.anchoredPosition = iconRestPosition;
        iconTransform.localScale = iconRestScale;

        OrderVfxSettings resolved = Settings;

        float peak = resolved != null ? resolved.IconPunchPeak : 1f + punchScale;
        float dip = resolved != null ? resolved.IconPunchDip : 1f;
        float duration = resolved != null ? resolved.IconPunchDuration : punchDuration;

        // 1.0 -> peak -> dip -> 1.0. The dip is what makes the hit feel like an
        // impact rather than a swell: a punch that only grows and relaxes reads as
        // the icon breathing.
        float up = duration * 0.35f;
        float down = duration * 0.3f;
        float settle = duration * 0.35f;

        punchSequence = DOTween.Sequence().SetUpdate(true);

        punchSequence.Append(iconTransform
            .DOScale(iconRestScale * peak, up)
            .SetEase(Ease.OutQuad));
        punchSequence.Join(iconTransform
            .DOAnchorPosY(iconRestPosition.y + punchHeight, up)
            .SetEase(Ease.OutQuad));

        punchSequence.Append(iconTransform
            .DOScale(iconRestScale * dip, down)
            .SetEase(Ease.InOutQuad));
        punchSequence.Join(iconTransform
            .DOAnchorPosY(iconRestPosition.y, down)
            .SetEase(Ease.InQuad));

        punchSequence.Append(iconTransform
            .DOScale(iconRestScale, settle)
            .SetEase(Ease.OutBack));
    }

    private void CaptureRestState()
    {
        if (restCaptured || iconTransform == null)
        {
            return;
        }

        iconRestPosition = iconTransform.anchoredPosition;
        iconRestScale = iconTransform.localScale;
        restCaptured = true;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static Color Color32Opaque(Color color)
    {
        color.a = 1f;
        return color;
    }
}
