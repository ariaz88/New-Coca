using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Board))]
public sealed class BoardEditor : Editor
{
    private const float CellButtonSize = 30f;

    private SerializedProperty widthProperty;
    private SerializedProperty heightProperty;
    private SerializedProperty removedCellsProperty;
    private SerializedProperty initialBoxesProperty;

    private Vector2Int selectedInitialCell = new Vector2Int(-1, -1);
    private GameObject copiedBoxPrefab;
    private readonly List<Soda.SodaColor> copiedSodas = new List<Soda.SodaColor>();
    private bool hasCopiedInitialBox;

    private void OnEnable()
    {
        widthProperty = serializedObject.FindProperty("width");
        heightProperty = serializedObject.FindProperty("height");
        removedCellsProperty = serializedObject.FindProperty("removedCells");
        initialBoxesProperty = serializedObject.FindProperty("initialBoxes");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Per-Level Board Layout", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Width, height, blocked cells, and starting boxes are saved only in this scene. " +
            "Green cells are playable; dark cells begin blocked and can be broken by an adjacent packed match.",
            MessageType.Info);

        EditorGUILayout.PropertyField(widthProperty, new GUIContent("Columns"));
        EditorGUILayout.PropertyField(heightProperty, new GUIContent("Rows"));

        widthProperty.intValue = Mathf.Max(1, widthProperty.intValue);
        heightProperty.intValue = Mathf.Max(1, heightProperty.intValue);

        DrawShapeGrid();
        DrawInitialBoxDesigner();

