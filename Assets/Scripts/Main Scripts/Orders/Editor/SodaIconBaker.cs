using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders the project's own soda model, once per color, into transparent PNG
/// sprites for the Orders panel, and writes them into a SodaVisualLibrary asset.
///
/// Why bake instead of rendering live
/// ----------------------------------
/// The requirement is that an order slot shows the same drink the player sees on
/// the board, not unrelated icon art. Two ways to do that: keep a second camera
/// rendering the model into a RenderTexture every frame, or render it once in the
/// editor and ship flat sprites.
///
/// Baking wins on a mobile puzzle game. A live RenderTexture costs a camera, a
/// render target, and a draw pass per slot, every frame, forever, to display
/// something that never changes. A baked sprite costs one texture fetch. The only
/// thing lost is runtime rotation of the icon, which the reference does not do.
///
/// The bake also samples each material's base color and stores it as the effect
/// tint, so the flying streak is guaranteed to match the drink it came from
/// rather than relying on hand-entered colors.
/// </summary>
public sealed class SodaIconBaker : EditorWindow
{
    // Bottle.prefab, not Soda.prefab. Bottle is what SpawnContoller actually
    // instantiates in every level scene; Soda.prefab is an older model whose
    // material list has since drifted - its slot 3 is still yellow where the
    // shipping Bottle is pink. Baking from the wrong prefab is exactly how the
    // Orders panel ended up showing a yellow icon for a pink drink.
    private const string DefaultSodaPrefabPath = "Assets/Prefabs/Bottle.prefab";
    private const string DefaultOutputFolder = "Assets/GameAssets/UI/OrderIcons";
    private const string DefaultLibraryFolder = "Assets/GameAssets/UI";
    private const string LibraryAssetName = "SodaVisualLibrary.asset";

    // Far enough from any real level geometry that the rig cannot capture it,
    // without pushing into floating-point precision trouble.
    private static readonly Vector3 RigOrigin = new Vector3(0f, -10000f, 0f);

    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    [SerializeField] private GameObject sodaPrefab;
    [SerializeField] private SodaVisualLibrary library;
    [SerializeField] private string outputFolder = DefaultOutputFolder;
    [SerializeField] private int iconSize = 256;
    [SerializeField, Tooltip("Rotation applied to the model before rendering. Matches the rotation SpawnContoller gives every soda it places, so the icon stands the same way up as the drink on the board.")]
    private Vector3 modelEuler = new Vector3(-90f, 0f, 0f);

    [SerializeField] private Vector3 cameraEuler = new Vector3(12f, 155f, 0f);
    [SerializeField] private float framingPadding = 1.06f;
    [SerializeField] private int supersample = 3;
    [SerializeField] private bool trimTransparentMargin = true;
    [SerializeField] private bool overwriteExistingColors = true;
    [SerializeField] private bool writeEffectColors = true;

    private Texture2D lastPreview;
    private string statusMessage;
    private MessageType statusType = MessageType.None;

    [MenuItem("Tools/Coca Sorting/Bake Soda Icons", false, 101)]
    public static void Open()
    {
        SodaIconBaker window = GetWindow<SodaIconBaker>(true, "Bake Soda Icons", true);
        window.minSize = new Vector2(380f, 460f);
        window.ResolveDefaults();
        window.Show();
    }

    /// <summary>
    /// Bakes every colour with the default settings and no window.
    ///
    /// The window exists so the camera angle can be dialled in by eye, but once
    /// that is settled a re-bake is a mechanical step - after a material changes,
    /// or after the source prefab is corrected - and should not need a human to
    /// open a window and press a button.
    /// </summary>
    [MenuItem("Tools/Coca Sorting/Bake Soda Icons (No Window)", false, 102)]
    public static void BakeAllHeadless()
    {
        SodaIconBaker baker = CreateInstance<SodaIconBaker>();
        try
        {
            baker.ResolveDefaults();
            if (baker.sodaPrefab == null)
            {
                Debug.LogError("Soda icon bake failed: no prefab at " + DefaultSodaPrefabPath);
                return;
            }

            baker.BakeAll();
            Debug.Log("Soda icons baked from " + AssetDatabase.GetAssetPath(baker.sodaPrefab) +
                      ". " + baker.statusMessage);
        }
        finally
        {
            DestroyImmediate(baker);
        }
    }

