using UnityEngine;

public enum VisualTonemappingMode
{
    None,
    Neutral,
    ACES
}

/// <summary>
/// Central, Inspector-editable visual settings shared by every gameplay scene.
/// The runtime volume reads this asset before the first scene is loaded.
/// </summary>
[CreateAssetMenu(fileName = "VisualStyleSettings", menuName = "Coca Sorting/Visual Style Settings")]
public sealed class VisualStyleSettings : ScriptableObject
{
    [Header("Background / General Lighting")]
    [Tooltip("Overall scene exposure in stops. +0.263 is approximately 20% brighter.")]
    [Range(-2f, 2f)] public float backgroundExposure = 0.05f;

    [Range(-50f, 50f)] public float contrast = 3f;
    public Color colorFilter = Color.white;
    [Range(-100f, 100f)] public float saturation = 4f;
    public VisualTonemappingMode tonemapping = VisualTonemappingMode.Neutral;

    [Header("Bloom")]
    [Min(0f)] public float bloomThreshold = 2f;
    [Min(0f)] public float bloomIntensity = 0.2f;
    [Range(0f, 1f)] public float bloomScatter = 0.55f;
    [Min(0f)] public float bloomClamp = 2f;
    public bool highQualityBloom = true;

    [Header("Scene Ambient")]
    [Tooltip("Applied to RenderSettings on every scene load. Only URP/Lit objects " +
             "react to this - CocaSorting/ToyGloss computes its own lighting and " +
             "never samples ambient, so the board and all legacy art are unaffected.")]
    public bool overrideAmbient = true;

    [Tooltip("Authored in gamma, converted to linear by Unity. The key light sits " +
             "at 1.0 linear, so this value sets the lit-to-shadow ratio: 0.72 gives " +
             "roughly 3:1 and reads flat, 0.42 gives roughly 7:1 and matches the " +
             "artist's reference render.")]
    [ColorUsage(false)] public Color ambientColor = new Color(0.42f, 0.43f, 0.50f);

    [Tooltip("The default reflection source is the built-in skybox, which is bright " +
             "and blue. Glossy materials pick it up as an environment sheen that " +
             "washes colour back out, so it is turned down rather than left at 1.15.")]
    [Range(0f, 1.5f)] public float reflectionIntensity = 0.25f;

    [Header("Soda Materials")]
    [Range(0f, 2f)] public float sodaBrightness = 1f;
    [Range(0f, 1f)] public float sodaGlossiness = 0.8f;
    [Range(0f, 3f)] public float sodaHighlightStrength = 1.5f;
    [Range(0f, 2f)] public float sodaRimStrength = 0.22f;

    [SerializeField, HideInInspector] private Material[] sodaMaterials = new Material[0];

    public void ApplyToSodaMaterials()
    {
        if (sodaMaterials == null)
            return;

        foreach (Material material in sodaMaterials)
        {
            if (material == null)
                continue;

            SetFloatIfChanged(material, "_Brightness", sodaBrightness);
            SetFloatIfChanged(material, "_Glossiness", sodaGlossiness);
            SetFloatIfChanged(material, "_HighlightStrength", sodaHighlightStrength);
            SetFloatIfChanged(material, "_RimStrength", sodaRimStrength);
        }
    }

    /// <summary>
    /// Pushes the ambient settings into the live RenderSettings. Called on every
    /// scene load so all 28 baked scenes pick it up without being re-authored,
    /// the same way the HUD is skinned at runtime rather than per scene.
    /// </summary>
    public void ApplyToRenderSettings()
    {
        if (!overrideAmbient)
            return;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.reflectionIntensity = reflectionIntensity;
    }

    private static void SetFloatIfChanged(Material material, string propertyName, float value)
    {
        if (!material.HasProperty(propertyName) || Mathf.Approximately(material.GetFloat(propertyName), value))
            return;

        material.SetFloat(propertyName, value);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(material);
#endif
    }

    private void OnValidate()
    {
        ApplyToSodaMaterials();

        // Also applied outside Play mode so the Scene and Game views show what the
        // build will show while the values are being dragged. The runtime override
        // runs on every scene load regardless, so whatever a scene happens to have
        // saved for ambient never reaches the player.
        ApplyToRenderSettings();

        if (Application.isPlaying)
            VisualStyleRuntime.ApplySettings(this);
    }
}
