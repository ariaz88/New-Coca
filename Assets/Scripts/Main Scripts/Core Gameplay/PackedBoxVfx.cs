using UnityEngine;

/// <summary>
/// Tuning for the burst a box plays as it closes.
/// Serialized on <see cref="Board"/> so it can be tweaked in the Inspector.
/// </summary>
[System.Serializable]
public class PackedBoxVfxSettings
{
    public bool enabled = true;

    [Tooltip("Scales the whole burst. 1 matches a standard board box.")]
    [Min(0.01f)] public float scale = 1f;

    [Tooltip("Party-popper paper. Deliberately sparse - the reference uses only a handful.")]
    [Range(0, 24)] public int confettiCount = 8;

    [Tooltip("Soft puffs low around the base. These stay behind when the box launches.")]
    [Range(0, 16)] public int dustCount = 6;

    [Tooltip("White four-point twinkles over the lid.")]
    [Range(0, 16)] public int sparkleCount = 5;
}

/// <summary>
/// The burst played when a box closes: a little confetti, a little dust, a few sparkles.
///
/// Everything here is built in code - meshes, textures and materials included - so the
/// effect needs no prefab and no per-scene wiring. That matters because the alternative,
/// a serialized prefab field on Board, would have to be assigned by hand in every level
/// scene.
/// </summary>
public static class PackedBoxVfx
{
    private static readonly Color[] ConfettiPalette =
    {
        new Color(1.00f, 0.85f, 0.25f), // yellow
        new Color(0.45f, 0.85f, 0.35f), // green
        new Color(1.00f, 0.40f, 0.70f), // pink
        new Color(0.40f, 0.75f, 1.00f), // blue
        new Color(1.00f, 0.60f, 0.25f), // orange
    };

    private static Material dustMaterial;
    private static Material confettiMaterial;
    private static Material sparkleMaterial;

    public static void Play(Vector3 position, Camera cam, PackedBoxVfxSettings settings)
    {
        if (settings == null) settings = new PackedBoxVfxSettings();
        if (!settings.enabled) return;

        float s = Mathf.Max(0.01f, settings.scale);

        GameObject root = new GameObject("PackedBoxVfx");
        root.transform.position = position;

        if (settings.dustCount > 0)
        {
            EmitDust(root, position, s, settings.dustCount);
        }

        if (settings.confettiCount > 0)
        {
            EmitConfetti(root, position, s, settings.confettiCount);
        }

        if (settings.sparkleCount > 0)
        {
            EmitSparkles(root, position, s, settings.sparkleCount);
        }

        // Comfortably past the longest particle lifetime below. Destroy is play-mode only,
        // so editor tooling that previews the burst tidies up immediately instead.
        if (Application.isPlaying)
        {
            Object.Destroy(root, 3f);
        }
        else
        {
            Object.DestroyImmediate(root);
        }
    }

    // ---------------------------------------------------------------- emitters

