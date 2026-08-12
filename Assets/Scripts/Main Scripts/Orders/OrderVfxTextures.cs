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

    private static Sprite sparkleSprite;
    private static Sprite streakSprite;
    private static Sprite glowSprite;
    private static Sprite starSprite;
    private static Sprite checkmarkSprite;

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
}
