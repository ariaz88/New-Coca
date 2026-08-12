using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the loading screen prefab to a PNG without entering play mode.
///
/// The screen is a Screen Space - Overlay canvas, so it never shows up in the Scene view and
/// normally can only be judged by pressing Play. This temporarily re-hosts a throwaway copy on
/// an off-screen camera, poses it at a representative frame, and grabs the result.
/// </summary>
public static class LoadingScreenPreview
{
    private const string PrefabPath = "Assets/Prefabs/OldPrefabs/LoadingScreen.prefab";
    private const int Width = 621;
    private const int Height = 1344;
    private const int UILayer = 5;

    [MenuItem("Tools/Loading Screen/Render Preview")]
    public static void Render()
    {
        string path = RenderTo(Path.Combine(Path.GetTempPath(), "loading_preview.png"), 0.62f);
        if (path != null)
        {
            Debug.Log("Loading screen preview written to " + path);
            EditorUtility.RevealInFinder(path);
        }
    }

    [MenuItem("Tools/Loading Screen/Render Title Filmstrip")]
    public static void RenderFilmstrip()
    {
        string path = RenderFilmstripTo(Path.Combine(Path.GetTempPath(), "loading_title.png"), 5, 2.4f);
        if (path != null)
        {
            Debug.Log("Title filmstrip written to " + path);
            EditorUtility.RevealInFinder(path);
        }
    }

    [MenuItem("Tools/Loading Screen/Render Responsive Preview Set")]
    public static void RenderResponsivePreviewSet()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputDirectory = Path.Combine(projectRoot, "Temp", "LoadingScreenPreviews");
        Directory.CreateDirectory(outputDirectory);

        RenderTo(Path.Combine(outputDirectory, "loading_9x16.png"), 0.62f, 540, 960, 1.35f);
        RenderTo(Path.Combine(outputDirectory, "loading_19_5x9.png"), 0.62f, 540, 1170, 1.35f);
        RenderTo(Path.Combine(outputDirectory, "loading_3x4.png"), 0.62f, 768, 1024, 1.35f);

        Debug.Log("Responsive loading-screen previews written to " + outputDirectory);
    }

    /// <summary>
    /// Renders the title breathing at evenly spaced moments, tiled left to right. The animation
    /// only exists in Update, so this is the only way to check its shape outside play mode.
    /// </summary>
    public static string RenderFilmstripTo(string outputPath, int frames, float lastMoment)
    {
        var shots = new Texture2D[frames];
        try
        {
            for (int i = 0; i < frames; i++)
            {
                shots[i] = RenderFrame(0.62f, lastMoment * i / (frames - 1f), Width, Height);
                if (shots[i] == null) return null;
            }

            int w = shots[0].width / 2, h = shots[0].height / 2;
            var strip = new Texture2D(w * frames, h, TextureFormat.RGBA32, false);
            for (int i = 0; i < frames; i++)
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                strip.SetPixel(i * w + x, y, shots[i].GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h));
            strip.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, strip.EncodeToPNG());
            Object.DestroyImmediate(strip);
            return outputPath;
        }
        finally
        {
            foreach (var s in shots) if (s != null) Object.DestroyImmediate(s);
        }
    }

    /// <summary>Renders the prefab at the given fill fraction and returns the file path, or null on failure.</summary>
    public static string RenderTo(string outputPath, float progress)
    {
        return RenderTo(outputPath, progress, Width, Height, 0f);
    }

    private static string RenderTo(string outputPath, float progress, int width, int height, float moment)
    {
        var shot = RenderFrame(progress, moment, width, height);
        if (shot == null) return null;
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllBytes(outputPath, shot.EncodeToPNG());
        Object.DestroyImmediate(shot);
        return outputPath;
    }

    private static Texture2D RenderFrame(float progress, float moment, int width, int height)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("LoadingScreenPreview: prefab not found at " + PrefabPath);
            return null;
        }

        GameObject temp = null;
        GameObject camGo = null;
        RenderTexture rt = null;
        Texture2D shot = null;

        try
        {
            temp = Object.Instantiate(prefab);
            temp.hideFlags = HideFlags.HideAndDontSave;
            temp.SetActive(true);
            SetLayerRecursive(temp.transform, UILayer);

            camGo = new GameObject("LoadingPreviewCam", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 1 << UILayer;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGo.transform.position = new Vector3(0f, 0f, -100f);

            rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            cam.targetTexture = rt;

            var canvas = temp.GetComponentInChildren<Canvas>(true);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            PoseAt(temp, progress);

            // Let the component compute the breathing pose, so the preview shows the real
            // animation rather than a second copy of the maths that could drift out of step.
            var view = temp.GetComponentInChildren<LoadingScreenUI>(true);
            if (view != null) view.EditorPoseAt(moment, progress);

            Canvas.ForceUpdateCanvases();
            cam.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            shot = new Texture2D(width, height, TextureFormat.RGBA32, false);
            shot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;

            cam.targetTexture = null;
            var captured = shot;
            shot = null; // ownership passes to the caller
            return captured;
        }
        finally
        {
            if (shot != null) Object.DestroyImmediate(shot);
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            if (camGo != null) Object.DestroyImmediate(camGo);
            if (temp != null) Object.DestroyImmediate(temp);
        }
    }

    // LoadingScreenUI.Update does not tick in edit mode, so stage the same state it would produce.
    private static void PoseAt(GameObject root, float progress)
    {
        var fill = Find<Image>(root, "Fill");
        if (fill != null)
        {
            fill.type = Image.Type.Sliced;
            fill.fillAmount = 1f;
        }

        Canvas.ForceUpdateCanvases();

        var fillArea = Find<RectTransform>(root, "FillArea");
        var clip = Find<RectTransform>(root, "FillClip");
        var shine = Find<RectTransform>(root, "Shine");
        if (fillArea != null && clip != null)
        {
            float filled = fillArea.rect.width * progress;
            if (fill != null)
            {
                RectTransform fillRect = fill.rectTransform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, filled);
            }
            clip.sizeDelta = new Vector2(filled, clip.sizeDelta.y);
            if (shine != null) shine.anchoredPosition = new Vector2(filled * 0.72f, 0f);
        }

        Set(root, "PercentText", Mathf.RoundToInt(progress * 100f) + "%");
        Set(root, "LoadingLabel", "LOADING..");
        Set(root, "TipText", "A full carrier box ships out on its own.");
    }

    private static void Set(GameObject root, string name, string text)
    {
        var t = Find<TMP_Text>(root, name);
        if (t == null) return;
        t.text = text;
        Color c = t.color;
        c.a = 1f; // tips fade in via coroutine at runtime
        t.color = c;
    }

    private static T Find<T>(GameObject root, string name) where T : Component
    {
        foreach (var c in root.GetComponentsInChildren<T>(true))
            if (c.gameObject.name == name) return c;
        return null;
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
    }
}
