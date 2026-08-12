using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sequences the effect that connects a packed box on the board to its order slot
/// in the panel.
///
/// Phase 1, at the box
///     A soft flash, a handful of warm dust motes, and the "+1" with the drink's
///     icon. This is the confirmation beat: it says the match registered, before
///     anything starts moving.
///
/// Phase 2, the delivery
///     A small white-hot head flies up to the slot along a gentle Bezier, dragging
///     a narrow white-cyan-lavender ribbon and shedding five-pointed stars and fine
///     dust. Owned by <see cref="ObjectiveDeliveryVfx"/>.
///
/// Phase 3, at the slot
///     A compact flash, a small radial star burst, an expanding cyan ring, and the
///     callback that lets the panel tick the number down. The slot's own halo and
///     hop are owned by OrderSlotUI.
///
/// Why the effect runs in UI space
/// -------------------------------
/// It starts on a 3D board and has to finish exactly on a UI element. A world-space
/// implementation would have to convert the slot's screen position back into the
/// world every frame and would still need sorting rules to draw over the canvas.
/// Converting once, at launch, into the canvas's own coordinates makes the landing
/// pixel-exact and removes the sorting question entirely.
///
/// It also means TrailRenderer and ParticleSystem are unavailable, since neither
/// draws inside a screen-space Canvas. The trail is a UI mesh
/// (<see cref="OrderTrailRibbon"/>) and the particles are pooled Images
/// (<see cref="OrderVfxParticles"/>), which give the same look and sort correctly
/// with the rest of the UI for free.
///
/// Tuning lives in <see cref="OrderVfxSettings"/>, not on this component, because
/// this component exists in five level scenes and per-scene copies would drift.
/// </summary>
[DisallowMultipleComponent]
public sealed class OrderVfxDirector : MonoBehaviour, IOrderImpactPresenter
{
    [Header("References")]
    [SerializeField, Tooltip("Camera that renders the board. Empty uses Camera.main.")]
    private Camera boardCamera;

    [SerializeField, Tooltip("Full-screen RectTransform the effects are parented to. Created automatically when empty.")]
    private RectTransform effectRoot;

    [SerializeField, Tooltip("Maps a soda color to its icon and tint. Falls back to built-in colors when empty.")]
    private SodaVisualLibrary visualLibrary;

    [SerializeField, Tooltip("All timings, colors and counts. Falls back to built-in defaults when empty.")]
    private OrderVfxSettings settings;

    [SerializeField, Tooltip("Additive UI material for the glow. Falls back to a runtime material when empty.")]
    private Material glowMaterial;

    [Header("Audio")]
    [SerializeField, Tooltip("Optional clip played when the head reaches its slot.")]
    private AudioClip impactClip;

    [SerializeField, Range(0f, 1f)] private float impactVolume = 0.7f;

    [Header("Debug")]
    [SerializeField, Tooltip("Board object the test delivery launches from. Empty uses the screen centre.")]
    private Transform testSource;

    [SerializeField, Tooltip("UI element the test delivery lands on. Empty uses the first order slot.")]
    private RectTransform testTarget;

    [SerializeField, Tooltip("Soda color the test delivery pretends to have packed.")]
    private Soda.SodaColor testColor = Soda.SodaColor.Blue;

    private Canvas parentCanvas;
    private Camera canvasCamera;
    private OrderVfxPool pool;
    private OrderVfxParticles particles;
    private OrderVfxSettings runtimeSettings;
    private Material runtimeGlowMaterial;

    private readonly Stack<ObjectiveDeliveryVfx> deliveryPool = new Stack<ObjectiveDeliveryVfx>();
    private readonly List<ObjectiveDeliveryVfx> activeDeliveries = new List<ObjectiveDeliveryVfx>();
    private readonly List<ImpactCallback> pendingCallbacks = new List<ImpactCallback>();

    /// <summary>
    /// A callback that can be fired from two places but runs at most once.
    ///
    /// Unity stops a MonoBehaviour's coroutines when it is disabled, so a delivery
    /// in flight when the panel is torn down would never reach its own callback.
    /// OrderManager counts pending impacts and only raises AllOrdersFulfilled once
    /// every one has landed, so a lost callback means a level that can never be
    /// completed. Wrapping the callback lets OnDisable settle anything still in
    /// flight without any risk of ticking an order down twice.
    /// </summary>
    private sealed class ImpactCallback
    {
        private System.Action action;

        public ImpactCallback(System.Action action)
        {
            this.action = action;
        }

