using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Rendering;

internal static class InstallUniversalRenderPipeline
{
    private const string PackageId = "com.unity.render-pipelines.universal@17.0.4";
    private const string SessionKey = "CocaSorting.InstallingURP";
    private const string SettingsFolder = "Assets/Settings";
    private const string PipelinePath = SettingsFolder + "/CocaSortingURP.asset";
    private const string ConvertedMaterialsFolder = SettingsFolder + "/URP Converted Materials";
    private const string UniversalAssembly = "Unity.RenderPipelines.Universal.Runtime";

    private static AddRequest request;

    [MenuItem("Tools/Coca Sorting/Legacy/Run URP Installer Manually")]
    private static void RunManually()
    {
        EditorApplication.delayCall += BeginInstallOrConfigure;
    }

    private static void BeginInstallOrConfigure()
    {
        Type pipelineType = GetUniversalType("UniversalRenderPipelineAsset");
        if (pipelineType != null)
        {
            SessionState.EraseBool(SessionKey);
            ConfigureProject(pipelineType);
            return;
        }

        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        Debug.Log($"Installing {PackageId} for the CocaSorting visual upgrade...");
        request = Client.Add(PackageId);
        EditorApplication.update += TrackInstall;
    }

    private static void TrackInstall()
    {
        if (request == null || !request.IsCompleted)
            return;

        EditorApplication.update -= TrackInstall;

        if (request.Status == StatusCode.Success)
        {
            Debug.Log($"Installed {request.Result.packageId} successfully. URP configuration will continue after reload.");
            return;
        }

        SessionState.EraseBool(SessionKey);
        Debug.LogError($"URP installation failed: {request.Error?.message}");
    }

    private static void ConfigureProject(Type pipelineType)
    {
        EnsureFolder(SettingsFolder);
        EnsureFolder(ConvertedMaterialsFolder);

        RenderPipelineAsset pipelineAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelinePath);
        if (pipelineAsset == null)
            pipelineAsset = CreatePipelineAsset(pipelineType);

        if (pipelineAsset == null)
        {
            Debug.LogError("Could not create the CocaSorting URP pipeline asset.");
            return;
        }

