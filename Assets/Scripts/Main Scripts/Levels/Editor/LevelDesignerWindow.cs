using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One window for the whole campaign: which levels exist, whether their scenes
/// match their definitions, whether they validate, and whether they have been
/// proven solvable.
///
/// The point is that "the campaign is fine" should be a glance, not an audit.
/// </summary>
public sealed class LevelDesignerWindow : EditorWindow
{
    private Vector2 scroll;
    private List<LevelDefinition> definitions = new List<LevelDefinition>();
    private LevelDefinition selected;
    private string statusMessage = string.Empty;

    [MenuItem("Tools/Coca Sorting/Levels/Level Designer", priority = 80)]
    public static void Open()
    {
        LevelDesignerWindow window = GetWindow<LevelDesignerWindow>("Levels");
        window.minSize = new Vector2(620f, 400f);
        window.Reload();
    }

    private void OnEnable()
    {
        Reload();
    }

    private void Reload()
    {
        definitions = LevelSceneGenerator.LoadAllDefinitions();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (definitions.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No LevelDefinition assets found.\n\n" +
                "Run Tools > Coca Sorting > Levels > Author Campaign Definitions to create the " +
                "25 campaign levels, then press Bake All.",
                MessageType.Info);
            return;
        }

        DrawHeaderRow();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (LevelDefinition definition in definitions)
        {
            DrawRow(definition);
        }