    private void OnEnable()
    {
        ResolveDefaults();
    }

    private void ResolveDefaults()
    {
        if (sodaPrefab == null)
        {
            sodaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSodaPrefabPath);
        }

        if (library == null)
        {
            string[] found = AssetDatabase.FindAssets("t:SodaVisualLibrary");
            if (found.Length > 0)
            {
                library = AssetDatabase.LoadAssetAtPath<SodaVisualLibrary>(
                    AssetDatabase.GUIDToAssetPath(found[0]));
            }
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        sodaPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Soda Prefab", "The in-game soda model. Its Soda component supplies the per-color materials."),
            sodaPrefab,
            typeof(GameObject),
            false);

        library = (SodaVisualLibrary)EditorGUILayout.ObjectField(
            new GUIContent("Visual Library", "Baked sprites are written here. Created automatically when empty."),
            library,
            typeof(SodaVisualLibrary),
            false);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        outputFolder = EditorGUILayout.TextField(
            new GUIContent("PNG Folder", "Created if it does not exist."),
            outputFolder);
        iconSize = Mathf.Clamp(
            EditorGUILayout.IntField(new GUIContent("Icon Size", "Square, in pixels."), iconSize),
            32,
            1024);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Framing", EditorStyles.boldLabel);
        cameraEuler = EditorGUILayout.Vector3Field(
            new GUIContent("Camera Angle", "Rotation of the camera around the model. Y turns the can, X tilts it."),
            cameraEuler);
        framingPadding = EditorGUILayout.Slider(
            new GUIContent("Padding", "Empty margin around the model. 1 means the model exactly fills the frame."),
            framingPadding,
            1f,
            2f);
        supersample = EditorGUILayout.IntSlider(
            new GUIContent("Supersample", "Renders this many times larger, then downsamples. Higher means smoother edges."),
            supersample,
            1,
            4);
        trimTransparentMargin = EditorGUILayout.Toggle(
            new GUIContent("Trim Margin", "Crops the empty border and re-squares, so the drink fills the icon."),
            trimTransparentMargin);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Write Options", EditorStyles.boldLabel);
        overwriteExistingColors = EditorGUILayout.Toggle(
            new GUIContent("Overwrite Existing", "Off keeps sprites that are already assigned in the library."),
            overwriteExistingColors);
        writeEffectColors = EditorGUILayout.Toggle(
            new GUIContent("Write Effect Tints", "Also store each material's base color as the streak tint."),
            writeEffectColors);

        EditorGUILayout.Space(10f);

        using (new EditorGUI.DisabledScope(sodaPrefab == null))
        {
            if (GUILayout.Button("Preview First Color", GUILayout.Height(24f)))
            {
                PreviewFirstColor();
            }

            if (GUILayout.Button("Bake All Colors", GUILayout.Height(32f)))
            {
                BakeAll();
            }
        }

        if (sodaPrefab == null)
        {
            EditorGUILayout.HelpBox(
                "Assign the soda prefab. The default is " + DefaultSodaPrefabPath + ".",
                MessageType.Warning);
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }

