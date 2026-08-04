#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TransferAlgorithm_version2Diagnostics
{
    [MenuItem("Tools/Coca Sorting/Run Version 2 Algorithm Tests")]
    public static void RunAll()
    {
        TestNonAdjacentBoxesNeverTransfer();
        TestCapacitySixCompletionWins();
        TestDirectionPriorityIsDeterministic();
        TestFullMixedBoxUnlocksWithoutPingPong();
        TestUnrelatedProgressCannotValidateUnlock();
        TestResolutionTerminatesAcrossAChain();
        Debug.Log("Version 2 transfer algorithm: all diagnostics passed.");
    }

    private static void TestNonAdjacentBoxesNeverTransfer()
    {
        var states = new List<TransferAlgorithm_version2.BoxState>
        {
            State(1, 0, 0, 4, 1, (Soda.SodaColor.Red, 1)),
            State(2, 1, 0, 4, 2, (Soda.SodaColor.Blue, 1)),
            State(3, 2, 0, 4, 3, (Soda.SodaColor.Red, 2))
        };
        var edges = new List<TransferAlgorithm_version2.Edge>
        {
            new TransferAlgorithm_version2.Edge(1, 2),
            new TransferAlgorithm_version2.Edge(2, 3)
        };

        bool found = TransferAlgorithm_version2.TrySelectMove(
            states, edges, 1, new HashSet<string>(), out _);
        Require(!found, "Non-adjacent boxes were allowed to transfer.");
    }

    private static void TestCapacitySixCompletionWins()
    {
        var states = new List<TransferAlgorithm_version2.BoxState>
        {
            State(1, 0, 0, 6, 2, (Soda.SodaColor.Red, 2)),
            State(2, 1, 0, 6, 1, (Soda.SodaColor.Red, 4)),
            State(3, 0, 1, 6, 1, (Soda.SodaColor.Blue, 1), (Soda.SodaColor.Red, 1))
        };
        var edges = new List<TransferAlgorithm_version2.Edge>
        {
            new TransferAlgorithm_version2.Edge(1, 2),
            new TransferAlgorithm_version2.Edge(1, 3)
        };

        Require(
            TransferAlgorithm_version2.TrySelectMove(
                states, edges, 1, new HashSet<string>(), out var move),
            "Capacity-six completion produced no move.");
        Require(
            move.SourceId == 1 && move.TargetId == 2 &&
            move.Color == Soda.SodaColor.Red && move.Amount == 2,
            $"Wrong capacity-six completion selected: {move}");
    }

    private static void TestDirectionPriorityIsDeterministic()
    {
        var states = new List<TransferAlgorithm_version2.BoxState>
        {
            State(1, 1, 1, 4, 3, (Soda.SodaColor.Red, 1)),
            State(2, 2, 1, 4, 1, (Soda.SodaColor.Red, 1)),
            State(3, 1, 2, 4, 2, (Soda.SodaColor.Red, 1))
        };
        var edges = new List<TransferAlgorithm_version2.Edge>
        {
            new TransferAlgorithm_version2.Edge(1, 2),
            new TransferAlgorithm_version2.Edge(1, 3)
        };

        Require(
            TransferAlgorithm_version2.TrySelectMove(
                states, edges, 1, new HashSet<string>(), out var move),
            "Direction-priority test produced no move.");
        Require(
            move.SourceId == 1 && move.TargetId == 2,
            $"Expected right-side priority, selected {move}.");
    }

    private static void TestFullMixedBoxUnlocksWithoutPingPong()
    {
        var states = new List<TransferAlgorithm_version2.BoxState>
        {
            State(
                1, 0, 0, 4, 1,
                (Soda.SodaColor.Red, 2),
                (Soda.SodaColor.Blue, 2)),
            State(
                2, 1, 0, 4, 2,
                (Soda.SodaColor.Red, 1),
                (Soda.SodaColor.Blue, 1),
                (Soda.SodaColor.Green, 1))
        };
        var edges = new List<TransferAlgorithm_version2.Edge>
        {
            new TransferAlgorithm_version2.Edge(1, 2)
        };
        var visited = new HashSet<string>
        {
            TransferAlgorithm_version2.BuildSignature(states)
        };

        Require(
            TransferAlgorithm_version2.TrySelectMove(
                states, edges, 2, visited, out var unlock),
            "Full mixed box could not create a useful vacancy.");
        Require(unlock.IsUnlockMove, $"Expected an unlock move, selected {unlock}.");

        List<TransferAlgorithm_version2.BoxState> afterUnlock =
            TransferAlgorithm_version2.ApplyForSimulation(states, unlock);
        visited.Add(TransferAlgorithm_version2.BuildSignature(afterUnlock));

        Require(
            TransferAlgorithm_version2.TrySelectMove(
                afterUnlock, edges, 2, visited, out var followUp),
            "Unlock move did not expose a follow-up.");
        Require(
            !(followUp.SourceId == unlock.TargetId &&
              followUp.TargetId == unlock.SourceId &&
              followUp.Color == unlock.Color),
            "Unlock immediately reversed into a ping-pong move.");
    }

    private static void TestResolutionTerminatesAcrossAChain()
    {
        var states = new List<TransferAlgorithm_version2.BoxState>
        {
            State(1, 0, 0, 5, 3, (Soda.SodaColor.Red, 1), (Soda.SodaColor.Blue, 1)),
            State(2, 1, 0, 5, 2, (Soda.SodaColor.Red, 2), (Soda.SodaColor.Blue, 1)),
            State(3, 2, 0, 5, 1, (Soda.SodaColor.Red, 1), (Soda.SodaColor.Green, 1))
        };
        var edges = new List<TransferAlgorithm_version2.Edge>
        {
            new TransferAlgorithm_version2.Edge(1, 2),
            new TransferAlgorithm_version2.Edge(2, 3)
        };
        var visited = new HashSet<string>();
        int moves = 0;

        while (moves < 100)
        {
            string signature = TransferAlgorithm_version2.BuildSignature(states);
            Require(visited.Add(signature), "Chain resolution repeated a state.");

            if (!TransferAlgorithm_version2.TrySelectMove(
                    states, edges, 1, visited, out var move))
            {
                break;
            }

            Require(
                IsEdge(edges, move.SourceId, move.TargetId),
                $"Chain selected a non-edge transfer: {move}");
            states = TransferAlgorithm_version2.ApplyForSimulation(states, move);
            moves++;
        }

        Require(moves < 100, "Chain resolution did not terminate.");
    }

    private static void TestUnrelatedProgressCannotValidateUnlock()
    {
        var states = new List<TransferAlgorithm_version2.BoxState>
        {
            State(
                1, 0, 0, 4, 1,
                (Soda.SodaColor.Red, 2),
                (Soda.SodaColor.Blue, 2)),
            State(
                2, 1, 0, 4, 2,
                (Soda.SodaColor.Red, 1),
                (Soda.SodaColor.Orange, 2)),
            State(3, 2, 0, 4, 3, (Soda.SodaColor.Green, 1)),
            State(4, 3, 0, 4, 4, (Soda.SodaColor.Green, 1))
        };
        var edges = new List<TransferAlgorithm_version2.Edge>
        {
            new TransferAlgorithm_version2.Edge(1, 2),
            new TransferAlgorithm_version2.Edge(2, 3),
            new TransferAlgorithm_version2.Edge(3, 4)
        };
        var visited = new HashSet<string>
        {
            TransferAlgorithm_version2.BuildSignature(states)
        };

        // Mark both otherwise-valid moves on the unrelated 3-4 edge as seen.
        // Before the fix, changing boxes 1-2 made those same local moves appear
        // new globally and incorrectly validated a useless unlock on boxes 1-2.
        visited.Add(TransferAlgorithm_version2.BuildSignature(
            TransferAlgorithm_version2.ApplyForSimulation(
                states,
                new TransferAlgorithm_version2.Decision(
                    3, 4, Soda.SodaColor.Green, 1, false))));
        visited.Add(TransferAlgorithm_version2.BuildSignature(
            TransferAlgorithm_version2.ApplyForSimulation(
                states,
                new TransferAlgorithm_version2.Decision(
                    4, 3, Soda.SodaColor.Green, 1, false))));

        bool found = TransferAlgorithm_version2.TrySelectMove(
            states, edges, 4, visited, out var move);
        Require(
            !found,
            $"Unrelated progress incorrectly validated an unlock: {move}");
    }

    private static TransferAlgorithm_version2.BoxState State(
        int id,
        int column,
        int row,
        int capacity,
        long placementOrder,
        params (Soda.SodaColor color, int count)[] colors)
    {
        var state = new TransferAlgorithm_version2.BoxState
        {
            Id = id,
            Column = column,
            Row = row,
            Capacity = capacity,
            PlacementOrder = placementOrder
        };

        foreach (var pair in colors)
        {
            state.Colors[pair.color] = pair.count;
        }

        return state;
    }

    private static bool IsEdge(
        IEnumerable<TransferAlgorithm_version2.Edge> edges,
        int first,
        int second)
    {
        foreach (var edge in edges)
        {
            if ((edge.FirstId == first && edge.SecondId == second) ||
                (edge.FirstId == second && edge.SecondId == first))
            {
                return true;
            }
        }

        return false;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
