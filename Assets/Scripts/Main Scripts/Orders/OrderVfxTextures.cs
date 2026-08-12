using UnityEngine;

/// <summary>
/// Procedurally generated sprites for the order effects.
///
/// The streak and its sparkles need soft, alpha-faded shapes. Shipping those as
/// PNG assets would mean the feature cannot run until an artist has produced
/// them, and a hard-edged built-in Unity sprite reads as a rectangle rather than
/// a light trail. Generating them in code removes that dependency: the effect
/// looks correct out of the box, and any serialized sprite assigned on
/// OrderVfxDirector overrides these without a script change.
///
/// Both textures are created once per session and cached statically, so the
/// cost is two small textures for the whole game rather than one per effect.
/// </summary>
public static class OrderVfxTextures
{
    private const int SparkleSize = 64;
    private const int StreakWidth = 128;
    private const int StreakHeight = 32;
    private const int GlowSize = 128;
    private const int StarSize = 64;
    private const int CheckSize = 96;
    private const int StarFiveSize = 96;
    private const int DustSize = 64;
    private const int RingSize = 128;
    private const int RibbonSectionWidth = 8;
    private const int RibbonSectionHeight = 64;

    private static Sprite sparkleSprite;
    private static Sprite streakSprite;
    private static Sprite glowSprite;
    private static Sprite starSprite;
    private static Sprite checkmarkSprite;
    private static Sprite starSparkleSprite;
    private static Sprite dustSprite;
    private static Sprite ringSprite;
    private static Texture2D ribbonSectionTexture;
    private static Sprite softBandSprite;
    private static Sprite haloBandSprite;

    /// <summary>
    /// A wide, soft radial halo for the impact flash.
    ///
    /// The panel builder previously used Unity's built-in UISprite here, which
    /// is a rounded rectangle. At full opacity that reads as a yellow box behind
    /// the drink and, being larger than the slot, visibly spilled past the
    /// card's border. A radial falloff has no edge to spill: it simply fades to
    /// nothing before it reaches the card.
    /// </summary>
    public static Sprite Glow
    {
        get
        {
            if (glowSprite != null)
            {
                return glowSprite;
            }

            Texture2D texture = CreateTexture("OrderVfx_Glow", GlowSize, GlowSize);
            float centre = (GlowSize - 1) * 0.5f;

            for (int y = 0; y < GlowSize; y++)
            {
                for (int x = 0; x < GlowSize; x++)
                {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    float normalized = Mathf.Clamp01(1f - distance / centre);

                    // A broad soft body plus a tight hot core. The body carries
                    // the warmth, the core keeps the centre from looking flat.
                    float body = Mathf.Pow(normalized, 2.2f) * 0.8f;
                    float core = Mathf.Pow(normalized, 9f) * 0.35f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(body + core)));
                }
            }

