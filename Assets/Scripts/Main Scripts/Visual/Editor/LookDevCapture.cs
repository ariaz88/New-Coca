using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders the new Coke Pack crate and cans under the CURRENT scene lighting and
/// writes a PNG plus measured lit/shadow numbers to the console.
///
/// This exists because the target for the art is a specific image - the artist's
/// own render, Preview.jpg - and "does it look right yet" is not a question worth
/// answering by eye. The reference was sampled to concrete linear values; this
/// produces the same measurement from the engine so the two can be compared as
/// numbers rather than impressions.
///
/// The rig is built far below the board and torn down again, so nothing is left
/// in the scene and no asset is modified. It runs outside Play mode, which is the
/// whole point - the alternative is entering Play mode for every tweak.
/// </summary>
public static class LookDevCapture
{
    private const string OutputFolder = "Builds/Previews";
    private const string CanPrefabPath = "Assets/Prefabs/New Prefs/NEW_Coke.prefab";
    private const string CratePrefabPath = "Assets/Prefabs/New Prefs/NEW_Box.prefab";
    private const int RenderSize = 768;

    // Well below the board, so the temp camera frames the rig and nothing else.
    private static readonly Vector3 RigOrigin = new Vector3(0f, -200f, 0f);

    // The prefabs are authored lying down; the spawner corrects with -90 X.
    private static readonly Quaternion ArtRotation = Quaternion.Euler(-90f, 0f, 0f);

    // The reference backdrop, sampled from Preview.jpg.
    private static readonly Color Backdrop = new Color32(0x1F, 0x1F, 0x1F, 0xFF);

    // Orange and Pink have no new art - their material slots are null - so the
    // four that do are what gets measured.
    private static readonly Soda.SodaColor[] MeasuredColors =
    {
        Soda.SodaColor.Red,
        Soda.SodaColor.Blue,
        Soda.SodaColor.Green,
        Soda.SodaColor.Purple
    };

    // Sampled from Preview.jpg with GetPixel, converted to linear. These are what
    // the engine output is being steered toward.
    private static readonly Dictionary<Soda.SodaColor, Vector2> ReferenceLitShadow =
        new Dictionary<Soda.SodaColor, Vector2>
        {
            { Soda.SodaColor.Red,    new Vector2(0.511f, 0.072f) },
            { Soda.SodaColor.Blue,   new Vector2(0.511f, 0.289f) },
            { Soda.SodaColor.Green,  new Vector2(0.567f, 0.043f) },
            { Soda.SodaColor.Purple, new Vector2(0.580f, 0.263f) }
        };

    /// <summary>
    /// Optional suffix for the output files, so successive states can be captured
    /// and held side by side instead of overwriting each other. Empty writes the
    /// plain "current state" pair.
    /// </summary>
    public static string Label = string.Empty;

    private static readonly List<string> Report = new List<string>();

    private static void Say(string line)
    {
        Report.Add(line);
        Debug.Log("[LookDev] " + line);
    }

    [MenuItem("Tools/Coca Sorting/Visual/Capture Look Dev")]
    public static void Capture()
    {
        GameObject rig = new GameObject("[LookDev] Rig") { hideFlags = HideFlags.HideAndDontSave };
        RenderTexture previousActive = RenderTexture.active;
        Report.Clear();

        try
        {
            Directory.CreateDirectory(OutputFolder);
            LogEnvironment();

            GameObject crate = InstantiateArt(CratePrefabPath, rig.transform);
            if (crate == null)
                return;

            if (!TryGetBounds(crate, out Bounds crateBounds))
            {
                Say("ERROR: the crate prefab has no renderers.");
                return;
            }

            Say(string.Format("crate bounds  center={0} size={1}",
                crateBounds.center.ToString("F3"), crateBounds.size.ToString("F3")));

            List<GameObject> cans = PlaceCans(crate);
            if (cans.Count == 0)
                return;

            if (!TryGetBounds(rig, out Bounds rigBounds))
                return;

            Say(string.Format("rig bounds    center={0} size={1}",
                rigBounds.center.ToString("F3"), rigBounds.size.ToString("F3")));

            Camera camera = CreateCamera(rig.transform, rigBounds);

            camera.backgroundColor = Backdrop;
            Texture2D beauty = Render(camera);
            File.WriteAllBytes(Path.Combine(OutputFolder, "LookDev" + Label + ".png"), beauty.EncodeToPNG());
            Object.DestroyImmediate(beauty);
            Say("wrote LookDev" + Label + ".png");

            MeasureCans(camera, rig, cans);
        }
        finally
        {
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(rig);
            File.WriteAllLines(Path.Combine(OutputFolder, "LookDev" + Label + ".txt"), Report);
        }
    }

    private static void LogEnvironment()
    {
        Color ambient = RenderSettings.ambientLight;
        Color ambientLinear = ambient.linear;

        Say(string.Format(
            "ambient mode={0} authored=({1:F3}, {2:F3}, {3:F3}) linear=({4:F3}, {5:F3}, {6:F3}) reflection={7:F2}",
            RenderSettings.ambientMode,
            ambient.r, ambient.g, ambient.b,
            ambientLinear.r, ambientLinear.g, ambientLinear.b,
            RenderSettings.reflectionIntensity));

        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional)
                continue;

