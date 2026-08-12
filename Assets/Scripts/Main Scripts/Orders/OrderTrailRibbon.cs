using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The luminous streak the delivery head drags behind it: a continuous band of
/// light running from the packed box to the order slot, like the exhaust trail of
/// a jet rather than a line of separate dots.
///
/// Why chained sprites and not a generated mesh
/// -------------------------------------------
/// This was first written as a custom MaskableGraphic that built the whole trail
/// as one mesh in OnPopulateMesh. It never drew a single pixel in this project:
/// instrumentation showed the trail recording 448 path points and emitting zero
/// vertices, flight after flight, while the head glow beside it rendered fine. A
/// custom Graphic has several ways to be silently skipped — culling makes
/// Graphic.Rebuild return before the mesh builder runs at all — and none of them
/// report anything.
///
/// So the trail is now built from the one primitive that is already proven to
/// render on this layer: a pooled UI Image. Each segment is a soft-edged band
/// sprite, rotated to the local direction of travel and stretched to the distance
/// it spans. Consecutive segments overlap, so the chain reads as one unbroken
/// streak. It costs more objects than a single mesh, but it cannot silently fail,
/// which matters far more for an effect that has to be seen.
///
/// Why not a TrailRenderer
/// ----------------------
/// TrailRenderer is a world-space renderer and does not draw inside a screen-space
/// Canvas at all, and this effect has to land pixel-exactly on a UI icon.
///
/// Three layers, drawn back to front:
///   outer   widest, lavender/pink, low opacity, hollow-centred so it reads as a
///           glow around the core instead of thickening it
///   middle  the transition band, the drink's colour lifted toward ice cyan
///   core    narrowest and brightest, the drink's own colour
/// </summary>
[DisallowMultipleComponent]
public sealed class OrderTrailRibbon : MonoBehaviour
{
    /// <summary>One recorded point of the path, with the time it was recorded.</summary>
    private struct Sample
    {
        public Vector2 Position;
        public float Time;
    }

    /// <summary>
    /// Segments are rented once per flight and then only moved, so a trail costs no
    /// allocation and no sibling reordering per frame.
    /// </summary>
    private const int MaxSegments = 28;

    private readonly Sample[] samples = new Sample[MaxSegments + 2];
    private int sampleCount;

    private Image[] hotSegments;
    private Image[] coreSegments;
    private Image[] middleSegments;
    private Image[] outerSegments;

    private OrderVfxPool pool;

    private float sampleInterval = 0.016f;
    private float lifetime = 0.42f;
    private float lastSampleTime = float.NegativeInfinity;

    private bool configured;
    private float hotWidth, coreWidth, middleWidth, outerWidth;
    private float hotAlpha, middleAlpha, outerAlpha, overallAlpha;
    private float tailTaper, opacityHold;
    private Color coreColor, middleColor, outerColor;

    /// <summary>
    /// True while any recorded point is still young enough to be drawn. The owner
    /// polls this so it can hold the ribbon until its tail has fully faded instead
    /// of pooling it the instant the head lands.
    /// </summary>
    public bool HasVisibleTrail => sampleCount > 0 && OldestAge() < lifetime;

    /// <summary>Recorded path points. Exposed so a trail that fails to draw can be
    /// diagnosed: no samples and no visible segments are different problems.</summary>
    public int SampleCount => sampleCount;

    /// <summary>Segments currently switched on. Zero with a healthy
    /// <see cref="SampleCount"/> means the chain is being built but not shown.</summary>
    public int VisibleSegments { get; private set; }

    /// <summary>Whether Configure has run. Nothing is drawn until it has.</summary>
    public bool HasSettings => configured;

