using System.Collections;
using UnityEngine;

/// <summary>
/// The bomb explosion.
///
/// Built as six layers that peak at different times, which is what separates an
/// explosion from a puff of particles: a white flash that is gone in three
/// frames, a fireball that expands and dims, a flat shockwave ring racing
/// outward along the board, a spark burst, tumbling debris, and smoke that
/// outlives all of it and drifts. Each layer alone reads as cheap; the
/// staggered timing is the effect.
///
/// Everything is procedural because this project cannot rely on post-processing
/// - VisualStyleRuntime force-disables it on every camera each scene load - so
/// there is no bloom to lean on. The "glow" is an additive unlit material and a
/// white core, which is the same trick UIGlow.shader uses for the order effects.
///
/// The host destroys itself, so a level that ends mid-explosion leaves nothing
/// behind.
/// </summary>
public static class BombExplosionVfx
{
    private static readonly Color CoreColor = new Color(1f, 0.98f, 0.86f, 1f);
    private static readonly Color FireInner = new Color(1f, 0.82f, 0.25f, 1f);
    private static readonly Color FireOuter = new Color(1f, 0.35f, 0.06f, 1f);
    private static readonly Color SmokeColor = new Color(0.30f, 0.28f, 0.30f, 1f);
    private static readonly Color SparkColor = new Color(1f, 0.90f, 0.55f, 1f);

    /// <summary>
    /// Fires one explosion at a world position.
    /// </summary>
    /// <param name="scale">Board cell size; every layer is measured in these.</param>
    /// <param name="fatal">A level-ending blast runs bigger and longer.</param>
    public static void Play(Vector3 worldPosition, float scale, bool fatal = false)
    {
        GameObject host = new GameObject("BombExplosion");
        host.transform.position = worldPosition;

        float power = fatal ? 1.45f : 1f;

        SpawnFlash(host.transform, scale * power);
        SpawnFireball(host.transform, scale * power);
        SpawnShockwave(host.transform, scale * power);
        SpawnSparks(host.transform, scale * power);
        SpawnDebris(host.transform, scale * power);
        SpawnSmoke(host.transform, scale * power);

        Object.Destroy(host, fatal ? 2.6f : 2.0f);
    }

    /// <summary>
    /// The first three frames. A hard white disc at full size that shrinks and
    /// fades almost instantly - this is what makes the blast feel like it has a
    /// moment of detonation rather than an expansion from nothing.
    /// </summary>
    private static void SpawnFlash(Transform parent, float scale)
    {
        // Just over one cell. The first version ran to 3.6x the cell size, which
        // on a 4x5 board is wider than the board itself - the "flash" whited out
        // the entire screen and read as a bug, not as an explosion.
        GameObject flash = CreateQuad(parent, "Flash", CoreColor, additive: true);
        flash.transform.localScale = Vector3.one * scale * 0.7f;
        CoroutineRunner.Run(FadeAndScale(flash, 0.13f, scale * 0.7f, scale * 1.5f, 0.85f, 0f));
    }

    /// <summary>
    /// The fireball: a bright core inside a wider, hotter-to-cooler shell. Two
    /// discs rather than one, because a single colour reads as a sticker while
    /// a core inside a halo reads as burning.
    /// </summary>
    private static void SpawnFireball(Transform parent, float scale)
    {
        GameObject outer = CreateQuad(parent, "FireOuter", FireOuter, additive: true);
        outer.transform.localScale = Vector3.one * scale * 0.3f;
        CoroutineRunner.Run(FadeAndScale(outer, 0.42f, scale * 0.3f, scale * 1.55f, 0.9f, 0f));

        GameObject inner = CreateQuad(parent, "FireInner", FireInner, additive: true);
        inner.transform.localScale = Vector3.one * scale * 0.22f;
        CoroutineRunner.Run(FadeAndScale(inner, 0.30f, scale * 0.22f, scale * 0.95f, 0.95f, 0f));
    }

