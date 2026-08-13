using UnityEngine;

/// <summary>
/// The sprites the bomb HUD is drawn from, generated in code.
///
/// Same reasoning as OrderPanelTextures: the HUD is added at runtime so it can
/// reach all 25 baked scenes at once, which means it cannot reference art that
/// only exists in a scene, and shipping three tiny PNGs for a rounded rect and a
/// radar glyph is more to keep in step than it is worth. Everything here is
/// built once and cached for the process.
/// </summary>
public static class BombHudTextures
{
    private static Sprite plate;
    private static Sprite pill;
    private static Sprite radarGlyph;

    /// <summary>Rounded square, 9-sliced. The button body.</summary>
    public static Sprite Plate => plate ??= BuildRounded(96, 28, "BombHudPlate");

    /// <summary>Fully rounded capsule, 9-sliced. Count badges and captions.</summary>
    public static Sprite Pill => pill ??= BuildRounded(64, 32, "BombHudPill");

    /// <summary>A radar sweep: concentric arcs plus a sweeping hand.</summary>
    public static Sprite RadarGlyph => radarGlyph ??= BuildRadar(128, "BombHudRadar");

    private static Sprite BuildRounded(int size, int radius, string name)
    {
        Texture2D texture = NewTexture(size, size, name);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float coverage = RoundedCoverage(x, y, size, size, radius);
                pixels[y * size + x] = new Color(1f, 1f, 1f, coverage);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        // The border is the radius, so 9-slicing stretches only the flat middle
        // and the corners keep their curve at any button size.
        float border = radius;
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
    /// Antialiased rounded-rectangle coverage. Sampling the distance field rather
    /// than testing inside/outside is what keeps the corners from stair-stepping
    /// at the sizes these buttons are drawn at.
    /// </summary>
    private static float RoundedCoverage(int x, int y, int width, int height, float radius)
    {
        float px = x + 0.5f;
        float py = y + 0.5f;

        float dx = Mathf.Max(Mathf.Abs(px - width * 0.5f) - (width * 0.5f - radius), 0f);
        float dy = Mathf.Max(Mathf.Abs(py - height * 0.5f) - (height * 0.5f - radius), 0f);
        float distance = Mathf.Sqrt(dx * dx + dy * dy);

        return Mathf.Clamp01(radius - distance + 0.5f);
    }

    private static Sprite BuildRadar(int size, string name)
    {
        Texture2D texture = NewTexture(size, size, name);
        Color[] pixels = new Color[size * size];

        float centre = size * 0.5f;
        float outer = size * 0.40f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - centre;
                float py = y + 0.5f - centre;
                float distance = Mathf.Sqrt(px * px + py * py);

                float alpha = 0f;

                // Three concentric rings.
                alpha = Mathf.Max(alpha, Ring(distance, outer, size * 0.055f));
                alpha = Mathf.Max(alpha, Ring(distance, outer * 0.64f, size * 0.042f));
                alpha = Mathf.Max(alpha, Ring(distance, outer * 0.30f, size * 0.038f));

                // The sweep hand, from the centre up and to the right.
                if (distance <= outer)
                {
                    float angle = Mathf.Atan2(py, px) * Mathf.Rad2Deg;
                    float delta = Mathf.Abs(Mathf.DeltaAngle(angle, 52f));
                    float hand = Mathf.Clamp01(1f - delta / 5f);
                    alpha = Mathf.Max(alpha, hand);
                }

                // A solid dot at the centre so the glyph has a focal point.
                alpha = Mathf.Max(alpha, Mathf.Clamp01(size * 0.055f - distance + 0.5f));

                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static float Ring(float distance, float radius, float thickness)
    {
        return Mathf.Clamp01(thickness * 0.5f - Mathf.Abs(distance - radius) + 0.5f);
    }

    private static Texture2D NewTexture(int width, int height, string name)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
    }
}
