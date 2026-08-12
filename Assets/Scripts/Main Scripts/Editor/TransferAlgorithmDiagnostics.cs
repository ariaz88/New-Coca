#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Guards the rule the levels are designed around: when a rail Box lands
/// between two Boxes and the group holds enough of one colour, the resolver has
/// to reach the four match instead of emptying the bridge Box into a neighbour.
/// The named scenarios run on every recompile; the exhaustive sweep is a menu
/// item because it walks every three Box layout the board can produce.
/// </summary>
public static class TransferAlgorithmDiagnostics
{
    private const int BoxCapacity = 4;
    private const int MaxResolverMoves = 12;

    private static readonly Vector2Int[] HorizontalLayout =
        { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) };

    private static readonly Vector2Int[] VerticalLayout =
        { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) };

    private static readonly Vector2Int[] LeftElbowLayout =
        { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 2) };

    private static readonly Vector2Int[] RightElbowLayout =
        { new Vector2Int(2, 1), new Vector2Int(1, 1), new Vector2Int(1, 0) };

    private static readonly (string name, Vector2Int[] cells)[] BridgeLayouts =
    {
        ("horizontal", HorizontalLayout),
        ("vertical", VerticalLayout),
        ("left elbow", LeftElbowLayout),
        ("right elbow", RightElbowLayout)
    };

    // The sweep costs a few seconds, so it runs once per editor session rather
    // than on every recompile. SessionState survives domain reloads and is
    // cleared when Unity restarts, which is exactly the cadence wanted here.
    // Bump the suffix whenever the planner changes, so the next recompile runs
    // the sweep again instead of trusting a result from the old algorithm.
    private const string SweepSessionKey = "CocaSorting.TransferSweepRan.v3";

    [InitializeOnLoadMethod]
    private static void RunOnceForVerification()
    {
        EditorApplication.delayCall += RunNamedScenarios;

        if (SessionState.GetBool(SweepSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(SweepSessionKey, true);
        EditorApplication.delayCall += SweepEveryThreeBoxLayout;
    }

    [MenuItem("Tools/Coca Sorting/Tests/Run Active Transfer Tests")]
    public static void RunNamedScenarios()
    {
        List<string> failures = new List<string>();

        foreach ((string name, Vector2Int[] cells) in BridgeLayouts)
        {
            // The classic report: two sodas on one side, one on the bridge, one
            // beside a blocker colour. Greedy scoring wants to empty the bridge.
            CheckBridgeScenario(
                $"{name}: 2 + 1 + (1 + blocker)",
                cells,
                new[] { Orange(2), Orange(1), Mixed(1, 1) },
                failures);

            // The bridge already carries a second colour, so filling it to the
            // brim locks it. Only a partial transfer keeps the group alive.
            CheckBridgeScenario(
                $"{name}: 2 + (1 + blocker) + 1",
                cells,
                new[] { Orange(2), Mixed(1, 1), Orange(1) },
                failures);

            CheckBridgeScenario(
                $"{name}: (1 + blocker) + 1 + 2",
                cells,
                new[] { Mixed(1, 1), Orange(1), Orange(2) },
                failures);

            CheckBridgeScenario(
                $"{name}: 1 + 2 + 1",
                cells,
                new[] { Orange(1), Orange(2), Orange(1) },
                failures);

            CheckBridgeScenario(
                $"{name}: 3 + 1 + blocker",
                cells,
                new[] { Orange(3), Orange(1), Mixed(1, 2) },
                failures);

            // Reported from a real level: one soda on the bridge and one in each
            // neighbour. No four match exists, so the old scoring emptied the
            // bridge into a neighbour and stranded the colour for good.
            CheckGatheringScenario(
                $"{name}: 1 + 1 + 1 with no fourth",
                cells,
                new[] { Mixed(1, 1), Orange(1), Mixed(1, 2) },
                failures);

            CheckGatheringScenario(
                $"{name}: 2 + 1 with a dead third",
                cells,
                new[] { Orange(2), Orange(1), Mixed(0, 3) },
                failures);

            CheckGatheringScenario(
                $"{name}: 1 + 1 + 1 all carrying passengers",
                cells,
                new[] { Mixed(1, 2), Orange(1), Mixed(1, 2) },
                failures);
        }

        Report("Named transfer scenarios", BridgeLayouts.Length * 8, failures);
    }

    [MenuItem("Tools/Coca Sorting/Tests/Sweep Every Three Box Layout")]
    public static void SweepEveryThreeBoxLayout()
    {
        List<string> failures = new List<string>();
        List<(int orange, int blue)> contents = BuildContentPermutations();
        int checkedLayouts = 0;

        foreach ((string name, Vector2Int[] cells) in BridgeLayouts)
        {
            foreach ((int orange, int blue) left in contents)
            {
                foreach ((int orange, int blue) middle in contents)
                {
                    foreach ((int orange, int blue) right in contents)
                    {
                        checkedLayouts++;
                        CheckReachableLayout(
                            name,
                            cells,
                            new[] { left, middle, right },
                            failures);

                        if (failures.Count >= 20)
                        {
                            Report(
                                $"Three Box sweep (stopped early after {checkedLayouts} layouts)",
                                checkedLayouts,
                                failures);
                            return;
                        }
                    }
                }
            }
        }

        Report("Three Box sweep", checkedLayouts, failures);
    }

    /// <summary>
    /// Every Box content the sweep considers: at least one soda, never already
    /// packed, because an empty or packed Box is retired before the resolver
    /// sees the component.
    /// </summary>
    private static List<(int orange, int blue)> BuildContentPermutations()
    {
        List<(int orange, int blue)> result = new List<(int orange, int blue)>();

        for (int orange = 0; orange <= BoxCapacity; orange++)
        {
            for (int blue = 0; blue + orange <= BoxCapacity; blue++)
            {
                int total = orange + blue;
                if (total == 0 || orange == BoxCapacity || blue == BoxCapacity)
                {
                    continue;
                }

                result.Add((orange, blue));
            }
        }

        return result;
    }

    private static void CheckBridgeScenario(
        string label,
        Vector2Int[] cells,
        (int orange, int blue)[] contents,
        List<string> failures)
    {
        List<TransferAlgorithm.BoxState> states = BuildStates(cells, contents);
        List<TransferAlgorithm.Edge> edges = BuildChainEdges();

        (bool packedReachable, int _) = TransferAlgorithm.ExploreReachableOutcomes(states, edges);
        if (!packedReachable)
        {
            failures.Add($"{label}: no four match is reachable, so the scenario itself is wrong.");
            return;
        }

        if (!TryRunResolver(states, edges, out int moves, out string firstMove, out _))
        {
            failures.Add($"{label}: the resolver stopped after {moves} moves without a four match (first move {firstMove}).");
        }
    }

    /// <summary>
    /// Three sodas of one colour with no fourth in sight still have to end up
    /// together. Scattering them across neighbours, or emptying the bridge Box
    /// that joins them, leaves the player with a colour that can never be
    /// completed.
    /// </summary>
    private static void CheckGatheringScenario(
        string label,
        Vector2Int[] cells,
        (int orange, int blue)[] contents,
        List<string> failures)
    {
        List<TransferAlgorithm.BoxState> states = BuildStates(cells, contents);
        List<TransferAlgorithm.Edge> edges = BuildChainEdges();

        (bool packedReachable, int bestConcentration) =
            TransferAlgorithm.ExploreReachableOutcomes(states, edges);

        if (packedReachable)
        {
            failures.Add($"{label}: this scenario is meant to have no four match, but one is reachable.");
            return;
        }

        TryRunResolver(states, edges, out int moves, out string firstMove, out int reached);

        if (reached < bestConcentration)
        {
            failures.Add(
                $"{label}: gathered only {reached} of a reachable {bestConcentration} " +
                $"after {moves} moves (first move {firstMove}).");
        }
    }

    private static void CheckReachableLayout(
        string layoutName,
        Vector2Int[] cells,
        (int orange, int blue)[] contents,
        List<string> failures)
    {
        List<TransferAlgorithm.BoxState> states = BuildStates(cells, contents);
        List<TransferAlgorithm.Edge> edges = BuildChainEdges();

        (bool packedReachable, int bestConcentration) =
            TransferAlgorithm.ExploreReachableOutcomes(states, edges);

        bool solved = TryRunResolver(states, edges, out int moves, out string firstMove, out int reached);

        if (packedReachable && !solved)
        {
            failures.Add(
                $"{layoutName} {Describe(contents)}: reachable four match missed after {moves} moves (first move {firstMove}).");
            return;
        }

        // No four match here, but the colour still has to end up gathered.
        if (!packedReachable && reached < bestConcentration)
        {
            failures.Add(
                $"{layoutName} {Describe(contents)}: gathered only {reached} of a reachable " +
                $"{bestConcentration} after {moves} moves (first move {firstMove}).");
        }
    }

    /// <summary>
    /// Replays the resolver loop exactly as UniversalSodaTransferSystem does,
    /// minus the animation timing, so a failure here is a planner failure.
    /// </summary>
    private static bool TryRunResolver(
        List<TransferAlgorithm.BoxState> states,
        List<TransferAlgorithm.Edge> edges,
        out int moves,
        out string firstMove,
        out int reachedConcentration)
    {
        HashSet<string> visited = new HashSet<string> { TransferAlgorithm.BuildSignature(states) };
        moves = 0;
        firstMove = "none";
        reachedConcentration = TransferAlgorithm.GetConcentrationValue(states);

        while (moves < MaxResolverMoves)
        {
            reachedConcentration = Mathf.Max(
                reachedConcentration,
                TransferAlgorithm.GetConcentrationValue(states));

            if (states.Any(state => state.IsPacked))
            {
                return true;
            }

            if (!TransferAlgorithm.TrySelectMove(
                    states,
                    edges,
                    TriggerId,
                    visited,
                    out TransferAlgorithm.Decision decision))
            {
                return false;
            }

            if (moves == 0)
            {
                firstMove = decision.ToString();
            }

            states = TransferAlgorithm.ApplyForSimulation(states, decision);
            moves++;
            reachedConcentration = Mathf.Max(
                reachedConcentration,
                TransferAlgorithm.GetConcentrationValue(states));

            if (states.Any(state => state.IsPacked))
            {
                return true;
            }

            // The resolver aborts on a repeated state to stay clear of a
            // ping-pong loop, so a repeat here is a failed resolution.
            if (!visited.Add(TransferAlgorithm.BuildSignature(states)))
            {
                return false;
            }
        }

        return states.Any(state => state.IsPacked);
    }

    private static List<TransferAlgorithm.BoxState> BuildStates(
        Vector2Int[] cells,
        (int orange, int blue)[] contents)
    {
        List<TransferAlgorithm.BoxState> states = new List<TransferAlgorithm.BoxState>();

        for (int index = 0; index < cells.Length; index++)
        {
            TransferAlgorithm.BoxState state = new TransferAlgorithm.BoxState
            {
                Id = index + 1,
                Column = cells[index].x,
                Row = cells[index].y,
                Capacity = BoxCapacity,
                // The bridge is the Box the player just dropped, so it carries
                // the newest placement order.
                PlacementOrder = index == 1 ? 10 : index + 1
            };

            if (contents[index].orange > 0)
            {
                state.Colors[Soda.SodaColor.Orange] = contents[index].orange;
            }

            if (contents[index].blue > 0)
            {
                state.Colors[Soda.SodaColor.Blue] = contents[index].blue;
            }

            states.Add(state);
        }

        return states;
    }

    // Box 2 sits between Box 1 and Box 3 in every layout above, so the chain is
    // always 1 - 2 - 3 whether the cells form a row, a column, or an elbow.
    private static List<TransferAlgorithm.Edge> BuildChainEdges()
    {
        return new List<TransferAlgorithm.Edge>
        {
            new TransferAlgorithm.Edge(1, 2),
            new TransferAlgorithm.Edge(2, 3)
        };
    }

    private static int TriggerId => 2;

    private static (int orange, int blue) Orange(int count) => (count, 0);

    private static (int orange, int blue) Mixed(int orange, int blue) => (orange, blue);

    private static string Describe((int orange, int blue)[] contents)
    {
        return "[" + string.Join(" | ", contents.Select(entry => $"O{entry.orange}B{entry.blue}")) + "]";
    }

    private static void Report(string title, int checkedCount, List<string> failures)
    {
        if (failures.Count == 0)
        {
            Debug.Log($"{title}: {checkedCount} layouts passed.");
            return;
        }

        Debug.LogError(
            $"{title}: {failures.Count} of {checkedCount} layouts failed.\n" +
            string.Join("\n", failures));
    }
}
#endif
