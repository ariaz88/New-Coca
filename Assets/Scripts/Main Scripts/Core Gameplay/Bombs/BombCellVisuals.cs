using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The marker a bomb cell shows, and the burst it makes when it goes off.
///
/// Generated in code for the same reason BlockedCellVisuals is: the board's cell
/// tiles are painted into the Platform mesh and there is no per-cell prefab to
/// hang art on, so anything that must sit on a specific cell has to be built at
/// runtime from the board's own spacing.
///
/// Four conventions are carried over from BlockedCellVisuals deliberately, so
/// the two read the same way:
///   - a named root child, found and destroyed first, so every call is idempotent
///   - sized from the board's cell spacing rather than from a prefab
///   - EVERY collider destroyed on generated art, so nothing intercepts a drop
///   - shadows off
///
/// This is grey-box art. It is legible and it animates, but the wire-cutting,
/// sparks and smoke in the design brief are a polish pass, not this one.
/// </summary>
public static class BombCellVisuals
{
    private const string MarkerName = "Bomb Marker";
    private const string BodyName = "Body";
    private const string FuseName = "Fuse";

    /// <summary>
    /// The bomb body. Black in every state, deliberately: a bomb that changed
    /// colour to show its state stopped reading as a bomb, and against this
    /// board's pale blue tiles black is the highest-contrast thing on screen -
    /// which is what a hazard the player has to memorise needs to be.
    /// </summary>
    private static readonly Color BodyTint = new Color(0.055f, 0.055f, 0.07f, 1f);

    // State is carried by the fuse instead, so the silhouette never changes.
    private static readonly Color RevealedFuse = new Color(0.90f, 0.16f, 0.14f, 1f);
    private static readonly Color ArmedFuse = new Color(1f, 0.52f, 0.06f, 1f);
    private static readonly Color DefusedFuse = new Color(0.24f, 0.78f, 0.38f, 1f);

    /// <summary>
    /// Builds the marker for one bomb, parented to the Board and centred on the
    /// cell. It is created hidden: the level-start preview is what first shows it.
    /// </summary>
    public static GameObject CreateMarker(Transform boardTransform, Vector3 localPosition, Vector2 cellSize)
    {
        if (boardTransform == null)
        {
            return null;
        }

        GameObject root = new GameObject(MarkerName);
        root.transform.SetParent(boardTransform, false);
        root.transform.localPosition = localPosition;
        root.transform.localRotation = Quaternion.identity;

        float cell = Mathf.Min(Mathf.Abs(cellSize.x), Mathf.Abs(cellSize.y));

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = BodyName;
        StripCollider(body);
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = Vector3.one * (cell * 0.52f);
        Tint(body, BodyTint);

        // A short stub on top. The sphere alone reads as a dot at phone size;
        // the stub is what makes it read as a bomb at a glance.
        GameObject fuse = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fuse.name = FuseName;
        StripCollider(fuse);
        fuse.transform.SetParent(root.transform, false);
        fuse.transform.localPosition = new Vector3(0f, cell * 0.30f, 0f);
        fuse.transform.localScale = new Vector3(cell * 0.10f, cell * 0.13f, cell * 0.10f);
        Tint(fuse, RevealedFuse);

        root.SetActive(false);
        return root;
    }

    /// <summary>
    /// Applies the state colour to the FUSE, never to the body.
    ///
    /// Kept separate from visibility so the scanner can show and hide a bomb
    /// repeatedly without rebuilding anything.
    /// </summary>
    public static void ApplyState(BombRuntime bomb)
    {
        if (bomb?.Visual == null)
        {
            return;
        }

        Transform fuse = bomb.Visual.transform.Find(FuseName);
        if (fuse == null)
        {
            return;
        }

        Color tint = bomb.IsDefused ? DefusedFuse : bomb.IsArmed ? ArmedFuse : RevealedFuse;
        Tint(fuse.gameObject, tint);
    }

