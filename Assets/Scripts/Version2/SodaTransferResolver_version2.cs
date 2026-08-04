using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Executes plans as serialized transactions. There is exactly one in-flight
/// soda, and its target slot is reserved before it leaves the source.
/// </summary>
[DisallowMultipleComponent]
public sealed class SodaTransferResolver_version2 : MonoBehaviour
{
    [SerializeField, Min(0f)] private float delayBetweenSodas = 0.05f;
    [SerializeField, Min(0f)] private float delayBetweenMoves = 0.08f;
    [SerializeField] private bool logDecisions;

    public IEnumerator Resolve(Board_version2 board, Box_version2 trigger)
    {
        if (board == null || trigger == null || trigger.IsRetired) yield break;

        int triggerId = trigger.StableId;
        List<Box_version2> component = board.GetConnectedComponent(trigger);
        var visitedStates = new HashSet<string>();

        while (true)
        {
            component.RemoveAll(box => box == null || box.IsRetired);
            if (component.Count == 0) break;

            float terminalDelay = board.RetireTerminalBoxes(component);
            if (terminalDelay > 0f)
            {
                yield return new WaitForSeconds(terminalDelay);
                component.RemoveAll(box => box == null || box.IsRetired);
                if (component.Count == 0) break;
            }

            List<TransferAlgorithm_version2.BoxState> states =
                BuildStates(component);
            List<TransferAlgorithm_version2.Edge> edges =
                BuildDirectEdges(board, component);
            string signature = TransferAlgorithm_version2.BuildSignature(states);

            if (!visitedStates.Add(signature))
            {
                Debug.LogError(
                    "V2 transfer resolver encountered a repeated state. " +
                    "Resolution stopped safely before a ping-pong loop.",
                    board);
                break;
            }

            if (!TransferAlgorithm_version2.TrySelectMove(
                    states,
                    edges,
                    triggerId,
                    visitedStates,
                    out TransferAlgorithm_version2.Decision decision))
            {
                break;
            }

            Box_version2 source =
                component.FirstOrDefault(box => box.StableId == decision.SourceId);
            Box_version2 target =
                component.FirstOrDefault(box => box.StableId == decision.TargetId);

            if (source == null || target == null ||
                !board.AreDirectlyAdjacent(source, target))
            {
                Debug.LogError(
                    $"Rejected invalid non-adjacent V2 decision: {decision}",
                    board);
                break;
            }

            if (logDecisions)
            {
                Debug.Log($"V2 transfer: {decision}", board);
            }

            bool completed = false;
            yield return ExecuteDecision(
                source,
                target,
                decision,
                value => completed = value);

            if (!completed)
            {
                Debug.LogError(
                    $"V2 transfer transaction failed safely: {decision}",
                    board);
                break;
            }

            if (delayBetweenMoves > 0f)
            {
                yield return new WaitForSeconds(delayBetweenMoves);
            }
        }

        component.RemoveAll(box => box == null || box.IsRetired);
        float finalDelay = board.RetireTerminalBoxes(component);
        if (finalDelay > 0f)
        {
            yield return new WaitForSeconds(finalDelay);
        }
    }

    private IEnumerator ExecuteDecision(
        Box_version2 source,
        Box_version2 target,
        TransferAlgorithm_version2.Decision decision,
        System.Action<bool> onFinished)
    {
        int moved = 0;

        for (int i = 0; i < decision.Amount; i++)
        {
            if (source == null || target == null ||
                source.IsRetired || target.IsRetired)
            {
                onFinished(false);
                yield break;
            }

            Soda soda = source.FindSoda(decision.Color);
            if (soda == null || !target.TryReserveEmptySlot(out int targetSlot))
            {
                onFinished(false);
                yield break;
            }

            if (!source.TryRemoveForTransfer(soda, out int sourceSlot))
            {
                target.ReleaseReservation(targetSlot);
                onFinished(false);
                yield break;
            }

            if (!target.TryAcceptReserved(soda, targetSlot))
            {
                target.ReleaseReservation(targetSlot);
                source.RollbackRemovedSoda(soda, sourceSlot);
                onFinished(false);
                yield break;
            }

            // Logical ownership has already changed atomically. Animation is
            // serialized, so no other soda can intersect or claim this slot.
            yield return target.AnimateSodaToSlot(soda, targetSlot);
            source.FinishTransfer();
            target.FinishTransfer();
            moved++;

            if (delayBetweenSodas > 0f && i + 1 < decision.Amount)
            {
                yield return new WaitForSeconds(delayBetweenSodas);
            }
        }

        onFinished(moved == decision.Amount);
    }

    private static List<TransferAlgorithm_version2.BoxState> BuildStates(
        IEnumerable<Box_version2> boxes)
    {
        var result = new List<TransferAlgorithm_version2.BoxState>();

        foreach (Box_version2 box in boxes
                     .Where(item => item != null && !item.IsRetired)
                     .OrderBy(item => item.StableId))
        {
            var state = new TransferAlgorithm_version2.BoxState
            {
                Id = box.StableId,
                Column = box.Column,
                Row = box.Row,
                Capacity = box.Capacity,
                PlacementOrder = box.PlacementOrder
            };

            foreach (var pair in box.GetColorCounts())
            {
                state.Colors.Add(pair.Key, pair.Value);
            }

            result.Add(state);
        }

        return result;
    }

    private static List<TransferAlgorithm_version2.Edge> BuildDirectEdges(
        Board_version2 board,
        IReadOnlyCollection<Box_version2> boxes)
    {
        var result = new List<TransferAlgorithm_version2.Edge>();
        var included = new HashSet<Box_version2>(
            boxes.Where(box => box != null && !box.IsRetired));

        foreach (Box_version2 box in included.OrderBy(item => item.StableId))
        {
            foreach (Box_version2 neighbour in board.GetDirectNeighbours(box))
            {
                if (neighbour == null || !included.Contains(neighbour)) continue;
                if (box.StableId < neighbour.StableId)
                {
                    result.Add(new TransferAlgorithm_version2.Edge(
                        box.StableId,
                        neighbour.StableId));
                }
            }
        }

        return result;
    }
}