    private static void EmitDust(GameObject root, Vector3 origin, float s, int count)
    {
        ParticleSystem ps = CreateSystem(root, "Dust", GetDustMaterial(), 0f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 1.35f)));

        for (int i = 0; i < count; i++)
        {
            // Ringed around the base rather than scattered, so the puffs read as being
            // kicked up by the box rather than floating around it.
            float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            var ep = new ParticleSystem.EmitParams
            {
                position = origin + outward * (0.09f * s) + Vector3.up * Random.Range(0f, 0.03f) * s,
                // Slow and low: the puffs should settle around the box, not shoot away.
                velocity = outward * Random.Range(0.05f, 0.14f) * s + Vector3.up * Random.Range(0.01f, 0.06f) * s,
                startSize = Random.Range(0.11f, 0.18f) * s,
                startLifetime = Random.Range(0.90f, 1.40f),
                startColor = new Color(1f, 1f, 1f, Random.Range(0.80f, 1f)),
                rotation = Random.Range(0f, 360f),
            };
            ps.Emit(ep, 1);
        }
    }

    private static void EmitConfetti(GameObject root, Vector3 origin, float s, int count)
    {
        // Positive gravity so the paper arcs and falls, the way a popper throws it.
        ParticleSystem ps = CreateSystem(root, "Confetti", GetConfettiMaterial(), 0.7f, true);

        for (int i = 0; i < count; i++)
        {
            // Spread evenly around the box rather than at fully random angles, which is
            // what stops the pieces clumping into one dense patch.
            float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            // Non-uniform size: a narrow, long quad reads as a strip of party paper,
            // where a square one just reads as a coloured cube.
            float w = Random.Range(0.009f, 0.015f) * s;
            float h = w * Random.Range(2.4f, 3.6f);

            var ep = new ParticleSystem.EmitParams
            {
                position = origin + Vector3.up * (0.09f * s) + outward * Random.Range(0f, 0.06f) * s,
                velocity = outward * Random.Range(0.40f, 1.00f) * s
                           + Vector3.up * Random.Range(0.75f, 1.35f) * s,
                startSize3D = new Vector3(w, h, 1f),
                startLifetime = Random.Range(1.1f, 1.8f),
                startColor = ConfettiPalette[Random.Range(0, ConfettiPalette.Length)],
                rotation = Random.Range(0f, 360f),
                angularVelocity = Random.Range(-320f, 320f),
            };
            ps.Emit(ep, 1);
        }
    }

    private static void EmitSparkles(GameObject root, Vector3 origin, float s, int count)
    {
        ParticleSystem ps = CreateSystem(root, "Sparkle", GetSparkleMaterial(), 0f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        // Pop out then shrink away, which is what makes a twinkle read as a twinkle.
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f)));

        for (int i = 0; i < count; i++)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = origin
                           + new Vector3(Random.Range(-0.13f, 0.13f), Random.Range(0.05f, 0.20f), Random.Range(-0.13f, 0.13f)) * s,
                velocity = Vector3.up * Random.Range(0.05f, 0.15f) * s,
                startSize = Random.Range(0.05f, 0.09f) * s,
                startLifetime = Random.Range(0.35f, 0.60f),
                startColor = Color.white,
                rotation = Random.Range(0f, 90f),
            };
            ps.Emit(ep, 1);
        }
    }

    // ---------------------------------------------------------------- plumbing

    private static ParticleSystem CreateSystem(GameObject root, string name, Material material, float gravity, bool nonUniformSize = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root.transform, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        // AddComponent starts the system playing straight away, and several main-module
        // properties - duration among them - cannot be assigned while it is running.
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1f;
        main.gravityModifier = gravity;
        main.maxParticles = 64;
        // Lets a particle be narrower than it is long, which is what turns a square into
        // a strip of confetti.
        main.startSize3D = nonUniformSize;
        // World space, so the particles stay where the box was instead of chasing it.
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // Emission and shape are both off: every particle is emitted explicitly above,
        // which is the only way to give each one its own colour from a fixed palette.
        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
        return ps;
    }

    private static Material GetDustMaterial()
    {
        if (dustMaterial == null) dustMaterial = BuildMaterial(BuildSoftCircle());
        return dustMaterial;
    }

    private static Material GetConfettiMaterial()
    {
        if (confettiMaterial == null) confettiMaterial = BuildMaterial(BuildSolidQuad());
        return confettiMaterial;
    }

    private static Material GetSparkleMaterial()
    {
        if (sparkleMaterial == null) sparkleMaterial = BuildMaterial(BuildStar());
        return sparkleMaterial;
    }

    private static Material BuildMaterial(Texture2D texture)
    {
        // Sprites/Default is alpha-blended out of the box. The URP particle shader is the
        // better fit on paper, but it defaults to *opaque* - and a half-configured opaque
        // material throws the texture's alpha away, drawing every particle as a hard
        // square. It is only used here once it has been fully switched to transparent.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        Material m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        m.mainTexture = texture;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);

        MakeTransparent(m);
        return m;
    }

    /// <summary>
    /// Switches a material to straight alpha blending. URP needs all of this - surface
    /// mode, blend factors, depth write, keyword and render queue - not just _Surface.
    /// </summary>
    private static void MakeTransparent(Material m)
    {
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    /// <summary>
    /// A lumpy cartoon puff: several overlapping lobes rather than one disc, which is
    /// what makes it read as a cloud of dust instead of a dot.
    /// </summary>
    private static Texture2D BuildSoftCircle()
    {
        const int size = 96;
        Texture2D tex = NewTexture(size);

        // Lobe centres and radii in normalised 0..1 space.
        Vector3[] lobes =
        {
            new Vector3(0.50f, 0.44f, 0.30f),
            new Vector3(0.32f, 0.54f, 0.22f),
            new Vector3(0.68f, 0.55f, 0.24f),
            new Vector3(0.50f, 0.66f, 0.20f),
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float v = (y + 0.5f) / size;

                // Take the strongest lobe at this pixel so the shapes merge into one
                // silhouette instead of darkening where they overlap.
                float a = 0f;
                foreach (Vector3 lobe in lobes)
                {
                    float d = Vector2.Distance(new Vector2(u, v), new Vector2(lobe.x, lobe.y)) / lobe.z;
                    // Solid core, soft rim.
                    a = Mathf.Max(a, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.45f, d)));
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D BuildSolidQuad()
    {
        const int size = 8;
        Texture2D tex = NewTexture(size);

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.white);

        tex.Apply();
        return tex;
    }

    private static Texture2D BuildStar()
    {
        const int size = 64;
        Texture2D tex = NewTexture(size);

        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - c) / c;
                float dy = Mathf.Abs(y + 0.5f - c) / c;

                // Bright along both axes and falling off fast away from them: the classic
                // four-point twinkle.
                float arm = Mathf.Clamp01(1f - dx) * Mathf.Clamp01(1f - dy);
                float a = Mathf.Pow(arm, 3f) * 4f;
                a = Mathf.Clamp01(a - Mathf.Min(dx, dy) * 1.6f);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D NewTexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
    }
}
