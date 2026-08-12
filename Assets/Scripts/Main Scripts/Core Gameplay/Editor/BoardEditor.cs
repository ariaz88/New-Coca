using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Board))]
public sealed class BoardEditor : Editor
{
    private const float CellButtonSize = 30f;

    /// <summary>
    /// Shape-grid painting mode. Cycle advances a cell through the four kinds on
    /// each click; the rest paint one kind directly and support click-and-drag,
    /// which is the difference between minutes and an hour when laying out 25
    /// levels.
    /// </summary>
    private enum ShapeBrush
    {
        Cycle = 0,
        Playable = 1,
        Removed = 2,
        Blocker = 3,
        Frozen = 4
    }

    private SerializedProperty widthProperty;
    private SerializedProperty heightProperty;
    private SerializedProperty cellStatesProperty;
    private SerializedProperty initialBoxesProperty;
    private SerializedProperty levelOrdersProperty;

    private Vector2Int selectedInitialCell = new Vector2Int(-1, -1);
    private GameObject copiedBoxPrefab;
    private readonly List<Soda.SodaColor> copiedSodas = new List<Soda.SodaColor>();
    private bool hasCopiedInitialBox;
    private ShapeBrush shapeBrush = ShapeBrush.Cycle;

    private void OnEnable()
    {
        widthProperty = serializedObject.FindProperty("width");
        heightProperty = serializedObject.FindProperty("height");
        cellStatesProperty = serializedObject.FindProperty("cellStates");
        initialBoxesProperty = serializedObject.FindProperty("initialBoxes");
        levelOrdersProperty = serializedObject.FindProperty("levelOrders");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Per-Level Board Layout", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Width, height, cell layout, and starting boxes are saved only in this scene.\n\n" +
            "O  Playable - an ordinary cell.\n" +
            "-  Hole - permanently unusable. Shapes the board; never breaks.\n" +
            "X  X Blocker - opens after ONE packed match in an orthogonally adjacent cell.\n" +
            "*  Frozen - needs TWO adjacent packed matches: the first cracks it, the second opens it.",
            MessageType.Info);

        EditorGUILayout.PropertyField(widthProperty, new GUIContent("Columns"));
        EditorGUILayout.PropertyField(heightProperty, new GUIContent("Rows"));

        widthProperty.intValue = Mathf.Max(1, widthProperty.intValue);
        heightProperty.intValue = Mathf.Max(1, heightProperty.intValue);

        DrawShapeGrid();
        DrawInitialBoxDesigner();
        DrawBlockedCellArt();
        DrawOrdersDesigner();