    /// <summary>
    /// Prepares the ribbon for one flight and rents its segments.
    ///
    /// Segments are rented here, in layer order, so the outer glow sits behind the
    /// middle band and the core draws on top. Renting them per frame would reorder
    /// the siblings constantly and make the layers flicker past each other.
    /// </summary>
    public void Configure(OrderVfxSettings vfxSettings, Color soda, OrderVfxPool vfxPool)
    {
        pool = vfxPool;

        lifetime = Mathf.Max(0.01f, vfxSettings.RibbonLifetime);

        // The chain has a fixed number of segments, so the sampling rate is clamped
        // to whatever lets those segments span the whole lifetime. Sampling faster
        // than that is not merely wasteful: the oldest points would be pushed out of
        // the buffer early and the streak would cover only part of the route while
        // appearing, from the settings, as though it should cover all of it.
        float spanningInterval = lifetime / MaxSegments;
        sampleInterval = Mathf.Max(vfxSettings.RibbonSampleInterval, spanningInterval);

        hotWidth = vfxSettings.RibbonHotWidth;
        coreWidth = vfxSettings.RibbonCoreWidth;
        middleWidth = vfxSettings.RibbonMiddleWidth;
        outerWidth = vfxSettings.RibbonOuterWidth;

        hotAlpha = vfxSettings.RibbonHotAlpha;
        middleAlpha = vfxSettings.RibbonMiddleAlpha;
        outerAlpha = vfxSettings.RibbonOuterAlpha;
        overallAlpha = vfxSettings.RibbonAlpha;
        tailTaper = vfxSettings.RibbonTailTaper;
        opacityHold = vfxSettings.RibbonOpacityHold;

        // Resolved once per flight rather than per segment. The core carries the
        // drink's colour, the middle is that colour lifted toward ice cyan, and the
        // outer halo stays on the fixed palette whatever was packed.
        coreColor = vfxSettings.CoreFollowsSodaColor
            ? vfxSettings.SodaCore(soda)
            : vfxSettings.IceCyan;

        // The halo is built from white, not from the lavender palette. Basing it on
        // a violet stop made every trail sit inside a purple sleeve regardless of
        // which drink was packed, which read as a second colour fighting the core
        // rather than as light spilling out of it. White carrying a small fraction
        // of the drink's hue is what makes it look like a glow.
        outerColor = Color.Lerp(Color.white, coreColor, vfxSettings.HaloSodaTint);

        // The middle sits between the two, so the three layers form one gradient
        // out from the coloured centre rather than three separate stripes.
        middleColor = Color.Lerp(coreColor, outerColor, vfxSettings.MiddleIceLift);

        Clear();

        // Only the core is solid. The middle and outer bands are hollow-centred, so
        // neither of them lays anything over the centre line. That is what keeps the
        // drink's colour pure: with a solid middle band sitting directly on top of a
        // narrower core, the two whitish outer layers washed the core out and the
        // whole streak read as one pale stripe with no distinguishable layers.
        outerSegments = RentLayer(outerSegments, OrderVfxTextures.HaloBand);
        middleSegments = RentLayer(middleSegments, OrderVfxTextures.HaloBand);
        coreSegments = RentLayer(coreSegments, OrderVfxTextures.SoftBand);

        // Rented last so it draws over the coloured core: a narrow white-hot line
        // down the middle, which is what gives the streak its own colour *and*
        // white, the way the reference does, instead of a flat coloured band.
        hotSegments = RentLayer(hotSegments, OrderVfxTextures.SoftBand);

        configured = true;
    }

    private Image[] RentLayer(Image[] existing, Sprite sprite)
    {
        Image[] layer = existing ?? new Image[MaxSegments];

        for (int index = 0; index < MaxSegments; index++)
        {
            if (layer[index] == null)
            {
                layer[index] = pool.RentImage();
            }

            Image segment = layer[index];
            segment.sprite = sprite;

            // Sliced would stretch a soft gradient's middle row into a hard bar.
            segment.type = Image.Type.Simple;
            segment.preserveAspect = false;

            // Hidden until the path is long enough to need it.
            segment.gameObject.SetActive(false);
        }

        return layer;
    }

    /// <summary>
    /// Records a point of the head's path.
    ///
    /// Rate-limited so the chain has the same density at 30 and at 240 fps. The
    /// newest point keeps following the head between recorded samples, so the
    /// ribbon never visibly detaches from the light at its tip.
    /// </summary>
    public void AddSample(Vector2 position)
    {
        if (!configured)
        {
            return;
        }

        float now = Time.unscaledTime;

        if (sampleCount > 0 && now - lastSampleTime < sampleInterval)
        {
            samples[sampleCount - 1].Position = position;
            Rebuild();
            return;
        }

        DropExpiredSamples();

        if (sampleCount >= samples.Length)
        {
            System.Array.Copy(samples, 1, samples, 0, samples.Length - 1);
            sampleCount = samples.Length - 1;
        }

        samples[sampleCount].Position = position;
        samples[sampleCount].Time = now;
        sampleCount++;
        lastSampleTime = now;

        Rebuild();
    }

    /// <summary>Hides every segment and forgets the path. Called on rent and return.</summary>
    public void Clear()
    {
        sampleCount = 0;
        lastSampleTime = float.NegativeInfinity;
        VisibleSegments = 0;

        HideLayer(hotSegments);
        HideLayer(coreSegments);
        HideLayer(middleSegments);
        HideLayer(outerSegments);
    }

    /// <summary>Returns every segment to the pool. Called when the ribbon is pooled.</summary>
    public void ReleaseSegments()
    {
        ReleaseLayer(ref hotSegments);
        ReleaseLayer(ref coreSegments);
        ReleaseLayer(ref middleSegments);
        ReleaseLayer(ref outerSegments);
        configured = false;
    }

    private void ReleaseLayer(ref Image[] layer)
    {
        if (layer == null)
        {
            return;
        }

        for (int index = 0; index < layer.Length; index++)
        {
            pool.ReturnImage(layer[index]);
            layer[index] = null;
        }

        layer = null;
    }