        if (lastPreview != null)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Preview", EditorStyles.miniBoldLabel);
            Rect rect = GUILayoutUtility.GetRect(128f, 128f, GUILayout.ExpandWidth(false));
            EditorGUI.DrawTextureTransparent(rect, lastPreview, ScaleMode.ScaleToFit);
        }
    }

    private void PreviewFirstColor()
    {
        Texture2D rendered = RenderColor(Soda.SodaColor.Red, out string error);
        if (rendered == null)
        {
            SetStatus(error, MessageType.Error);
            return;
        }

        if (lastPreview != null)
        {
            DestroyImmediate(lastPreview);
        }

        lastPreview = rendered;
        SetStatus("Preview rendered. Adjust the camera angle until the can reads clearly, then bake.", MessageType.Info);
    }

    private void BakeAll()
    {
        if (!EnsureFolder(outputFolder))
        {
            SetStatus("Could not create the output folder: " + outputFolder, MessageType.Error);
            return;
        }

        SodaVisualLibrary targetLibrary = library != null ? library : CreateLibraryAsset();
        if (targetLibrary == null)
        {
            SetStatus("Could not create a SodaVisualLibrary asset.", MessageType.Error);
            return;
        }

        library = targetLibrary;

        List<string> baked = new List<string>();
        List<string> skipped = new List<string>();

        try
        {
            // One import pass at the end instead of six keeps the editor
            // responsive and avoids six separate reimport dialogs.
            AssetDatabase.StartAssetEditing();

            foreach (Soda.SodaColor color in System.Enum.GetValues(typeof(Soda.SodaColor)))
            {
                if (!overwriteExistingColors && targetLibrary.GetIcon(color) != null)
                {
                    skipped.Add(color.ToString());
                    continue;
                }

                Texture2D rendered = RenderColor(color, out string error);
                if (rendered == null)
                {
                    skipped.Add($"{color} ({error})");
                    continue;
                }

                string path = Path.Combine(outputFolder, $"OrderIcon_{color}.png").Replace('\\', '/');
                File.WriteAllBytes(path, rendered.EncodeToPNG());
                DestroyImmediate(rendered);
                baked.Add(color.ToString());
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        // Importer settings and library wiring happen after the refresh, because
        // the sprites do not exist as assets until then.
        foreach (Soda.SodaColor color in System.Enum.GetValues(typeof(Soda.SodaColor)))
        {
            string path = Path.Combine(outputFolder, $"OrderIcon_{color}.png").Replace('\\', '/');
            if (!File.Exists(path))
            {
                continue;
            }

            ApplySpriteImportSettings(path);

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                continue;
            }

            SodaVisualLibrary.Entry entry = targetLibrary.GetOrCreateEntry(color);
            if (overwriteExistingColors || entry.iconSprite == null)
            {
                entry.iconSprite = sprite;
            }

            if (writeEffectColors && TryGetMaterialColor(color, out Color materialColor))
            {
                entry.effectColor = materialColor;
            }
        }

        EditorUtility.SetDirty(targetLibrary);
        AssetDatabase.SaveAssets();

        int wired = WireLibraryIntoOpenScenes(targetLibrary);

        string message = baked.Count > 0
            ? "Baked: " + string.Join(", ", baked)
            : "Nothing was baked.";

        if (skipped.Count > 0)
        {
            message += "\nSkipped: " + string.Join(", ", skipped);
        }

        message += wired > 0
            ? $"\n\nAssigned the library to {wired} component(s) in the open scene(s). Save the scene to keep it."
            : "\n\nNo Orders components were found in the open scene(s). Assign this library " +
              "on OrderPanelUI and OrderVfxDirector after creating the panel.";
        SetStatus(message, baked.Count > 0 ? MessageType.Info : MessageType.Warning);
        Selection.activeObject = targetLibrary;
    }

    /// <summary>
    /// Writes the library reference into every OrderPanelUI and OrderVfxDirector
    /// in the open scenes.
    ///
    /// The runtime auto-resolve in SodaVisualLibrary.Resolve covers the editor,
    /// but a player build has no AssetDatabase, and the asset is not in a
    /// Resources folder. A real serialized reference in the scene is what makes
    /// the icons appear in a build, so the bake writes one rather than leaving
    /// it as a step to remember.
    /// </summary>
    private static int WireLibraryIntoOpenScenes(SodaVisualLibrary targetLibrary)
    {
        int wired = 0;

        // Includes inactive objects, because the Orders panel commonly lives
        // under a Canvas that is disabled until gameplay starts.
        foreach (OrderPanelUI panel in Resources.FindObjectsOfTypeAll<OrderPanelUI>())
        {
            if (AssignLibraryField(panel, targetLibrary))
            {
                wired++;
            }
        }

        foreach (OrderVfxDirector director in Resources.FindObjectsOfTypeAll<OrderVfxDirector>())
        {
            if (AssignLibraryField(director, targetLibrary))
            {
                wired++;
            }
        }

        return wired;
    }

    private static bool AssignLibraryField(Component component, SodaVisualLibrary targetLibrary)
    {
        // Prefab assets and hidden objects are skipped: only real scene
        // instances should be modified by a bake.
        if (component == null || EditorUtility.IsPersistent(component) ||
            !component.gameObject.scene.IsValid())
        {
            return false;
        }

        SerializedObject serialized = new SerializedObject(component);
        SerializedProperty property = serialized.FindProperty("visualLibrary");
        if (property == null || property.objectReferenceValue == targetLibrary)
        {
            return false;
        }

        property.objectReferenceValue = targetLibrary;
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(component);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        return true;
    }

    /// <summary>
    /// Renders one color to a transparent texture. Returns null and a reason
    /// rather than throwing, so a bad prefab reports a readable message instead
    /// of an editor exception.
    /// </summary>
    private Texture2D RenderColor(Soda.SodaColor color, out string error)
    {
        error = null;

        if (sodaPrefab == null)
        {
            error = "no soda prefab";
            return null;
        }

        GameObject rig = null;
        RenderTexture renderTexture = null;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            // The whole rig is HideAndDontSave so it never appears in the
            // hierarchy, is never saved into the open scene, and cannot be
            // orphaned if this method throws.
            rig = new GameObject("SodaIconBakeRig") { hideFlags = HideFlags.HideAndDontSave };
            rig.transform.position = RigOrigin;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sodaPrefab);
            if (instance == null)
            {
                error = "prefab could not be instantiated";
                return null;
            }

            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetParent(rig.transform, false);
            instance.transform.localPosition = Vector3.zero;

            // The same corrective rotation SpawnContoller.SpawnSodaAtSlot applies
            // to every soda it places in a box. The model is authored lying down,
            // so baking it at identity produced icons of a bottle on its side while
            // the board showed it standing up.
            instance.transform.localRotation = Quaternion.Euler(modelEuler);

            if (!ApplyMaterial(instance, color, out error))
            {
                return null;
            }

            if (!TryGetVisualBounds(instance, out Bounds bounds))
            {
                error = "the prefab has no renderers";
                return null;
            }

            Camera camera = CreateCamera(rig.transform, bounds);
            CreateLights(rig.transform);

            // Rendered larger than the final icon, then downsampled. The extra
            // resolution is what removes the stair-stepping on the can's curved
            // silhouette, which is very visible at 256 px and smaller.
            int renderSize = Mathf.Min(2048, iconSize * Mathf.Max(1, supersample));

            renderTexture = RenderTexture.GetTemporary(
                renderSize,
                renderSize,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            renderTexture.antiAliasing = 4;

            camera.targetTexture = renderTexture;
            RenderCamera(camera, renderTexture);

            RenderTexture.active = renderTexture;
            Texture2D raw = new Texture2D(renderSize, renderSize, TextureFormat.RGBA32, false, false);
            raw.ReadPixels(new Rect(0f, 0f, renderSize, renderSize), 0, 0);
            raw.Apply();
            camera.targetTexture = null;

            return Postprocess(raw);
        }
        catch (System.Exception exception)
        {
            error = exception.Message;
            return null;
        }
        finally
        {
            RenderTexture.active = previousActive;

            if (renderTexture != null)
            {
                RenderTexture.ReleaseTemporary(renderTexture);
            }

            if (rig != null)
            {
                DestroyImmediate(rig);
            }
        }
    }

    /// <summary>
    /// Turns the raw render into the final icon: trim the empty margin, pad back
    /// to a square, then downsample to the requested size.
    ///
    /// Trimming is what actually fixes "the icon looks tiny". Even with perfect
    /// camera framing, a tall narrow can leaves wide transparent bands on a
    /// square target, and the UI Image then scales the whole square, margin
    /// included, so the drink itself ends up small. Cropping to the drawn pixels
    /// and re-squaring means the can fills the icon.
    /// </summary>
    private Texture2D Postprocess(Texture2D raw)
    {
        Texture2D working = raw;

        if (trimTransparentMargin && TryGetOpaqueBounds(raw, out RectInt drawn))
        {
            Texture2D cropped = CropToSquare(raw, drawn);
            if (cropped != null)
            {
                DestroyImmediate(working);
                working = cropped;
            }
        }

        if (working.width == iconSize && working.height == iconSize)
        {
            return working;
        }

        Texture2D resized = ResizeBilinear(working, iconSize);
        DestroyImmediate(working);
        return resized;
    }

    /// <summary>
    /// Finds the pixel box that actually contains the model. Alpha is tested
    /// against a small threshold rather than zero, so anti-aliased edge pixels
    /// are kept but compression noise is not mistaken for content.
    /// </summary>
    private static bool TryGetOpaqueBounds(Texture2D texture, out RectInt bounds)
    {
        const float alphaThreshold = 0.02f;

        Color32[] pixels = texture.GetPixels32();
        int width = texture.width;
        int height = texture.height;

        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (pixels[row + x].a <= alphaThreshold * 255f)
                {
                    continue;
                }

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            bounds = default;
            return false;
        }

        bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    /// <summary>
    /// Copies the drawn region into a centred square with a small even margin,
    /// so every color produces an icon with identical proportions no matter how
    /// wide or narrow its silhouette is.
    /// </summary>
    private static Texture2D CropToSquare(Texture2D source, RectInt drawn)
    {
        int side = Mathf.Max(drawn.width, drawn.height);
        side = Mathf.RoundToInt(side * 1.06f);
        side = Mathf.Min(side, Mathf.Max(source.width, source.height));

        Texture2D result = new Texture2D(side, side, TextureFormat.RGBA32, false, false);

        Color32[] target = new Color32[side * side];
        Color32 clear = new Color32(0, 0, 0, 0);
        for (int index = 0; index < target.Length; index++)
        {
            target[index] = clear;
        }

        Color32[] pixels = source.GetPixels32();
        int offsetX = (side - drawn.width) / 2;
        int offsetY = (side - drawn.height) / 2;

        for (int y = 0; y < drawn.height; y++)
        {
            int sourceRow = (drawn.y + y) * source.width;
            int targetRow = (offsetY + y) * side;

            for (int x = 0; x < drawn.width; x++)
            {
                target[targetRow + offsetX + x] = pixels[sourceRow + drawn.x + x];
            }
        }

        result.SetPixels32(target);
        result.Apply();
        return result;
    }

    /// <summary>
    /// Bilinear downsample. Texture2D has no resize, and a nearest-neighbour
    /// copy would reintroduce the aliasing the supersampled render removed.
    /// </summary>
    private static Texture2D ResizeBilinear(Texture2D source, int size)
    {
        Texture2D result = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        Color[] target = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;
            int row = y * size;

            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                target[row + x] = source.GetPixelBilinear(u, v);
            }
        }

        result.SetPixels(target);
        result.Apply();
        return result;
    }

    /// <summary>
    /// Assigns the material for a color directly from the Soda component's array
    /// rather than calling Soda.SetColor. SetColor writes through a serialized
    /// private renderer field that is not guaranteed to be wired on the prefab,
    /// and it would create a leaked material instance. The array index equals the
    /// enum value, which is exactly what SetColor's own switch does.
    /// </summary>
    private static bool ApplyMaterial(GameObject instance, Soda.SodaColor color, out string error)
    {
        error = null;

        Soda soda = instance.GetComponentInChildren<Soda>(true);
        if (soda == null)
        {
            error = "the prefab has no Soda component";
            return false;
        }

        int index = (int)color;
        if (soda.sodaMaterials == null || index >= soda.sodaMaterials.Length || soda.sodaMaterials[index] == null)
        {
            error = $"sodaMaterials has no entry {index}";
            return false;
        }

        Renderer renderer = instance.GetComponentInChildren<Renderer>(true);
        if (renderer == null)
        {
            error = "the prefab has no renderer";
            return false;
        }

        renderer.sharedMaterial = soda.sodaMaterials[index];
        return true;
    }

    private Camera CreateCamera(Transform parent, Bounds bounds)
    {
        GameObject cameraObject = new GameObject("BakeCamera") { hideFlags = HideFlags.HideAndDontSave };
        cameraObject.transform.SetParent(parent, false);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;

        // Zero alpha is what makes the PNG transparent. It has to be a fully
        // transparent black, not a transparent white, or edge pixels pick up a
        // white fringe when the sprite is blended.
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.allowHDR = false;
        camera.allowMSAA = true;

        Quaternion rotation = Quaternion.Euler(cameraEuler);
        float radius = bounds.extents.magnitude;

        cameraObject.transform.position = bounds.center - rotation * Vector3.forward * (radius * 4f);
        cameraObject.transform.rotation = rotation;

        // Framed from the bounding box projected into camera space, not from the
        // bounding sphere. A soda can is tall and narrow, so its sphere radius is
        // far larger than what the camera actually needs to cover, and using it
        // left the can as a small shape floating in a mostly empty icon.
        Vector3 extents = bounds.extents;
        float halfWidth = 0f;
        float halfHeight = 0f;

        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 offset = new Vector3(
                (corner & 1) == 0 ? -extents.x : extents.x,
                (corner & 2) == 0 ? -extents.y : extents.y,
                (corner & 4) == 0 ? -extents.z : extents.z);

            Vector3 local = cameraObject.transform.InverseTransformPoint(bounds.center + offset);
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(local.x));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.y));
        }

        // The render target is square, so the larger of the two half-extents is
        // what the orthographic size has to cover.
        camera.orthographicSize = Mathf.Max(0.001f, Mathf.Max(halfWidth, halfHeight) * framingPadding);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = radius * 12f + 10f;

        return camera;
    }

    private static void CreateLights(Transform parent)
    {
        // A key and a softer fill. One light alone leaves the unlit side of a
        // cylinder almost black, which reads badly at icon size.
        CreateLight(parent, new Vector3(35f, 210f, 0f), 1.15f, Color.white);
        CreateLight(parent, new Vector3(15f, 20f, 0f), 0.55f, new Color(0.85f, 0.9f, 1f));
    }

    private static void CreateLight(Transform parent, Vector3 euler, float intensity, Color color)
    {
        GameObject lightObject = new GameObject("BakeLight") { hideFlags = HideFlags.HideAndDontSave };
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.rotation = Quaternion.Euler(euler);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
    }

    /// <summary>
    /// Renders through the active render pipeline. URP does not support a plain
    /// Camera.Render call, so a render request is used when the pipeline accepts
    /// one, with the built-in path kept as a fallback.
    /// </summary>
    private static void RenderCamera(Camera camera, RenderTexture target)
    {
        RenderPipeline pipeline = RenderPipelineManager.currentPipeline;
        if (pipeline != null)
        {
            RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest
            {
                destination = target
            };

            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
                return;
            }
        }

        camera.Render();
    }

    private static bool TryGetVisualBounds(GameObject instance, out Bounds bounds)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return true;
    }

    private bool TryGetMaterialColor(Soda.SodaColor color, out Color result)
    {
        result = Color.white;
        if (sodaPrefab == null)
        {
            return false;
        }

        Soda soda = sodaPrefab.GetComponentInChildren<Soda>(true);
        int index = (int)color;
        if (soda == null || soda.sodaMaterials == null ||
            index >= soda.sodaMaterials.Length || soda.sodaMaterials[index] == null)
        {
            return false;
        }

        Material material = soda.sodaMaterials[index];
        if (material.HasProperty(BaseColorProperty))
        {
            result = material.GetColor(BaseColorProperty);
        }
        else if (material.HasProperty(ColorProperty))
        {
            result = material.GetColor(ColorProperty);
        }
        else
        {
            return false;
        }

        // The streak is additive-looking light, so a dark base color would make
        // it invisible. Brightening to a consistent value keeps every trail
        // readable while preserving the hue that identifies the drink.
        Color.RGBToHSV(result, out float h, out float s, out float v);
        result = Color.HSVToRGB(h, Mathf.Clamp(s, 0.45f, 0.9f), Mathf.Max(v, 0.85f));
        result.a = 1f;
        return true;
    }

    private static void ApplySpriteImportSettings(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private SodaVisualLibrary CreateLibraryAsset()
    {
        if (!EnsureFolder(DefaultLibraryFolder))
        {
            return null;
        }

        string path = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(DefaultLibraryFolder, LibraryAssetName).Replace('\\', '/'));

        SodaVisualLibrary created = CreateInstance<SodaVisualLibrary>();
        AssetDatabase.CreateAsset(created, path);
        AssetDatabase.SaveAssets();
        return created;
    }

    private static bool EnsureFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !folder.StartsWith("Assets"))
        {
            return false;
        }

        if (AssetDatabase.IsValidFolder(folder))
        {
            return true;
        }

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }

        return AssetDatabase.IsValidFolder(folder);
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
        Repaint();
    }
}
