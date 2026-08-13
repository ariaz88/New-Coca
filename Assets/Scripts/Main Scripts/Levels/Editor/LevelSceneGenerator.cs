using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public readonly struct GenerationResult
{
    public readonly LevelDefinition Definition;
    public readonly bool Created;
    public readonly bool Updated;
    public readonly bool Failed;
    public readonly string Message;

    public GenerationResult(LevelDefinition definition, bool created, bool updated, bool failed, string message)
    {
        Definition = definition;
        Created = created;
        Updated = updated;
        Failed = failed;
        Message = message;
    }

    public override string ToString()
    {
        string state = Failed ? "FAILED" : Created ? "created" : Updated ? "updated" : "unchanged";
        return $"{(Definition != null ? Definition.SceneName : "?")}: {state} - {Message}";
    }
}

/// <summary>
/// Bakes LevelDefinition assets into the level scenes.
///
/// The scene stays authoritative at runtime; this only writes into it. Every
/// generated scene is a copy of the template, which is the one scene known to
/// have every manager wired (Board, SpawnContoller, GameManager, OrderManager,
/// OrderPanelUI, OrderVfxDirector, UIManager, LiftTruckManager, ...). Copying
/// rather than building a scene from scratch means new levels inherit that wiring
/// for free, at the cost of also inheriting the template's camera framing and
/// truck placement - so the template is worth auditing before a bulk generate.
/// </summary>
public static class LevelSceneGenerator
{
    public const string TemplateScenePath = "Assets/Scenes/MainLevels/Level1.unity";
    public const string DefinitionFolder = "Assets/Levels/LevelDefinitions";

    private static readonly string[] FixedBuildScenes =
    {
        "Assets/Scenes/PersistanceScene.unity",
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/TUTORIAL.unity"
    };

    // ---------------------------------------------------------------- loading

    public static List<LevelDefinition> LoadAllDefinitions()
    {
        List<LevelDefinition> definitions = new List<LevelDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:LevelDefinition"))
        {
            LevelDefinition definition =
                AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null)
            {
                definitions.Add(definition);
            }
        }

