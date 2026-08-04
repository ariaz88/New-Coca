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

        if (Application.isPlaying)
            VisualStyleRuntime.ApplySettings(this);
    }
}