        EditorGUILayout.Space();
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "width",
            "height",
            "removedCells",
            "initialBoxes");

        if (serializedObject.ApplyModifiedProperties())
        {
            SceneView.RepaintAll();
        }
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
    private static void DrawBoardLayoutGizmo(Board board, GizmoType gizmoType)
    {
        if (board != null)
        {
            board.DrawLayoutGizmos();
        }
    }

    private void DrawShapeGrid()
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Shape (top row first)", EditorStyles.miniBoldLabel);
        DrawCoordinateGrid(false);

        if (GUILayout.Button("Clear All Blocked Cells"))
        {
            removedCellsProperty.ClearArray();
        }
    }

    private void DrawInitialBoxDesigner()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Initial Boxes", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Click an active cell below. E = empty, B# = starting Box with # sodas, X = blocked cell. " +
            "Only occupied cells are stored, which keeps each level configuration small.",
            MessageType.Info);

        DrawCoordinateGrid(true);
        DrawSelectedCellSettings();

        EditorGUILayout.Space(4f);
        using (new EditorGUI.DisabledScope(initialBoxesProperty.arraySize == 0))
        {
            if (GUILayout.Button("Clear All Initial Boxes") &&
                EditorUtility.DisplayDialog(
                    "Clear Initial Boxes",
                    "Remove every configured starting Box from this level?",
                    "Clear All",
                    "Cancel"))
            {
                initialBoxesProperty.ClearArray();
            }
        }
    }

    private void DrawCoordinateGrid(bool initialBoxMode)
    {
        int columns = widthProperty.intValue;
        int rows = heightProperty.intValue;

        for (int row = rows - 1; row >= 0; row--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(row.ToString(), GUILayout.Width(18f));

            for (int column = 0; column < columns; column++)
            {
                Vector2Int cell = new Vector2Int(column, row);
                int removedIndex = FindRemovedCellIndex(cell);
                bool isPlayable = removedIndex < 0;
                int initialIndex = FindInitialBoxIndex(cell);

                Color previousColor = GUI.backgroundColor;
                bool previousEnabled = GUI.enabled;
                string label;
                string tooltip;

                if (!initialBoxMode)
                {
                    GUI.backgroundColor = isPlayable
                        ? new Color(0.45f, 0.85f, 0.5f)
                        : new Color(0.35f, 0.35f, 0.35f);
                    label = isPlayable ? "O" : "X";
                    tooltip = isPlayable
                        ? $"Cell ({column}, {row}) is playable. Click to block it."
                        : $"Cell ({column}, {row}) begins blocked. Click to restore it.";
                }
                else if (!isPlayable)
                {
                    GUI.enabled = false;
                    GUI.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
                    label = "X";
                    tooltip = $"Cell ({column}, {row}) begins blocked and cannot contain a starting Box.";
                }
                else
                {
                    bool isSelected = selectedInitialCell == cell;
                    GUI.backgroundColor = isSelected
                        ? new Color(1f, 0.75f, 0.25f)
                        : initialIndex >= 0
                            ? new Color(0.35f, 0.65f, 1f)
                            : new Color(0.72f, 0.72f, 0.72f);
                    int sodaCount = initialIndex >= 0
                        ? GetStartingSodasProperty(initialIndex).arraySize
                        : 0;
                    label = initialIndex >= 0 ? $"B{sodaCount}" : "E";
                    tooltip = initialIndex >= 0
                        ? $"Cell ({column}, {row}) starts with a Box containing {sodaCount} soda(s)."
                        : $"Cell ({column}, {row}) starts empty.";
                }

                if (GUILayout.Button(
                        new GUIContent(label, tooltip),
                        GUILayout.Width(CellButtonSize),
                        GUILayout.Height(CellButtonSize)))
                {
                    if (initialBoxMode)
                    {
                        selectedInitialCell = cell;
                    }
                    else
                    {
                        ToggleCell(cell, removedIndex);
                    }
                }

                GUI.enabled = previousEnabled;
                GUI.backgroundColor = previousColor;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(18f);
        for (int column = 0; column < columns; column++)
        {
            GUILayout.Label(
                column.ToString(),
                EditorStyles.centeredGreyMiniLabel,
                GUILayout.Width(CellButtonSize));
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSelectedCellSettings()
    {
        if (!IsInsideCurrentLayout(selectedInitialCell) ||
            FindRemovedCellIndex(selectedInitialCell) >= 0)
        {
            EditorGUILayout.HelpBox("Select an active cell from the Initial Boxes grid.", MessageType.None);
            return;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            $"Selected Cell ({selectedInitialCell.x}, {selectedInitialCell.y})",
            EditorStyles.boldLabel);

        int initialIndex = FindInitialBoxIndex(selectedInitialCell);
        if (initialIndex < 0)
        {
            EditorGUILayout.LabelField("Starts", "Empty");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Starting Box"))
            {
                initialIndex = AddInitialBox(selectedInitialCell);
            }

            using (new EditorGUI.DisabledScope(!hasCopiedInitialBox))
            {
                if (GUILayout.Button("Paste Copied Box"))
                {
                    initialIndex = AddInitialBox(selectedInitialCell);
                    PasteInitialBox(initialIndex);
                }
            }
            EditorGUILayout.EndHorizontal();
            return;
        }

        SerializedProperty initialBox = initialBoxesProperty.GetArrayElementAtIndex(initialIndex);
        SerializedProperty prefabProperty = initialBox.FindPropertyRelative("boxPrefabOverride");
        SerializedProperty sodasProperty = initialBox.FindPropertyRelative("startingSodas");

        EditorGUILayout.LabelField("Starts", "Occupied");
        EditorGUILayout.PropertyField(prefabProperty, new GUIContent("Box Prefab Override"));

        GameObject resolvedPrefab = ResolveBoxPrefab(prefabProperty.objectReferenceValue as GameObject);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Resolved Box Prefab", resolvedPrefab, typeof(GameObject), false);
        }

        int capacity = GetBoxCapacity(resolvedPrefab);
        if (resolvedPrefab == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a prefab override or assign Box Prefab on this scene's SpawnContoller.",
                MessageType.Error);
        }
        else if (capacity <= 0)
        {
            EditorGUILayout.HelpBox(
                "The resolved prefab has no Box component or SodaPosition slots.",
                MessageType.Error);
        }
        else
        {
            EditorGUILayout.LabelField("Real Prefab Capacity", capacity.ToString());
        }

        DrawStartingSodas(sodasProperty, capacity);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy Box Settings"))
        {
            CopyInitialBox(prefabProperty, sodasProperty);
        }

        if (GUILayout.Button("Remove Starting Box"))
        {
            initialBoxesProperty.DeleteArrayElementAtIndex(initialIndex);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawStartingSodas(SerializedProperty sodasProperty, int capacity)
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Starting Sodas (slot order)", EditorStyles.miniBoldLabel);

        if (capacity > 0 && sodasProperty.arraySize > capacity)
        {
            EditorGUILayout.HelpBox(
                $"This list exceeds the prefab capacity of {capacity}. Remove extra items; runtime ignores overflow.",
                MessageType.Error);
        }

        int removeIndex = -1;
        for (int index = 0; index < sodasProperty.arraySize; index++)
        {
            SerializedProperty colorProperty = sodasProperty.GetArrayElementAtIndex(index);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel($"Slot {index}");
            EditorGUILayout.PropertyField(colorProperty, GUIContent.none);

            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUILayout.Button("↑", GUILayout.Width(25f)))
                {
                    sodasProperty.MoveArrayElement(index, index - 1);
                }
            }

            using (new EditorGUI.DisabledScope(index >= sodasProperty.arraySize - 1))
            {
                if (GUILayout.Button("↓", GUILayout.Width(25f)))
                {
                    sodasProperty.MoveArrayElement(index, index + 1);
                }
            }

            if (GUILayout.Button("−", GUILayout.Width(25f)))
            {
                removeIndex = index;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
        {
            sodasProperty.DeleteArrayElementAtIndex(removeIndex);
        }

        using (new EditorGUI.DisabledScope(capacity <= 0 || sodasProperty.arraySize >= capacity))
        {
            if (GUILayout.Button("Add Soda"))
            {
                int index = sodasProperty.arraySize;
                sodasProperty.InsertArrayElementAtIndex(index);
                sodasProperty.GetArrayElementAtIndex(index).enumValueIndex = 0;
            }
        }
    }

    private int FindRemovedCellIndex(Vector2Int cell)
    {
        for (int index = 0; index < removedCellsProperty.arraySize; index++)
        {
            if (removedCellsProperty.GetArrayElementAtIndex(index).vector2IntValue == cell)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindInitialBoxIndex(Vector2Int cell)
    {
        for (int index = 0; index < initialBoxesProperty.arraySize; index++)
        {
            SerializedProperty coordinate = initialBoxesProperty
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative("coordinate");
            if (coordinate.vector2IntValue == cell)
            {
                return index;
            }
        }

        return -1;
    }

    private SerializedProperty GetStartingSodasProperty(int initialIndex)
    {
        return initialBoxesProperty
            .GetArrayElementAtIndex(initialIndex)
            .FindPropertyRelative("startingSodas");
    }

    private void ToggleCell(Vector2Int cell, int removedIndex)
    {
        if (removedIndex >= 0)
        {
            removedCellsProperty.DeleteArrayElementAtIndex(removedIndex);
            return;
        }

        int totalCells = widthProperty.intValue * heightProperty.intValue;
        if (removedCellsProperty.arraySize >= totalCells - 1)
        {
            EditorUtility.DisplayDialog(
                "Board Layout",
                "At least one Board cell must remain playable.",
                "OK");
            return;
        }

        int initialIndex = FindInitialBoxIndex(cell);
        if (initialIndex >= 0)
        {
            initialBoxesProperty.DeleteArrayElementAtIndex(initialIndex);
        }

        int newIndex = removedCellsProperty.arraySize;
        removedCellsProperty.InsertArrayElementAtIndex(newIndex);
        removedCellsProperty.GetArrayElementAtIndex(newIndex).vector2IntValue = cell;
    }

    private int AddInitialBox(Vector2Int cell)
    {
        int existingIndex = FindInitialBoxIndex(cell);
        if (existingIndex >= 0)
        {
            return existingIndex;
        }

        int newIndex = initialBoxesProperty.arraySize;
        initialBoxesProperty.InsertArrayElementAtIndex(newIndex);
        SerializedProperty element = initialBoxesProperty.GetArrayElementAtIndex(newIndex);
        element.FindPropertyRelative("coordinate").vector2IntValue = cell;
        element.FindPropertyRelative("boxPrefabOverride").objectReferenceValue = null;
        element.FindPropertyRelative("startingSodas").ClearArray();
        return newIndex;
    }

    private void CopyInitialBox(SerializedProperty prefabProperty, SerializedProperty sodasProperty)
    {
        copiedBoxPrefab = prefabProperty.objectReferenceValue as GameObject;
        copiedSodas.Clear();
        for (int index = 0; index < sodasProperty.arraySize; index++)
        {
            copiedSodas.Add((Soda.SodaColor)sodasProperty.GetArrayElementAtIndex(index).intValue);
        }

        hasCopiedInitialBox = true;
    }

    private void PasteInitialBox(int initialIndex)
    {
        SerializedProperty element = initialBoxesProperty.GetArrayElementAtIndex(initialIndex);
        element.FindPropertyRelative("boxPrefabOverride").objectReferenceValue = copiedBoxPrefab;
        SerializedProperty sodas = element.FindPropertyRelative("startingSodas");
        sodas.ClearArray();
        for (int index = 0; index < copiedSodas.Count; index++)
        {
            sodas.InsertArrayElementAtIndex(index);
            sodas.GetArrayElementAtIndex(index).intValue = (int)copiedSodas[index];
        }
    }

    private GameObject ResolveBoxPrefab(GameObject overridePrefab)
    {
        if (overridePrefab != null)
        {
            return overridePrefab;
        }

        SpawnContoller spawnController = Object.FindFirstObjectByType<SpawnContoller>();
        return spawnController != null ? spawnController.boxPrefab : null;
    }

    private static int GetBoxCapacity(GameObject prefab)
    {
        if (prefab == null)
        {
            return 0;
        }

        Box box = prefab.GetComponent<Box>();
        return box != null ? box.DiscoverableCapacity : 0;
    }

    private bool IsInsideCurrentLayout(Vector2Int coordinate)
    {
        return coordinate.x >= 0 && coordinate.x < widthProperty.intValue &&
               coordinate.y >= 0 && coordinate.y < heightProperty.intValue;
    }
}