        EditorGUILayout.EndScrollView();

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }

        DrawSummary();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60f)))
        {
            Reload();
        }

        if (GUILayout.Button("Bake All", EditorStyles.toolbarButton, GUILayout.Width(70f)))
        {
            BakeAll();
        }

        if (GUILayout.Button("Sync Build Settings", EditorStyles.toolbarButton, GUILayout.Width(130f)))
        {
            statusMessage = LevelSceneGenerator.SyncBuildSettings(definitions);
        }

        if (GUILayout.Button("Validate All", EditorStyles.toolbarButton, GUILayout.Width(90f)))
        {
            ValidateAll();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Copy Design Table", EditorStyles.toolbarButton, GUILayout.Width(130f)))
        {
            EditorGUIUtility.systemCopyBuffer = BuildDesignTable();
            statusMessage = "Design table copied to the clipboard as markdown.";
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawHeaderRow()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label("Level", EditorStyles.miniBoldLabel, GUILayout.Width(58f));
        GUILayout.Label("Board", EditorStyles.miniBoldLabel, GUILayout.Width(52f));
        GUILayout.Label("Cells", EditorStyles.miniBoldLabel, GUILayout.Width(96f));
        GUILayout.Label("Orders", EditorStyles.miniBoldLabel, GUILayout.Width(88f));
        GUILayout.Label("Rail", EditorStyles.miniBoldLabel, GUILayout.Width(42f));
        GUILayout.Label("Diff", EditorStyles.miniBoldLabel, GUILayout.Width(34f));
        GUILayout.Label("Scene", EditorStyles.miniBoldLabel, GUILayout.Width(60f));
        GUILayout.Label("Sync", EditorStyles.miniBoldLabel, GUILayout.Width(48f));
        GUILayout.Label("", GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawRow(LevelDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        bool sceneExists = File.Exists(definition.ScenePath);
        bool inBuild = LevelSceneGenerator.IsInBuildSettings(definition.ScenePath);
        bool inSync = definition.IsBakedCurrent;

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        if (GUILayout.Button(definition.SceneName, EditorStyles.linkLabel, GUILayout.Width(58f)))
        {
            selected = definition;
            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
        }

        GUILayout.Label($"{definition.Width}x{definition.Height}", GUILayout.Width(52f));

        GUILayout.Label(
            $"{definition.CountCellsOfKind(BoardCellKind.Blocker)}X " +
            $"{definition.CountCellsOfKind(BoardCellKind.Frozen)}F " +
            $"{definition.CountCellsOfKind(BoardCellKind.Removed)}#",
            GUILayout.Width(96f));

        GUILayout.Label(DescribeOrders(definition), GUILayout.Width(88f));
        GUILayout.Label(definition.RailQueue.Count.ToString(), GUILayout.Width(42f));
        GUILayout.Label(definition.DifficultyRating.ToString(), GUILayout.Width(34f));

        DrawChip(sceneExists ? (inBuild ? "in build" : "no build") : "missing",
            sceneExists && inBuild, 60f);
        DrawChip(inSync ? "ok" : "stale", inSync, 48f);

        using (new EditorGUI.DisabledScope(!sceneExists))
        {
            if (GUILayout.Button("Open", GUILayout.Width(48f)))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(definition.ScenePath, OpenSceneMode.Single);
                }
            }
        }

        if (GUILayout.Button("Bake", GUILayout.Width(48f)))
        {
            GenerationResult result = LevelSceneGenerator.GenerateOrUpdate(definition);
            statusMessage = result.ToString();
            AssetDatabase.SaveAssetIfDirty(definition);
        }

        if (GUILayout.Button("Pull", GUILayout.Width(44f)))
        {
            LevelSceneGenerator.TryCaptureFromScene(definition, out string message);
            statusMessage = message;
            AssetDatabase.SaveAssetIfDirty(definition);
        }

        EditorGUILayout.EndHorizontal();
    }

    private static void DrawChip(string label, bool good, float width)
    {
        Color previous = GUI.color;
        GUI.color = good ? new Color(0.55f, 0.9f, 0.55f) : new Color(1f, 0.72f, 0.4f);
        GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(width));
        GUI.color = previous;
    }

    private static string DescribeOrders(LevelDefinition definition)
    {
        StringBuilder builder = new StringBuilder();
        foreach (LevelOrderData order in definition.Orders)
        {
            if (order == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(order.color.ToString().Substring(0, 1)).Append(order.requiredCount);
        }

        return builder.Length > 0 ? builder.ToString() : "none";
    }

    private void DrawSummary()
    {
        int scenes = 0;
        int synced = 0;
        float totalSeconds = 0f;

        foreach (LevelDefinition definition in definitions)
        {
            if (definition == null)
            {
                continue;
            }

            if (File.Exists(definition.ScenePath))
            {
                scenes++;
            }

            if (definition.IsBakedCurrent)
            {
                synced++;
            }

            totalSeconds += definition.ExpectedSeconds;
        }

        EditorGUILayout.LabelField(
            $"{definitions.Count} definitions - {scenes} scenes - {synced} in sync - " +
            $"estimated campaign length {totalSeconds / 60f:0} minutes",
            EditorStyles.centeredGreyMiniLabel);
    }

    private void BakeAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        List<GenerationResult> results = LevelSceneGenerator.GenerateAll(definitions);

        int failed = 0;
        StringBuilder log = new StringBuilder();
        foreach (GenerationResult result in results)
        {
            log.AppendLine(result.ToString());
            if (result.Failed)
            {
                failed++;
            }
        }

        statusMessage = failed == 0
            ? $"Baked {results.Count} levels with no failures."
            : $"Baked {results.Count} levels, {failed} FAILED. See the console.";

        if (failed == 0)
        {
            Debug.Log("Level bake:\n" + log);
        }
        else
        {
            Debug.LogError("Level bake had failures:\n" + log);
        }

        Reload();
    }

    private void ValidateAll()
    {
        StringBuilder log = new StringBuilder();
        int errors = 0;
        int warnings = 0;

        foreach (LevelDefinition definition in definitions)
        {
            List<ValidationIssue> issues = LevelValidator.Validate(definition);
            if (issues.Count == 0)
            {
                continue;
            }

            log.AppendLine($"--- {definition.SceneName} ---");
            foreach (ValidationIssue issue in issues)
            {
                log.AppendLine("  " + issue);
                if (issue.Severity == IssueSeverity.Error)
                {
                    errors++;
                }
                else if (issue.Severity == IssueSeverity.Warning)
                {
                    warnings++;
                }
            }
        }

        statusMessage = errors == 0 && warnings == 0
            ? $"All {definitions.Count} levels validate cleanly."
            : $"{errors} errors, {warnings} warnings across {definitions.Count} levels. See the console.";

        if (errors > 0)
        {
            Debug.LogError("Level validation:\n" + log);
        }
        else if (warnings > 0)
        {
            Debug.LogWarning("Level validation:\n" + log);
        }
        else
        {
            Debug.Log($"Level validation: all {definitions.Count} levels clean.");
        }
    }

    private string BuildDesignTable()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("| # | Board | Colours | Cells (X/F/hole) | Orders | Rail | Main challenge | Diff | Est. |");
        builder.AppendLine("|---|---|---|---|---|---|---|---|---|");

        foreach (LevelDefinition definition in definitions)
        {
            if (definition == null)
            {
                continue;
            }

            builder.AppendLine(
                $"| {definition.LevelNumber} " +
                $"| {definition.Width}x{definition.Height} ({definition.PlayableCellCount} open) " +
                $"| {definition.Palette.Count} " +
                $"| {definition.CountCellsOfKind(BoardCellKind.Blocker)}/" +
                $"{definition.CountCellsOfKind(BoardCellKind.Frozen)}/" +
                $"{definition.CountCellsOfKind(BoardCellKind.Removed)} " +
                $"| {DescribeOrders(definition)} " +
                $"| {definition.RailQueue.Count} boxes " +
                $"| {definition.MainChallenge} " +
                $"| {definition.DifficultyRating} " +
                $"| {definition.ExpectedSeconds / 60f:0.0} min |");
        }

        return builder.ToString();
    }
}