            texture.Apply(false, false);
            glowSprite = CreateSprite(texture, "OrderVfx_Glow");
            return glowSprite;
        }
    }

    /// <summary>
    /// A four-pointed twinkle. Round dots read as dust; the impact burst is
    /// supposed to read as small bright stars, which needs actual points.
    /// </summary>
    public static Sprite Star
    {
        get
        {
            if (starSprite != null)
            {
                return starSprite;
            }

            Texture2D texture = CreateTexture("OrderVfx_Star", StarSize, StarSize);
            float centre = (StarSize - 1) * 0.5f;

            for (int y = 0; y < StarSize; y++)
            {
                for (int x = 0; x < StarSize; x++)
                {
                    float u = (x - centre) / centre;
                    float v = (y - centre) / centre;

                    // An astroid: |u|^e + |v|^e = 1 with e below 1 pulls the
                    // edges inward and leaves four sharp points on the axes.
                    float shape = Mathf.Pow(Mathf.Abs(u), 0.5f) + Mathf.Pow(Mathf.Abs(v), 0.5f);
                    float alpha = Mathf.Clamp01(1f - shape);
                    alpha = Mathf.Pow(alpha, 0.55f);

                    // Small round core so the very centre stays solid white.
                    float distance = Mathf.Sqrt(u * u + v * v);
                    alpha = Mathf.Max(alpha, Mathf.Pow(Mathf.Clamp01(1f - distance * 4.5f), 2f));

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);
            starSprite = CreateSprite(texture, "OrderVfx_Star");
            return starSprite;
        }
    }

    /// <summary>
    /// An actual tick mark. The panel builder had been placing a plain green
    /// square as a placeholder, which is the most obvious difference from the
    /// reference once an order completes.
    /// </summary>
    public static Sprite Checkmark
    {
        get
        {
            if (checkmarkSprite != null)
            {
                return checkmarkSprite;
            }

            Texture2D texture = CreateTexture("OrderVfx_Checkmark", CheckSize, CheckSize);

            // Two segments in normalised space: the short down-stroke and the
            // long up-stroke of a tick.
            Vector2 a = new Vector2(0.16f, 0.55f);
            Vector2 b = new Vector2(0.40f, 0.26f);
            Vector2 c = new Vector2(0.86f, 0.78f);

            const float halfThickness = 0.105f;
            const float softness = 0.03f;

            for (int y = 0; y < CheckSize; y++)
            {
                for (int x = 0; x < CheckSize; x++)
                {
                    Vector2 point = new Vector2(
                        (x + 0.5f) / CheckSize,
                        (y + 0.5f) / CheckSize);

                    float distance = Mathf.Min(
                        DistanceToSegment(point, a, b),
                        DistanceToSegment(point, b, c));

                    float alpha = Mathf.Clamp01((halfThickness - distance) / softness);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);
            checkmarkSprite = CreateSprite(texture, "OrderVfx_Checkmark");
            return checkmarkSprite;
        }
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= Mathf.Epsilon)
        {
            return Vector2.Distance(point, start);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
        return Vector2.Distance(point, start + segment * t);
    }

    private static Texture2D CreateTexture(string name, int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private static Sprite CreateSprite(Texture2D texture, string name)
    {
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    /// <summary>
    /// A soft round dot: opaque at the centre, fading to nothing at the rim.
    /// Used for the sparkles that trail behind the streak.
    /// </summary>
    public static Sprite Sparkle
    {
        get
        {
            if (sparkleSprite != null)
            {
                return sparkleSprite;
            }

            Texture2D texture = new Texture2D(SparkleSize, SparkleSize, TextureFormat.RGBA32, false)
            {
                name = "OrderVfx_Sparkle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            float centre = (SparkleSize - 1) * 0.5f;
            float radius = centre;

            for (int y = 0; y < SparkleSize; y++)
            {
                for (int x = 0; x < SparkleSize; x++)
                {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    float normalized = Mathf.Clamp01(1f - distance / radius);

                    // Squared falloff gives a bright core with a soft halo, which
                    // reads as a spark rather than as a flat circle.
                    float alpha = normalized * normalized;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);
            sparkleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, SparkleSize, SparkleSize),
                new Vector2(0.5f, 0.5f),
                100f);
            sparkleSprite.name = "OrderVfx_Sparkle";
            sparkleSprite.hideFlags = HideFlags.HideAndDontSave;
            return sparkleSprite;
        }
    }

    /// <summary>
    /// A tapered horizontal comet: bright at the right edge, thinning and fading
    /// toward the left. The director rotates it so the bright end leads.
    /// </summary>
    public static Sprite Streak
    {
        get
        {
            if (streakSprite != null)
            {
                return streakSprite;
            }

            Texture2D texture = new Texture2D(StreakWidth, StreakHeight, TextureFormat.RGBA32, false)
            {
                name = "OrderVfx_Streak",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            float verticalCentre = (StreakHeight - 1) * 0.5f;

            for (int x = 0; x < StreakWidth; x++)
            {
                // 0 at the tail, 1 at the head.
                float along = x / (float)(StreakWidth - 1);

                // The tail is both thinner and dimmer, which is what makes the
                // shape read as motion instead of as a bar.
                float halfThickness = Mathf.Lerp(0.08f, 1f, along * along) * verticalCentre;
                float lengthFade = Mathf.Pow(along, 1.6f);

                for (int y = 0; y < StreakHeight; y++)
                {
                    float distance = Mathf.Abs(y - verticalCentre);
                    float across = halfThickness <= 0.001f
                        ? 0f
                        : Mathf.Clamp01(1f - distance / halfThickness);

                    float alpha = lengthFade * across * across;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);
            streakSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, StreakWidth, StreakHeight),
                new Vector2(0.5f, 0.5f),
                100f);
            streakSprite.name = "OrderVfx_Streak";
            streakSprite.hideFlags = HideFlags.HideAndDontSave;
            return streakSprite;
        }
    }

    /// <summary>
    /// A four-pointed sparkle whose arms are solid triangles, matching the stars in
    /// the reference footage.
    ///
    /// The previous shape was built from a polar lobe function, which produced four
    /// thin tapering spikes. At the sizes this effect uses, those read as three
    /// crossing hairlines rather than as a star. This one is a true star polygon:
    /// straight edges running from each outer tip down to an inner vertex between
    /// them, so every arm is a filled triangle with real area.
    ///
    /// <paramref name="innerRadius"/> controls how fat the arms are. Around a third
    /// of the outer radius gives the chunky sparkle the reference uses; much lower
    /// and the arms thin back into needles.
    /// </summary>
    public static Sprite StarSparkle
    {
        get
        {
            if (starSparkleSprite != null)
            {
                return starSparkleSprite;
            }

            const int points = 4;
            const float innerRadius = 0.34f;
            const float sector = Mathf.PI * 2f / points;

            Texture2D texture = CreateTexture("OrderVfx_StarSparkle", StarFiveSize, StarFiveSize);
            float centre = (StarFiveSize - 1) * 0.5f;

            // The straight edge of one arm, as a line between the outer tip at angle
            // 0 and the inner vertex at half a sector. Every other edge of the star
            // is this same line mirrored and rotated, so it is solved once.
            Vector2 tip = new Vector2(1f, 0f);
            Vector2 valley = new Vector2(
                innerRadius * Mathf.Cos(sector * 0.5f),
                innerRadius * Mathf.Sin(sector * 0.5f));

            Vector2 edge = valley - tip;
            Vector2 normal = new Vector2(edge.y, -edge.x).normalized;
            float offset = Vector2.Dot(normal, tip);

            for (int y = 0; y < StarFiveSize; y++)
            {
                for (int x = 0; x < StarFiveSize; x++)
                {
                    float u = (x - centre) / centre;
                    float v = (y - centre) / centre;
                    float distance = Mathf.Sqrt(u * u + v * v);

                    // Fold the angle into one arm, then mirror it about the arm's
                    // centre line, so only the half-sector above needs solving.
                    float angle = Mathf.Atan2(v, u);
                    angle -= Mathf.Floor(angle / sector) * sector;
                    if (angle > sector * 0.5f)
                    {
                        angle = sector - angle;
                    }

                    float projection = normal.x * Mathf.Cos(angle) + normal.y * Mathf.Sin(angle);
                    float boundary = projection <= 0.0001f ? 1f : offset / projection;

                    // One texel of feathering, so the arms stay crisp but do not
                    // alias into a staircase when the star is scaled up.
                    float feather = 1.6f / centre;
                    float alpha = Mathf.Clamp01((boundary - distance) / feather);

                    // A small solid dot at the centre keeps a tiny star readable as
                    // a bright point rather than as four arms meeting at nothing.
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(1f - distance * 7f));

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);
            starSparkleSprite = CreateSprite(texture, "OrderVfx_StarSparkle");
            return starSparkleSprite;
        }
    }

    /// <summary>
    /// A very soft round mote for the fine dust that drifts along the trail.
    ///
    /// Deliberately softer than <see cref="Sparkle"/>: the dust sits underneath the
    /// stars and must not compete with them, so its falloff is cubic rather than
    /// squared and it has no hot core at all.
    /// </summary>
    public static Sprite SoftDust
    {
        get
        {
            if (dustSprite != null)
            {
                return dustSprite;
            }

            Texture2D texture = CreateTexture("OrderVfx_Dust", DustSize, DustSize);
            float centre = (DustSize - 1) * 0.5f;

            for (int y = 0; y < DustSize; y++)
            {
                for (int x = 0; x < DustSize; x++)
                {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    float normalized = Mathf.Clamp01(1f - distance / centre);
                    float alpha = normalized * normalized * normalized;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);
            dustSprite = CreateSprite(texture, "OrderVfx_Dust");
            return dustSprite;
        }
    }

    /// <summary>
    /// A thin annulus, feathered on both edges, for the expanding impact ring.
    ///
    /// A hard-edged ring reads as a drawn circle. Feathering both sides makes the
    /// same shape read as a shockwave of light, which is what lets it be used at
    /// low opacity without looking like an outline.
    /// </summary>
    public static Sprite Ring
    {
        get
        {
            if (ringSprite != null)
            {
                return ringSprite;
            }

            Texture2D texture = CreateTexture("OrderVfx_Ring", RingSize, RingSize);
            float centre = (RingSize - 1) * 0.5f;

            const float radius = 0.76f;
            const float halfThickness = 0.09f;

            for (int y = 0; y < RingSize; y++)
            {
                for (int x = 0; x < RingSize; x++)
                {
                    float u = (x - centre) / centre;
                    float v = (y - centre) / centre;
                    float distance = Mathf.Sqrt(u * u + v * v);

                    float offset = Mathf.Abs(distance - radius) / halfThickness;
                    float alpha = Mathf.Clamp01(1f - offset);

                    // Squared so the band's own edges fall off smoothly instead of
                    // ending on a straight ramp.
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }

            texture.Apply(false, false);
            ringSprite = CreateSprite(texture, "OrderVfx_Ring");
            return ringSprite;
        }
    }

    /// <summary>
    /// A horizontal band: solid down its centre line, feathered to nothing at the
    /// top and bottom edges, and constant along its length.
    ///
    /// This is one segment of the trail. The ribbon lays a chain of these along the
    /// flight path, each rotated to the local direction of travel and stretched to
    /// the distance it spans, so consecutive segments overlap into one continuous
    /// streak. Because the falloff is in the texture, a segment stays a single
    /// stretched quad however soft its edges are.
    /// </summary>
    public static Sprite SoftBand
    {
        get
        {
            if (softBandSprite != null)
            {
                return softBandSprite;
            }

            softBandSprite = CreateBand("OrderVfx_Band", false);
            return softBandSprite;
        }
    }

    /// <summary>
    /// The same band with its centre hollowed out, for the outer halo.
    ///
    /// Every layer of the trail is centred on the same line, so a halo with a solid
    /// profile simply adds its brightness to the core and the two read as one thick
    /// stripe. Holding the middle down lets the halo be seen as a glow *around* the
    /// coloured core, which is what the reference shows.
    /// </summary>
    public static Sprite HaloBand
    {
        get
        {
            if (haloBandSprite != null)
            {
                return haloBandSprite;
            }

            haloBandSprite = CreateBand("OrderVfx_HaloBand", true);
            return haloBandSprite;
        }
    }

    private static Sprite CreateBand(string name, bool hollow)
    {
        Texture2D texture = CreateTexture(name, RibbonSectionWidth, RibbonSectionHeight);

        for (int y = 0; y < RibbonSectionHeight; y++)
        {
            float v = (y + 0.5f) / RibbonSectionHeight;
            float centred = 1f - Mathf.Abs(2f * v - 1f);
            float solid = centred * centred * (3f - 2f * centred);

            float alpha;
            if (hollow)
            {
                // Peaks 55% out from the centre, with only a fifth of the strength
                // left in the middle.
                float distance = Mathf.Abs(2f * v - 1f);
                float band = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.55f) / 0.45f);
                band = band * band * (3f - 2f * band);
                alpha = Mathf.Max(band, solid * 0.2f);
            }
            else
            {
                alpha = solid;
            }

            for (int x = 0; x < RibbonSectionWidth; x++)
            {
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, false);
        return CreateSprite(texture, name);
    }

    /// <summary>
    /// The ribbon's cross-section profiles, packed into one texture.
    ///
    /// <see cref="OrderTrailRibbon"/> maps this across the width of every strip it
    /// builds. Doing the falloff in the texture rather than in the mesh means a
    /// strip stays two triangles wide however soft its edges are.
    ///
    /// Two profiles, selected by U, because a Graphic has exactly one texture and
    /// the ribbon needs two different shapes in a single mesh:
    ///
    ///   U = 0.25  Solid. Opaque down the centre line, feathering to nothing at both
    ///             edges. Used by the coloured core and the middle band.
    ///
    ///   U = 0.75  Halo. Dim in the middle and brightest away from the centre. Used
    ///             by the outer glow.
    ///
    /// The halo profile is the important one. Every strip is centred on the same
    /// line, so with three solid profiles stacked additively the middle of the trail
    /// receives all three at once and saturates to white, which hides both the
    /// drink's colour and the outer glow. Hollowing out the centre of the widest
    /// layer lets it read as a glow *around* the core instead of adding to it.
    ///
    /// Each half is constant along U, so sampling at 0.25 and 0.75 stays clear of
    /// the seam at 0.5 and bilinear filtering never mixes the two profiles.
    /// </summary>
    public static Texture2D RibbonCrossSection
    {
        get
        {
            if (ribbonSectionTexture != null)
            {
                return ribbonSectionTexture;
            }

            Texture2D texture = CreateTexture(
                "OrderVfx_RibbonSection",
                RibbonSectionWidth,
                RibbonSectionHeight);

            int half = RibbonSectionWidth / 2;

            for (int y = 0; y < RibbonSectionHeight; y++)
            {
                float v = (y + 0.5f) / RibbonSectionHeight;

                // 1 at the centre line, 0 at both edges.
                float centred = 1f - Mathf.Abs(2f * v - 1f);

                // Smoothed, so the strip has a flat bright middle and a soft fringe.
                float solid = centred * centred * (3f - 2f * centred);

                // Peaks at 55% out from the centre and is held down in the middle,
                // so the widest layer surrounds the core rather than covering it.
                float distance = Mathf.Abs(2f * v - 1f);
                float band = 1f - Mathf.Abs(distance - 0.55f) / 0.45f;
                band = Mathf.Clamp01(band);
                band = band * band * (3f - 2f * band);

                float halo = Mathf.Max(band, solid * 0.22f);

                for (int x = 0; x < RibbonSectionWidth; x++)
                {
                    float alpha = x < half ? solid : halo;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, false);
            ribbonSectionTexture = texture;
            return ribbonSectionTexture;
        }
    }
}
