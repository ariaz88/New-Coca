using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the menu scene. MenuSceneUI builds its own canvas at runtime, so the
/// saved scene only needs a camera, an EventSystem and one host object - which is
/// why this is a generator rather than a hand-authored scene file.
/// Re-running it overwrites the scene, so layout changes stay in the script.
/// </summary>
public static class MenuSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/" + MenuSceneUI.SceneName + ".unity";

    [MenuItem("Tools/Coca Sorting/Menu/Create Menu Scene")]
    public static void CreateMenuScene()
    {
        // Additive, not Single: the scene the user has open may have unsaved
        // changes, and generating an unrelated scene must never discard them.
        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        GameObject cameraHost = new GameObject("Main Camera");
        cameraHost.tag = "MainCamera";
        Camera camera = cameraHost.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.10f, 1f);
        camera.orthographic = true;
        cameraHost.AddComponent<AudioListener>();

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();

        GameObject menu = new GameObject("MenuUI");
        menu.AddComponent<MenuSceneUI>();

        // New GameObjects land in the active scene, which is still the user's.
        foreach (GameObject created in new[] { cameraHost, eventSystem, menu })
        {
            SceneManager.MoveGameObjectToScene(created, scene);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);

        AddToBuildSettings();

        Debug.Log($"Created '{ScenePath}' and added it to Build Settings.");
    }

    /// <summary>
    /// Appends the menu scene to Build Settings if it is not already there. It is
    /// appended rather than inserted: the campaign scenes are in level order and
    /// LevelNaming resolves them by name, but inserting still churns every index.
    /// </summary>
    private static void AddToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes =
            new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        foreach (EditorBuildSettingsScene existing in scenes)
        {
            if (string.Equals(existing.path, ScenePath, System.StringComparison.OrdinalIgnoreCase))
            {
                existing.enabled = true;
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
