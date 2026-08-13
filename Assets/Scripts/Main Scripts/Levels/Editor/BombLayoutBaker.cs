using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CocaSorting.Levels.Simulation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates each level's pool of bomb layouts and proves every one of them
/// winnable before it is allowed into the pool.
///
/// This is how "random bombs, always solvable" is delivered. The runtime cannot
/// solve a level - the solver takes seconds and allocates freely - so the
/// randomness is moved into the editor: many candidate layouts are drawn here,
/// each is solved with its bomb cells treated as unusable, and only the winners
/// are stored. At runtime the level picks pool[attempt % count], which is a
/// genuinely different board every restart and provably completable every time.
/// </summary>
public static class BombLayoutBaker
{
    /// <summary>Layouts kept per level. Enough that a player rarely repeats one.</summary>
    private const int TargetPoolSize = 8;

    /// <summary>Draws attempted per level before giving up on filling the pool.</summary>
    private const int MaxDraws = 60;

    [MenuItem("Tools/Coca Sorting/Levels/Generate Bomb Layouts", priority = 92)]
    public static void GenerateAll()
    {
        List<LevelDefinition> definitions = LevelSceneGenerator.LoadAllDefinitions();
        if (definitions.Count == 0)
        {
            Debug.LogWarning("No LevelDefinition assets found.");
            return;
        }

        StringBuilder log = new StringBuilder("Bomb layout pools\n");
        int levelsWithBombs = 0;
        int failures = 0;

        try
        {
            for (int index = 0; index < definitions.Count; index++)
            {
                LevelDefinition definition = definitions[index];
                EditorUtility.DisplayProgressBar(
                    "Generating bomb layouts",
                    definition.SceneName,
                    index / (float)definitions.Count);

                if (!definition.Bombs.IsActive)
                {
                    definition.EditorSetBombLayoutPool(new List<BombLayout>());
                    EditorUtility.SetDirty(definition);
                    AssetDatabase.SaveAssetIfDirty(definition);
                    continue;
                }

                levelsWithBombs++;
                List<BombLayout> pool = BuildPool(definition, out int drawn);

                definition.EditorSetBombLayoutPool(pool);
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);

                log.AppendLine(
                    $"  {definition.SceneName}: {pool.Count}/{TargetPoolSize} verified " +
                    $"from {drawn} draws ({definition.Bombs.bombCount} bombs)");

                if (pool.Count == 0)
                {
                    failures++;
                    log.AppendLine(
                        $"    >>> {definition.SceneName} HAS NO SOLVABLE BOMB LAYOUT <<<");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        log.AppendLine($"{levelsWithBombs} levels with bombs, {failures} with an empty pool.");

        if (failures > 0)
        {
            Debug.LogError(log.ToString());
        }
        else
        {
            Debug.Log(log.ToString());
        }
    }

    /// <summary>
    /// Draws candidates and keeps the ones the solver wins.
    ///
    /// Draws are seeded from the level number so a re-bake reproduces the same
    /// pool: the campaign is supposed to be a designed artefact, and a pool that
    /// changed on every bake would make "level 14 is fair" a claim about a build
    /// rather than about the level.
    /// </summary>
    private static List<BombLayout> BuildPool(LevelDefinition definition, out int drawn)
    {
        List<Vector2Int> legalCells = BombLayoutGenerator.GetLegalCells(
            definition.Width, definition.Height, definition.CellStates, definition.InitialBoxes);

        int bombCount = definition.Bombs.bombCount;
        List<BombLayout> pool = new List<BombLayout>();
        HashSet<string> seen = new HashSet<string>();

        if (legalCells.Count <= bombCount)
        {
            // Filling the board with bombs leaves nothing to play on. Better to
            // report an empty pool than to bake a level that cannot be started.
            drawn = 0;
            return pool;
        }

        System.Random random = new System.Random(4517 + definition.LevelNumber * 191);
        List<List<Vector2Int>> candidates = new List<List<Vector2Int>>();

        while (candidates.Count < MaxDraws)
        {
            List<Vector2Int> layout = BombLayoutGenerator.Draw(legalCells, bombCount, random);
            if (layout.Count < bombCount || !seen.Add(BombLayoutGenerator.GetSignature(layout)))
            {
                // The space of distinct layouts is finite and small on a 4x5
                // board; once draws stop producing new ones there is nothing left
                // to find and looping to MaxDraws would just spin.
                if (seen.Count >= CountDistinctPossible(legalCells.Count, bombCount))
                {
                    break;
                }

                continue;
            }

            candidates.Add(layout);
        }

        drawn = candidates.Count;

        // The simulator touches no Unity API, so candidates solve in parallel -
        // the same reason Verify All can. LastSelectionSource is already
        // [ThreadStatic], which is what makes the shared TransferAlgorithm safe.
        bool[] solved = new bool[candidates.Count];
        SolveOptions options = new SolveOptions();
        Parallel.For(0, candidates.Count, index =>
        {
            LevelSolveReport report = LevelVerification.VerifyWithBombs(
                definition, candidates[index], options);
            solved[index] = report.Outcome == SolveOutcome.Solved;
        });

        for (int index = 0; index < candidates.Count && pool.Count < TargetPoolSize; index++)
        {
            if (solved[index])
            {
                pool.Add(new BombLayout(candidates[index]));
            }
        }

        return pool;
    }

    /// <summary>
    /// C(n, k), capped so a large board cannot overflow. Only used to decide when
    /// drawing has exhausted the space.
    /// </summary>
    private static long CountDistinctPossible(int cellCount, int bombCount)
    {
        if (bombCount <= 0 || bombCount > cellCount)
        {
            return 0;
        }

        long result = 1;
        for (int i = 1; i <= bombCount; i++)
        {
            result = result * (cellCount - bombCount + i) / i;
            if (result > MaxDraws)
            {
                return MaxDraws + 1;
            }
        }

        return result;
    }
}