    private static void HideLayer(Image[] layer)
    {
        if (layer == null)
        {
            return;
        }

        for (int index = 0; index < layer.Length; index++)
        {
            if (layer[index] != null)
            {
                layer[index].gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (!configured || sampleCount == 0)
        {
            return;
        }

        // The trail keeps ageing after the head has stopped feeding it, so it is
        // rebuilt every frame rather than only when a sample arrives.
        DropExpiredSamples();
        Rebuild();
    }

    /// <summary>
    /// Positions one segment per gap between consecutive samples, for all three
    /// layers.
    ///
    /// Each segment spans from one recorded point to the next: centred on their
    /// midpoint, rotated to face along the gap, and stretched slightly longer than
    /// the gap so neighbouring segments overlap and leave no seam.
    /// </summary>
    private void Rebuild()
    {
        int gaps = Mathf.Min(sampleCount - 1, MaxSegments);
        VisibleSegments = Mathf.Max(0, gaps);

        if (gaps <= 0)
        {
            HideLayer(coreSegments);
            HideLayer(middleSegments);
            HideLayer(outerSegments);
            return;
        }

        float now = Time.unscaledTime;

        for (int index = 0; index < MaxSegments; index++)
        {
            if (index >= gaps)
            {
                SetSegmentActive(hotSegments, index, false);
                SetSegmentActive(coreSegments, index, false);
                SetSegmentActive(middleSegments, index, false);
                SetSegmentActive(outerSegments, index, false);
                continue;
            }

            // Walk from the newest gap backwards, so index 0 is always at the head
            // and the tail is whatever the lifetime still allows.
            int newest = sampleCount - 1 - index;
            Vector2 from = samples[newest - 1].Position;
            Vector2 to = samples[newest].Position;

            Vector2 delta = to - from;
            float length = delta.magnitude;
            Vector2 centre = (from + to) * 0.5f;

            float angle = length < 0.0001f
                ? 0f
                : Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            // 0 at the head, 1 at the oldest surviving point.
            float age = Mathf.Clamp01((now - samples[newest].Time) / lifetime);

            // Only the tail narrows, and mildly, so the streak stays a band along
            // most of its length instead of pinching to a point.
            float taper = Mathf.Lerp(1f, tailTaper, age);
            float fade = FadeAt(age) * overallAlpha;

            // Overlapped by a few units so consecutive quads leave no gap on a
            // curve, where the outer edge of the bend stretches furthest.
            float span = length + 6f;

            PlaceSegment(outerSegments, index, centre, angle, span, outerWidth * taper, outerColor, outerAlpha * fade);
            PlaceSegment(middleSegments, index, centre, angle, span, middleWidth * taper, middleColor, middleAlpha * fade);
            PlaceSegment(coreSegments, index, centre, angle, span, coreWidth * taper, coreColor, fade);
            PlaceSegment(hotSegments, index, centre, angle, span, hotWidth * taper, Color.white, hotAlpha * fade);
        }
    }

    private static void SetSegmentActive(Image[] layer, int index, bool active)
    {
        if (layer == null || layer[index] == null)
        {
            return;
        }

        if (layer[index].gameObject.activeSelf != active)
        {
            layer[index].gameObject.SetActive(active);
        }
    }

    private static void PlaceSegment(
        Image[] layer,
        int index,
        Vector2 centre,
        float angle,
        float length,
        float width,
        Color color,
        float alpha)
    {
        if (layer == null || layer[index] == null)
        {
            return;
        }

        Image segment = layer[index];

        if (width <= 0.01f || alpha <= 0.002f)
        {
            SetSegmentActive(layer, index, false);
            return;
        }

        RectTransform rect = segment.rectTransform;
        rect.anchoredPosition = centre;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        rect.sizeDelta = new Vector2(length, width);
        rect.localScale = Vector3.one;

        color.a = alpha;
        segment.color = color;

        SetSegmentActive(layer, index, true);
    }

    /// <summary>
    /// Removes points older than the lifetime from the front of the buffer.
    ///
    /// Samples are appended in time order, so everything expired is contiguous at
    /// the start and one shift clears them all.
    /// </summary>
    private void DropExpiredSamples()
    {
        float now = Time.unscaledTime;

        int firstLive = 0;
        while (firstLive < sampleCount && now - samples[firstLive].Time > lifetime)
        {
            firstLive++;
        }

        if (firstLive == 0)
        {
            return;
        }

        if (firstLive >= sampleCount)
        {
            sampleCount = 0;
            return;
        }

        System.Array.Copy(samples, firstLive, samples, 0, sampleCount - firstLive);
        sampleCount -= firstLive;
    }

    private float OldestAge()
    {
        return sampleCount == 0 ? float.MaxValue : Time.unscaledTime - samples[0].Time;
    }

    /// <summary>
    /// Opacity by age.
    ///
    /// Held flat for most of the trail and dropped only at the very end. The streak
    /// is meant to read as a solid band of light spanning the route; fading it
    /// gradually from the head made it look permanently half-finished and washed
    /// the drink's colour out along its whole length.
    /// </summary>
    private float FadeAt(float age)
    {
        if (age <= opacityHold)
        {
            return 1f;
        }

        float t = (age - opacityHold) / Mathf.Max(0.001f, 1f - opacityHold);
        return (1f - t) * (1f - t);
    }
}
