using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays the sequence that connects a packed box on the board to its order slot
/// in the panel. This is stages 3 to 7 of the reference effect; stages 1 and 2,
/// closing and lifting the box, are already handled by the existing gameplay.
///
///   3. "+1" label rises from where the box was and fades out.
///   4. A tinted comet flies from that point up to the slot, along a curved
///      path, shedding sparkles behind it.
///   5. Arrival. The director calls back, and the panel flashes the halo and
///      decrements the number.
///   6. The slot's icon hops. Owned by OrderSlotUI, not by this script.
///   7. The remaining sparkles finish falling and everything returns to a pool.
///
/// Why this is done in UI space
/// ----------------------------
/// The effect starts on a 3D board and has to land exactly on a UI element. Any
/// world-space implementation would have to convert the target back every frame
/// and would still sort awkwardly against the canvas. Converting once, at the
/// start, into the canvas's own coordinates makes the landing pixel-exact and
/// removes all sorting questions.
///
/// Why plain Images instead of a ParticleSystem
/// --------------------------------------------
/// Unity's particle systems do not render inside a Canvas without an extra
/// package. Pooled Image objects give the same look here, sort correctly with
/// the rest of the UI for free, and keep the whole effect in one hierarchy.
/// </summary>
[DisallowMultipleComponent]
public sealed class OrderVfxDirector : MonoBehaviour, IOrderImpactPresenter
{
    [Header("References")]
    [SerializeField, Tooltip("Camera that renders the board. Empty uses Camera.main.")]
    private Camera boardCamera;

    [SerializeField, Tooltip("Full-screen RectTransform the effects are parented to. Created automatically when empty.")]
    private RectTransform effectRoot;

    [SerializeField, Tooltip("Maps a soda color to its tint. Falls back to built-in colors when empty.")]
    private SodaVisualLibrary visualLibrary;

    [Header("Stage 3 - Plus One Label")]
    [SerializeField, Tooltip("Turn off to skip the +1 label entirely.")]
    private bool showPlusOneLabel = true;

    [SerializeField, Min(0.01f)] private float plusOneDuration = 0.7f;
    [SerializeField, Tooltip("How far the label rises, in canvas units.")]
    private float plusOneRise = 80f;
    [SerializeField, Min(1f)] private float plusOneFontSize = 52f;

    [SerializeField, Tooltip("Draws the drink's icon next to the +1, as in the reference.")]
    private bool showPlusOneIcon = true;

    [SerializeField, Tooltip("Height of that icon, in canvas units.")]
    private float plusOneIconSize = 76f;

    [SerializeField, Tooltip("Distance from the centre of the text to the centre of the icon.")]
    private float plusOneIconGap = 92f;

    [Header("Stage 4 - Travelling Streak")]
    [SerializeField, Min(0f), Tooltip("Pause after packing before the streak launches, so the +1 is read first.")]
    private float streakDelay = 0.22f;

    [SerializeField, Min(0.05f)] private float streakDuration = 0.5f;

    [SerializeField, Tooltip("Sideways bow of the flight path, as a fraction of its length. 0 is a straight line.")]
    [Range(-0.6f, 0.6f)]
    private float pathCurvature = 0.3f;

    [SerializeField, Tooltip("Length and thickness of the comet, in canvas units.")]
    private Vector2 streakSize = new Vector2(300f, 84f);

    [SerializeField, Range(0f, 1f), Tooltip("Peak opacity of the comet.")]
    private float streakAlpha = 1f;

    [SerializeField, Tooltip("Draws a narrower white-hot core inside the tinted comet, so it reads as light rather than as a smear.")]
    private bool useHotCore = true;

    [SerializeField, Range(0.1f, 1f), Tooltip("Size of that core relative to the comet.")]
    private float hotCoreScale = 0.52f;

    [SerializeField, Tooltip("Optional override for the generated comet sprite.")]
    private Sprite streakSpriteOverride;

    [Header("Stage 4 - Sparkles")]
    [SerializeField, Min(0), Tooltip("Sparkles shed along the flight. 0 disables them.")]
    private int sparkleCount = 24;

