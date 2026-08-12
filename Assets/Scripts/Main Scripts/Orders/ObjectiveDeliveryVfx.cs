using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One delivery: the bright energy head that flies from a packed box up to its
/// order slot, the ribbon it drags behind it, and the stars and dust it sheds on
/// the way.
///
/// Split out of OrderVfxDirector so that the director is only a sequencer. The
/// director decides when a delivery happens and what it aims at; this class owns
/// everything that happens between launch and landing.
///
/// The two-part release
/// --------------------
/// <see cref="Fly"/> ends the moment the head reaches the slot, so the director can
/// fire the impact and tick the counter on exactly that frame. The ribbon is still
/// full of light at that point, so it is deliberately *not* released there;
/// <see cref="ReleaseWhenTrailFaded"/> holds it until its tail has faded out and
/// only then returns everything to the pool. Releasing both together would cut the
/// streak off in mid-air.
///
/// Instances are reused by the director, so a level's worth of deliveries allocates
/// one of these rather than one per packed box.
/// </summary>
public sealed class ObjectiveDeliveryVfx
{
    private readonly OrderVfxPool pool;
    private readonly OrderVfxParticles particles;

    private OrderVfxSettings settings;
    private OrderTrailRibbon ribbon;
    private Image headCore;
    private Image headGlow;
    private Image headHalo;

    public ObjectiveDeliveryVfx(OrderVfxPool pool, OrderVfxParticles particles)
    {
        this.pool = pool;
        this.particles = particles;
    }

    /// <summary>
    /// Flies the head from <paramref name="origin"/> to <paramref name="target"/>,
    /// both in effect-layer coordinates. Completes on the frame the head arrives.
    /// </summary>
    public IEnumerator Fly(
        Vector2 origin,
        Vector2 target,
        Color sodaColor,
        OrderVfxSettings vfxSettings)
    {
        settings = vfxSettings;

        // Activated before configuring: the ribbon rents its segments in Configure,
        // and Unity will not run Update on a component whose object is still
        // inactive, so the chain would sit there unrebuilt until the next sample.
        ribbon = pool.RentRibbon();
        ribbon.gameObject.SetActive(true);
        ribbon.Configure(settings, sodaColor, pool);

        // Created after the ribbon so the head's sprites are rented later and
        // therefore draw on top of the trail they are laying down.
        CreateHead(origin, sodaColor);

        // Quadratic Bezier. The control point is pushed sideways from the midpoint
        // so the flight bows gently rather than running dead straight. Curvature is
        // deliberately small: the reference path is nearly direct, and a large bow
        // reads as the item being lobbed rather than shot.
        Vector2 middle = (origin + target) * 0.5f;
        Vector2 direction = target - origin;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized;
        Vector2 control = middle + perpendicular * (direction.magnitude * settings.Curvature);

        float duration = Mathf.Max(0.05f, settings.FlightDuration);
        float elapsed = 0f;
        int starsSpawned = 0;
        int dustSpawned = 0;

        Vector2 position = origin;
        Vector2 previous = origin;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // DOTween evaluates the Ease exposed in the settings asset, so changing
            // the curve in the inspector actually changes the motion without any
            // ease code living here.
            float eased = DOVirtual.EasedValue(0f, 1f, t, settings.FlightEase);

            position = EvaluateBezier(origin, control, target, eased);
            ribbon.AddSample(position);

            Vector2 travel = position - previous;
            UpdateHead(position, t);

            // Emission is driven by progress along the path rather than by frames,
            // so the trail has the same density at 30 and at 120 fps.
            int wantedStars = Mathf.FloorToInt(t * settings.StarCount);
            while (starsSpawned < wantedStars)
            {
                EmitStar(position, travel, sodaColor);
                starsSpawned++;
            }

            int wantedDust = Mathf.FloorToInt(t * settings.DustCount);
            while (dustSpawned < wantedDust)
            {
                EmitDust(position, travel, sodaColor);
                dustSpawned++;
            }

            previous = position;
            yield return null;
        }

        // Snapped to the exact destination. Ending on whatever the last frame
        // produced would leave the impact a pixel or two off the icon.
        ribbon.AddSample(target);

