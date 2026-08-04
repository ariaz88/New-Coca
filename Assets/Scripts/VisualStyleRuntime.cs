using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies the URP presentation settings to every gameplay camera without
/// changing camera transforms, viewport rectangles, or gameplay behaviour.
/// Reflection keeps the project able to compile while URP is being installed.
/// </summary>
internal static class VisualStyleRuntime
{
    private const string UniversalAssembly = "Unity.RenderPipelines.Universal.Runtime";
    private const string CoreAssembly = "Unity.RenderPipelines.Core.Runtime";
    private static GameObject volumeRoot;
    private static VisualStyleSettings settings;
    private static object colorAdjustments;
    private static object tonemapping;
    private static object bloom;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        settings = Resources.Load<VisualStyleSettings>("VisualStyleSettings");
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<VisualStyleSettings>();
            settings.hideFlags = HideFlags.DontSave;
        }

        settings.ApplyToSodaMaterials();
        CreateGlobalVolume();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureSceneRendering();
    }

    private static void CreateGlobalVolume()
    {
        Type volumeType = Type.GetType($"UnityEngine.Rendering.Volume, {CoreAssembly}");
        Type profileType = Type.GetType($"UnityEngine.Rendering.VolumeProfile, {CoreAssembly}");
        if (volumeType == null || profileType == null)
            return;

        volumeRoot = new GameObject("[Visual Style] Global URP Volume");
        UnityEngine.Object.DontDestroyOnLoad(volumeRoot);

        Component volume = volumeRoot.AddComponent(volumeType);
        ScriptableObject profile = ScriptableObject.CreateInstance(profileType);
        profile.name = "CocaSorting Runtime Color Grade";
        profile.hideFlags = HideFlags.DontSave;

        SetMember(volume, "isGlobal", true);
        SetMember(volume, "priority", 100f);
        SetMember(volume, "weight", 1f);
        SetMember(volume, "sharedProfile", profile);

        colorAdjustments = AddVolumeOverride(profile, "ColorAdjustments");
        tonemapping = AddVolumeOverride(profile, "Tonemapping");
        bloom = AddVolumeOverride(profile, "Bloom");
        ApplySettings(settings);
    }

    /// <summary>
    /// Refreshes the active runtime volume when the settings asset is edited
    /// during Play mode.
    /// </summary>
    internal static void ApplySettings(VisualStyleSettings updatedSettings)
    {
        if (updatedSettings == null)
            return;

        settings = updatedSettings;
        settings.ApplyToSodaMaterials();

        SetParameter(colorAdjustments, "postExposure", settings.backgroundExposure);
        SetParameter(colorAdjustments, "contrast", settings.contrast);
        SetParameter(colorAdjustments, "colorFilter", settings.colorFilter);
        SetParameter(colorAdjustments, "saturation", settings.saturation);

        SetEnumParameter(tonemapping, "mode", settings.tonemapping.ToString());

        SetParameter(bloom, "threshold", settings.bloomThreshold);
        SetParameter(bloom, "intensity", settings.bloomIntensity);
        SetParameter(bloom, "scatter", settings.bloomScatter);
        SetParameter(bloom, "clamp", settings.bloomClamp);
        SetParameter(bloom, "highQualityFiltering", settings.highQualityBloom);
    }

    private static object AddVolumeOverride(ScriptableObject profile, string overrideName)
    {
        Type overrideType = Type.GetType($"UnityEngine.Rendering.Universal.{overrideName}, {UniversalAssembly}");
        if (overrideType == null)
            return null;

        MethodInfo addMethod = profile.GetType().GetMethod(
            "Add",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(Type), typeof(bool) },
            null);

        object component = addMethod?.Invoke(profile, new object[] { overrideType, true });
        SetMember(component, "active", true);
        return component;
    }

    private static void ConfigureSceneRendering()
    {
        Type cameraDataType = Type.GetType(
            $"UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, {UniversalAssembly}");

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Camera camera in cameras)
        {
            camera.allowHDR = true;
            camera.allowMSAA = true;

            if (cameraDataType == null)
                continue;

            Component cameraData = camera.GetComponent(cameraDataType) ?? camera.gameObject.AddComponent(cameraDataType);
            SetMember(cameraData, "renderPostProcessing", true);
            SetMember(cameraData, "renderShadows", false);
            SetMember(cameraData, "dithering", true);
            SetMember(cameraData, "stopNaN", true);
            SetEnumMember(cameraData, "antialiasing", "SubpixelMorphologicalAntiAliasing");
            SetEnumMember(cameraData, "antialiasingQuality", "High");
        }

        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Light light in lights)
            light.shadows = LightShadows.None;

        Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Renderer renderer in renderers)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static void SetParameter(object volumeComponent, string fieldName, object value)
    {
        if (volumeComponent == null)
            return;

        FieldInfo field = volumeComponent.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        object parameter = field?.GetValue(volumeComponent);
        if (parameter == null)
            return;

        SetMember(parameter, "overrideState", true);
        SetMember(parameter, "value", value);
    }

    private static void SetEnumParameter(object volumeComponent, string fieldName, string valueName)
    {
        if (volumeComponent == null)
            return;

        FieldInfo field = volumeComponent.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        object parameter = field?.GetValue(volumeComponent);
        if (parameter == null)
            return;

        Type valueType = GetMemberType(parameter.GetType(), "value");
        if (valueType == null || !valueType.IsEnum)
            return;

        SetMember(parameter, "overrideState", true);
        SetMember(parameter, "value", Enum.Parse(valueType, valueName));
    }

    private static void SetEnumMember(object target, string memberName, string valueName)
    {
        if (target == null)
            return;

        Type valueType = GetMemberType(target.GetType(), memberName);
        if (valueType == null || !valueType.IsEnum)
            return;

        SetMember(target, memberName, Enum.Parse(valueType, valueName));
    }

    private static Type GetMemberType(Type targetType, string memberName)
    {
        PropertyInfo property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null)
            return property.PropertyType;

        FieldInfo field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
        return field?.FieldType;
    }

    private static void SetMember(object target, string memberName, object value)
    {
        if (target == null)
            return;

        Type targetType = target.GetType();
        PropertyInfo property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, ConvertValue(value, property.PropertyType));
            return;
        }

        FieldInfo field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (field != null)
            field.SetValue(target, ConvertValue(value, field.FieldType));
    }

    private static object ConvertValue(object value, Type destinationType)
    {
        if (value == null || destinationType.IsInstanceOfType(value))
            return value;

        return Convert.ChangeType(value, destinationType);
    }
}