    /// <summary>
    /// A thin ring travelling outward across the board surface. It is what sells
    /// the blast as happening ON the board rather than in front of the camera,
    /// because it is flat against the same plane the cells are on.
    /// </summary>
    private static void SpawnShockwave(Transform parent, float scale)
    {
        GameObject ring = CreateQuad(parent, "Shockwave", new Color(1f, 0.95f, 0.80f, 1f), additive: true);
        ring.transform.localScale = Vector3.one * scale * 0.4f;
        CoroutineRunner.Run(RingRoutine(ring, 0.5f, scale * 0.4f, scale * 2.6f));
    }

    private static void SpawnSparks(Transform parent, float scale)
    {
        ParticleSystem system = CreateSystem(parent, "Sparks");

        ParticleSystem.MainModule main = system.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(scale * 4f, scale * 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(scale * 0.06f, scale * 0.16f);
        main.startColor = SparkColor;
        main.gravityModifier = 0.9f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.playOnAwake = false;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 34) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = scale * 0.12f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        // Stretched so a spark reads as a streak of light rather than a dot.
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.06f;
        renderer.lengthScale = 2.4f;
        ApplyParticleMaterial(renderer, additive: true);

        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = true;
        fade.color = new ParticleSystem.MinMaxGradient(Fade(Color.white, 1f, 0.45f));

        system.Play();
    }

    private static void SpawnDebris(Transform parent, float scale)
    {
        ParticleSystem system = CreateSystem(parent, "Debris");

        ParticleSystem.MainModule main = system.main;
        main.duration = 0.7f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(scale * 2.5f, scale * 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(scale * 0.10f, scale * 0.22f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.playOnAwake = false;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.20f, 0.19f, 0.22f, 1f), new Color(0.42f, 0.28f, 0.16f, 1f));

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 48f;
        shape.radius = scale * 0.10f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = true;
        spin.x = new ParticleSystem.MinMaxCurve(-620f, 620f);
        spin.y = new ParticleSystem.MinMaxCurve(-620f, 620f);
        spin.z = new ParticleSystem.MinMaxCurve(-620f, 620f);

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetChunkMesh();
        renderer.alignment = ParticleSystemRenderSpace.World;
        ApplyParticleMaterial(renderer, additive: false);

        system.Play();
    }

