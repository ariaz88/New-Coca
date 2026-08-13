using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum IssueSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public readonly struct ValidationIssue
{
    public readonly IssueSeverity Severity;
    public readonly string Code;
    public readonly string Message;
    public readonly Vector2Int? Cell;

    public ValidationIssue(IssueSeverity severity, string code, string message, Vector2Int? cell = null)
    {
        Severity = severity;
        Code = code;
        Message = message;
        Cell = cell;
    }

    public override string ToString()
    {
        string where = Cell.HasValue ? $" at ({Cell.Value.x}, {Cell.Value.y})" : string.Empty;
        return $"[{Severity}] {Code}: {Message}{where}";
    }
}

/// <summary>
/// Static checks over an authored level, run before the expensive solvability
/// search. Most authoring mistakes are structural - a blocker on a starting box,
/// an order for a colour the level never delivers - and catching those here means
/// the solver only ever runs on levels that could in principle work.
/// </summary>
public static class LevelValidator
{
    private const int BoxCapacity = 4;

    private static readonly Vector2Int[] Orthogonal =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0)
    };

    public static List<ValidationIssue> Validate(LevelDefinition definition)
    {
        List<ValidationIssue> issues = new List<ValidationIssue>();

        if (definition == null)
        {
            issues.Add(new ValidationIssue(IssueSeverity.Error, "NULL", "Definition is null."));
            return issues;
        }

        ValidateLayout(definition, issues);
        ValidateOrders(definition, issues);
        ValidateRail(definition, issues);
        ValidateWiring(definition, issues);

        return issues;
    }

    // ---------------------------------------------------------------- layout

    private static void ValidateLayout(LevelDefinition definition, List<ValidationIssue> issues)
    {
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();

        foreach (BoardCellEntry entry in definition.CellStates)
        {
            if (entry.coordinate.x < 0 || entry.coordinate.x >= definition.Width ||
                entry.coordinate.y < 0 || entry.coordinate.y >= definition.Height)
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Error, "CELL_OOB",
                    "Cell is outside the board bounds.", entry.coordinate));
                continue;
            }

            if (!seen.Add(entry.coordinate))
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Error, "CELL_DUP",
                    "Cell is listed more than once.", entry.coordinate));
            }
        }

        HashSet<Vector2Int> boxCells = new HashSet<Vector2Int>();
        foreach (InitialBoardBoxData box in definition.InitialBoxes)
        {
            if (box == null)
            {
                continue;
            }

            if (box.coordinate.x < 0 || box.coordinate.x >= definition.Width ||
                box.coordinate.y < 0 || box.coordinate.y >= definition.Height)
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Error, "BOX_OOB",
                    "Starting box is outside the board bounds.", box.coordinate));
                continue;
            }

            if (!boxCells.Add(box.coordinate))
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Error, "BOX_DUP",
                    "Two starting boxes share a cell.", box.coordinate));
            }

            if (definition.GetCellKind(box.coordinate) != BoardCellKind.Playable)
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Error, "CELL_ON_BOX",
                    "A blocker or hole sits on a cell that also has a starting box.", box.coordinate));
            }

            int sodaCount = box.startingSodas != null ? box.startingSodas.Count : 0;
            if (sodaCount == 0)
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Warning, "BOX_EMPTY",
                    "Starting box has no sodas and will retire immediately.", box.coordinate));
            }
            else if (sodaCount > BoxCapacity)
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Error, "BOX_OVERFULL",
                    $"Starting box holds {sodaCount} sodas but capacity is {BoxCapacity}.", box.coordinate));
            }
        }

        int playable = definition.PlayableCellCount;
        if (playable <= 0)
        {
            issues.Add(new ValidationIssue(
                IssueSeverity.Error, "NO_PLAYABLE", "The board has no playable cell."));
        }
        else if (playable < 4)
        {
            issues.Add(new ValidationIssue(
                IssueSeverity.Warning, "TOO_FEW_PLAYABLE",
                $"Only {playable} playable cells; a box needs neighbours to transfer with."));
        }

        ValidatePockets(definition, issues);
    }

    /// <summary>
    /// Finds cells that can never touch another box.
    ///
    /// A soda only ever moves between orthogonal neighbours, so a playable cell
    /// walled in by permanent holes is a trap: anything placed there is stuck for
    /// the rest of the level. Breakable blockers do not count as walls, since they
    /// open up - so the search treats them as passable.
    /// </summary>
    private static void ValidatePockets(LevelDefinition definition, List<ValidationIssue> issues)
    {
        for (int x = 0; x < definition.Width; x++)
        {
            for (int y = 0; y < definition.Height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (definition.GetCellKind(cell) != BoardCellKind.Playable)
                {
                    continue;
                }

                bool hasReachableNeighbour = false;
                foreach (Vector2Int offset in Orthogonal)
                {
                    Vector2Int neighbour = cell + offset;
                    if (neighbour.x < 0 || neighbour.x >= definition.Width ||
                        neighbour.y < 0 || neighbour.y >= definition.Height)
                    {
                        continue;
                    }

                    if (definition.GetCellKind(neighbour) != BoardCellKind.Removed)
                    {
                        hasReachableNeighbour = true;
                        break;
                    }
                }

                if (!hasReachableNeighbour)
                {
                    issues.Add(new ValidationIssue(
                        IssueSeverity.Error, "POCKET",
                        "Playable cell is sealed in by holes and can never transfer with anything.",
                        cell));
                }
            }
        }
    }

    // ---------------------------------------------------------------- orders

    private static void ValidateOrders(LevelDefinition definition, List<ValidationIssue> issues)
    {
        if (definition.Orders.Count == 0)
        {
            issues.Add(new ValidationIssue(
                IssueSeverity.Error, "NO_ORDERS",
                "The level has no orders, so it falls back to the legacy 10-box win condition."));
            return;
        }

        HashSet<Soda.SodaColor> seenColors = new HashSet<Soda.SodaColor>();

        foreach (LevelOrderData order in definition.Orders)
        {
            if (order == null)
            {
                continue;
            }

            // OrderManager drops duplicate colours at runtime, so a duplicate here
            // silently changes the level's goal rather than doubling it.
            if (!seenColors.Add(order.color))
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Error, "ORDER_DUP_COLOR",
                    $"{order.color} is ordered twice; OrderManager will ignore the second entry."));
            }

            if (order.requiredCount < 1)
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Error, "ORDER_COUNT",
                    $"{order.color} order requires {order.requiredCount} boxes."));
            }

            int available = definition.CountAvailableSodas(order.color);
            int needed = order.requiredCount * BoxCapacity;

            if (available == 0)
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Error, "ORDER_COLOR_ABSENT",
                    $"{order.color} is ordered but never appears on the board or in the rail queue."));
            }
            else if (available < needed)
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Error, "ORDER_SUPPLY",
                    $"{order.color} needs {needed} sodas ({order.requiredCount} boxes) but only " +
                    $"{available} exist. The level cannot be completed from its authored material."));
            }
        }
    }

    // ------------------------------------------------------------------ rail

    private static void ValidateRail(LevelDefinition definition, List<ValidationIssue> issues)
    {
        if (definition.RailQueue.Count == 0)
        {
            issues.Add(new ValidationIssue(
                IssueSeverity.Error, "RAIL_EMPTY",
                "The rail queue is empty, so the level depends entirely on its starting board."));
            return;
        }

        HashSet<Soda.SodaColor> palette = new HashSet<Soda.SodaColor>(definition.Palette);

        for (int index = 0; index < definition.RailQueue.Count; index++)
        {
            TutorialBoxRecipe recipe = definition.RailQueue[index];

            if (recipe == null || recipe.TotalCount == 0)
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Error, "RAIL_EMPTY_RECIPE",
                    $"Rail box {index} is empty and would spawn a box that retires on placement."));
                continue;
            }

            // The random path clamps its fill to capacity-1, but an authored recipe
            // is taken literally: a box that arrives already packed cannot be
            // usefully dragged anywhere.
            if (recipe.TotalCount >= BoxCapacity)
            {
                issues.Add(new ValidationIssue(
                    IssueSeverity.Error, "RAIL_FULL_RECIPE",
                    $"Rail box {index} holds {recipe.TotalCount} sodas; a rail box must leave at " +
                    $"least one free slot (max {BoxCapacity - 1})."));
            }

            foreach (KeyValuePair<Soda.SodaColor, int> amount in recipe.ToDictionary())
            {
                if (palette.Count > 0 && !palette.Contains(amount.Key))
                {
                    issues.Add(new ValidationIssue(
                        IssueSeverity.Warning, "RAIL_COLOR_OFF_PALETTE",
                        $"Rail box {index} delivers {amount.Key}, which is not in the level palette."));
                }
            }
        }

        if (definition.RailBatchSize < 1)
        {
            issues.Add(new ValidationIssue(
                IssueSeverity.Error, "RAIL_BATCH", "Rail batch size must be at least 1."));
        }

        // A level verified against its authored queue must not need the fallback
        // to be winnable; looping makes "solved in N moves" meaningless.
        if (definition.RailExhaustionPolicy == RailExhaustionPolicy.LoopQueue)
        {
            issues.Add(new ValidationIssue(
                IssueSeverity.Warning, "RAIL_POLICY",
                "LoopQueue makes the rail unbounded, so a solvability proof cannot be trusted."));
        }
    }

    // --------------------------------------------------------------- wiring

    private static void ValidateWiring(LevelDefinition definition, List<ValidationIssue> issues)
    {
        if (!File.Exists(definition.ScenePath))
        {
            issues.Add(new ValidationIssue(
                IssueSeverity.Error, "SCENE_MISSING",
                $"No scene at {definition.ScenePath}. Press Bake to generate it."));
            return;
        }

        if (!LevelSceneGenerator.IsInBuildSettings(definition.ScenePath))
        {
            issues.Add(new ValidationIssue(
                IssueSeverity.Error, "SCENE_NOT_IN_BUILD",
                $"{definition.SceneName} is not an enabled scene in Build Settings, so progression " +
                "cannot reach it."));
        }

        if (!definition.IsBakedCurrent)
        {
            issues.Add(new ValidationIssue(
                IssueSeverity.Warning, "SCENE_DRIFT",
                "The scene was baked from an older version of this definition. Press Bake."));
        }
    }
}
