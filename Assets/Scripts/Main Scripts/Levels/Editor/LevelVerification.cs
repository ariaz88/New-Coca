using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CocaSorting.Levels.Simulation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns a LevelDefinition into a simulation state and runs the solver over it.
///
/// This is what makes "every level is completable" a fact rather than a claim.
/// It deliberately verifies against the AUTHORED rail queue only - the random
/// fallback is excluded, so a level proven here is winnable from its designed
/// material even if the player never triggers the fallback at all.
/// </summary>
public static class LevelVerification
{
    /// <summary>
    /// Builds the simulation state with a set of cells treated as permanently
    /// unusable, which is how a bomb layout is verified.
    ///
    /// Modelling a bomb as a hole is deliberately CONSERVATIVE: it assumes the
    /// player never defuses anything and never dares drop on a bomb cell, so a
    /// layout that passes is winnable by a player who simply avoids all of them.
    /// The real level is strictly easier than what was proved, which is the right
    /// direction for a guarantee to be wrong in.
    /// </summary>
    public static LevelSimState BuildStateWithBombs(
        LevelDefinition definition, IReadOnlyList<Vector2Int> bombCells, int boxCapacity = 4)
    {
        LevelSimState state = BuildState(definition, boxCapacity);
        if (bombCells == null)
        {
            return state;
        }

        foreach (Vector2Int cell in bombCells)
        {
            if (!state.IsInside(cell.x, cell.y))
            {
                continue;
            }

            int index = state.Index(cell.x, cell.y);

            // A bomb only ever sits on an empty playable cell, so anything else
            // here means the layout is malformed - leave it alone rather than
            // quietly deleting a starting box and proving the wrong level.
            if (state.Cells[index] == SimCell.Playable && state.Boxes[index] == null)
            {
                state.Cells[index] = SimCell.Removed;
            }
        }

        return state;
    }

    public static LevelSolveReport VerifyWithBombs(
        LevelDefinition definition, IReadOnlyList<Vector2Int> bombCells, SolveOptions options = null)
    {
        options ??= new SolveOptions();
        LevelSimState state = BuildStateWithBombs(definition, bombCells);
        List<int[]> queue = BuildQueue(definition);
        return LevelSolver.Solve(state, queue, options);
    }

    public static LevelSimState BuildState(LevelDefinition definition, int boxCapacity = 4)
    {
        int width = definition.Width;
        int height = definition.Height;
        int cellCount = width * height;

        LevelSimState state = new LevelSimState
        {
            Width = width,
            Height = height,
            BoxCapacity = boxCapacity,
            Cells = new SimCell[cellCount],
            Boxes = new SimBox[cellCount],
            Rail = new SimBox[Mathf.Max(1, definition.RailBatchSize)],
            LastDamage = new long[cellCount]
        };

        for (int index = 0; index < cellCount; index++)
        {
            state.LastDamage[index] = -1;
        }

        foreach (BoardCellEntry entry in definition.CellStates)
        {
            if (!state.IsInside(entry.coordinate.x, entry.coordinate.y))
            {
                continue;
            }

            int index = state.Index(entry.coordinate.x, entry.coordinate.y);
            switch (entry.kind)
            {
                case BoardCellKind.Removed: state.Cells[index] = SimCell.Removed; break;
                case BoardCellKind.Blocker: state.Cells[index] = SimCell.Blocker; break;
                case BoardCellKind.Frozen: state.Cells[index] = SimCell.Frozen; break;
                default: state.Cells[index] = SimCell.Playable; break;
            }
        }

        foreach (InitialBoardBoxData data in definition.InitialBoxes)
        {
            if (data == null || !state.IsInside(data.coordinate.x, data.coordinate.y))
            {
                continue;
            }

            int index = state.Index(data.coordinate.x, data.coordinate.y);
            if (state.Cells[index] != SimCell.Playable)
            {
                continue;
            }

            SimBox box = new SimBox
            {
                Id = state.NextId++,
                Column = data.coordinate.x,
                Row = data.coordinate.y,
                Capacity = boxCapacity,
                PlacementOrder = ++state.PlacementSequence
            };

            if (data.startingSodas != null)
            {
                foreach (Soda.SodaColor soda in data.startingSodas)
                {
                    box.Add((int)soda, 1);
                }
            }

            state.Boxes[index] = box;
        }

        foreach (LevelOrderData order in definition.Orders)
        {
            if (order == null)
            {
                continue;
            }

            if (order.IsBlocks)
            {
                state.BlocksOrderRemaining += Mathf.Max(0, order.requiredCount);
            }
            else
            {
                state.OrdersRemaining[(int)order.color] += Mathf.Max(0, order.requiredCount);
            }
        }

        return state;
    }

