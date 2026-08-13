using UnityEngine;

/// <summary>
/// Builds the two things a blocked Board cell needs beyond its box model:
/// the packing-tape cross taped across its lid, and the dust-and-confetti burst
/// that plays when a packed match breaks it open.
///
/// Both are generated in code rather than authored as assets. The tape has to
/// sit exactly on top of whatever box prefab a level uses and match that box's
/// own color, which a fixed prefab cannot do; and the burst is two particle
/// systems whose entire configuration is a handful of numbers. Keeping them here
/// means a level designer only assigns a box prefab and everything else follows.
///
/// Nothing in this file touches Board state. It creates GameObjects, plays them,
/// and cleans them up.
/// </summary>
public static class BlockedCellVisuals
{
    private const string TapeRootName = "TapeCross";
    private const string FrostRootName = "Frost";
    private const string CrackRootName = "Cracks";

    /// <summary>
    /// Lays two crossed tape strips over the top face of a blocked box.
    ///
    /// The strips are sized and placed from the box's own renderer bounds, so
    /// this works for any box prefab without per-prefab tuning. Their color is
    /// derived from the box's material rather than fixed, which is what keeps
    /// the cross reading as tape on cardboard instead of as a painted decal.
    /// </summary>
    /// <param name="boxVisual">The instantiated blocked-cell box.</param>
    /// <param name="tapeColor">Explicit strip color, or null to derive a lighter shade of the box.</param>
    /// <param name="lighten">Used only when <paramref name="tapeColor"/> is null.</param>
    /// <param name="widthFraction">Strip width as a fraction of the box's smaller top edge.</param>
    /// <param name="lengthFactor">Strip length as a fraction of the lid's diagonal.</param>
    /// <param name="surfaceLift">Extra height above the lid, to avoid z-fighting.</param>
    public static void AttachTapeCross(
        GameObject boxVisual,
        Color? tapeColor = null,
        float lighten = 0.22f,
        float widthFraction = 0.12f,
        float lengthFactor = 0.75f,
        float surfaceLift = 0.004f)
    {
        if (boxVisual == null)
        {
            return;
        }

        // Removing a previous cross first makes this safe to call twice, which
        // matters because a level can regenerate its blockers.
        Transform existing = boxVisual.transform.Find(TapeRootName);
        if (existing != null)
        {
            Object.Destroy(existing.gameObject);
        }

        if (!TryGetWorldBounds(boxVisual, out Bounds bounds, out Color boxColor))
        {
            return;
        }

        // An explicit color is the reliable path. Deriving one from the box's
        // material only works when the box is actually colored by its material;
        // if the color arrives through a MaterialPropertyBlock, as the blocked
        // cell tint used to, the sampled value is the material's untinted white
        // and the tape comes out white instead of cream.
        Material tapeMaterial = CreateTapeMaterial(tapeColor ?? Lighten(boxColor, lighten));

        GameObject root = new GameObject(TapeRootName);
        root.transform.SetParent(boxVisual.transform, false);

        // Placed in world space from the bounds, then re-parented, because the
        // box prefab may have arbitrary local scale or a pivot that is not at
        // its centre. Working from bounds sidesteps both.
        Vector3 top = new Vector3(bounds.center.x, bounds.max.y + surfaceLift, bounds.center.z);

        float shortEdge = Mathf.Min(bounds.size.x, bounds.size.z);
        float stripWidth = Mathf.Max(0.001f, shortEdge * widthFraction);
        float stripThickness = Mathf.Max(0.0005f, shortEdge * 0.015f);

        // Measured against the lid's diagonal, because the strips run at 45
        // degrees. A full-diagonal strip reaches corner to corner and, once its
        // own width is added, spills past them; three quarters keeps the ends
        // inside the box the way taped cardboard looks.
        float stripLength = new Vector2(bounds.size.x, bounds.size.z).magnitude * lengthFactor;

        CreateStrip(root.transform, top, stripLength, stripThickness, stripWidth, 45f, tapeMaterial);
        CreateStrip(root.transform, top, stripLength, stripThickness, stripWidth, -45f, tapeMaterial);
    }

