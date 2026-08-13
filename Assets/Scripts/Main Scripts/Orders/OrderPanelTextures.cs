using UnityEngine;

/// <summary>
/// Procedurally generated sprites for the Orders card.
///
/// Follows the doctrine already set by OrderVfxTextures: every shape is built in
/// code and cached statically, so the panel needs no imported art, no atlas and
/// no artist round-trip to look finished. It also means the card restyles itself
/// at runtime in every level scene at once, rather than each of the 25 scenes
/// carrying its own copy of the placeholder art the builder originally made.
///
/// All rounded shapes are generated with 9-slice borders, so one texture stretches
/// to any card or chip size without the corners smearing.
/// </summary>
public static class OrderPanelTextures
{
    private const int CornerTextureSize = 96;

    private static Sprite cardSprite;
    private static Sprite cardBorderSprite;
    private static Sprite chipSprite;
    private static Sprite shadowSprite;
    private static Sprite pillSprite;
    private static Sprite sheenSprite;
    private static Sprite verticalFadeSprite;
    private static Sprite blockIconSprite;

    /// <summary>Solid rounded rectangle. Tint with Image.color.</summary>
    public static Sprite Card => cardSprite != null
        ? cardSprite
        : cardSprite = BuildRoundedRect("OrderCard", 28f, 0f, 1f);

    /// <summary>Rounded rectangle outline, drawn over the card as a rim.</summary>
    public static Sprite CardBorder => cardBorderSprite != null
        ? cardBorderSprite
        : cardBorderSprite = BuildRoundedRect("OrderCardBorder", 28f, 5f, 1f);

    /// <summary>Smaller-radius rounded rectangle for the per-order chips.</summary>
    public static Sprite Chip => chipSprite != null
        ? chipSprite
        : chipSprite = BuildRoundedRect("OrderChip", 22f, 0f, 1f);

    /// <summary>Fully rounded pill for the header ribbon and the count badge.</summary>
    public static Sprite Pill => pillSprite != null
        ? pillSprite
        : pillSprite = BuildRoundedRect("OrderPill", 46f, 0f, 1f);

    /// <summary>Soft blurred rounded rectangle used as a drop shadow.</summary>
    public static Sprite Shadow => shadowSprite != null
        ? shadowSprite
        : shadowSprite = BuildShadow("OrderCardShadow", 30f, 14f);

    /// <summary>
    /// Icon for a "open N locked blocks" order: a taped crate seen face on.
    ///
    /// Drawn rather than baked from the blocker prefab, because the blocker is a
    /// plain cube with the tape generated onto it at runtime - there is no prefab
    /// to render that already looks like the thing the player sees.
    /// </summary>
    public static Sprite BlockIcon => blockIconSprite != null
        ? blockIconSprite
        : blockIconSprite = BuildBlockIcon("OrderBlockIcon");

    /// <summary>
    /// Top-down gloss: opaque at the top edge, gone by the middle. Laid over the
    /// card to read as a lit plastic surface rather than a flat fill.
    /// </summary>
    public static Sprite Sheen => sheenSprite != null
        ? sheenSprite
        : sheenSprite = BuildVerticalGradient("OrderSheen", 1f, 0f, 0.55f);

    /// <summary>Bottom-up shade, darkening the base of the card.</summary>
    public static Sprite VerticalFade => verticalFadeSprite != null
        ? verticalFadeSprite
        : verticalFadeSprite = BuildVerticalGradient("OrderFade", 0f, 1f, 1f);

    // --------------------------------------------------------------- builders