    public static List<int[]> BuildQueue(LevelDefinition definition)
    {
        List<int[]> queue = new List<int[]>();
        foreach (TutorialBoxRecipe recipe in definition.RailQueue)
        {
            int[] counts = new int[SimBox.ColorCount];
            if (recipe != null)
            {
                foreach (KeyValuePair<Soda.SodaColor, int> amount in recipe.ToDictionary())
                {
                    counts[(int)amount.Key] += amount.Value;
                }
            }

            queue.Add(counts);
        }

        return queue;
    }

    public static LevelSolveReport Verify(LevelDefinition definition, SolveOptions options = null)
    {
        options ??= new SolveOptions();
        LevelSimState state = BuildState(definition);
        List<int[]> queue = BuildQueue(definition);
        return LevelSolver.Solve(state, queue, options);
    }

    /// <summary>
    /// Verifies every level. The simulator touches no Unity API, so the levels
    /// are solved in parallel; only the progress bar and the asset writes happen
    /// on the main thread.
    /// </summary>
    [MenuItem("Tools/Coca Sorting/Levels/Verify All Levels Are Solvable", priority = 91)]
    public static void VerifyAll()
    {
        List<LevelDefinition> definitions = LevelSceneGenerator.LoadAllDefinitions();
        if (definitions.Count == 0)
        {
            Debug.LogWarning("No LevelDefinition assets found.");
            return;
        }

        SolveOptions options = new SolveOptions();
        LevelSolveReport[] reports = new LevelSolveReport[definitions.Count];

        EditorUtility.DisplayProgressBar("Verifying levels", "Solving...", 0.5f);
        try
        {
            Parallel.For(0, definitions.Count, index =>
            {
                reports[index] = Verify(definitions[index], options);
            });
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        StringBuilder log = new StringBuilder();
        int solved = 0;
        int failed = 0;
        int narrow = 0;

        for (int index = 0; index < definitions.Count; index++)
        {
            LevelDefinition definition = definitions[index];
            LevelSolveReport report = reports[index];

            log.AppendLine(report.Describe(definition.SceneName));

            if (report.Outcome == SolveOutcome.Solved)
            {
                solved++;
                if (report.IsNarrow(options))
                {
                    narrow++;
                    log.AppendLine($"    NARROW: {report.NodesExplored} nodes - the winning line may be " +
                                   "too tight to find on a phone.");
                }
            }
            else
            {
                failed++;
                log.AppendLine($"    >>> {definition.SceneName} COULD NOT BE SOLVED <<<");
            }

            definition.EditorSetVerification(
                report.Outcome == SolveOutcome.Solved, report.WinningLine.Count, report.NodesExplored);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
        }

        string summary = $"Solvability: {solved}/{definitions.Count} solved, {failed} failed, {narrow} narrow.";
        if (failed > 0)
        {
            Debug.LogError(summary + "\n" + log);
        }
        else if (narrow > 0)
        {
            Debug.LogWarning(summary + "\n" + log);
        }
        else
        {
            Debug.Log(summary + "\n" + log);
        }
    }

    /// <summary>Prints the full winning line for one level, for hand-checking a design.</summary>
    public static string DescribeSolution(LevelDefinition definition, LevelSolveReport report)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(report.Describe(definition.SceneName));
        for (int index = 0; index < report.WinningLine.Count; index++)
        {
            builder.Append("  #").Append(index + 1).Append(": ")
                   .AppendLine(report.WinningLine[index].ToString());
        }

        return builder.ToString();
    }
}