    /// <summary>
    /// Smoke outlives the fire and keeps drifting, which is what stops the blast
    /// from vanishing as if it had been switched off.
    /// </summary>
    private static void SpawnSmoke(Transform parent, float scale)
    {
        ParticleSystem system = CreateSystem(parent, "Smoke");

        ParticleSystem.MainModule main = system.main;
        main.duration = 0.9f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(scale * 0.5f, scale * 1.8f);

        // Sized against the cell, not the screen. One puff is about half a cell
        // and grows to roughly one; bigger than that and a handful of them cover
        // the whole board on a portrait phone.
        main.startSize = new ParticleSystem.MinMaxCurve(scale * 0.45f, scale * 0.85f);
        main.startColor = new ParticleSystem.MinMaxGradient(SmokeColor, new Color(0.55f, 0.53f, 0.55f, 1f));
        main.gravityModifier = -0.10f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.playOnAwake = false;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 7),
            new ParticleSystem.Burst(0.12f, 4)
        });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = scale * 0.30f;

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1.9f));

        ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-70f, 70f);

        // Holds opacity briefly then falls away, rather than fading from frame
        // one - smoke that starts dying immediately reads as fog.
        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = true;
        fade.color = new ParticleSystem.MinMaxGradient(Fade(Color.white, 0.62f, 0.35f));

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        ApplyParticleMaterial(renderer, additive: false, texture: OrderVfxTextures.Sparkle.texture);

        system.Play();
    }

    // ----------------------------------------------------------------- pieces

    private static IEnumerator FadeAndScale(
        GameObject target, float seconds, float fromScale, float toScale, float fromAlpha, float toAlpha)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        Material material = renderer != null ? renderer.sharedMaterial : null;
        Color baseColor = material != null ? material.GetColor("_BaseColor") : Color.white;

        float elapsed = 0f;
        while (elapsed < seconds && target != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);

            // Fast out, slow in: the first frames of an explosion carry it.
            float eased = 1f - (1f - t) * (1f - t);
            target.transform.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, eased);

            if (material != null)
            {
                Color tint = baseColor;
                tint.a = Mathf.Lerp(fromAlpha, toAlpha, t);
                SetColor(material, tint);
            }

            yield return null;
        }

        if (target != null)
        {
            target.SetActive(false);
        }
    }

    private static IEnumerator RingRoutine(GameObject ring, float seconds, float fromScale, float toScale)
    {
        Renderer renderer = ring.GetComponent<Renderer>();
        Material material = renderer != null ? renderer.sharedMaterial : null;
        Color baseColor = material != null ? material.GetColor("_BaseColor") : Color.white;

        float elapsed = 0f;
        while (elapsed < seconds && ring != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            // Flattened on Y so it hugs the board plane instead of ballooning.
            float radius = Mathf.Lerp(fromScale, toScale, eased);
            ring.transform.localScale = new Vector3(radius, radius * 0.18f, radius);

            if (material != null)
            {
                Color tint = baseColor;
                tint.a = Mathf.Lerp(0.85f, 0f, t * t);
                SetColor(material, tint);
            }

            yield return null;
        }

        if (ring != null)
        {
            ring.SetActive(false);
        }
    }

    private static GameObject CreateQuad(Transform parent, string name, Color color, bool additive)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        quad.name = name;

        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = Vector3.zero;

        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateMaterial(color, additive);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return quad;
    }

    private static ParticleSystem CreateSystem(Transform parent, string name)
    {
        GameObject host = new GameObject(name);
        host.transform.SetParent(parent, false);
        host.transform.localPosition = Vector3.zero;

        ParticleSystem system = host.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    private static void ApplyParticleMaterial(
        ParticleSystemRenderer renderer, bool additive, Texture texture = null)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Particles/Standard Unlit")
                        ?? Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        if (texture != null)
        {
            material.mainTexture = texture;
        }

        // Transparency has to be configured for BOTH modes. URP's particle
        // shaders default to opaque, and setting the blend factors alone is not
        // enough - the surface type, ZWrite, the keyword and the render queue all
        // move together. Leaving the alpha-blended path unconfigured rendered the
        // smoke as solid grey slabs that covered the whole board.
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 1f : 0f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", additive
            ? (int)UnityEngine.Rendering.BlendMode.One
            : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        renderer.material = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Material CreateMaterial(Color color, bool additive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Unlit/Color")
                        ?? Shader.Find("Standard");

        Material material = new Material(shader) { name = "BombBlast" };

        material.SetFloat("_Surface", 1f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", additive
            ? (int)UnityEngine.Rendering.BlendMode.One
            : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50;

        SetColor(material, color);
        return material;
    }

    private static void SetColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static Gradient Fade(Color color, float holdAlpha, float holdUntil)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[]
            {
                new GradientAlphaKey(holdAlpha, 0f),
                new GradientAlphaKey(holdAlpha, holdUntil),
                new GradientAlphaKey(0f, 1f)
            });

        return gradient;
    }

    private static Mesh chunkMesh;

    private static Mesh GetChunkMesh()
    {
        if (chunkMesh != null)
        {
            return chunkMesh;
        }

        // A cube, so debris tumbles with visible faces. Built once and shared.
        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chunkMesh = probe.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(probe);
        return chunkMesh;
    }
}

/// <summary>
/// Runs a coroutine with no MonoBehaviour of its own to host it.
///
/// The explosion layers are driven by a static class, and the objects they
/// animate get destroyed on their own schedule, so hanging the coroutines off
/// any of them risks the routine dying with its target. A dedicated hidden
/// runner outlives all of them.
/// </summary>
internal sealed class CoroutineRunner : MonoBehaviour
{
    private static CoroutineRunner instance;

    public static void Run(IEnumerator routine)
    {
        if (instance == null)
        {
            GameObject host = new GameObject("~CoroutineRunner") { hideFlags = HideFlags.HideAndDontSave };
            instance = host.AddComponent<CoroutineRunner>();
        }

        instance.StartCoroutine(routine);
    }
}
