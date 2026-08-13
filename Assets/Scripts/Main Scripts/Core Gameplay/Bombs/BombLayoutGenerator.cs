using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Produces candidate bomb layouts.
///
/// One class rather than two because the editor and the runtime must agree on
/// what a legal bomb cell is. The editor generates a pool and the solver proves
/// each entry winnable; the runtime only ever picks from that pool. The runtime
/// fallback exists for a level that has bombs enabled but no baked pool - it
/// keeps such a level playable in the editor instead of silently having no
/// bombs, and it is not something a shipped level should ever reach.
/// </summary>
public static class BombLayoutGenerator
{
    /// <summary>
    /// Legal bomb cells for a level described by its authored data, with no
    /// Board in existence. Mirrors Board.CanHostBomb exactly: a plain playable
    /// cell with no starting box on it.
    ///
    /// Blocker, frozen and hole cells are excluded because they have no Node -
    /// GetDropTargetNode returns grid[c,r] - so a bomb there could never be
    /// dropped on, defused or triggered. Starting-box cells are excluded because
    /// the bomb would be hidden under something the player cannot pick up.
    /// </summary>
    public static List<Vector2Int> GetLegalCells(
        int width,
        int height,
        IReadOnlyList<BoardCellEntry> cellStates,
        IReadOnlyList<InitialBoardBoxData> initialBoxes)
    {
        HashSet<Vector2Int> excluded = new HashSet<Vector2Int>();

        if (cellStates != null)
        {
            foreach (BoardCellEntry entry in cellStates)
            {
                if (BoardCellRules.BlocksPlacement(entry.kind))
                {
                    excluded.Add(entry.coordinate);
                }
            }
        }

        if (initialBoxes != null)
        {
            foreach (InitialBoardBoxData box in initialBoxes)
            {
                if (box != null)
                {
                    excluded.Add(box.coordinate);
                }
            }
        }

        List<Vector2Int> legal = new List<Vector2Int>();
        for (int column = 0; column < width; column++)
        {
            for (int row = 0; row < height; row++)
            {
                Vector2Int coordinate = new Vector2Int(column, row);
                if (!excluded.Contains(coordinate))
                {
                    legal.Add(coordinate);
                }
            }
        }

        return legal;
    }

    /// <summary>
    /// Draws one layout of the requested size from the legal cells.
    ///
    /// Sampling without replacement rather than picking N times: duplicates were
    /// called out as a thing to prevent, and a set of coordinates cannot express
    /// two bombs in one cell anyway, so drawing with replacement would silently
    /// produce short layouts.
    /// </summary>
    public static List<Vector2Int> Draw(IReadOnlyList<Vector2Int> legalCells, int bombCount, System.Random random)
    {
        List<Vector2Int> pool = new List<Vector2Int>(legalCells);
        List<Vector2Int> layout = new List<Vector2Int>();

        int target = Mathf.Min(bombCount, pool.Count);
        for (int i = 0; i < target; i++)
        {
            int index = random.Next(0, pool.Count);
            layout.Add(pool[index]);
            pool.RemoveAt(index);
        }

        layout.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return layout;
    }

    /// <summary>
    /// A layout drawn against a live Board. Only used when a level's baked pool
    /// is missing, and NOT proved solvable.
    /// </summary>
    public static List<Vector2Int> GenerateRuntimeFallback(Board board, int bombCount)
    {
        List<Vector2Int> legal = new List<Vector2Int>();
        if (board == null)
        {
            return legal;
        }

        for (int column = 0; column < board.Width; column++)
        {
            for (int row = 0; row < board.Height; row++)
            {
                if (board.CanHostBomb(column, row))
                {
                    legal.Add(new Vector2Int(column, row));
                }
            }
        }

        return Draw(legal, bombCount, new System.Random(Random.Range(int.MinValue, int.MaxValue)));
    }

    /// <summary>
    /// A stable key for a layout, so a pool can reject duplicates without
    /// depending on list ordering.
    /// </summary>
    public static string GetSignature(IReadOnlyList<Vector2Int> layout)
    {
        List<Vector2Int> sorted = new List<Vector2Int>(layout);
        sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        foreach (Vector2Int coordinate in sorted)
        {
            builder.Append(coordinate.x).Append(',').Append(coordinate.y).Append(';');
        }

        return builder.ToString();
    }
}