    /// <summary>
    /// Rounded rectangle with a signed-distance edge so the corners stay smooth at
    /// any scale. A borderThickness above zero produces a hollow outline instead of
    /// a filled shape.
    /// </summary>
    private static Sprite BuildRoundedRect(string name, float radius, float borderThickness, float alpha)
    {
        int size = CornerTextureSize;
        Texture2D texture = CreateTexture(name, size);
        Color[] pixels = new Color[size * size];

        // The texture is a square whose 9-slice border is the corner radius, so the
        // straight edges are a single stretched pixel column and only the corners
        // carry real detail.
        float half = size * 0.5f;
        float scaledRadius = Mathf.Min(radius, half - 1f);
        float inner = half - scaledRadius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance from the rounded-rect surface, measured in pixels.
                float dx = Mathf.Abs(x + 0.5f - half) - inner;
                float dy = Mathf.Abs(y + 0.5f - half) - inner;
                float outside = Mathf.Sqrt(
                    Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) +
                    Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float distance = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - scaledRadius;

                // One pixel of feather is what removes the stair-stepping.
                float coverage = Mathf.Clamp01(0.5f - distance);

                if (borderThickness > 0f)
                {
                    // Hollow: subtract an inset copy of the same shape.
                    float innerCoverage = Mathf.Clamp01(0.5f - (distance + borderThickness));
                    coverage = Mathf.Clamp01(coverage - innerCoverage);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, coverage * alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        float border = Mathf.Min(scaledRadius + 2f, half - 1f);
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
    }

    /// <summary>
    /// A rounded rectangle whose edge falls off over <paramref name="softness"/>
    /// pixels, used behind the card so it sits on the screen instead of floating.
    /// </summary>
    private static Sprite BuildShadow(string name, float radius, float softness)
    {
        int size = CornerTextureSize;
        Texture2D texture = CreateTexture(name, size);
        Color[] pixels = new Color[size * size];

        float half = size * 0.5f;

        // The shape is inset by the full softness so the falloff finishes INSIDE
        // the texture. Without this margin the outermost pixel is still fully
        // opaque, and because 9-slicing stretches that edge column along every
        // straight run, the "soft shadow" renders as a hard rectangle with only
        // its corners rounded.
        float margin = softness + 2f;
        float scaledRadius = Mathf.Min(radius, half - margin - 1f);
        float inner = half - margin - scaledRadius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - half) - inner;
                float dy = Mathf.Abs(y + 0.5f - half) - inner;
                float outside = Mathf.Sqrt(
                    Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) +
                    Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float distance = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - scaledRadius;

                float coverage = Mathf.Clamp01(1f - distance / Mathf.Max(0.001f, softness));

                // Squared falloff reads as a soft shadow; linear looks like a haze.
                coverage *= coverage;
                pixels[y * size + x] = new Color(1f, 1f, 1f, coverage);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        float border = Mathf.Min(scaledRadius + margin, half - 1f);
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
    }

    /// <summary>
    /// A 1-pixel-wide vertical ramp. Stretched across a rect it becomes a gradient
    /// overlay; the exponent biases the falloff so the highlight hugs the edge.
    /// </summary>
    private static Sprite BuildVerticalGradient(string name, float bottomAlpha, float topAlpha, float exponent)
    {
        const int height = 128;
        Texture2D texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[height];
        for (int y = 0; y < height; y++)
        {
            float t = y / (float)(height - 1);
            float shaped = Mathf.Pow(t, Mathf.Max(0.01f, exponent));
            pixels[y] = new Color(1f, 1f, 1f, Mathf.Lerp(bottomAlpha, topAlpha, shaped));
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(0f, 8f, 0f, 8f));
    }

    /// <summary>
    /// A cardboard crate with a taped cross, matching the blockers on the board.
    /// Full colour rather than a white mask, so the slot does not have to tint it.
    /// </summary>
    private static Sprite BuildBlockIcon(string name)
    {
        const int size = 128;
        Texture2D texture = CreateTexture(name, size);
        Color[] pixels = new Color[size * size];

        Color cardboard = new Color(0.80f, 0.58f, 0.34f, 1f);
        Color cardboardDark = new Color(0.66f, 0.45f, 0.25f, 1f);
        Color tape = new Color(0.95f, 0.88f, 0.72f, 1f);

        float half = size * 0.5f;
        const float radius = 14f;
        float inner = half - 8f - radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;

                float dx = Mathf.Abs(px) - inner;
                float dy = Mathf.Abs(py) - inner;
                float outside = Mathf.Sqrt(
                    Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float distance = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;

                float coverage = Mathf.Clamp01(0.5f - distance);
                if (coverage <= 0f)
                {
                    pixels[y * size + x] = new Color(0f, 0f, 0f, 0f);
                    continue;
                }

                // Vertical shade so the crate reads as a lit solid rather than a
                // flat brown square.
                Color body = Color.Lerp(cardboardDark, cardboard, (y / (float)size) * 0.7f + 0.3f);

                // The taped cross: distance to each of the two diagonals.
                float diagonalA = Mathf.Abs(px - py) * 0.70710678f;
                float diagonalB = Mathf.Abs(px + py) * 0.70710678f;
                float tapeDistance = Mathf.Min(diagonalA, diagonalB);
                float tapeCoverage = Mathf.Clamp01(6f - tapeDistance);

                Color final = Color.Lerp(body, tape, tapeCoverage);
                final.a = coverage;
                pixels[y * size + x] = final;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        return Sprite.Create(
            texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect);
    }

    private static Texture2D CreateTexture(string name, int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
    }
}