            Say(string.Format("light '{0}' intensity={1:F2} euler={2}",
                light.name, light.intensity, light.transform.eulerAngles.ToString("F0")));
        }
    }

    private static GameObject InstantiateArt(string prefabPath, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError("[LookDev] Missing prefab: " + prefabPath);
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.hideFlags = HideFlags.HideAndDontSave;
        instance.transform.SetPositionAndRotation(RigOrigin, ArtRotation);
        return instance;
    }

    /// <summary>
    /// Four cans seated in the crate the way the game seats them: parented to the
    /// crate's own SodaPosition slots. The crate root is scaled 0.19 to fit a
    /// board cell and the can prefab is authored at full size, so a can only
    /// reaches the right size by inheriting that scale through the slot. Placing
    /// cans by world position instead produced a can twice the crate's width.
    /// </summary>
    private static List<GameObject> PlaceCans(GameObject crate)
    {
        List<GameObject> cans = new List<GameObject>();

        for (int index = 0; index < MeasuredColors.Length; index++)
        {
            Transform slot = crate.transform.Find("SodaPosition" + index);
            if (slot == null)
            {
                Say("ERROR: crate has no SodaPosition" + index);
                return cans;
            }

            GameObject can = InstantiateArt(CanPrefabPath, slot);
            if (can == null)
                return cans;

            can.transform.localPosition = Vector3.zero;
            can.transform.localRotation = Quaternion.identity;
            can.transform.localScale = Vector3.one;

            Soda soda = can.GetComponent<Soda>();
            if (soda != null)
                soda.SetColor(MeasuredColors[index]);

            can.name = "[LookDev] Can " + MeasuredColors[index];
            cans.Add(can);
        }

        if (cans.Count > 0 && TryGetBounds(cans[0], out Bounds canBounds))
            Say("can bounds    size=" + canBounds.size.ToString("F3"));

        return cans;
    }

    private static Camera CreateCamera(Transform parent, Bounds bounds)
    {
        GameObject cameraObject = new GameObject("[LookDev] Camera") { hideFlags = HideFlags.HideAndDontSave };
        cameraObject.transform.SetParent(parent, false);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.orthographic = false;
        camera.fieldOfView = 30f;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        camera.cullingMask = ~0;

        // Looking down from roughly the reference's elevation.
        Quaternion rotation = Quaternion.Euler(38f, 0f, 0f);
        float radius = bounds.extents.magnitude;
        float distance = radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;

        cameraObject.transform.position = bounds.center - rotation * Vector3.forward * distance;
        cameraObject.transform.rotation = rotation;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = distance + radius * 4f + 10f;

        return camera;
    }

    /// <summary>
    /// Renders each can on its own against a transparent background, so every
    /// opaque pixel is known to belong to that can. Measuring from the beauty
    /// render instead would mix in crate, ice and backdrop pixels and the
    /// percentiles would describe the composition rather than the material.
    /// </summary>
    private static void MeasureCans(Camera camera, GameObject rig, List<GameObject> cans)
    {
        Renderer[] all = rig.GetComponentsInChildren<Renderer>(true);
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);

        Say("--- measured vs reference (linear, dominant channel) ---");

        foreach (GameObject can in cans)
        {
            HashSet<Renderer> mine = new HashSet<Renderer>(can.GetComponentsInChildren<Renderer>(true));
            foreach (Renderer renderer in all)
                renderer.enabled = mine.Contains(renderer);

            Texture2D shot = Render(camera);
            ReportOne(can.GetComponent<Soda>(), shot);
            Object.DestroyImmediate(shot);
        }

        foreach (Renderer renderer in all)
            renderer.enabled = true;
    }

    private static void ReportOne(Soda soda, Texture2D shot)
    {
        if (soda == null)
            return;

        List<Color> opaque = new List<Color>();
        Color[] pixels = shot.GetPixels();

        foreach (Color pixel in pixels)
        {
            if (pixel.a > 0.9f)
                opaque.Add(pixel);
        }

        if (opaque.Count < 32)
        {
            Say("WARNING " + soda.sodaColor + ": too few pixels to measure (" + opaque.Count + ").");
            return;
        }

        opaque.Sort((a, b) => Luminance(a).CompareTo(Luminance(b)));

        Color shadow = opaque[Mathf.RoundToInt((opaque.Count - 1) * 0.10f)].linear;
        Color lit = opaque[Mathf.RoundToInt((opaque.Count - 1) * 0.90f)].linear;

        float litMax = Mathf.Max(lit.r, Mathf.Max(lit.g, lit.b));
        float shadowMax = Mathf.Max(shadow.r, Mathf.Max(shadow.g, shadow.b));
        float ratio = shadowMax > 0.0001f ? litMax / shadowMax : 999f;

        Vector2 target = ReferenceLitShadow.TryGetValue(soda.sodaColor, out Vector2 found)
            ? found
            : Vector2.zero;
        float targetRatio = target.y > 0.0001f ? target.x / target.y : 0f;

        Say(string.Format(
            "{0,-7} lit={1:F3} shadow={2:F3} ratio={3:F1}:1   |   reference lit={4:F3} shadow={5:F3} ratio={6:F1}:1",
            soda.sodaColor, litMax, shadowMax, ratio, target.x, target.y, targetRatio));
    }

    private static float Luminance(Color c)
    {
        return c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
    }

    private static Texture2D Render(Camera camera)
    {
        RenderTexture target = RenderTexture.GetTemporary(
            RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

        try
        {
            camera.targetTexture = target;
            RenderThroughPipeline(camera, target);

            RenderTexture.active = target;
            Texture2D result = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false, false);
            result.ReadPixels(new Rect(0f, 0f, RenderSize, RenderSize), 0, 0);
            result.Apply();
            return result;
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    /// <summary>
    /// URP does not honour a plain Camera.Render, so a render request is used
    /// when the pipeline accepts one. Mirrors SodaIconBaker.RenderCamera.
    /// </summary>
    private static void RenderThroughPipeline(Camera camera, RenderTexture target)
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

    private static bool TryGetBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
            bounds.Encapsulate(renderers[index].bounds);

        return true;
    }
}