    /// <summary>
    /// Shows or hides a bomb marker. Hiding is what makes the mechanic a memory
    /// test, so this is the single place that decides whether a bomb is on screen.
    /// </summary>
    public static void SetVisible(BombRuntime bomb, bool visible)
    {
        if (bomb?.Visual == null)
        {
            return;
        }

        bomb.Visual.SetActive(visible);
        if (visible)
        {
            ApplyState(bomb);
        }
    }

    /// <summary>
    /// The red pulse a revealed bomb runs while it is on screen. Driven by a
    /// coroutine on a caller-supplied runner rather than by a tween, so a scanner
    /// sweep that is cancelled mid-flight leaves nothing running.
    /// </summary>
    public static IEnumerator PulseRoutine(BombRuntime bomb, float seconds, float pulsesPerSecond = 3f)
    {
        if (bomb?.Visual == null || seconds <= 0f)
        {
            yield break;
        }

        Transform body = bomb.Visual.transform.Find(BodyName);
        if (body == null)
        {
            yield break;
        }

        Vector3 baseScale = body.localScale;
        float elapsed = 0f;

        while (elapsed < seconds && bomb.Visual != null && bomb.Visual.activeSelf)
        {
            elapsed += Time.deltaTime;
            float wave = 0.5f + 0.5f * Mathf.Sin(elapsed * pulsesPerSecond * Mathf.PI * 2f);
            body.localScale = baseScale * Mathf.Lerp(0.86f, 1.16f, wave);
            yield return null;
        }

        if (body != null)
        {
            body.localScale = baseScale;
        }
    }

    /// <summary>
    /// The defuse animation: the marker turns green, swells once and settles.
    /// Short on purpose - it plays inside the drop the player just made, and a
    /// long one would read as the game hanging.
    /// </summary>
    public static IEnumerator DefuseRoutine(BombRuntime bomb, float seconds = 0.45f)
    {
        if (bomb?.Visual == null)
        {
            yield break;
        }

        // ApplyState turns the fuse green. It deliberately stays visible: with the
        // body black in every state, the fuse is the only thing carrying "this one
        // is safe now", and hiding it would leave the defuse reading as nothing.
        ApplyState(bomb);

        Transform body = bomb.Visual.transform.Find(BodyName);
        if (body == null)
        {
            yield break;
        }

        Vector3 baseScale = body.localScale;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            float swell = Mathf.Sin(t * Mathf.PI);
            body.localScale = baseScale * (1f + swell * 0.35f);
            yield return null;
        }

        body.localScale = baseScale;
    }

    /// <summary>
    /// The expanding ring an explosion or a defuse pulse leaves behind. Runs on a
    /// caller-supplied runner and destroys itself, so nothing is left in the scene
    /// if the level ends mid-animation.
    /// </summary>
    public static IEnumerator ShockwaveRoutine(
        Transform parent,
        Vector3 localPosition,
        float cellSize,
        Color color,
        float seconds = 0.4f,
        float maxRadiusInCells = 2.2f)
    {
        if (parent == null)
        {
            yield break;
        }

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Bomb Shockwave";
        StripCollider(ring);
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = localPosition;
        Tint(ring, color);

        Renderer ringRenderer = ring.GetComponent<Renderer>();
        Material material = ringRenderer != null ? ringRenderer.sharedMaterial : null;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            float radius = Mathf.Lerp(0.2f, maxRadiusInCells, t) * cellSize;
            ring.transform.localScale = new Vector3(radius, cellSize * 0.02f, radius);

            if (material != null)
            {
                Color faded = color;
                faded.a = Mathf.Lerp(color.a, 0f, t);
                SetMaterialColor(material, faded);
            }

            yield return null;
        }

        Object.Destroy(ring);
    }

    private static void StripCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }
    }

    private static void Tint(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.sharedMaterial = CreateMaterial(color);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader) { name = "BombMarker" };

        // Transparent so the shockwave can fade. The surface-type switch on
        // URP/Lit is not one property - blend state, ZWrite, queue and the
        // keyword all have to move together or it renders opaque.
        if (color.a < 1f && material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        SetMaterialColor(material, color);
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.25f);
        }

        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
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
}
