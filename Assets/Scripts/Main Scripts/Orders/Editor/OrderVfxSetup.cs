using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click setup for the order delivery effect's assets.
///
/// Both assets this creates are optional at runtime: OrderVfxDirector falls back to
/// built-in default settings and to a material created from the shader on the fly.
/// The tool exists for two reasons anyway.
///
/// The first is tuning. Default settings live in code and cannot be edited in the
/// inspector; a real asset can, and one asset drives all five level scenes at once.
///
/// The second is builds, and it is the important one. A shader that is only ever
/// found through Shader.Find, with no material asset referencing it, is not pulled
/// into a player build and the effect silently loses its glow on device. Creating
/// the material and registering the shader as always-included both fix that; this
/// tool does both.
/// </summary>
public static class OrderVfxSetup
{
    private const string ShaderName = "Coca Sorting/UI Glow";
    private const string SettingsPath = "Assets/Resources/OrderVfxSettings.asset";
    private const string MaterialPath = "Assets/Material/UI_OrderVfxGlow.mat";

    [MenuItem("Tools/Coca Sorting/Set Up Order VFX Assets", false, 101)]
    public static void SetUpOrderVfxAssets()
    {
        OrderVfxSettings settings = CreateSettingsAsset();
        Material material = CreateGlowMaterial();

        RegisterAlwaysIncludedShader();

        int wired = AssignToDirectorsInOpenScenes(settings, material);

        // Saved individually rather than through AssetDatabase.SaveAssets. A blanket
        // save also tries to write every other dirty asset in the project, including
        // read-only ones inside immutable package folders, which fails loudly and
        // has nothing to do with this tool.
        if (settings != null)
        {
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        if (material != null)
        {
            AssetDatabase.SaveAssetIfDirty(material);
        }

        Debug.Log(
            $"Order VFX assets ready.\n" +
            $"  Settings: {SettingsPath}\n" +
            $"  Material: {MaterialPath}\n" +
            $"  Directors wired in open scenes: {wired}\n" +
            "Open each of the other level scenes and run this again, or assign the two " +
            "assets on their OrderVfxDirector by hand.");

        Selection.activeObject = settings;
    }

    private static OrderVfxSettings CreateSettingsAsset()
    {
        OrderVfxSettings existing = AssetDatabase.LoadAssetAtPath<OrderVfxSettings>(SettingsPath);
        if (existing != null)
        {
            // Never overwritten. Re-running the tool must not silently discard a
            // designer's tuning.
            return existing;
        }

        EnsureFolder("Assets/Resources");

        // Placed in Resources so OrderVfxSettings.Resolve finds it in a player
        // build, matching how SodaVisualLibrary is handled.
        OrderVfxSettings created = ScriptableObject.CreateInstance<OrderVfxSettings>();
        AssetDatabase.CreateAsset(created, SettingsPath);
        return created;
    }

    private static Material CreateGlowMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError(
                $"Shader '{ShaderName}' was not found. Make sure Assets/Shaders/UIGlow.shader " +
                "compiled without errors before running this tool.");
            return null;
        }

        EnsureFolder("Assets/Material");

        Material created = new Material(shader) { name = "UI_OrderVfxGlow" };
        AssetDatabase.CreateAsset(created, MaterialPath);
        return created;
    }

    /// <summary>
    /// Adds the glow shader to the Always Included Shaders list.
    ///
    /// Without this, a build that reaches the shader only through Shader.Find gets
    /// a null and the effect renders with no glow at all on device while looking
    /// perfect in the editor. That is a difficult failure to diagnose after the
    /// fact, so it is prevented here rather than documented.
    /// </summary>
    private static void RegisterAlwaysIncludedShader()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            return;
        }

        Object[] graphicsSettings =
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");

        if (graphicsSettings == null || graphicsSettings.Length == 0)
        {
            Debug.LogWarning(
                "Could not open GraphicsSettings, so the glow shader was not registered as " +
                "always-included. Add it by hand under Project Settings > Graphics, or the " +
                "effect will lose its glow in a player build.");
            return;
        }

        SerializedObject serialized = new SerializedObject(graphicsSettings[0]);

        SerializedProperty included = serialized.FindProperty("m_AlwaysIncludedShaders");
        if (included == null)
        {
            return;
        }

        for (int index = 0; index < included.arraySize; index++)
        {
            if (included.GetArrayElementAtIndex(index).objectReferenceValue == shader)
            {
                return;
            }
        }

        included.InsertArrayElementAtIndex(included.arraySize);
        included.GetArrayElementAtIndex(included.arraySize - 1).objectReferenceValue = shader;
        serialized.ApplyModifiedProperties();
    }

    /// <summary>
    /// Assigns both assets onto every director in the currently open scenes, so the
    /// common case needs no manual dragging at all.
    /// </summary>
    private static int AssignToDirectorsInOpenScenes(OrderVfxSettings settings, Material material)
    {
        List<OrderVfxDirector> directors = new List<OrderVfxDirector>(
            Object.FindObjectsByType<OrderVfxDirector>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None));

        for (int index = 0; index < directors.Count; index++)
        {
            OrderVfxDirector director = directors[index];
            SerializedObject serialized = new SerializedObject(director);

            SerializedProperty settingsProperty = serialized.FindProperty("settings");
            if (settingsProperty != null && settings != null)
            {
                settingsProperty.objectReferenceValue = settings;
            }

            SerializedProperty materialProperty = serialized.FindProperty("glowMaterial");
            if (materialProperty != null && material != null)
            {
                materialProperty.objectReferenceValue = material;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(director);
        }

        if (directors.Count > 0)
        {
            EditorSceneManager.MarkAllScenesDirty();
        }

        return directors.Count;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int lastSlash = path.LastIndexOf('/');
        string parent = path.Substring(0, lastSlash);
        string leaf = path.Substring(lastSlash + 1);

        AssetDatabase.CreateFolder(parent, leaf);
    }
}