        public void Fire()
        {
            System.Action pending = action;
            action = null;
            pending?.Invoke();
        }
    }

    private void Awake()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            parentCanvas = parentCanvas.rootCanvas;

            // Overlay canvases convert with a null camera; every other mode needs
            // the canvas's own camera. Getting this wrong offsets every effect, and
            // this project ships both kinds: Level 2 is Overlay, MainLevels/Level1
            // is Screen Space Camera.
            canvasCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : parentCanvas.worldCamera;
        }

        EnsureEffectRoot();
        EnsureRuntime();
    }

    private void OnDisable()
    {
        // Disabling stops every coroutine, so any delivery still in flight would
        // otherwise never reach its callback. Settle them here: the order ticks
        // down without its animation, which is far better than a level that can
        // never be completed.
        for (int index = 0; index < pendingCallbacks.Count; index++)
        {
            pendingCallbacks[index].Fire();
        }

        pendingCallbacks.Clear();

        // Anything still in flight releases the pooled objects it holds, rather
        // than leaving them stranded active on the effect layer.
        for (int index = 0; index < activeDeliveries.Count; index++)
        {
            activeDeliveries[index]?.Abort();
        }

        activeDeliveries.Clear();
        particles?.ClearAll();
    }

    /// <summary>
    /// IOrderImpactPresenter. Starts the sequence and guarantees that
    /// <paramref name="onImpact"/> runs exactly once, even if the effect cannot be
    /// played at all.
    ///
    /// This is the single most important rule in the file. OrderManager counts
    /// pending impacts and only raises AllOrdersFulfilled when every one has
    /// landed; a skipped callback leaves an order permanently in flight and the
    /// level can never be completed.
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
        EnsureRuntime();

        ImpactCallback callback = new ImpactCallback(onImpact);
        pendingCallbacks.Add(callback);

        // Same auto-resolve as the panel: an unassigned inspector reference must not
        // silently turn every trail white.
        if (visualLibrary == null)
        {
            visualLibrary = SodaVisualLibrary.Resolve();
        }

        Color sodaColor = visualLibrary != null
            ? visualLibrary.GetEffectColor(consumedEvent.Color)
            : SodaVisualLibrary.DefaultColorFor(consumedEvent.Color);

        Vector2 origin = WorldToEffectSpace(consumedEvent.SourceWorldPosition, ResolveBoardCamera());

        PlayMatchConfirmation(origin, consumedEvent.Color, sodaColor);

        if (runtimeSettings.DeliveryDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(runtimeSettings.DeliveryDelay);
        }

        // Resolved after the delay rather than before: the panel may have been
        // rebuilt, and a layout pass could have moved the slot in the meantime.
        if (targetSlot == null)
        {
            callback.Fire();
            pendingCallbacks.Remove(callback);
            yield break;
        }

        Vector2 target = UiWorldToEffectSpace(targetSlot.ImpactWorldPosition);

        ObjectiveDeliveryVfx delivery = RentDelivery();
        activeDeliveries.Add(delivery);

        yield return delivery.Fly(origin, target, sodaColor, runtimeSettings);

        PlayDestinationImpact(target, sodaColor);
        PlayImpactSound();

        // The counter changes here and nowhere else. OrderPanelUI.ApplyImpact is
        // the only thing this callback reaches, so the number can never move before
        // the head has visually landed.
        callback.Fire();
        pendingCallbacks.Remove(callback);

        // The ribbon is still bright at this point and is released separately, once
        // its tail has faded. Pooling it here would cut the streak off in mid-air.
        yield return delivery.ReleaseWhenTrailFaded();

        activeDeliveries.Remove(delivery);
        deliveryPool.Push(delivery);
    }

    // -------------------------------------------------------- phase 1, the box

    /// <summary>
    /// The confirmation beat at the completed box: a soft flash, a few warm motes,
    /// an optional subtle ring, and the "+1".
    ///
    /// This is the only place warm gold appears in the whole effect. It marks the
    /// match itself; everything that travels is cool by design.
    /// </summary>
    private void PlayMatchConfirmation(Vector2 origin, Soda.SodaColor color, Color sodaColor)
    {
        StartCoroutine(PlayConfirmFlash(origin));

        EmitConfirmDust(origin);

        if (runtimeSettings.ConfirmRingEnabled)
        {
            StartCoroutine(PlayRing(
                origin,
                runtimeSettings.ConfirmRingSize,
                Vector2.one,
                runtimeSettings.ConfirmRingDuration,
                WithAlpha(runtimeSettings.IceCyan, runtimeSettings.ConfirmRingAlpha)));
        }

        if (!runtimeSettings.ShowPlusOne)
        {
            return;
        }

        Sprite icon = runtimeSettings.ShowPlusOneIcon && visualLibrary != null
            ? visualLibrary.GetIcon(color)
            : null;

        StartCoroutine(PlayPlusOne(origin, icon));
    }

    /// <summary>
    /// The golden flash at the completed box.
    ///
    /// Both layers are gold. An earlier version put a white core inside a gold
    /// halo, which at these sizes reads as a white flash with a faint warm rim
    /// rather than as a gold one, because the bright centre is what the eye takes
    /// the colour from. Two gold layers at different sizes give the same sense of
    /// depth while the flash stays unambiguously gold.
    /// </summary>
    private IEnumerator PlayConfirmFlash(Vector2 origin)
    {
        Image outer = pool.RentImage();
        outer.sprite = OrderVfxTextures.Glow;
        outer.rectTransform.sizeDelta = Vector2.one * runtimeSettings.ConfirmFlashSize;
        outer.rectTransform.anchoredPosition = origin;
        outer.gameObject.SetActive(true);

        Image inner = pool.RentImage();
        inner.sprite = OrderVfxTextures.Glow;
        inner.rectTransform.sizeDelta = Vector2.one * (runtimeSettings.ConfirmFlashSize * 0.55f);
        inner.rectTransform.anchoredPosition = origin;
        inner.gameObject.SetActive(true);

        Color gold = runtimeSettings.ConfirmGold;
        Color deepGold = runtimeSettings.ConfirmGoldDeep;

        float duration = runtimeSettings.ConfirmFlashDuration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Snaps to full brightness and decays. Fading in would make the match
            // feel soft, which is the opposite of a confirmation.
            float alpha = 1f - t * t;
            float scale = Mathf.Lerp(0.6f, 1.15f, EaseOutCubic(t));

            outer.rectTransform.localScale = new Vector3(scale, scale, 1f);
            inner.rectTransform.localScale = new Vector3(scale * 0.9f, scale * 0.9f, 1f);

            // The deeper gold goes on the outside so the flash keeps a warm rim as
            // it expands instead of washing pale at the edge.
            outer.color = WithAlpha(deepGold, alpha * 0.75f);
            inner.color = WithAlpha(gold, alpha);

            yield return null;
        }

        pool.ReturnImage(outer);
        pool.ReturnImage(inner);
    }

    private void EmitConfirmDust(Vector2 origin)
    {
        int count = runtimeSettings.ConfirmDustCount;
        if (count <= 0)
        {
            return;
        }

        for (int index = 0; index < count; index++)
        {
            // Biased upward: the box is being lifted away, so its dust should rise
            // rather than spray evenly in a circle.
            float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            float radius = Random.Range(
                runtimeSettings.ConfirmDustRadius.x,
                runtimeSettings.ConfirmDustRadius.y);

            float lifetime = Random.Range(
                runtimeSettings.ConfirmDustLifetime.x,
                runtimeSettings.ConfirmDustLifetime.y);

            // Gold throughout, matching the flash it belongs to. White motes made
            // the whole confirmation read as a white pop with a warm edge.
            Color color = Random.value < 0.6f
                ? runtimeSettings.ConfirmGold
                : runtimeSettings.ConfirmGoldDeep;

            particles.Spawn(
                OrderVfxTextures.SoftDust,
                origin,
                direction * (radius / Mathf.Max(0.01f, lifetime)),
                color,
                Random.Range(runtimeSettings.ConfirmDustSize.x, runtimeSettings.ConfirmDustSize.y),
                lifetime,
                0f,
                6f);
        }
    }

    /// <summary>
    /// The "+1" and the drink's own icon beside it. Scales from 0.70 through a 1.15
    /// overshoot to 1.0, rises a short distance, holds while it is read, then fades
    /// as the delivery launches.
    ///
    /// The two are separate pooled objects moved by the same offset rather than a
    /// parented pair, because the pools stay flat and the icon is optional.
    /// </summary>
    private IEnumerator PlayPlusOne(Vector2 origin, Sprite icon)
    {
        TextMeshProUGUI label = pool.RentLabel();
        label.text = "+1";
        label.fontSize = runtimeSettings.PlusOneFontSize;
        label.color = Color.white;

        // A dark outline, not a tinted one. The board is a light wooden surface, so
        // a pale outline leaves white text barely readable; the drink's identity is
        // already carried by the icon beside it.
        label.outlineColor = new Color(0.1f, 0.08f, 0.06f, 1f);
        label.outlineWidth = 0.32f;
        label.gameObject.SetActive(true);

        Image iconImage = null;
        if (icon != null)
        {
            // Not additive: this is real artwork and has to render as itself.
            iconImage = pool.RentImage(false);
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.type = Image.Type.Simple;
            iconImage.color = Color.white;
            iconImage.rectTransform.sizeDelta = Vector2.one * runtimeSettings.PlusOneIconSize;
            iconImage.gameObject.SetActive(true);
        }

        // The pair is centred on the origin, so adding an icon does not shift the
        // "+1" away from where the box actually was.
        float halfGap = iconImage != null ? runtimeSettings.PlusOneIconGap * 0.5f : 0f;
        Vector2 labelOffset = new Vector2(-halfGap, 0f);
        Vector2 iconOffset = new Vector2(halfGap, 0f);

        float duration = runtimeSettings.PlusOneDuration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector2 rise = new Vector2(0f, runtimeSettings.PlusOneRise * EaseOutCubic(t));
            float scale = PlusOneScale(t);

            // Held opaque for the first half so the number is readable, then faded.
            // A linear fade from zero reads as a flicker.
            float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;

            label.rectTransform.anchoredPosition = origin + labelOffset + rise;
            label.rectTransform.localScale = new Vector3(scale, scale, 1f);
            label.color = new Color(1f, 1f, 1f, alpha);

            if (iconImage != null)
            {
                iconImage.rectTransform.anchoredPosition = origin + iconOffset + rise;
                iconImage.rectTransform.localScale = new Vector3(scale, scale, 1f);
                iconImage.color = new Color(1f, 1f, 1f, alpha);
            }

            yield return null;
        }

        pool.ReturnLabel(label);
        pool.ReturnImage(iconImage);
    }

    /// <summary>
    /// 0.70 -> 1.15 -> 1.0, with the overshoot reached early. Done as an explicit
    /// two-segment curve so the peak lands at a known moment rather than wherever
    /// an OutBack ease happens to put it.
    /// </summary>
    private float PlusOneScale(float t)
    {
        const float overshootEnd = 0.28f;
        const float settleEnd = 0.45f;

        if (t < overshootEnd)
        {
            return Mathf.Lerp(
                runtimeSettings.PlusOneStartScale,
                runtimeSettings.PlusOnePeakScale,
                EaseOutCubic(t / overshootEnd));
        }

        if (t < settleEnd)
        {
            return Mathf.Lerp(
                runtimeSettings.PlusOnePeakScale,
                1f,
                (t - overshootEnd) / (settleEnd - overshootEnd));
        }

        return 1f;
    }

    // ------------------------------------------------------ phase 3, the slot

    /// <summary>
    /// The arrival: a compact white-cyan flash, a small ring of stars, and an
    /// expanding thin ring. Deliberately not a golden explosion; the destination
    /// stays in the same cool palette as the streak that fed it.
    /// </summary>
    private void PlayDestinationImpact(Vector2 at, Color sodaColor)
    {
        StartCoroutine(PlayImpactFlash(at));

        int count = runtimeSettings.ImpactStarCount;
        if (count > 0)
        {
            // Evenly spaced angles with a random offset, rather than fully random
            // ones: random directions clump, and a clumped burst looks like a
            // mistake rather than a spray.
            float step = 360f / count;
            float offset = Random.Range(0f, step);

            for (int index = 0; index < count; index++)
            {
                float angle = (offset + index * step + Random.Range(-step * 0.2f, step * 0.2f))
                              * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                float radius = Random.Range(
                    runtimeSettings.ImpactStarRadius.x,
                    runtimeSettings.ImpactStarRadius.y);

                float lifetime = runtimeSettings.ImpactStarLifetime;

                particles.Spawn(
                    OrderVfxTextures.StarSparkle,
                    at,
                    direction * (radius / Mathf.Max(0.01f, lifetime)),
                    Random.value < 0.6f
                        ? runtimeSettings.CoreWhite
                        : runtimeSettings.Tint(runtimeSettings.ElectricCyan, sodaColor, 0.5f),
                    Random.Range(runtimeSettings.ImpactStarSize.x, runtimeSettings.ImpactStarSize.y),
                    lifetime,
                    Random.Range(runtimeSettings.StarSpin.x, runtimeSettings.StarSpin.y));
            }
        }

        if (runtimeSettings.ImpactRingEnabled)
        {
            StartCoroutine(PlayRing(
                at,
                runtimeSettings.ImpactRingSize,
                runtimeSettings.ImpactRingScale,
                runtimeSettings.ImpactRingDuration,
                WithAlpha(runtimeSettings.IceCyan, runtimeSettings.ImpactRingAlpha)));
        }
    }

    private IEnumerator PlayImpactFlash(Vector2 at)
    {
        Image glow = pool.RentImage();
        glow.sprite = OrderVfxTextures.Glow;
        glow.rectTransform.sizeDelta = Vector2.one * runtimeSettings.ImpactFlashSize;
        glow.rectTransform.anchoredPosition = at;
        glow.gameObject.SetActive(true);

        Image core = pool.RentImage();
        core.sprite = OrderVfxTextures.Sparkle;
        core.rectTransform.sizeDelta = Vector2.one * (runtimeSettings.ImpactFlashSize * 0.4f);
        core.rectTransform.anchoredPosition = at;
        core.gameObject.SetActive(true);

        float duration = runtimeSettings.ImpactFlashDuration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float alpha = 1f - t * t;
            float scale = Mathf.Lerp(0.5f, 1.2f, EaseOutCubic(t));

            glow.rectTransform.localScale = new Vector3(scale, scale, 1f);
            core.rectTransform.localScale = new Vector3(scale * 0.8f, scale * 0.8f, 1f);

            glow.color = WithAlpha(runtimeSettings.ElectricCyan, alpha * 0.85f);
            core.color = WithAlpha(runtimeSettings.CoreWhite, alpha);

            yield return null;
        }

        pool.ReturnImage(glow);
        pool.ReturnImage(core);
    }

    /// <summary>
    /// A thin ring that expands and fades. Shared by the optional confirmation ring
    /// at the box and the impact ring at the slot, which differ only in their
    /// numbers.
    /// </summary>
    private IEnumerator PlayRing(Vector2 at, float size, Vector2 scaleRange, float duration, Color color)
    {
        Image ring = pool.RentImage();
        ring.sprite = OrderVfxTextures.Ring;
        ring.rectTransform.sizeDelta = Vector2.one * size;
        ring.rectTransform.anchoredPosition = at;
        ring.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, EaseOutCubic(t));
            ring.rectTransform.localScale = new Vector3(scale, scale, 1f);
            ring.color = WithAlpha(color, color.a * (1f - t));

            yield return null;
        }

        pool.ReturnImage(ring);
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

    // ----------------------------------------------------------------- debug

    /// <summary>
    /// Plays the whole sequence without a real match, so the effect can be tuned
    /// without hunting for a board state that produces one.
    ///
    /// Enter Play Mode first: the effect is driven by coroutines and pooled runtime
    /// objects, neither of which exist in Edit Mode.
    /// </summary>
    [ContextMenu("Play Test Delivery")]
    public void PlayTestDelivery()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter Play Mode before running the test delivery.", this);
            return;
        }

        EnsureEffectRoot();
        EnsureRuntime();

        Vector3 sourceWorld = testSource != null
            ? testSource.position
            : ResolveTestSourceFallback();

        // No slot and no explicit target is not a failure: the effect is being
        // tuned, and refusing to play makes it impossible to look at before a level
        // has built its orders. It aims at the top of the canvas instead.
        OrderSlotUI slot = ResolveTestSlot();

        StartCoroutine(PlayTestSequence(sourceWorld, slot));
    }

    private IEnumerator PlayTestSequence(Vector3 sourceWorld, OrderSlotUI slot)
    {
        if (visualLibrary == null)
        {
            visualLibrary = SodaVisualLibrary.Resolve();
        }

        Color sodaColor = visualLibrary != null
            ? visualLibrary.GetEffectColor(testColor)
            : SodaVisualLibrary.DefaultColorFor(testColor);

        Vector2 origin = WorldToEffectSpace(sourceWorld, ResolveBoardCamera());

        PlayMatchConfirmation(origin, testColor, sodaColor);

        if (runtimeSettings.DeliveryDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(runtimeSettings.DeliveryDelay);
        }

        Vector2 target;
        if (testTarget != null)
        {
            target = UiWorldToEffectSpace(testTarget.position);
        }
        else if (slot != null)
        {
            target = UiWorldToEffectSpace(slot.ImpactWorldPosition);
        }
        else
        {
            // Roughly where the Orders card sits: upper middle of the canvas.
            Rect area = effectRoot.rect;
            target = new Vector2(0f, area.height * 0.35f);
        }

        ObjectiveDeliveryVfx delivery = RentDelivery();
        activeDeliveries.Add(delivery);

        yield return delivery.Fly(origin, target, sodaColor, runtimeSettings);

        PlayDestinationImpact(target, sodaColor);
        PlayImpactSound();

        // The real sequence would tick the counter here. The test deliberately does
        // not, so it can be replayed without consuming an order.
        slot?.PlayImpact();

        yield return delivery.ReleaseWhenTrailFaded();

        activeDeliveries.Remove(delivery);
        deliveryPool.Push(delivery);
    }

    private Vector3 ResolveTestSourceFallback()
    {
        Camera camera = ResolveBoardCamera();
        if (camera == null)
        {
            return Vector3.zero;
        }

        return camera.ViewportToWorldPoint(new Vector3(0.5f, 0.35f, 10f));
    }

    private OrderSlotUI ResolveTestSlot()
    {
        OrderPanelUI panel = GetComponentInParent<OrderPanelUI>();
        if (panel == null || panel.Slots.Count == 0)
        {
            return null;
        }

        return panel.Slots[0];
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
    /// Converts a UI element's world position into the effect root's local space.
    /// Canvas elements need the canvas camera rather than the board camera, which
    /// is why this is separate from the board conversion.
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

    // --------------------------------------------------------------- runtime

    /// <summary>
    /// Creates the full-screen layer the effects live in. It is added as the last
    /// child of the canvas so streaks draw over the Orders card, and it ignores
    /// raycasts so it can never block a drag.
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

        effectRoot = rootObject.GetComponent<RectTransform>();
        effectRoot.anchorMin = Vector2.zero;
        effectRoot.anchorMax = Vector2.one;
        effectRoot.offsetMin = Vector2.zero;
        effectRoot.offsetMax = Vector2.zero;

        CanvasGroup group = rootObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    /// <summary>
    /// Builds the pool, the particle simulation and the settings fallback. Called
    /// from Awake and again from every entry point, because a scene can be loaded
    /// with this component starting disabled.
    /// </summary>
    private void EnsureRuntime()
    {
        if (runtimeSettings == null)
        {
            // A scene that was never given the asset still plays a correct effect
            // rather than nothing at all. Resolve falls back to Resources, then to
            // the built-in defaults, so this is never null.
            runtimeSettings = settings != null ? settings : OrderVfxSettings.Resolve();
        }

        if (runtimeSettings.DrawAbovePanel && effectRoot != null)
        {
            effectRoot.SetAsLastSibling();
        }

        if (pool == null && effectRoot != null)
        {
            pool = new OrderVfxPool(effectRoot, ResolveGlowMaterial());
        }

        if (particles == null && effectRoot != null)
        {
            GameObject created = new GameObject("OrderVfxParticles");
            created.transform.SetParent(effectRoot, false);

            particles = created.AddComponent<OrderVfxParticles>();
            particles.Initialize(pool);
        }
    }

    /// <summary>
    /// The additive UI material.
    ///
    /// An Overlay canvas gets no URP post-processing, so bloom cannot be used to
    /// make this effect glow. The glow has to come from the blend mode instead,
    /// which is what the Coca Sorting/UI Glow shader provides. If neither the
    /// assigned material nor the shader can be found, null is returned and the
    /// graphics fall back to Unity's standard UI material: dimmer, but correct.
    /// </summary>
    private Material ResolveGlowMaterial()
    {
        if (glowMaterial != null)
        {
            return glowMaterial;
        }

        if (runtimeGlowMaterial != null)
        {
            return runtimeGlowMaterial;
        }

        Shader shader = Shader.Find("Coca Sorting/UI Glow");
        if (shader == null)
        {
            Debug.LogWarning(
                "Shader 'Coca Sorting/UI Glow' was not found, so the order effects will render " +
                "without additive glow. Assign a glow material on OrderVfxDirector, or make sure " +
                "the shader is included in the build.",
                this);
            return null;
        }

        runtimeGlowMaterial = new Material(shader)
        {
            name = "OrderVfx_Glow (runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };

        return runtimeGlowMaterial;
    }

    private ObjectiveDeliveryVfx RentDelivery()
    {
        return deliveryPool.Count > 0
            ? deliveryPool.Pop()
            : new ObjectiveDeliveryVfx(pool, particles);
    }

    // ---------------------------------------------------------------- math

    private static float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