        ConfigurePipelineAsset(pipelineAsset);
        ActivatePipelineForEveryQualityLevel(pipelineAsset);

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("URP/Lit shader was not found after URP installation.");
            return;
        }

        int convertedExternal = ConvertExternalStandardMaterials(urpLit);
        int convertedEmbedded = ConvertEmbeddedModelMaterials(urpLit);

        PlayerSettings.colorSpace = ColorSpace.Linear;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"CocaSorting URP visual configuration complete. " +
            $"Converted {convertedExternal} material assets and {convertedEmbedded} embedded model materials. " +
            "HDR, 4x MSAA, integrated post-processing, and shadow-free rendering are enabled.");
    }

    private static RenderPipelineAsset CreatePipelineAsset(Type pipelineType)
    {
        Type rendererDataType = GetUniversalType("UniversalRendererData");
        Type rendererTypeEnum = GetUniversalType("RendererType");
        if (rendererDataType == null || rendererTypeEnum == null)
            return null;

        ScriptableObject rendererData = null;
        MethodInfo createRendererAsset = pipelineType.GetMethod(
            "CreateRendererAsset",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(string), rendererTypeEnum, typeof(bool), typeof(string) },
            null);

        if (createRendererAsset != null)
        {
            object universalRenderer = Enum.Parse(rendererTypeEnum, "UniversalRenderer");
            rendererData = createRendererAsset.Invoke(
                null,
                new[] { PipelinePath, universalRenderer, true, "Renderer" }) as ScriptableObject;
        }

        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance(rendererDataType);
            AssetDatabase.CreateAsset(rendererData, SettingsFolder + "/CocaSortingURP_Renderer.asset");
        }

        MethodInfo createPipeline = pipelineType.GetMethods(BindingFlags.Static | BindingFlags.Public)
            .FirstOrDefault(method => method.Name == "Create" && method.GetParameters().Length == 1);
        RenderPipelineAsset pipelineAsset = createPipeline?.Invoke(null, new object[] { rendererData }) as RenderPipelineAsset;

        if (pipelineAsset != null)
            AssetDatabase.CreateAsset(pipelineAsset, PipelinePath);

        return pipelineAsset;
    }

    private static void ConfigurePipelineAsset(RenderPipelineAsset pipelineAsset)
    {
        SerializedObject serialized = new SerializedObject(pipelineAsset);
        SetBool(serialized, "m_RequireDepthTexture", false);
        SetBool(serialized, "m_RequireOpaqueTexture", false);
        SetBool(serialized, "m_SupportsTerrainHoles", true);
        SetBool(serialized, "m_SupportsHDR", true);
        SetInt(serialized, "m_HDRColorBufferPrecision", 0);
        SetInt(serialized, "m_MSAA", 4);
        SetFloat(serialized, "m_RenderScale", 1f);
        SetInt(serialized, "m_MainLightRenderingMode", 1);
        SetBool(serialized, "m_MainLightShadowsSupported", false);
        SetInt(serialized, "m_AdditionalLightsRenderingMode", 1);
        SetInt(serialized, "m_AdditionalLightsPerObjectLimit", 2);
        SetBool(serialized, "m_AdditionalLightShadowsSupported", false);
        SetBool(serialized, "m_AnyShadowsSupported", false);
        SetBool(serialized, "m_SoftShadowsSupported", false);
        SetFloat(serialized, "m_ShadowDistance", 0f);
        SetInt(serialized, "m_ShadowCascadeCount", 1);
        SetBool(serialized, "m_ReflectionProbeBlending", false);
        SetBool(serialized, "m_ReflectionProbeBoxProjection", false);
        SetBool(serialized, "m_UseSRPBatcher", true);
        SetBool(serialized, "m_SupportsDynamicBatching", true);
        SetBool(serialized, "m_MixedLightingSupported", false);
        SetBool(serialized, "m_SupportsLightCookies", false);
        SetBool(serialized, "m_SupportsLightLayers", false);
        SetBool(serialized, "m_UseAdaptivePerformance", false);
        SetInt(serialized, "m_ColorGradingMode", 0);
        SetInt(serialized, "m_ColorGradingLutSize", 32);
        SetBool(serialized, "m_UseFastSRGBLinearConversion", true);
        SetBool(serialized, "m_SupportDataDrivenLensFlare", false);
        SetBool(serialized, "m_SupportScreenSpaceLensFlare", false);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipelineAsset);

        SerializedProperty rendererList = serialized.FindProperty("m_RendererDataList");
        if (rendererList != null && rendererList.arraySize > 0)
        {
            ScriptableObject rendererData = rendererList.GetArrayElementAtIndex(0).objectReferenceValue as ScriptableObject;
            if (rendererData != null)
            {
                SerializedObject rendererSerialized = new SerializedObject(rendererData);
                SetInt(rendererSerialized, "m_RenderingMode", 0);
                SetInt(rendererSerialized, "m_DepthPrimingMode", 0);
                SetBool(rendererSerialized, "m_AccurateGbufferNormals", false);
                SetBool(rendererSerialized, "m_NativeRenderPass", true);
                rendererSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rendererData);
            }
        }
    }

    private static void ActivatePipelineForEveryQualityLevel(RenderPipelineAsset pipelineAsset)
    {
        GraphicsSettings.defaultRenderPipeline = pipelineAsset;

        int originalQuality = QualitySettings.GetQualityLevel();
        string[] qualityNames = QualitySettings.names;
        for (int index = 0; index < qualityNames.Length; index++)
        {
            QualitySettings.SetQualityLevel(index, false);
            QualitySettings.renderPipeline = pipelineAsset;
            QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
            QualitySettings.shadowDistance = 0f;
            QualitySettings.antiAliasing = 4;
            QualitySettings.pixelLightCount = 2;
        }

        QualitySettings.SetQualityLevel(originalQuality, false);
    }

    private static int ConvertExternalStandardMaterials(Shader urpLit)
    {
        int converted = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                continue;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null ||
                (!IsBuiltInStandard(material.shader) && !IsSerializedBuiltInStandard(path)))
                continue;

            ConvertMaterial(material, material, urpLit);
            EditorUtility.SetDirty(material);
            converted++;
        }

        return converted;
    }

    private static int ConvertEmbeddedModelMaterials(Shader urpLit)
    {
        int converted = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
                continue;

            Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> remaps = importer.GetExternalObjectMap();
            bool importerChanged = false;

            foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>())
            {
                var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name);
                if (remaps.TryGetValue(identifier, out UnityEngine.Object remapped) && remapped is Material)
                    continue;

                if (!IsBuiltInStandard(embedded.shader))
                    continue;

                string modelName = MakeSafeFileName(Path.GetFileNameWithoutExtension(modelPath));
                string materialName = MakeSafeFileName(embedded.name);
                string convertedPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{ConvertedMaterialsFolder}/{modelName}_{materialName}.mat");

                Material convertedMaterial = new Material(urpLit) { name = embedded.name };
                ConvertMaterial(embedded, convertedMaterial, urpLit);
                AssetDatabase.CreateAsset(convertedMaterial, convertedPath);
                importer.AddRemap(identifier, convertedMaterial);
                importerChanged = true;
                converted++;
            }

            if (importerChanged)
                importer.SaveAndReimport();
        }

        return converted;
    }

    private static void ConvertMaterial(Material source, Material destination, Shader urpLit)
    {
        Texture mainTexture = GetSavedTexture(source, "_MainTex", out Vector2 textureScale, out Vector2 textureOffset);
        Color color = GetSavedColor(source, "_Color", Color.white);
        float smoothness = GetSavedFloat(source, "_Glossiness", 0.35f);
        float metallic = GetSavedFloat(source, "_Metallic", 0f);
        int renderQueue = source.renderQueue;

        destination.shader = urpLit;
        destination.SetTexture("_BaseMap", mainTexture);
        destination.SetTextureScale("_BaseMap", textureScale);
        destination.SetTextureOffset("_BaseMap", textureOffset);
        destination.SetColor("_BaseColor", color);
        destination.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
        destination.SetFloat("_Metallic", Mathf.Clamp01(metallic));
        destination.SetFloat("_SpecularHighlights", 1f);
        destination.SetFloat("_EnvironmentReflections", 1f);
        destination.renderQueue = renderQueue;
    }

    private static bool IsBuiltInStandard(Shader shader)
    {
        if (shader == null)
            return false;

        return shader.name == "Standard" ||
               shader.name == "Standard (Specular setup)" ||
               shader.name == "Hidden/InternalErrorShader";
    }

    private static bool IsSerializedBuiltInStandard(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
            return false;

        string serializedMaterial = File.ReadAllText(assetPath);
        return serializedMaterial.Contains(
                   "m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}") ||
               serializedMaterial.Contains(
                   "m_Shader: {fileID: 47, guid: 0000000000000000f000000000000000, type: 0}");
    }

    private static Texture GetSavedTexture(
        Material material,
        string propertyName,
        out Vector2 scale,
        out Vector2 offset)
    {
        scale = Vector2.one;
        offset = Vector2.zero;

        if (material.HasProperty(propertyName))
        {
            scale = material.GetTextureScale(propertyName);
            offset = material.GetTextureOffset(propertyName);
            return material.GetTexture(propertyName);
        }

        SerializedProperty entry = FindSavedProperty(material, "m_TexEnvs", propertyName);
        SerializedProperty value = entry?.FindPropertyRelative("second");
        if (value == null)
            return null;

        scale = value.FindPropertyRelative("m_Scale")?.vector2Value ?? Vector2.one;
        offset = value.FindPropertyRelative("m_Offset")?.vector2Value ?? Vector2.zero;
        return value.FindPropertyRelative("m_Texture")?.objectReferenceValue as Texture;
    }

    private static Color GetSavedColor(Material material, string propertyName, Color fallback)
    {
        if (material.HasProperty(propertyName))
            return material.GetColor(propertyName);

        SerializedProperty entry = FindSavedProperty(material, "m_Colors", propertyName);
        SerializedProperty value = entry?.FindPropertyRelative("second");
        return value != null ? value.colorValue : fallback;
    }

    private static float GetSavedFloat(Material material, string propertyName, float fallback)
    {
        if (material.HasProperty(propertyName))
            return material.GetFloat(propertyName);

        SerializedProperty entry = FindSavedProperty(material, "m_Floats", propertyName);
        SerializedProperty value = entry?.FindPropertyRelative("second");
        return value != null ? value.floatValue : fallback;
    }

    private static SerializedProperty FindSavedProperty(
        Material material,
        string collectionName,
        string propertyName)
    {
        SerializedObject serializedMaterial = new SerializedObject(material);
        SerializedProperty collection = serializedMaterial.FindProperty(
            $"m_SavedProperties.{collectionName}");
        if (collection == null || !collection.isArray)
            return null;

        for (int index = 0; index < collection.arraySize; index++)
        {
            SerializedProperty entry = collection.GetArrayElementAtIndex(index);
            SerializedProperty key = entry.FindPropertyRelative("first");
            if (key != null && key.stringValue == propertyName)
                return entry.Copy();
        }

        return null;
    }

    private static Type GetUniversalType(string typeName)
    {
        return Type.GetType($"UnityEngine.Rendering.Universal.{typeName}, {UniversalAssembly}");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static string MakeSafeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return value.Replace('#', '_').Replace('/', '_').Replace('\\', '_');
    }

    private static void SetBool(SerializedObject serialized, string name, bool value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetInt(SerializedObject serialized, string name, int value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
            property.intValue = value;
    }

    private static void SetFloat(SerializedObject serialized, string name, float value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
            property.floatValue = value;
    }
}