        // The head is gone the instant it lands; the flash at the destination takes
        // over from here. The ribbon stays alive and keeps fading.
        ReleaseHead();
    }

    /// <summary>
    /// Holds the ribbon until its oldest point has aged out, then pools it. The
    /// linger is capped so a stalled ribbon can never hold a pooled object forever.
    /// </summary>
    public IEnumerator ReleaseWhenTrailFaded()
    {
        if (ribbon == null)
        {
            yield break;
        }

        float deadline = Time.unscaledTime + Mathf.Max(0.05f, settings.TrailLingerAfterImpact);

        while (ribbon.HasVisibleTrail && Time.unscaledTime < deadline)
        {
            yield return null;
        }

        pool.ReturnRibbon(ribbon);
        ribbon = null;
    }

    /// <summary>
    /// Returns everything this delivery holds, immediately. Called when the
    /// director is disabled mid-flight so no pooled object is stranded active.
    /// </summary>
    public void Abort()
    {
        ReleaseHead();

        if (ribbon != null)
        {
            pool.ReturnRibbon(ribbon);
            ribbon = null;
        }
    }

    // ------------------------------------------------------------------ head

    /// <summary>
    /// Three stacked additive sprites: a tiny pure-white centre, a cyan-blue glow,
    /// and a wide soft lavender halo.
    ///
    /// Layering is what makes this read as a point of light rather than as a disc.
    /// A single sprite at any size looks like a ball; three at different sizes and
    /// opacities have no visible edge, which is how real bloom behaves.
    /// </summary>
    private void CreateHead(Vector2 position, Color sodaColor)
    {
        headHalo = CreateHeadLayer(
            OrderVfxTextures.Glow,
            settings.HeadHaloSize,
            WithAlpha(settings.Tint(settings.PinkLavender, sodaColor), 0.55f),
            position);

        // Follows the drink's colour, matching the core of the ribbon it is about to
        // lay down. If the head glowed cyan while its trail ran red, the streak
        // would look like two unrelated effects stuck together.
        Color glowColor = settings.CoreFollowsSodaColor
            ? settings.SodaCore(sodaColor)
            : settings.Tint(settings.ElectricCyan, sodaColor, 0.5f);

        headGlow = CreateHeadLayer(
            OrderVfxTextures.Glow,
            settings.HeadGlowSize,
            WithAlpha(glowColor, 0.9f),
            position);

        // Never tinted. This tiny centre is the brightest element of the whole
        // effect and stays white whatever drink was packed, so the head always
        // reads as a point of light sitting inside its coloured glow.
        headCore = CreateHeadLayer(
            OrderVfxTextures.Sparkle,
            settings.HeadCoreSize,
            settings.CoreWhite,
            position);
    }

    private Image CreateHeadLayer(Sprite sprite, float size, Color color, Vector2 position)
    {
        Image image = pool.RentImage();
        image.sprite = sprite;
        image.color = color;
        image.rectTransform.sizeDelta = new Vector2(size, size);
        image.rectTransform.anchoredPosition = position;
        image.gameObject.SetActive(true);
        return image;
    }

    private void UpdateHead(Vector2 position, float t)
    {
        // A shallow pulse over two or three cycles. Enough to look alive, small
        // enough that it never reads as a wobble.
        float pulse = 1f + Mathf.Sin(t * settings.HeadPulseCycles * Mathf.PI * 2f) * settings.HeadPulseAmount;
        Vector3 scale = new Vector3(pulse, pulse, 1f);

        SetHeadLayer(headHalo, position, scale);
        SetHeadLayer(headGlow, position, scale);
        SetHeadLayer(headCore, position, scale);
    }

    private static void SetHeadLayer(Image image, Vector2 position, Vector3 scale)
    {
        if (image == null)
        {
            return;
        }

        image.rectTransform.anchoredPosition = position;
        image.rectTransform.localScale = scale;
    }

    private void ReleaseHead()
    {
        pool.ReturnImage(headCore);
        pool.ReturnImage(headGlow);
        pool.ReturnImage(headHalo);

        headCore = null;
        headGlow = null;
        headHalo = null;
    }

    // ------------------------------------------------------------- particles

    /// <summary>
    /// A small cluster of five-pointed stars around a point on the path.
    ///
    /// Emitted as a cluster rather than singly, and displaced both sideways and
    /// along the direction of travel, because stars released one at a time at
    /// evenly spaced points read as a dotted line drawn along the path. Scattering
    /// them on both axes turns that line into a drift of stars that happens to
    /// follow the path, which is what the reference shows.
    ///
    /// Sizes are rolled squared so fine pinpoints are common and large stars are
    /// occasional, rather than every star landing mid-range.
    /// </summary>
    private void EmitStar(Vector2 position, Vector2 travel, Color sodaColor)
    {
        Vector2 sideways = Perpendicular(travel);
        Vector2 along = travel.sqrMagnitude < 0.0001f ? Vector2.up : travel.normalized;
        float scatter = settings.StarSpawnScatter;

        int count = Random.Range(
            Mathf.Max(1, settings.StarCluster.x),
            Mathf.Max(1, settings.StarCluster.y) + 1);

        for (int index = 0; index < count; index++)
        {
            // Concentrated near the centre line rather than spread evenly across
            // the band. In the reference the stars sit inside the glow and thin out
            // toward its edge; an even spread threw them well outside the halo and
            // the trail stopped reading as one object.
            float sideRoll = Random.value * Random.value * (Random.value < 0.5f ? -1f : 1f);

            Vector2 offset =
                sideways * (sideRoll * scatter) +
                along * Random.Range(-scatter, scatter);

            // Drifts along the path, not away from it. Pushing stars outward made
            // the cloud open into a spray that left the halo behind entirely.
            float speed = Random.Range(settings.StarDrift.x, settings.StarDrift.y);
            Vector2 velocity = along * -speed + sideways * (speed * 0.15f * Mathf.Sign(sideRoll));

            // One of three discrete sizes rather than a point in a range. A range
            // produced a smear of near-identical middling stars; distinct large,
            // medium and small tiers are what read as a natural mix.
            float size = settings.PickStarSize();

            particles.Spawn(
                OrderVfxTextures.StarSparkle,
                position + offset,
                velocity,
                PickStarColor(sodaColor),
                size,
                Random.Range(settings.StarLifetime.x, settings.StarLifetime.y),
                Random.Range(settings.StarSpin.x, settings.StarSpin.y),
                settings.StarNoise);
        }
    }

    private void EmitDust(Vector2 position, Vector2 travel, Color sodaColor)
    {
        Vector2 sideways = Perpendicular(travel);
        Vector2 along = travel.sqrMagnitude < 0.0001f ? Vector2.up : travel.normalized;

        // Dust is scattered the same way as the stars, at a smaller radius, so it
        // fills the gaps between the clusters rather than lining up on the path.
        float scatter = settings.StarSpawnScatter * 0.7f;
        Vector2 offset =
            sideways * Random.Range(-scatter, scatter) +
            along * Random.Range(-scatter, scatter);

        float speed = Random.Range(settings.DustDrift.x, settings.DustDrift.y);
        Vector2 velocity = sideways * (speed * (Random.value < 0.5f ? -1f : 1f));

        Color color = Random.value < 0.5f
            ? settings.IceCyan
            : settings.SodaCore(sodaColor);

        particles.Spawn(
            OrderVfxTextures.SoftDust,
            position + offset,
            velocity,
            WithAlpha(color, settings.DustAlpha),
            Random.Range(settings.DustSize.x, settings.DustSize.y),
            Random.Range(settings.DustLifetime.x, settings.DustLifetime.y),
            0f,
            settings.StarNoise * 0.5f);
    }

    /// <summary>
    /// The star colour mix.
    ///
    /// A share of the stars now take the drink's own colour, so the trail's
    /// particles agree with the coloured core rather than being a separate white
    /// cloud floating over it. White still leads the mix, because white is what
    /// makes the stars read as sparkle rather than as confetti.
    /// </summary>
    private Color PickStarColor(Color sodaColor)
    {
        float roll = Random.value;

        if (roll < 0.42f)
        {
            return settings.CoreWhite;
        }

        if (roll < 0.68f)
        {
            return settings.SodaCore(sodaColor);
        }

        if (roll < 0.86f)
        {
            return settings.IceCyan;
        }

        return Color.Lerp(settings.PaleLavender, settings.PinkLavender, Random.value);
    }

    // ------------------------------------------------------------------ math

    private static Vector2 Perpendicular(Vector2 travel)
    {
        if (travel.sqrMagnitude < 0.0001f)
        {
            return Vector2.right;
        }

        Vector2 normalized = travel.normalized;
        return new Vector2(-normalized.y, normalized.x);
    }

    private static Vector2 EvaluateBezier(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