    private static void CreateStrip(
        Transform parent,
        Vector3 worldCentre,
        float length,
        float thickness,
        float width,
        float yawDegrees,
        Material material)
    {
        GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = $"Tape {yawDegrees:0}";

        // The cube primitive ships with a collider. A decorative blocker must
        // never intercept a drop raycast, so it is removed immediately.
        Collider collider = strip.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        strip.transform.SetParent(parent, true);
        strip.transform.position = worldCentre;
        strip.transform.rotation = parent.rotation * Quaternion.Euler(0f, yawDegrees, 0f);
        SetWorldScale(strip.transform, new Vector3(length, thickness, width));

        Renderer renderer = strip.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    /// <summary>
    /// Wraps a blocker in a pale translucent frost shell.
    ///
    /// This is what tells the player a cell costs two adjacent matches instead of
    /// one, and it has to read BEFORE any damage is dealt - a blocker whose extra
    /// cost only becomes visible after the first wasted match is a trap, not a
    /// mechanic. Sized from the box's own bounds, so it fits any box prefab.
    /// </summary>
    public static void AttachFrostOverlay(
        GameObject boxVisual,
        Color frostColor,
        float thicknessFraction = 0.1f)
    {
        if (boxVisual == null)
        {
            return;
        }

        Transform existing = boxVisual.transform.Find(FrostRootName);
        if (existing != null)
        {
            Object.Destroy(existing.gameObject);
        }

        if (!TryGetWorldBounds(boxVisual, out Bounds bounds, out _))
        {
            return;
        }

        GameObject root = new GameObject(FrostRootName);
        root.transform.SetParent(boxVisual.transform, false);

        GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shell.name = "FrostShell";

        Collider collider = shell.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        shell.transform.SetParent(root.transform, true);
        shell.transform.position = bounds.center;
        shell.transform.rotation = boxVisual.transform.rotation;

        // Slightly larger than the box on every axis so the frost reads as a rime
        // growing over the cardboard rather than as z-fighting with it.
        float swell = 1f + Mathf.Clamp(thicknessFraction, 0.01f, 0.4f);
        SetWorldScale(shell.transform, bounds.size * swell);

        Renderer renderer = shell.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateFrostMaterial(frostColor);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    /// <summary>
    /// Marks a blocker as damaged but not yet open.
    ///
    /// The damaged state is carried on three independent channels, because a
    /// single one is not reliably readable on a phone mid-animation:
    /// fracture lines across the lid (geometry), a thinned frost shell (colour),
    /// and a permanently reduced silhouette (shape). A player glancing at a still
    /// frame has to be able to tell one-hit-left from untouched.
    ///
    /// The pattern is seeded from the caller, so a replayed level cracks
    /// identically - the level tooling compares screenshots.
    /// </summary>
    public static void ApplyCrackedLook(
        GameObject boxVisual,
        int seed,
        Color crackColor,
        int crackCount = 5,
        float widthFraction = 0.035f,
        float surfaceLift = 0.006f)
    {
        if (boxVisual == null)
        {
            return;
        }

        Transform existing = boxVisual.transform.Find(CrackRootName);
        if (existing != null)
        {
            Object.Destroy(existing.gameObject);
        }

        if (!TryGetWorldBounds(boxVisual, out Bounds bounds, out _))
        {
            return;
        }

        // Thin the frost so the box looks like it is losing its shell, and shrink
        // the whole visual a touch so the damaged silhouette differs from intact.
        Transform frost = boxVisual.transform.Find(FrostRootName);
        if (frost != null)
        {
            foreach (Renderer frostRenderer in frost.GetComponentsInChildren<Renderer>(true))
            {
                Material material = frostRenderer.sharedMaterial;
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    Color faded = material.GetColor("_BaseColor");
                    faded.a *= 0.45f;
                    material.SetColor("_BaseColor", faded);
                }

                if (material.HasProperty("_Color"))
                {
                    Color faded = material.GetColor("_Color");
                    faded.a *= 0.45f;
                    material.SetColor("_Color", faded);
                }
            }
        }

        boxVisual.transform.localScale *= 0.94f;

        if (crackCount <= 0)
        {
            return;
        }

        GameObject root = new GameObject(CrackRootName);
        root.transform.SetParent(boxVisual.transform, false);

        Material crackMaterial = CreateTapeMaterial(crackColor);

        Vector3 top = new Vector3(bounds.center.x, bounds.max.y + surfaceLift, bounds.center.z);
        float shortEdge = Mathf.Min(bounds.size.x, bounds.size.z);
        float crackWidth = Mathf.Max(0.0006f, shortEdge * widthFraction);
        float crackThickness = Mathf.Max(0.0004f, shortEdge * 0.012f);
        float diagonal = new Vector2(bounds.size.x, bounds.size.z).magnitude;

        // A symmetric star radiating from the centre of the lid.
        //
        // The first version scattered five strips at random angles and random
        // offsets, aiming for a shattered pane. At the size a cell occupies on a
        // phone that did not read as damage at all - it read as a dark scribble
        // someone had drawn on the box, and players could not tell what it meant.
        // Evenly spaced spokes from one point are instantly legible as a crack,
        // and being symmetric they look deliberate rather than like an artefact.
        System.Random random = new System.Random(seed);
        int spokes = Mathf.Max(2, crackCount);
        float baseYaw = (float)(random.NextDouble() * 180.0);

        for (int index = 0; index < spokes; index++)
        {
            float yaw = baseYaw + index * (180f / spokes);

            // Slight per-spoke jitter so the star is not mechanically perfect.
            yaw += (float)(random.NextDouble() - 0.5) * 12f;
            float length = diagonal * Mathf.Lerp(0.5f, 0.72f, (float)random.NextDouble());

            CreateStrip(root.transform, top, length, crackThickness, crackWidth, yaw, crackMaterial);
        }
    }

    /// <summary>
    /// Small shard spray for a blocker that cracked without opening. Reuses the
    /// confetti system with a single-colour palette and a lower count, so a crack
    /// reads as a smaller event than a break.
    /// </summary>
    public static void PlayCrackBurst(
        Vector3 worldPosition,
        float cellSize,
        Color shardColor,
        int shardCount = 12)
    {
        if (shardCount <= 0)
        {
            return;
        }

        SpawnConfetti(worldPosition, cellSize, new[] { shardColor, Lighten(shardColor, 0.3f) }, shardCount);
    }

    private static Material CreateFrostMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader) { name = "BlockedCellFrost" };