        definitions.Sort((a, b) => a.LevelNumber.CompareTo(b.LevelNumber));
        return definitions;
    }

    // ---------------------------------------------------------------- baking

    public static List<GenerationResult> GenerateAll(IReadOnlyList<LevelDefinition> definitions)
    {
        List<GenerationResult> results = new List<GenerationResult>();
        if (definitions == null || definitions.Count == 0)
        {
            return results;
        }

        try
        {
            for (int index = 0; index < definitions.Count; index++)
            {
                LevelDefinition definition = definitions[index];
                EditorUtility.DisplayProgressBar(
                    "Baking Levels",
                    definition != null ? definition.SceneName : "?",
                    (float)index / definitions.Count);

                results.Add(GenerateOrUpdate(definition));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // Per-asset: a blanket SaveAssets fails on this project's read-only
        // package assets and buries the real result.
        foreach (LevelDefinition definition in definitions)
        {
            if (definition != null)
            {
                AssetDatabase.SaveAssetIfDirty(definition);
            }
        }

        return results;
    }

    public static GenerationResult GenerateOrUpdate(LevelDefinition definition, bool createIfMissing = true)
    {
        if (definition == null)
        {
            return new GenerationResult(null, false, false, true, "Definition was null.");
        }

        string scenePath = definition.ScenePath;
        bool created = false;

        if (!File.Exists(scenePath))
        {
            if (!createIfMissing)
            {
                return new GenerationResult(definition, false, false, true, $"Scene missing: {scenePath}");
            }

            if (!File.Exists(TemplateScenePath))
            {
                return new GenerationResult(
                    definition, false, false, true, $"Template scene missing: {TemplateScenePath}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            if (!AssetDatabase.CopyAsset(TemplateScenePath, scenePath))
            {
                return new GenerationResult(
                    definition, false, false, true, $"Could not copy template to {scenePath}");
            }

            AssetDatabase.ImportAsset(scenePath);
            created = true;
        }

        // Additive so a 25-scene loop never disturbs whichever level the designer
        // has open. Every path below closes it again.
        Scene scene = default;
        bool opened = false;
        try
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            opened = true;

            if (!TryFindComponents(scene, out Board board, out SpawnContoller spawner, out string missing))
            {
                return new GenerationResult(definition, created, false, true, missing);
            }

            ApplyToBoard(definition, board);
            ApplyToSpawner(definition, spawner);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            definition.EditorMarkBaked();
            EditorUtility.SetDirty(definition);

            return new GenerationResult(
                definition, created, !created, false,
                $"{definition.Width}x{definition.Height}, " +
                $"{definition.CountCellsOfKind(BoardCellKind.Blocker)} X, " +
                $"{definition.CountCellsOfKind(BoardCellKind.Frozen)} frozen, " +
                $"{definition.CountCellsOfKind(BoardCellKind.Removed)} holes, " +
                $"{definition.RailQueue.Count} rail boxes");
        }
        catch (System.Exception exception)
        {
            return new GenerationResult(definition, created, false, true, exception.Message);
        }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static bool TryFindComponents(
        Scene scene, out Board board, out SpawnContoller spawner, out string missing)
    {
        board = null;
        spawner = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (board == null)
            {
                board = root.GetComponentInChildren<Board>(true);
            }

            if (spawner == null)
            {
                spawner = root.GetComponentInChildren<SpawnContoller>(true);
            }
        }

        // Failing loudly rather than skipping: a scene with no Board would
        // otherwise be produced silently and only fail when a player opened it.
        if (board == null || spawner == null)
        {
            missing = $"{scene.path} is missing " +
                      (board == null ? "a Board" : string.Empty) +
                      (board == null && spawner == null ? " and " : string.Empty) +
                      (spawner == null ? "a SpawnContoller" : string.Empty) +
                      ". The template scene is probably broken.";
            return false;
        }

        missing = null;
        return true;
    }

    /// <summary>
    /// Writes through SerializedObject rather than public setters, so the level
    /// fields on Board stay private. Making them public to let a generator write
    /// them would re-open the exact hole Phase 1 closed: anything could then
    /// mutate the authored layout at runtime.
    /// </summary>
    private static void ApplyToBoard(LevelDefinition definition, Board board)
    {
        SerializedObject serialized = new SerializedObject(board);

        serialized.FindProperty("width").intValue = definition.Width;
        serialized.FindProperty("height").intValue = definition.Height;

        // The legacy list must end up empty, or Board would merge stale blockers
        // from the template on top of the authored layout.
        serialized.FindProperty("removedCells").ClearArray();

        SerializedProperty cells = serialized.FindProperty("cellStates");
        cells.ClearArray();
        for (int index = 0; index < definition.CellStates.Count; index++)
        {
            BoardCellEntry entry = definition.CellStates[index];
            cells.InsertArrayElementAtIndex(index);
            SerializedProperty element = cells.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("coordinate").vector2IntValue = entry.coordinate;
            element.FindPropertyRelative("kind").enumValueIndex = (int)entry.kind;
        }

        SerializedProperty boxes = serialized.FindProperty("initialBoxes");
        boxes.ClearArray();
        for (int index = 0; index < definition.InitialBoxes.Count; index++)
        {
            InitialBoardBoxData data = definition.InitialBoxes[index];
            if (data == null)
            {
                continue;
            }

            boxes.InsertArrayElementAtIndex(index);
            SerializedProperty element = boxes.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("coordinate").vector2IntValue = data.coordinate;
            element.FindPropertyRelative("boxPrefabOverride").objectReferenceValue = data.boxPrefabOverride;

            SerializedProperty sodas = element.FindPropertyRelative("startingSodas");
            sodas.ClearArray();
            if (data.startingSodas == null)
            {
                continue;
            }

            for (int sodaIndex = 0; sodaIndex < data.startingSodas.Count; sodaIndex++)
            {
                sodas.InsertArrayElementAtIndex(sodaIndex);
                sodas.GetArrayElementAtIndex(sodaIndex).enumValueIndex = (int)data.startingSodas[sodaIndex];
            }
        }

        SerializedProperty orders = serialized.FindProperty("levelOrders");
        orders.ClearArray();
        for (int index = 0; index < definition.Orders.Count; index++)
        {
            LevelOrderData order = definition.Orders[index];
            if (order == null)
            {
                continue;
            }

            orders.InsertArrayElementAtIndex(index);
            SerializedProperty element = orders.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("color").enumValueIndex = (int)order.color;
            element.FindPropertyRelative("requiredCount").intValue = Mathf.Max(1, order.requiredCount);
        }

        ApplyBlockerStyle(serialized);

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(board);
    }

    /// <summary>
    /// Pushes the current blocker look into every generated scene.
    ///
    /// These are style values, not level design, but they are serialized on Board -
    /// so once a scene has been generated it keeps whatever the C# defaults were on
    /// that day, and editing the default afterwards changes nothing. That is
    /// exactly how the frozen blockers ended up rendering with an old, far too
    /// opaque frost in all 25 scenes. Writing them here means one edit to the
    /// defaults plus a re-bake updates the whole campaign.
    ///
    /// Values are read from a throwaway Board instance rather than duplicated as
    /// literals, so the defaults live in exactly one place: Board itself.
    /// </summary>
    private static void ApplyBlockerStyle(SerializedObject serialized)
    {
        GameObject probeObject = new GameObject("~BoardStyleProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            Board probe = probeObject.AddComponent<Board>();
            SerializedObject defaults = new SerializedObject(probe);

            string[] styleFields =
            {
                "frozenCellFrostColor",
                "frozenCellFrostThickness",
                "frozenCellCrackColor",
                "frozenCellCrackCount",
                "frozenCellCrackShardCount",
                "frozenCellCrackPunchDuration"
            };

            foreach (string field in styleFields)
            {
                SerializedProperty source = defaults.FindProperty(field);
                SerializedProperty target = serialized.FindProperty(field);
                if (source == null || target == null)
                {
                    continue;
                }

                switch (source.propertyType)
                {
                    case SerializedPropertyType.Color:
                        target.colorValue = source.colorValue;
                        break;
                    case SerializedPropertyType.Float:
                        target.floatValue = source.floatValue;
                        break;
                    case SerializedPropertyType.Integer:
                        target.intValue = source.intValue;
                        break;
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(probeObject);
        }
    }

    private static void ApplyToSpawner(LevelDefinition definition, SpawnContoller spawner)
    {
        SerializedObject serialized = new SerializedObject(spawner);

        serialized.FindProperty("railMode").enumValueIndex = (int)RailMode.AuthoredQueue;
        serialized.FindProperty("railExhaustionPolicy").enumValueIndex =
            (int)definition.RailExhaustionPolicy;
        serialized.FindProperty("maxBoxCount").intValue = definition.RailBatchSize;

        // Derived from the level number so every level's fallback is reproducible
        // but no two levels share a sequence.
        serialized.FindProperty("fallbackSeed").intValue = 9173 + definition.LevelNumber * 7919;

        SerializedProperty palette = serialized.FindProperty("allowedColors");
        palette.ClearArray();
        for (int index = 0; index < definition.Palette.Count; index++)
        {
            palette.InsertArrayElementAtIndex(index);
            palette.GetArrayElementAtIndex(index).enumValueIndex = (int)definition.Palette[index];
        }

        SerializedProperty queue = serialized.FindProperty("railQueue");
        queue.ClearArray();
        for (int index = 0; index < definition.RailQueue.Count; index++)
        {
            TutorialBoxRecipe recipe = definition.RailQueue[index];
            queue.InsertArrayElementAtIndex(index);
            SerializedProperty element = queue.GetArrayElementAtIndex(index);
            SerializedProperty sodas = element.FindPropertyRelative("sodas");
            sodas.ClearArray();

            if (recipe == null)
            {
                continue;
            }

            int slot = 0;
            foreach (KeyValuePair<Soda.SodaColor, int> amount in recipe.ToDictionary())
            {
                sodas.InsertArrayElementAtIndex(slot);
                SerializedProperty entry = sodas.GetArrayElementAtIndex(slot);
                entry.FindPropertyRelative("color").enumValueIndex = (int)amount.Key;
                entry.FindPropertyRelative("count").intValue = amount.Value;
                slot++;
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(spawner);
    }

    // ---------------------------------------------------------------- reverse

    /// <summary>
    /// Reads a scene's authored data back into a definition. This is how the five
    /// existing levels become definitions without re-authoring them by hand.
    /// </summary>
    public static bool TryCaptureFromScene(LevelDefinition definition, out string message)
    {
        if (definition == null)
        {
            message = "Definition was null.";
            return false;
        }

        string scenePath = File.Exists(definition.ScenePath)
            ? definition.ScenePath
            : LevelNaming.GetLegacyScenePath(definition.LevelNumber);

        if (!File.Exists(scenePath))
        {
            message = $"No scene found at {definition.ScenePath} or {scenePath}.";
            return false;
        }

        Scene scene = default;
        bool opened = false;
        try
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            opened = true;

            if (!TryFindComponents(scene, out Board board, out SpawnContoller spawner, out string missing))
            {
                message = missing;
                return false;
            }

            definition.EditorSetBoard(
                board.Width,
                board.Height,
                new List<BoardCellEntry>(board.AuthoredCellStates),
                CloneInitialBoxes(board.InitialBoxes));

            definition.EditorSetOrders(CloneOrders(board.LevelOrders));

            SerializedObject spawnerObject = new SerializedObject(spawner);
            List<Soda.SodaColor> palette = new List<Soda.SodaColor>();
            SerializedProperty colors = spawnerObject.FindProperty("allowedColors");
            for (int index = 0; index < colors.arraySize; index++)
            {
                palette.Add((Soda.SodaColor)colors.GetArrayElementAtIndex(index).enumValueIndex);
            }

            // The rail queue is not captured: a random-mode scene has no queue to
            // read. Existing levels keep whatever queue the definition already
            // holds, so a capture never silently empties one.
            definition.EditorSetRail(
                palette,
                new List<TutorialBoxRecipe>(definition.RailQueue),
                spawnerObject.FindProperty("maxBoxCount").intValue,
                definition.RailExhaustionPolicy);

            EditorUtility.SetDirty(definition);
            message = $"Captured {board.Width}x{board.Height}, {board.LevelOrders.Count} orders, " +
                      $"{board.InitialBoxes.Count} starting boxes from {Path.GetFileName(scenePath)}.";
            return true;
        }
        catch (System.Exception exception)
        {
            message = exception.Message;
            return false;
        }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static List<InitialBoardBoxData> CloneInitialBoxes(IReadOnlyList<InitialBoardBoxData> source)
    {
        List<InitialBoardBoxData> clones = new List<InitialBoardBoxData>();
        if (source == null)
        {
            return clones;
        }

        foreach (InitialBoardBoxData data in source)
        {
            if (data == null)
            {
                continue;
            }

            clones.Add(new InitialBoardBoxData
            {
                coordinate = data.coordinate,
                boxPrefabOverride = data.boxPrefabOverride,
                startingSodas = data.startingSodas != null
                    ? new List<Soda.SodaColor>(data.startingSodas)
                    : new List<Soda.SodaColor>()
            });
        }

        return clones;
    }

    private static List<LevelOrderData> CloneOrders(IReadOnlyList<LevelOrderData> source)
    {
        List<LevelOrderData> clones = new List<LevelOrderData>();
        if (source == null)
        {
            return clones;
        }

        foreach (LevelOrderData order in source)
        {
            if (order != null)
            {
                clones.Add(new LevelOrderData { color = order.color, requiredCount = order.requiredCount });
            }
        }

        return clones;
    }

    // ---------------------------------------------------------- build settings

    /// <summary>
    /// Rebuilds the build scene list as the three fixed scenes followed by the
    /// campaign in level order. Order matters: build indices are load order, and
    /// AndroidBuildAutomation filters on the enabled flag.
    /// </summary>
    public static string SyncBuildSettings(IReadOnlyList<LevelDefinition> definitions)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        List<string> skipped = new List<string>();

        foreach (string path in FixedBuildScenes)
        {
            if (File.Exists(path))
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
            else
            {
                skipped.Add(path);
            }
        }

        foreach (LevelDefinition definition in definitions)
        {
            if (definition == null)
            {
                continue;
            }

            if (File.Exists(definition.ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(definition.ScenePath, true));
            }
            else
            {
                skipped.Add(definition.ScenePath);
            }
        }

        EditorBuildSettings.scenes = scenes.ToArray();

        string message = $"Build Settings now lists {scenes.Count} scenes.";
        if (skipped.Count > 0)
        {
            message += $" Missing and skipped: {string.Join(", ", skipped)}";
        }

        return message;
    }

    public static bool IsInBuildSettings(string scenePath)
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == scenePath && scene.enabled)
            {
                return true;
            }
        }

        return false;
    }
}