    [SerializeField] private Vector2 sparkleSizeRange = new Vector2(16f, 40f);
    [SerializeField, Min(0.05f)] private float sparkleLifetime = 0.5f;
    [SerializeField, Tooltip("How far a sparkle drifts sideways from the path before fading.")]
    private float sparkleScatter = 34f;
    [SerializeField, Tooltip("Downward drift applied over a sparkle's life, so the trail settles.")]
    private float sparkleFall = 40f;
    [SerializeField, Tooltip("Optional override for the generated sparkle sprite.")]
    private Sprite sparkleSpriteOverride;

    [Header("Stage 5 - Impact Star Burst")]
    [SerializeField, Min(0), Tooltip("Bright stars thrown outward from the slot on arrival. 0 disables the burst.")]
    private int burstStarCount = 12;

    [SerializeField] private Vector2 burstStarSizeRange = new Vector2(14f, 34f);
    [SerializeField, Min(0.05f)] private float burstLifetime = 0.55f;
    [SerializeField, Tooltip("How far the stars travel from the slot.")]
    private Vector2 burstRadiusRange = new Vector2(30f, 95f);
    [SerializeField, Tooltip("Color of the burst stars. Warm yellow matches the impact halo.")]
    private Color burstColor = new Color(1f, 0.93f, 0.55f, 1f);
    [SerializeField, Tooltip("Optional override for the generated star sprite.")]
    private Sprite starSpriteOverride;

    [Header("Audio")]
    [SerializeField, Tooltip("Optional clip played when the streak reaches its slot.")]
    private AudioClip impactClip;

    [SerializeField, Range(0f, 1f)] private float impactVolume = 0.7f;

    private Canvas parentCanvas;
    private Camera canvasCamera;
    private readonly Stack<Image> imagePool = new Stack<Image>();
    private readonly Stack<TextMeshProUGUI> labelPool = new Stack<TextMeshProUGUI>();

    private void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            parentCanvas = parentCanvas.rootCanvas;