        // URP/Lit defaults to opaque, and the surface-type switch is not a single
        // property: the blend state, ZWrite, render queue and the _SURFACE_TYPE
        // keyword all have to move together or the shell renders as a solid box.
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.9f);
        }

        return material;
    }

    /// <summary>
    /// Plays the break burst at a cell: a low puff of dust plus a spray of
    /// colored paper. Both systems destroy themselves when they finish.
    /// </summary>
    /// <param name="worldPosition">Centre of the cell that is opening.</param>
    /// <param name="cellSize">Board cell size, used to scale the burst.</param>
    /// <param name="dustColor">Tint of the dust puff.</param>
    /// <param name="confettiColors">Palette the paper picks from.</param>
    /// <param name="dustCount">Dust particles emitted.</param>
    /// <param name="confettiCount">Paper pieces emitted.</param>
    public static void PlayBreakBurst(
        Vector3 worldPosition,
        float cellSize,
        Color dustColor,
        Color[] confettiColors,
        int dustCount = 18,
        int confettiCount = 26)
    {
        if (dustCount > 0)
        {
            SpawnDust(worldPosition, cellSize, dustColor, dustCount);
        }

        if (confettiCount > 0 && confettiColors != null && confettiColors.Length > 0)
        {
            SpawnConfetti(worldPosition, cellSize, confettiColors, confettiCount);
        }
    }

    private static void SpawnDust(Vector3 worldPosition, float cellSize, Color color, int count)
    {
        GameObject host = new GameObject("BlockedCellDust");
        host.transform.position = worldPosition;

        ParticleSystem system = host.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.duration = 0.6f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(cellSize * 0.6f, cellSize * 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(cellSize * 0.35f, cellSize * 0.75f);
        main.startColor = color;
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.playOnAwake = false;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        // A flat hemisphere: dust from a box breaking should spread sideways
        // along the board, not shoot upward like a fountain.
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = cellSize * 0.28f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.4f));

        // Dust should billow then thin out, so opacity holds briefly and then
        // falls away rather than fading linearly from the first frame.
        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = true;
        fade.color = new ParticleSystem.MinMaxGradient(BuildFadeGradient(Color.white, 0.85f, 0.25f));

        ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-90f, 90f);

        ConfigureRenderer(system, OrderVfxTextures.Sparkle.texture, false);
        system.Play();
    }

    private static void SpawnConfetti(Vector3 worldPosition, float cellSize, Color[] palette, int count)
    {
        GameObject host = new GameObject("BlockedCellConfetti");
        host.transform.position = worldPosition;

        ParticleSystem system = host.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.duration = 0.8f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(cellSize * 1.2f, cellSize * 2.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(cellSize * 0.10f, cellSize * 0.20f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.55f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.playOnAwake = false;

        // Random between a set of colors, so every piece of paper is one solid
        // color from the palette instead of a blend of two.
        main.startColor = BuildPaletteGradient(palette);

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 55f;
        shape.radius = cellSize * 0.16f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        // Tumbling is what separates paper from sparks. Without it the pieces
        // read as glowing dots.
        ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = true;
        spin.x = new ParticleSystem.MinMaxCurve(-540f, 540f);
        spin.y = new ParticleSystem.MinMaxCurve(-540f, 540f);
        spin.z = new ParticleSystem.MinMaxCurve(-540f, 540f);

        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = true;
        fade.color = new ParticleSystem.MinMaxGradient(BuildFadeGradient(Color.white, 1f, 0.7f));

        // Rectangular strips rather than billboards: a stretched quad mesh gives
        // paper its shape, and the tumbling above then shows both faces.
        ParticleSystemRenderer renderer = ConfigureRenderer(system, null, true);
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetConfettiMesh();
        renderer.alignment = ParticleSystemRenderSpace.World;

        system.Play();
    }

    private static ParticleSystemRenderer ConfigureRenderer(
        ParticleSystem system,
        Texture texture,
        bool opaqueTint)
    {
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Material material = new Material(GetParticleShader());
        if (texture != null)
        {
            material.mainTexture = texture;
        }

        if (opaqueTint)
        {
            material.color = Color.white;
        }

        renderer.material = material;
        return renderer;
    }

    /// <summary>
    /// Finds a shader that works for particles under the active pipeline.
    /// Sprites/Default is the reliable fallback: it is always present and
    /// renders vertex-colored transparent quads correctly under URP too.
    /// </summary>
    private static Shader GetParticleShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        return shader != null ? shader : Shader.Find("Sprites/Default");
    }

    private static Mesh confettiMesh;

    private static Mesh GetConfettiMesh()
    {
        if (confettiMesh != null)
        {
            return confettiMesh;
        }

        // A single quad, wider than it is tall, so a piece looks like a strip of
        // paper. Built once and reused by every burst.
        confettiMesh = new Mesh
        {
            name = "ConfettiQuad",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = new[]
            {
                new Vector3(-0.5f, -0.28f, 0f),
                new Vector3(0.5f, -0.28f, 0f),
                new Vector3(0.5f, 0.28f, 0f),
                new Vector3(-0.5f, 0.28f, 0f)
            },
            uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            },
            triangles = new[] { 0, 2, 1, 0, 3, 2 }
        };

        confettiMesh.RecalculateNormals();
        confettiMesh.RecalculateBounds();
        return confettiMesh;
    }

    private static Gradient BuildFadeGradient(Color color, float holdAlpha, float holdUntil)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(holdAlpha, 0f),
                new GradientAlphaKey(holdAlpha, holdUntil),
                new GradientAlphaKey(0f, 1f)
            });

        return gradient;
    }

    private static ParticleSystem.MinMaxGradient BuildPaletteGradient(Color[] palette)
    {
        if (palette.Length == 1)
        {
            return new ParticleSystem.MinMaxGradient(palette[0]);
        }

        // Two hard-stepped gradients used as a random range. Hard stops mean a
        // particle picks one palette entry outright rather than an interpolated
        // in-between color.
        Gradient low = BuildSteppedGradient(palette, false);
        Gradient high = BuildSteppedGradient(palette, true);
        return new ParticleSystem.MinMaxGradient(low, high);
    }

    private static Gradient BuildSteppedGradient(Color[] palette, bool reversed)
    {
        int count = Mathf.Min(8, palette.Length);
        GradientColorKey[] colorKeys = new GradientColorKey[count];

        for (int index = 0; index < count; index++)
        {
            Color color = palette[reversed ? count - 1 - index : index];
            colorKeys[index] = new GradientColorKey(color, count == 1 ? 0f : index / (float)(count - 1));
        }

        Gradient gradient = new Gradient { mode = GradientMode.Fixed };
        gradient.SetKeys(
            colorKeys,
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        return gradient;
    }

    private static bool TryGetWorldBounds(GameObject target, out Bounds bounds, out Color dominantColor)
    {
        bounds = default;
        dominantColor = new Color(0.72f, 0.55f, 0.35f, 1f);

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return false;
        }

        bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        // Sampled from the box's own material so the tape always relates to the
        // cardboard it is stuck to, including when a level overrides the color.
        Material material = renderers[0].sharedMaterial;
        if (material != null)
        {
            if (material.HasProperty("_BaseColor"))
            {
                dominantColor = material.GetColor("_BaseColor");
            }
            else if (material.HasProperty("_Color"))
            {
                dominantColor = material.GetColor("_Color");
            }
        }

        return true;
    }

    /// <summary>
    /// Moves a color toward white while pulling a little saturation out, which
    /// is how masking tape reads against cardboard. A plain Lerp to white also
    /// washes out the hue and made the cross look grey.
    /// </summary>
    public static Color Lighten(Color color, float amount)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        s = Mathf.Clamp01(s * (1f - amount * 0.55f));
        v = Mathf.Clamp01(v + (1f - v) * amount + 0.05f);

        Color result = Color.HSVToRGB(h, s, v);
        result.a = color.a;
        return result;
    }

    private static Material CreateTapeMaterial(Color color)
    {
        // Lit so the tape shades with the board's lighting like the box does.
        // An unlit strip would sit on the lid as a flat sticker.
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader) { name = "BlockedCellTape" };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.1f);
        }

        return material;
    }

    private static void SetWorldScale(Transform target, Vector3 worldScale)
    {
        Transform parent = target.parent;
        if (parent == null)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale = new Vector3(
            parentScale.x != 0f ? worldScale.x / parentScale.x : worldScale.x,
            parentScale.y != 0f ? worldScale.y / parentScale.y : worldScale.y,
            parentScale.z != 0f ? worldScale.z / parentScale.z : worldScale.z);
    }
}