        EditorGUILayout.Space();
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "width",
            "height",
            "removedCells",
            "cellStates",
            "initialBoxes",
            "levelOrders",
            "removedCellVisualPrefab");

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
        EditorGUILayout.LabelField("Board Shape (top row first)", EditorStyles.miniBoldLabel);

        shapeBrush = (ShapeBrush)GUILayout.Toolbar(
            (int)shapeBrush,
            new[] { "Cycle", "O Playable", "- Hole", "X Blocker", "* Frozen" });

        EditorGUILayout.LabelField(
            shapeBrush == ShapeBrush.Cycle
                ? "Click cycles Playable -> Hole -> X -> Frozen. Shift+Click reverses, Alt+Click clears."
                : "Click or drag to paint.",
            EditorStyles.centeredGreyMiniLabel);

        DrawCoordinateGrid(false);

        using (new EditorGUI.DisabledScope(cellStatesProperty.arraySize == 0))
        {
            if (GUILayout.Button("Clear All Blockers And Holes"))
            {
                cellStatesProperty.ClearArray();
            }
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

    /// <summary>
    /// Surfaces the blocked-cell box prefab where a level designer will look for
    /// it, instead of leaving it buried in the fallback property list.
    ///
    /// Leaving it empty is easy to do and produces a plain grey cube, which does
    /// not read as a sealed box at all. The one-click button assigns the
    /// project's closed box so the common case takes no searching. The
    /// assignment is deliberately a button rather than something that happens
    /// automatically on selection: silently editing a scene because someone
    /// clicked the Board object would be surprising.
    /// </summary>
    private void DrawBlockedCellArt()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Blocked Cell Art", EditorStyles.boldLabel);

        SerializedProperty prefabProperty = serializedObject.FindProperty("removedCellVisualPrefab");
        if (prefabProperty == null)
        {
            return;
        }

        EditorGUILayout.PropertyField(prefabProperty, new GUIContent("Blocked Box Prefab"));

        if (prefabProperty.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "No prefab assigned, so blocked cells will be plain cubes. Assign the project's " +
                "closed box to make them look like the sealed, taped boxes in the reference.",
                MessageType.Warning);

            GameObject closedBox = FindClosedBoxPrefab();
            using (new EditorGUI.DisabledScope(closedBox == null))
            {
                string label = closedBox != null
                    ? $"Use \"{closedBox.name}\""
                    : "No closed box prefab found";

                if (GUILayout.Button(label))
                {
                    prefabProperty.objectReferenceValue = closedBox;
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "The taped cross is generated on top of this prefab at runtime, sized from its own " +
                "bounds and tinted a little lighter than its material. Turn it off with " +
                "\"Draw Blocked Cell Tape\" below.",
                MessageType.None);
        }
    }

    /// <summary>
    /// Looks for the project's closed box prefab by name, preferring an exact
    /// "FullBox" match before falling back to anything that looks like a closed
    /// box, so a renamed asset still gets found.
    /// </summary>
    private static GameObject FindClosedBoxPrefab()
    {
        string[] candidates = { "FullBox", "ClosedBox", "Box01" };

        for (int index = 0; index < candidates.Length; index++)
        {
            string[] found = AssetDatabase.FindAssets($"{candidates[index]} t:Prefab");
            for (int guidIndex = 0; guidIndex < found.Length; guidIndex++)
            {
                string path = AssetDatabase.GUIDToAssetPath(found[guidIndex]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.name == candidates[index])
                {
                    return prefab;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Level-design UI for this scene's Orders panel.
    ///
    /// Each row is one <see cref="LevelOrderData"/>: a soda color plus how many
    /// packed boxes of that color the level requires. A color swatch is drawn
    /// beside the dropdown so a designer can read the list at a glance, and the
    /// rows can be reordered because row order is also the left-to-right order
    /// of the slots in the runtime panel.
    /// </summary>
    private void DrawOrdersDesigner()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Orders (win condition)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The level is won when every order below is fulfilled. One packed box of a color " +
            "decreases that color's remaining count by one. Row order is the left-to-right slot " +
            "order in the on-screen Orders panel.",
            MessageType.Info);

        int removeIndex = -1;
        for (int index = 0; index < levelOrdersProperty.arraySize; index++)
        {
            SerializedProperty element = levelOrdersProperty.GetArrayElementAtIndex(index);
            SerializedProperty colorProperty = element.FindPropertyRelative("color");
            SerializedProperty countProperty = element.FindPropertyRelative("requiredCount");

            EditorGUILayout.BeginHorizontal();

            // Swatch first: the fastest way to scan a list of orders visually.
            Rect swatchRect = GUILayoutUtility.GetRect(
                18f,
                EditorGUIUtility.singleLineHeight,
                GUILayout.Width(18f));
            EditorGUI.DrawRect(swatchRect, GetSwatchColor((Soda.SodaColor)colorProperty.enumValueIndex));

            EditorGUILayout.PropertyField(colorProperty, GUIContent.none);

            GUILayout.Label("x", GUILayout.Width(12f));
            countProperty.intValue = Mathf.Max(
                1,
                EditorGUILayout.IntField(countProperty.intValue, GUILayout.Width(45f)));

            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUILayout.Button("↑", GUILayout.Width(25f)))
                {
                    levelOrdersProperty.MoveArrayElement(index, index - 1);
                }
            }

            using (new EditorGUI.DisabledScope(index >= levelOrdersProperty.arraySize - 1))
            {
                if (GUILayout.Button("↓", GUILayout.Width(25f)))
                {
                    levelOrdersProperty.MoveArrayElement(index, index + 1);
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
            levelOrdersProperty.DeleteArrayElementAtIndex(removeIndex);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Order"))
        {
            AddOrder();
        }

        using (new EditorGUI.DisabledScope(levelOrdersProperty.arraySize == 0))
        {
            if (GUILayout.Button("Clear All Orders") &&
                EditorUtility.DisplayDialog(
                    "Clear Orders",
                    "Remove every order from this level?",
                    "Clear All",
                    "Cancel"))
            {
                levelOrdersProperty.ClearArray();
            }
        }
        EditorGUILayout.EndHorizontal();

        DrawOrderValidation();
    }

    /// <summary>
    /// Reports the authoring mistakes that silently break a level: no orders at
    /// all, the same color listed twice, and an order asking for more boxes of a
    /// color than the level's own starting sodas could ever fill.
    /// </summary>
    private void DrawOrderValidation()
    {
        if (levelOrdersProperty.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "This level has no orders, so its Orders panel will be empty and it can never be won. " +
                "Add at least one order.",
                MessageType.Error);
            return;
        }

        Board board = target as Board;
        HashSet<Soda.SodaColor> seenColors = new HashSet<Soda.SodaColor>();
        List<string> duplicates = new List<string>();
        List<string> shortages = new List<string>();
        int totalBoxes = 0;

        // Resolved once: ResolveBoxPrefab performs a scene search, which must not
        // run per row on every inspector repaint.
        int boxCapacity = GetBoxCapacity(ResolveBoxPrefab(null));

        for (int index = 0; index < levelOrdersProperty.arraySize; index++)
        {
            SerializedProperty element = levelOrdersProperty.GetArrayElementAtIndex(index);
            Soda.SodaColor color = (Soda.SodaColor)element.FindPropertyRelative("color").enumValueIndex;
            int required = Mathf.Max(1, element.FindPropertyRelative("requiredCount").intValue);
            totalBoxes += required;

            if (!seenColors.Add(color))
            {
                duplicates.Add(color.ToString());
                continue;
            }

            // A packed box needs a full box of one color, so the level needs at
            // least required * capacity sodas of that color from somewhere.
            if (board == null || boxCapacity <= 0)
            {
                continue;
            }

            int needed = required * boxCapacity;
            int available = board.CountInitialSodasOfColor(color);
            if (available < needed)
            {
                shortages.Add($"{color}: needs {needed} sodas, level starts with {available}");
            }
        }

        if (duplicates.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "The same color is listed more than once: " + string.Join(", ", duplicates) +
                ". Merge those rows into one order; the runtime panel shows one slot per color.",
                MessageType.Error);
        }

        if (shortages.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "These orders cannot be filled from the level's starting boxes alone:\n" +
                string.Join("\n", shortages) +
                "\nThis is only a warning: the spawner may still supply the missing sodas.",
                MessageType.Warning);
        }

        EditorGUILayout.LabelField(
            "Total packed boxes required",
            totalBoxes.ToString(),
            EditorStyles.miniLabel);
    }

    private void AddOrder()
    {
        int newIndex = levelOrdersProperty.arraySize;
        levelOrdersProperty.InsertArrayElementAtIndex(newIndex);
        SerializedProperty element = levelOrdersProperty.GetArrayElementAtIndex(newIndex);

        // Default to the first color that is not used yet so repeated clicks
        // never create an invalid duplicate row.
        element.FindPropertyRelative("color").enumValueIndex = (int)FindFirstUnusedColor();
        element.FindPropertyRelative("requiredCount").intValue = 1;
    }

    private Soda.SodaColor FindFirstUnusedColor()
    {
        HashSet<Soda.SodaColor> used = new HashSet<Soda.SodaColor>();
        for (int index = 0; index < levelOrdersProperty.arraySize; index++)
        {
            used.Add((Soda.SodaColor)levelOrdersProperty
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative("color")
                .enumValueIndex);
        }

        foreach (Soda.SodaColor color in System.Enum.GetValues(typeof(Soda.SodaColor)))
        {
            if (!used.Contains(color))
            {
                return color;
            }
        }

        return Soda.SodaColor.Red;
    }

    /// <summary>
    /// Inspector-only approximation of each soda color. This drives the small
    /// swatch beside a row and never affects gameplay or the runtime VFX tint,
    /// which come from the SodaVisualLibrary asset instead.
    /// </summary>
    private static Color GetSwatchColor(Soda.SodaColor color)
    {
        switch (color)
        {
            case Soda.SodaColor.Red: return new Color(0.87f, 0.19f, 0.19f);
            case Soda.SodaColor.Blue: return new Color(0.20f, 0.47f, 0.92f);
            case Soda.SodaColor.Orange: return new Color(0.95f, 0.58f, 0.13f);
            case Soda.SodaColor.Yellow: return new Color(0.96f, 0.85f, 0.17f);
            case Soda.SodaColor.Green: return new Color(0.30f, 0.75f, 0.32f);
            case Soda.SodaColor.Purple: return new Color(0.60f, 0.32f, 0.83f);
            default: return Color.gray;
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
                BoardCellKind kind = GetCellKind(cell);
                bool isPlayable = kind == BoardCellKind.Playable;
                int initialIndex = FindInitialBoxIndex(cell);

                Color previousColor = GUI.backgroundColor;
                bool previousEnabled = GUI.enabled;

                if (!initialBoxMode)
                {
                    GUI.backgroundColor = BoardCellRules.GetEditorColor(kind);
                    string tooltip =
                        $"Cell ({column}, {row}): {BoardCellRules.GetDisplayName(kind)}.\n" +
                        (BoardCellRules.IsBreakable(kind)
                            ? $"Opens after {BoardCellRules.GetRequiredHits(kind)} adjacent packed match(es)."
                            : isPlayable
                                ? "A box can be placed here."
                                : "Permanently unusable.");

                    // Drawn as a Box rather than a Button so the same rect can serve
                    // click AND drag; GUILayout.Button only reports discrete clicks.
                    Rect rect = GUILayoutUtility.GetRect(
                        CellButtonSize,
                        CellButtonSize,
                        GUILayout.Width(CellButtonSize),
                        GUILayout.Height(CellButtonSize));
                    GUI.Box(
                        rect,
                        new GUIContent(BoardCellRules.GetGlyph(kind), tooltip),
                        GUI.skin.button);

                    HandleShapeCellInput(rect, cell, kind);

                    GUI.enabled = previousEnabled;
                    GUI.backgroundColor = previousColor;
                    continue;
                }

                string label;
                string boxTooltip;

                if (!isPlayable)
                {
                    GUI.enabled = false;
                    GUI.backgroundColor = BoardCellRules.GetEditorColor(kind);
                    label = BoardCellRules.GetGlyph(kind);
                    boxTooltip =
                        $"Cell ({column}, {row}) is a {BoardCellRules.GetDisplayName(kind)} and cannot hold a starting Box.";
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
                    boxTooltip = initialIndex >= 0
                        ? $"Cell ({column}, {row}) starts with a Box containing {sodaCount} soda(s)."
                        : $"Cell ({column}, {row}) starts empty.";
                }

                if (GUILayout.Button(
                        new GUIContent(label, boxTooltip),
                        GUILayout.Width(CellButtonSize),
                        GUILayout.Height(CellButtonSize)))
                {
                    selectedInitialCell = cell;
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
            GetCellKind(selectedInitialCell) != BoardCellKind.Playable)
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

    /// <summary>
    /// Routes a click or drag over one shape-grid cell to the right mutation.
    /// Painting on drag is what makes a 25-level layout pass tolerable, and it is
    /// why the cell is drawn as a Box rather than a Button.
    /// </summary>
    private void HandleShapeCellInput(Rect rect, Vector2Int cell, BoardCellKind currentKind)
    {
        Event current = Event.current;
        bool isPress = current.type == EventType.MouseDown;
        bool isDrag = current.type == EventType.MouseDrag;

        if ((!isPress && !isDrag) || current.button != 0 || !rect.Contains(current.mousePosition))
        {
            return;
        }

        if (shapeBrush != ShapeBrush.Cycle)
        {
            BoardCellKind painted = BrushToKind(shapeBrush);
            if (painted != currentKind)
            {
                SetCellKind(cell, painted);
            }

            current.Use();
            return;
        }

        // Cycling on drag would race through all four kinds under one gesture, so
        // the cycle brush only responds to a discrete press.
        if (!isPress)
        {
            return;
        }

        BoardCellKind next = current.alt
            ? BoardCellKind.Playable
            : current.shift
                ? BoardCellRules.Previous(currentKind)
                : BoardCellRules.Next(currentKind);

        SetCellKind(cell, next);
        current.Use();
    }

    private static BoardCellKind BrushToKind(ShapeBrush brush)
    {
        switch (brush)
        {
            case ShapeBrush.Removed: return BoardCellKind.Removed;
            case ShapeBrush.Blocker: return BoardCellKind.Blocker;
            case ShapeBrush.Frozen: return BoardCellKind.Frozen;
            default: return BoardCellKind.Playable;
        }
    }

    private int FindCellStateIndex(Vector2Int cell)
    {
        for (int index = 0; index < cellStatesProperty.arraySize; index++)
        {
            SerializedProperty coordinate = cellStatesProperty
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative("coordinate");
            if (coordinate.vector2IntValue == cell)
            {
                return index;
            }
        }

        return -1;
    }

    private BoardCellKind GetCellKind(Vector2Int cell)
    {
        int index = FindCellStateIndex(cell);
        if (index < 0)
        {
            return BoardCellKind.Playable;
        }

        SerializedProperty kind = cellStatesProperty
            .GetArrayElementAtIndex(index)
            .FindPropertyRelative("kind");
        return (BoardCellKind)kind.enumValueIndex;
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

    private void SetCellKind(Vector2Int cell, BoardCellKind kind)
    {
        int existingIndex = FindCellStateIndex(cell);

        if (kind == BoardCellKind.Playable)
        {
            if (existingIndex >= 0)
            {
                cellStatesProperty.DeleteArrayElementAtIndex(existingIndex);
            }

            return;
        }

        // A board with nothing placeable cannot spawn or accept a box at all, so
        // the last playable cell is protected. Only checked when adding a new
        // non-playable cell; changing one blocker kind into another is always safe.
        if (existingIndex < 0)
        {
            int totalCells = widthProperty.intValue * heightProperty.intValue;
            if (cellStatesProperty.arraySize >= totalCells - 1)
            {
                EditorUtility.DisplayDialog(
                    "Board Layout",
                    "At least one Board cell must remain playable.",
                    "OK");
                return;
            }
        }

        // A blocker occupies the cell, so any starting box authored there is gone.
        int initialIndex = FindInitialBoxIndex(cell);
        if (initialIndex >= 0)
        {
            initialBoxesProperty.DeleteArrayElementAtIndex(initialIndex);
            if (selectedInitialCell == cell)
            {
                selectedInitialCell = new Vector2Int(-1, -1);
            }
        }

        if (existingIndex < 0)
        {
            existingIndex = cellStatesProperty.arraySize;
            cellStatesProperty.InsertArrayElementAtIndex(existingIndex);
            cellStatesProperty
                .GetArrayElementAtIndex(existingIndex)
                .FindPropertyRelative("coordinate")
                .vector2IntValue = cell;
        }

        cellStatesProperty
            .GetArrayElementAtIndex(existingIndex)
            .FindPropertyRelative("kind")
            .enumValueIndex = (int)kind;
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