            // Overlay canvases convert with a null camera; every other mode needs
            // the canvas's own camera. Getting this wrong offsets every effect.
            canvasCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : parentCanvas.worldCamera;
        }

        EnsureEffectRoot();
    }

    /// <summary>
    /// IOrderImpactPresenter. Starts the sequence and guarantees that
    /// <paramref name="onImpact"/> runs exactly once, even if the effect cannot
    /// be played at all. Skipping it would leave the order stuck mid-flight and
    /// the level unable to complete.
    /// </summary>
    public void PlayImpact(OrderConsumedEvent consumedEvent, OrderSlotUI targetSlot, System.Action onImpact)
    {
        if (targetSlot == null || effectRoot == null || !isActiveAndEnabled)
        {
            onImpact?.Invoke();
            return;
        }

        StartCoroutine(PlaySequence(consumedEvent, targetSlot, onImpact));
    }

    private IEnumerator PlaySequence(
        OrderConsumedEvent consumedEvent,
        OrderSlotUI targetSlot,
        System.Action onImpact)
    {
        // Same auto-resolve as the panel: an unassigned inspector reference
        // must not silently turn every trail white.
        if (visualLibrary == null)
        {
            visualLibrary = SodaVisualLibrary.Resolve();
        }

        Color tint = visualLibrary != null
            ? visualLibrary.GetEffectColor(consumedEvent.Color)
            : SodaVisualLibrary.DefaultColorFor(consumedEvent.Color);

        Vector2 origin = WorldToEffectSpace(consumedEvent.SourceWorldPosition, ResolveBoardCamera());

        if (showPlusOneLabel)
        {
            Sprite icon = showPlusOneIcon && visualLibrary != null
                ? visualLibrary.GetIcon(consumedEvent.Color)
                : null;

            StartCoroutine(PlayPlusOneLabel(origin, tint, icon));
        }

        if (streakDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(streakDelay);
        }

        // Resolved after the delay: the panel may have been rebuilt, and the
        // target could have moved with a layout pass.
        if (targetSlot == null)
        {
            onImpact?.Invoke();
            yield break;
        }

        Vector2 target = UiWorldToEffectSpace(targetSlot.ImpactWorldPosition);
        yield return PlayStreak(origin, target, tint);

        PlayImpactBurst(target);
        PlayImpactSound();

        // Stage 5 and 6 belong to the panel and the slot. The director's job ends
        // the moment the comet arrives.
        onImpact?.Invoke();
    }

    /// <summary>
    /// The "+1" and, beside it, the drink's own icon, exactly as the reference
    /// shows. The two are separate pooled objects moved by the same offset each
    /// frame rather than a parented pair, because the pools stay flat and the
    /// icon is optional.
    /// </summary>
    private IEnumerator PlayPlusOneLabel(Vector2 origin, Color tint, Sprite icon)
    {
        TextMeshProUGUI label = RentLabel();
        RectTransform labelRect = label.rectTransform;
        labelRect.localScale = Vector3.one;

        label.text = "+1";
        label.fontSize = plusOneFontSize;
        label.color = Color.white;

        // A dark outline, not a tinted one. The board is a light blue grid, so a
        // pale outline left the white text barely readable; the drink's identity
        // is already carried by the icon beside it and by the streak.
        label.outlineColor = new Color(0.1f, 0.08f, 0.06f, 1f);
        label.outlineWidth = 0.32f;
        label.gameObject.SetActive(true);

        Image iconImage = null;
        if (icon != null)
        {
            iconImage = RentImage();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.type = Image.Type.Simple;
            iconImage.color = Color.white;
            iconImage.rectTransform.sizeDelta = new Vector2(plusOneIconSize, plusOneIconSize);
            iconImage.rectTransform.localRotation = Quaternion.identity;
            iconImage.rectTransform.localScale = Vector3.one;
            iconImage.gameObject.SetActive(true);
        }

        // The pair is centred on the origin, so adding an icon does not shift the
        // "+1" away from where the box actually was.
        float halfGap = iconImage != null ? plusOneIconGap * 0.5f : 0f;
        Vector2 labelOffset = new Vector2(-halfGap, 0f);
        Vector2 iconOffset = new Vector2(halfGap, 0f);

        float elapsed = 0f;
        while (elapsed < plusOneDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / plusOneDuration);

            Vector2 rise = new Vector2(0f, plusOneRise * EaseOutCubic(t));

            // Held fully opaque for the first half so the number is readable,
            // then faded. A linear fade from zero reads as a flicker.
            float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;

            labelRect.anchoredPosition = origin + labelOffset + rise;
            label.color = new Color(1f, 1f, 1f, alpha);

            if (iconImage != null)
            {
                iconImage.rectTransform.anchoredPosition = origin + iconOffset + rise;
                iconImage.color = new Color(1f, 1f, 1f, alpha);
            }

            yield return null;
        }

        ReturnLabel(label);
        ReturnImage(iconImage);
    }

    private IEnumerator PlayStreak(Vector2 origin, Vector2 target, Color tint)
    {
        Sprite streakSprite = streakSpriteOverride != null ? streakSpriteOverride : OrderVfxTextures.Streak;

        Image streak = RentImage();
        RectTransform rect = streak.rectTransform;
        rect.sizeDelta = streakSize;
        streak.sprite = streakSprite;
        streak.color = new Color(tint.r, tint.g, tint.b, streakAlpha);
        streak.gameObject.SetActive(true);

        // A single tinted shape reads as a faint smear. Layering a narrower,
        // near-white copy on top gives the comet a hot centre with a colored
        // fringe, which is what makes it look like emitted light.
        Image core = null;
        if (useHotCore)
        {
            core = RentImage();
            core.sprite = streakSprite;
            core.rectTransform.sizeDelta = streakSize * hotCoreScale;
            core.color = Color.Lerp(tint, Color.white, 0.75f);
            core.gameObject.SetActive(true);
        }

        // Quadratic Bezier. The control point is pushed sideways from the middle
        // of the flight so the path bows instead of running dead straight, which
        // is what the reference footage shows.
        Vector2 middle = (origin + target) * 0.5f;
        Vector2 direction = target - origin;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized;
        Vector2 control = middle + perpendicular * (direction.magnitude * pathCurvature);

        int sparklesSpawned = 0;
        float elapsed = 0f;
        Vector2 previous = origin;

        while (elapsed < streakDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / streakDuration);

            // Eased so the comet accelerates away and decelerates into the slot.
            float eased = EaseInOutQuad(t);
            Vector2 position = EvaluateBezier(origin, control, target, eased);

            rect.anchoredPosition = position;

            Vector2 travel = position - previous;
            if (travel.sqrMagnitude > 0.0001f)
            {
                // The bright end of the sprite is its right edge, so aligning the
                // rotation with the travel vector points the head forward.
                float angle = Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg;
                rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (core != null)
            {
                core.rectTransform.anchoredPosition = position;
                core.rectTransform.localRotation = rect.localRotation;
            }

            // Sparkles are released by distance travelled rather than per frame,
            // so the trail density does not change with frame rate.
            int wanted = Mathf.FloorToInt(t * sparkleCount);
            while (sparklesSpawned < wanted)
            {
                StartCoroutine(PlaySparkle(position, tint));
                sparklesSpawned++;
            }

            // Fades out over the last quarter so it dissolves into the impact
            // glow instead of vanishing on a frame boundary.
            float alpha = t < 0.75f ? streakAlpha : streakAlpha * (1f - (t - 0.75f) / 0.25f);
            streak.color = new Color(tint.r, tint.g, tint.b, alpha);

            if (core != null)
            {
                Color hot = Color.Lerp(tint, Color.white, 0.75f);
                core.color = new Color(hot.r, hot.g, hot.b, alpha);
            }

            previous = position;
            yield return null;
        }

        ReturnImage(streak);
        ReturnImage(core);
    }

    /// <summary>
    /// The arrival burst: small bright stars thrown outward from the slot.
    ///
    /// The halo alone was reading as a flat yellow box. Stars give the impact
    /// the scatter the reference has, and being separate short-lived objects
    /// they cost nothing once the burst is over.
    /// </summary>
    private void PlayImpactBurst(Vector2 at)
    {
        if (burstStarCount <= 0)
        {
            return;
        }

        // Evenly spaced angles with a random offset, rather than fully random
        // angles: random directions clump, and a clumped burst looks like a
        // mistake rather than a spray.
        float angleStep = 360f / burstStarCount;
        float angleOffset = Random.Range(0f, angleStep);

        for (int index = 0; index < burstStarCount; index++)
        {
            float angle = (angleOffset + index * angleStep + Random.Range(-angleStep * 0.25f, angleStep * 0.25f))
                          * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float radius = Random.Range(burstRadiusRange.x, burstRadiusRange.y);

            StartCoroutine(PlayBurstStar(at, direction * radius));
        }
    }

    private IEnumerator PlayBurstStar(Vector2 origin, Vector2 travel)
    {
        Image star = RentImage();
        RectTransform rect = star.rectTransform;

        float size = Random.Range(burstStarSizeRange.x, burstStarSizeRange.y);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = origin;

        // A random roll stops twelve identical four-point stars from looking
        // like a stamped pattern.
        rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));

        star.sprite = starSpriteOverride != null ? starSpriteOverride : OrderVfxTextures.Star;
        star.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < burstLifetime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / burstLifetime);

            rect.anchoredPosition = origin + travel * EaseOutCubic(t);

            // Pops to full size quickly, then shrinks away. A constant size makes
            // the burst appear and vanish rather than bloom.
            float scale = t < 0.2f
                ? Mathf.Lerp(0.3f, 1f, t / 0.2f)
                : Mathf.Lerp(1f, 0.25f, (t - 0.2f) / 0.8f);
            rect.localScale = new Vector3(scale, scale, 1f);

            star.color = new Color(burstColor.r, burstColor.g, burstColor.b, 1f - t * t);
            yield return null;
        }

        ReturnImage(star);
    }

    private IEnumerator PlaySparkle(Vector2 position, Color tint)
    {
        Image sparkle = RentImage();
        RectTransform rect = sparkle.rectTransform;

        float size = Random.Range(sparkleSizeRange.x, sparkleSizeRange.y);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = position;

        // Stars rather than round dots. Dots read as dust; the reference trail is
        // made of small bright sparkles with visible points.
        sparkle.sprite = sparkleSpriteOverride != null ? sparkleSpriteOverride : OrderVfxTextures.Star;
        sparkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
        sparkle.gameObject.SetActive(true);

        Vector2 drift = new Vector2(
            Random.Range(-sparkleScatter, sparkleScatter),
            Random.Range(-sparkleScatter, sparkleScatter) - sparkleFall);

        float elapsed = 0f;
        while (elapsed < sparkleLifetime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / sparkleLifetime);

            rect.anchoredPosition = position + drift * EaseOutCubic(t);

            // Shrinking as it fades keeps the trail from looking like a row of
            // dots that all switch off together.
            float scale = Mathf.Lerp(1f, 0.35f, t);
            rect.localScale = new Vector3(scale, scale, 1f);
            sparkle.color = new Color(tint.r, tint.g, tint.b, 1f - t);
            yield return null;
        }

        ReturnImage(sparkle);
    }

    private void PlayImpactSound()
    {
        if (impactClip == null)
        {
            return;
        }

        Camera listener = ResolveBoardCamera();
        Vector3 at = listener != null ? listener.transform.position : Vector3.zero;
        AudioSource.PlayClipAtPoint(impactClip, at, impactVolume);
    }

    // ---------------------------------------------------------------- space

    /// <summary>Converts a board position into the effect root's local space.</summary>
    private Vector2 WorldToEffectSpace(Vector3 worldPosition, Camera sourceCamera)
    {
        Vector2 screenPoint = sourceCamera != null
            ? (Vector2)sourceCamera.WorldToScreenPoint(worldPosition)
            : (Vector2)worldPosition;

        return ScreenToEffectSpace(screenPoint);
    }

    /// <summary>
    /// Converts a UI element's world position into the effect root's local
    /// space. Canvas elements need the canvas camera rather than the board
    /// camera, which is why this is separate from the board conversion.
    /// </summary>
    private Vector2 UiWorldToEffectSpace(Vector3 uiWorldPosition)
    {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, uiWorldPosition);
        return ScreenToEffectSpace(screenPoint);
    }

    private Vector2 ScreenToEffectSpace(Vector2 screenPoint)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                effectRoot,
                screenPoint,
                canvasCamera,
                out Vector2 local))
        {
            return local;
        }

        return Vector2.zero;
    }

    private Camera ResolveBoardCamera()
    {
        return boardCamera != null ? boardCamera : Camera.main;
    }

    /// <summary>
    /// Creates the full-screen layer the effects live in. It is added as the
    /// last child of the canvas so streaks draw over the Orders card, and it
    /// ignores raycasts so it can never block a drag.
    /// </summary>
    private void EnsureEffectRoot()
    {
        if (effectRoot != null)
        {
            return;
        }

        Transform parent = parentCanvas != null ? parentCanvas.transform : transform;
        GameObject rootObject = new GameObject("OrderVfxLayer", typeof(RectTransform));
        rootObject.transform.SetParent(parent, false);
        rootObject.transform.SetAsLastSibling();

        effectRoot = rootObject.GetComponent<RectTransform>();
        effectRoot.anchorMin = Vector2.zero;
        effectRoot.anchorMax = Vector2.one;
        effectRoot.offsetMin = Vector2.zero;
        effectRoot.offsetMax = Vector2.zero;

        CanvasGroup group = rootObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    // ---------------------------------------------------------------- pool

    private Image RentImage()
    {
        Image image = imagePool.Count > 0 ? imagePool.Pop() : CreateImage();
        image.transform.SetAsLastSibling();
        return image;
    }

    private void ReturnImage(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.gameObject.SetActive(false);
        image.rectTransform.localScale = Vector3.one;
        image.rectTransform.localRotation = Quaternion.identity;

        // Reset here, not at rent time. The same pooled Image serves as a comet,
        // a sparkle, and a "+1" icon, and only the icon wants aspect preserved.
        // Leaving it on would squash the next comet into its sprite's ratio.
        image.preserveAspect = false;
        image.sprite = null;

        imagePool.Push(image);
    }

    private Image CreateImage()
    {
        GameObject created = new GameObject("OrderVfxImage", typeof(RectTransform));
        created.transform.SetParent(effectRoot, false);

        Image image = created.AddComponent<Image>();
        image.raycastTarget = false;

        // Anchored to the centre so anchoredPosition maps directly onto the
        // canvas coordinates the conversion helpers produce.
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        created.SetActive(false);
        return image;
    }

    private TextMeshProUGUI RentLabel()
    {
        TextMeshProUGUI label = labelPool.Count > 0 ? labelPool.Pop() : CreateLabel();
        label.transform.SetAsLastSibling();
        return label;
    }

    private void ReturnLabel(TextMeshProUGUI label)
    {
        if (label == null)
        {
            return;
        }

        label.gameObject.SetActive(false);
        labelPool.Push(label);
    }

    private TextMeshProUGUI CreateLabel()
    {
        GameObject created = new GameObject("OrderVfxLabel", typeof(RectTransform));
        created.transform.SetParent(effectRoot, false);

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

    // ---------------------------------------------------------------- math

    private static Vector2 EvaluateBezier(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }

    private static float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInOutQuad(float t)
    {
        return t < 0.5f
            ? 2f * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
    }
}
