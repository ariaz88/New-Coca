using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;

/// <summary>
/// Active transfer resolver.
/// It serializes transfers as transactions: reserve target slot, remove from
/// source, accept into target, then animate one soda. That prevents two sodas
/// from ever colliding into the same slot.
/// </summary>
[DisallowMultipleComponent]
public class UniversalSodaTransferSystem : MonoBehaviour
{
    [SerializeField, Min(0f)] private float delayBetweenSodas = 0.05f;
    [SerializeField, Min(0f)] private float delayBetweenMoves = 0.08f;
    [SerializeField] private bool logDecisions;

    private bool isTransferInProgress;

    public bool IsTransferInProgress => isTransferInProgress;

    public void ProcessAllTransfers(Box currentBox, List<Box> adjacentBoxes)
    {
        if (isTransferInProgress || Board.instance == null || currentBox == null)
        {
            return;
        }

        StartCoroutine(Resolve(Board.instance, currentBox));
    }

    public IEnumerator Resolve(Board board, Box trigger)
    {
        if (board == null || trigger == null || trigger.IsRetired || isTransferInProgress)
        {
            yield break;
        }

        isTransferInProgress = true;
        int triggerId = trigger.StableId;
        List<Box> component = board.GetConnectedComponent(trigger);
        HashSet<string> visitedStates = new HashSet<string>();

        while (true)
        {
            component.RemoveAll(box => box == null || box.IsRetired);
            if (component.Count == 0)
            {
                break;
            }

            float terminalDelay = board.RetireTerminalBoxes(component);
            if (terminalDelay > 0f)
            {
                yield return new WaitForSeconds(terminalDelay);
                component.RemoveAll(box => box == null || box.IsRetired);
                if (component.Count == 0)
                {
                    break;
                }
            }

            List<TransferAlgorithm.BoxState> states = BuildStates(component);
            List<TransferAlgorithm.Edge> edges = BuildDirectEdges(board, component);
            string signature = TransferAlgorithm.BuildSignature(states);

            if (!visitedStates.Add(signature))
            {
                Debug.LogError(
                    "Transfer resolver encountered a repeated state. Resolution stopped safely before a ping-pong loop.",
                    board);
                break;
            }

            if (!TransferAlgorithm.TrySelectMove(
                    states,
                    edges,
                    triggerId,
                    visitedStates,
                    out TransferAlgorithm.Decision decision))
            {
                break;
            }

            Box source = component.FirstOrDefault(box => box.StableId == decision.SourceId);
            Box target = component.FirstOrDefault(box => box.StableId == decision.TargetId);

            if (source == null || target == null || !board.AreDirectlyAdjacent(source, target))
            {
                Debug.LogError($"Rejected invalid non-adjacent transfer decision: {decision}", board);
                break;
            }

            if (logDecisions)
            {
                Debug.Log($"Transfer: {decision}", board);
            }

            bool completed = false;
            yield return ExecuteDecision(source, target, decision, value => completed = value);

            if (!completed)
            {
                Debug.LogError($"Transfer transaction failed safely: {decision}", board);
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

        isTransferInProgress = false;
    }

    private IEnumerator ExecuteDecision(
        Box source,
        Box target,
        TransferAlgorithm.Decision decision,
        Action<bool> onFinished)
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

    private static List<TransferAlgorithm.BoxState> BuildStates(IEnumerable<Box> boxes)
    {
        List<TransferAlgorithm.BoxState> result = new List<TransferAlgorithm.BoxState>();

        foreach (Box box in boxes
                     .Where(item => item != null && !item.IsRetired)
                     .OrderBy(item => item.StableId))
        {
            TransferAlgorithm.BoxState state = new TransferAlgorithm.BoxState
            {
                Id = box.StableId,
                Column = box.column,
                Row = box.row,
                Capacity = box.Capacity,
                PlacementOrder = box.PlacementOrder
            };

            foreach (KeyValuePair<Soda.SodaColor, int> pair in box.GetSodaColorCounts())
            {
                state.Colors.Add(pair.Key, pair.Value);
            }

            result.Add(state);
        }

        return result;
    }

    private static List<TransferAlgorithm.Edge> BuildDirectEdges(Board board, IReadOnlyCollection<Box> boxes)
    {
        List<TransferAlgorithm.Edge> result = new List<TransferAlgorithm.Edge>();
        HashSet<Box> included = new HashSet<Box>(boxes.Where(box => box != null && !box.IsRetired));

        foreach (Box box in included.OrderBy(item => item.StableId))
        {
            foreach (Box neighbour in board.GetDirectNeighbours(box))
            {
                if (neighbour == null || !included.Contains(neighbour))
                {
                    continue;
                }

                if (box.StableId < neighbour.StableId)
                {
                    result.Add(new TransferAlgorithm.Edge(box.StableId, neighbour.StableId));
                }
            }
        }

        return result;
    }
}

/// <summary>
/// Pure deterministic transfer planner. It contains no Unity timing or scene
/// references, so all matching priorities are centralized and testable.
/// </summary>
public static class TransferAlgorithm
{
    public sealed class BoxState
    {
        public int Id;
        public int Column;
        public int Row;
        public int Capacity;
        public long PlacementOrder;
        public readonly Dictionary<Soda.SodaColor, int> Colors = new Dictionary<Soda.SodaColor, int>();

        public int TotalCount => Colors.Values.Sum();
        public int FreeSlots => Capacity - TotalCount;
        public int DistinctColorCount => Colors.Count(pair => pair.Value > 0);
        public bool IsFull => TotalCount == Capacity;
        public bool IsPacked => IsFull && DistinctColorCount == 1;

        public int GetCount(Soda.SodaColor color)
        {
            return Colors.TryGetValue(color, out int count) ? count : 0;
        }

        public BoxState Clone()
        {
            BoxState clone = new BoxState
            {
                Id = Id,
                Column = Column,
                Row = Row,
                Capacity = Capacity,
                PlacementOrder = PlacementOrder
            };

            foreach (KeyValuePair<Soda.SodaColor, int> pair in Colors)
            {
                if (pair.Value > 0)
                {
                    clone.Colors.Add(pair.Key, pair.Value);
                }
            }

            return clone;
        }
    }

    public readonly struct Edge
    {
        public readonly int FirstId;
        public readonly int SecondId;

        public Edge(int firstId, int secondId)
        {
            FirstId = firstId;
            SecondId = secondId;
        }
    }

    public readonly struct Decision
    {
        public readonly int SourceId;
        public readonly int TargetId;
        public readonly Soda.SodaColor Color;
        public readonly int Amount;
        public readonly bool IsUnlockMove;

        public Decision(int sourceId, int targetId, Soda.SodaColor color, int amount, bool isUnlockMove)
        {
            SourceId = sourceId;
            TargetId = targetId;
            Color = color;
            Amount = amount;
            IsUnlockMove = isUnlockMove;
        }

        public override string ToString()
        {
            return $"{SourceId}->{TargetId}: {Amount} {Color}" +
                   (IsUnlockMove ? " (unlock)" : string.Empty);
        }
    }

    private sealed class Candidate
    {
        public Decision Decision;
        public bool CompletesTarget;
        public bool AvoidsBlockedMixedTarget;
        public bool EmptiesSource;
        public bool RemovesSourceColor;
        public long ConcentrationDelta;
        public int TargetColorAfter;
        public int TargetDistinctAfter;
        public int DistanceFromTrigger;
        public int DirectionRank;
        public bool SourceIsTrigger;
        public long TargetPlacementOrder;
        public string ResultSignature;
        public Candidate FollowUp;
    }

    public static bool TrySelectMove(
        IReadOnlyList<BoxState> states,
        IReadOnlyList<Edge> edges,
        int triggerId,
        ISet<string> visitedStates,
        out Decision decision)
    {
        decision = default;
        if (states == null || edges == null || states.Count == 0)
        {
            return false;
        }

        Dictionary<int, BoxState> byId = states.ToDictionary(state => state.Id);
        List<Candidate> progress = GenerateProgressCandidates(byId, edges, triggerId, visitedStates);

        if (progress.Count > 0)
        {
            decision = progress.OrderBy(candidate => candidate, CandidateComparer.Instance)
                               .First()
                               .Decision;
            return true;
        }

        List<Candidate> unlocks = GenerateUnlockCandidates(byId, edges, triggerId, visitedStates);
        if (unlocks.Count == 0)
        {
            return false;
        }

        decision = unlocks.OrderBy(candidate => candidate, UnlockComparer.Instance)
                          .First()
                          .Decision;
        return true;
    }

    public static string BuildSignature(IReadOnlyList<BoxState> states)
    {
        if (states == null)
        {
            return string.Empty;
        }

        return string.Join("|", states
            .OrderBy(state => state.Id)
            .Select(state =>
            {
                string colors = string.Join(",", state.Colors
                    .Where(pair => pair.Value > 0)
                    .OrderBy(pair => pair.Key)
                    .Select(pair => $"{(int)pair.Key}:{pair.Value}"));
                return $"{state.Id}[{colors}]";
            }));
    }

    public static List<BoxState> ApplyForSimulation(IReadOnlyList<BoxState> states, Decision decision)
    {
        List<BoxState> result = states.Select(state => state.Clone()).ToList();
        Dictionary<int, BoxState> byId = result.ToDictionary(state => state.Id);

        if (!byId.TryGetValue(decision.SourceId, out BoxState source) ||
            !byId.TryGetValue(decision.TargetId, out BoxState target))
        {
            return result;
        }

        int sourceCount = source.GetCount(decision.Color);
        int amount = Math.Min(decision.Amount, Math.Min(sourceCount, target.FreeSlots));
        if (amount <= 0)
        {
            return result;
        }

        SetCount(source, decision.Color, sourceCount - amount);
        SetCount(target, decision.Color, target.GetCount(decision.Color) + amount);
        return result;
    }

    private static List<Candidate> GenerateProgressCandidates(
        Dictionary<int, BoxState> byId,
        IReadOnlyList<Edge> edges,
        int triggerId,
        ISet<string> visitedStates)
    {
        List<Candidate> result = new List<Candidate>();

        foreach (Edge edge in edges)
        {
            if (!byId.TryGetValue(edge.FirstId, out BoxState first) ||
                !byId.TryGetValue(edge.SecondId, out BoxState second))
            {
                continue;
            }

            AddProgressCandidates(first, second, byId, triggerId, visitedStates, result);
            AddProgressCandidates(second, first, byId, triggerId, visitedStates, result);
        }

        return result;
    }

    private static void AddProgressCandidates(
        BoxState source,
        BoxState target,
        Dictionary<int, BoxState> allStates,
        int triggerId,
        ISet<string> visitedStates,
        List<Candidate> result)
    {
        if (source.TotalCount <= 0 || source.IsPacked || target.FreeSlots <= 0)
        {
            return;
        }

        foreach (KeyValuePair<Soda.SodaColor, int> pair in source.Colors.OrderBy(pair => pair.Key))
        {
            Soda.SodaColor color = pair.Key;
            int sourceColorCount = pair.Value;
            int targetColorCount = target.GetCount(color);

            if (sourceColorCount <= 0 || targetColorCount <= 0)
            {
                continue;
            }

            int amount = Math.Min(sourceColorCount, target.FreeSlots);
            if (amount <= 0)
            {
                continue;
            }

            Candidate candidate = CreateCandidate(source, target, color, amount, false, allStates, triggerId);
            bool makesProgress =
                candidate.CompletesTarget ||
                candidate.RemovesSourceColor ||
                candidate.ConcentrationDelta > 0;

            if (!makesProgress || IsVisited(candidate.ResultSignature, visitedStates))
            {
                continue;
            }

            result.Add(candidate);
        }
    }

    private static List<Candidate> GenerateUnlockCandidates(
        Dictionary<int, BoxState> byId,
        IReadOnlyList<Edge> edges,
        int triggerId,
        ISet<string> visitedStates)
    {
        List<Candidate> result = new List<Candidate>();

        foreach (Edge edge in edges)
        {
            if (!byId.TryGetValue(edge.FirstId, out BoxState first) ||
                !byId.TryGetValue(edge.SecondId, out BoxState second))
            {
                continue;
            }

            AddUnlockCandidates(first, second, byId, edges, triggerId, visitedStates, result);
            AddUnlockCandidates(second, first, byId, edges, triggerId, visitedStates, result);
        }

        return result;
    }

    private static void AddUnlockCandidates(
        BoxState source,
        BoxState target,
        Dictionary<int, BoxState> allStates,
        IReadOnlyList<Edge> edges,
        int triggerId,
        ISet<string> visitedStates,
        List<Candidate> result)
    {
        if (!source.IsFull || source.IsPacked || target.FreeSlots <= 0)
        {
            return;
        }

        foreach (KeyValuePair<Soda.SodaColor, int> pair in source.Colors.OrderBy(pair => pair.Key))
        {
            Soda.SodaColor color = pair.Key;
            if (pair.Value <= 0 || target.GetCount(color) <= 0)
            {
                continue;
            }

            Candidate unlock = CreateCandidate(source, target, color, 1, true, allStates, triggerId);
            if (IsVisited(unlock.ResultSignature, visitedStates))
            {
                continue;
            }

            List<BoxState> simulated = ApplyForSimulation(
                allStates.Values.OrderBy(state => state.Id).ToList(),
                unlock.Decision);
            Dictionary<int, BoxState> simulatedById = simulated.ToDictionary(state => state.Id);

            List<Candidate> followUps = GenerateProgressCandidates(simulatedById, edges, triggerId, visitedStates);
            // The unlock creates exactly one new capability: a free slot in its
            // formerly full source. A causal follow-up must fill that slot;
            // progress on any other target already existed independently and
            // must not validate this move.
            followUps.RemoveAll(candidate =>
                candidate.Decision.TargetId != unlock.Decision.SourceId);

            if (followUps.Count == 0)
            {
                continue;
            }

            unlock.FollowUp = followUps.OrderBy(candidate => candidate, CandidateComparer.Instance).First();
            result.Add(unlock);
        }
    }

    private static Candidate CreateCandidate(
        BoxState source,
        BoxState target,
        Soda.SodaColor color,
        int amount,
        bool isUnlock,
        Dictionary<int, BoxState> allStates,
        int triggerId)
    {
        int sourceColorBefore = source.GetCount(color);
        int targetColorBefore = target.GetCount(color);
        int sourceColorAfter = sourceColorBefore - amount;
        int targetColorAfter = targetColorBefore + amount;
        int targetTotalAfter = target.TotalCount + amount;
        int targetDistinctAfter = target.DistinctColorCount;

        bool completesTarget =
            targetTotalAfter == target.Capacity &&
            targetDistinctAfter == 1 &&
            targetColorAfter == target.Capacity;
        bool createsBlockedMixed = targetTotalAfter == target.Capacity && !completesTarget;

        long before = (long)sourceColorBefore * sourceColorBefore +
                      (long)targetColorBefore * targetColorBefore;
        long after = (long)sourceColorAfter * sourceColorAfter +
                     (long)targetColorAfter * targetColorAfter;

        Decision decision = new Decision(source.Id, target.Id, color, amount, isUnlock);
        List<BoxState> simulated = ApplyForSimulation(
            allStates.Values.OrderBy(state => state.Id).ToList(),
            decision);

        return new Candidate
        {
            Decision = decision,
            CompletesTarget = completesTarget,
            AvoidsBlockedMixedTarget = !createsBlockedMixed,
            EmptiesSource = source.TotalCount == amount,
            RemovesSourceColor = sourceColorAfter == 0,
            ConcentrationDelta = after - before,
            TargetColorAfter = targetColorAfter,
            TargetDistinctAfter = targetDistinctAfter,
            DistanceFromTrigger = GetDistanceFromTrigger(source, target, allStates, triggerId),
            DirectionRank = GetDirectionRank(source, target, allStates, triggerId),
            SourceIsTrigger = source.Id == triggerId,
            TargetPlacementOrder = target.PlacementOrder,
            ResultSignature = BuildSignature(simulated)
        };
    }

    private static int GetDistanceFromTrigger(
        BoxState source,
        BoxState target,
        Dictionary<int, BoxState> allStates,
        int triggerId)
    {
        if (!allStates.TryGetValue(triggerId, out BoxState trigger))
        {
            return 0;
        }

        int sourceDistance = Math.Abs(source.Column - trigger.Column) +
                             Math.Abs(source.Row - trigger.Row);
        int targetDistance = Math.Abs(target.Column - trigger.Column) +
                             Math.Abs(target.Row - trigger.Row);
        return Math.Min(sourceDistance, targetDistance);
    }

    private static int GetDirectionRank(
        BoxState source,
        BoxState target,
        Dictionary<int, BoxState> allStates,
        int triggerId)
    {
        if (!allStates.TryGetValue(triggerId, out BoxState trigger))
        {
            return CoordinateRank(target);
        }

        BoxState other = source.Id == triggerId ? target :
                         target.Id == triggerId ? source : target;
        int dx = other.Column - trigger.Column;
        int dy = other.Row - trigger.Row;

        if (dx > 0 && dy == 0) return 0;
        if (dx == 0 && dy > 0) return 1;
        if (dx < 0 && dy == 0) return 2;
        if (dx == 0 && dy < 0) return 3;
        return 4 + CoordinateRank(other);
    }

    private static int CoordinateRank(BoxState state)
    {
        unchecked
        {
            return (state.Row * 397) ^ state.Column;
        }
    }

    private static bool IsVisited(string signature, ISet<string> visitedStates)
    {
        return visitedStates != null && visitedStates.Contains(signature);
    }

    private static void SetCount(BoxState state, Soda.SodaColor color, int count)
    {
        if (count <= 0)
        {
            state.Colors.Remove(color);
        }
        else
        {
            state.Colors[color] = count;
        }
    }

    private sealed class CandidateComparer : IComparer<Candidate>
    {
        public static readonly CandidateComparer Instance = new CandidateComparer();

        public int Compare(Candidate left, Candidate right)
        {
            int result;
            if ((result = CompareDescending(left.CompletesTarget, right.CompletesTarget)) != 0) return result;
            if ((result = CompareDescending(left.AvoidsBlockedMixedTarget, right.AvoidsBlockedMixedTarget)) != 0) return result;
            if ((result = CompareDescending(left.EmptiesSource, right.EmptiesSource)) != 0) return result;
            if ((result = CompareDescending(left.RemovesSourceColor, right.RemovesSourceColor)) != 0) return result;
            if ((result = right.ConcentrationDelta.CompareTo(left.ConcentrationDelta)) != 0) return result;
            if ((result = right.TargetColorAfter.CompareTo(left.TargetColorAfter)) != 0) return result;
            if ((result = left.TargetDistinctAfter.CompareTo(right.TargetDistinctAfter)) != 0) return result;
            if ((result = left.DistanceFromTrigger.CompareTo(right.DistanceFromTrigger)) != 0) return result;
            if ((result = CompareDescending(left.SourceIsTrigger, right.SourceIsTrigger)) != 0) return result;
            if ((result = left.DirectionRank.CompareTo(right.DirectionRank)) != 0) return result;
            if ((result = left.TargetPlacementOrder.CompareTo(right.TargetPlacementOrder)) != 0) return result;
            if ((result = left.Decision.TargetId.CompareTo(right.Decision.TargetId)) != 0) return result;
            if ((result = left.Decision.SourceId.CompareTo(right.Decision.SourceId)) != 0) return result;
            return left.Decision.Color.CompareTo(right.Decision.Color);
        }
    }

    private sealed class UnlockComparer : IComparer<Candidate>
    {
        public static readonly UnlockComparer Instance = new UnlockComparer();

        public int Compare(Candidate left, Candidate right)
        {
            int followUp = CandidateComparer.Instance.Compare(left.FollowUp, right.FollowUp);
            if (followUp != 0) return followUp;

            int result;
            if ((result = left.DistanceFromTrigger.CompareTo(right.DistanceFromTrigger)) != 0) return result;
            if ((result = CompareDescending(left.SourceIsTrigger, right.SourceIsTrigger)) != 0) return result;
            if ((result = left.DirectionRank.CompareTo(right.DirectionRank)) != 0) return result;
            if ((result = left.TargetPlacementOrder.CompareTo(right.TargetPlacementOrder)) != 0) return result;
            if ((result = left.Decision.TargetId.CompareTo(right.Decision.TargetId)) != 0) return result;
            if ((result = left.Decision.SourceId.CompareTo(right.Decision.SourceId)) != 0) return result;
            return left.Decision.Color.CompareTo(right.Decision.Color);
        }
    }

    private static int CompareDescending(bool left, bool right)
    {
        return right.CompareTo(left);
    }
}

#if false
public class UniversalSodaTransferSystem_OldVersion : MonoBehaviour
{
    private bool isTransferInProgress = false;
    private const float TRANSFER_DELAY = 0.3f;
    private const float SODA_MOVE_DURATION = 0.3f;
    
    public void ProcessAllTransfers(Box currentBox, List<Box> adjacentBoxes)
    {
        if (isTransferInProgress) return;
        StartCoroutine(ExecuteUniversalTransferAlgorithm(currentBox, adjacentBoxes));
    }
    
    private IEnumerator ExecuteUniversalTransferAlgorithm(Box currentBox, List<Box> adjacentBoxes)
    {
        isTransferInProgress = true;
        
        // Continue until no more beneficial transfers can be made
        bool transferOccurred;
        int maxIterations = 10; // Prevent infinite loops
        int iterations = 0;
        
        do
        {
            transferOccurred = false;
            iterations++;
            
            // Find the single best transfer across ALL boxes
            var bestTransfer = FindBestUniversalTransfer(currentBox, adjacentBoxes);
            
            if (bestTransfer != null)
            {
                yield return ExecuteSingleTransfer(bestTransfer);
                transferOccurred = true;
                yield return new WaitForSeconds(TRANSFER_DELAY);
            }
            
        } while (transferOccurred && iterations < maxIterations);
        
        isTransferInProgress = false;
        
        // Clean up empty boxes and complete full boxes
        if (Board.instance != null)
        {
            Board.instance.RemoveEmptyBoxes();
            // CheckBoardFill is handled internally in the cleanup process
        }
    }
    
    private UniversalTransfer FindBestUniversalTransfer(Box currentBox, List<Box> adjacentBoxes)
    {
        List<UniversalTransfer> allPossibleTransfers = new List<UniversalTransfer>();
        List<Box> allBoxes = new List<Box> { currentBox };
        allBoxes.AddRange(adjacentBoxes);
        
        // Generate ALL possible transfers between ALL boxes
        for (int i = 0; i < allBoxes.Count; i++)
        {
            for (int j = 0; j < allBoxes.Count; j++)
            {
                if (i == j) continue; // Same box
                
                Box sourceBox = allBoxes[i];
                Box targetBox = allBoxes[j];
                
                if (!CanTransfer(sourceBox, targetBox)) continue;
                
                // Check each color for transfer potential
                foreach (var colorCount in sourceBox.GetSodaColorCounts())
                {
                    var transfer = EvaluateTransfer(sourceBox, targetBox, colorCount.Key);
                    if (transfer != null)
                    {
                        allPossibleTransfers.Add(transfer);
                    }
                }
            }
        }
        
        // Return the transfer with highest benefit score
        return allPossibleTransfers
            .OrderByDescending(t => t.BenefitScore)
            .FirstOrDefault();
    }
    
    private UniversalTransfer EvaluateTransfer(Box sourceBox, Box targetBox, Soda.SodaColor color)
    {
        int sourceColorCount = sourceBox.GetColorCount(color);
        int targetColorCount = targetBox.GetColorCount(color);
        int targetAvailableSpace = targetBox.GetAvailableSpaces();
        
        if (sourceColorCount == 0 || targetAvailableSpace == 0) return null;
        
        // Calculate optimal transfer amount using universal rules
        int transferAmount = CalculateOptimalAmount(sourceBox, targetBox, color);
        if (transferAmount <= 0) return null;
        
        // Calculate benefit score using universal scoring
        float benefitScore = CalculateUniversalBenefitScore(sourceBox, targetBox, color, transferAmount);
        
        return new UniversalTransfer
        {
            SourceBox = sourceBox,
            TargetBox = targetBox,
            Color = color,
            Amount = transferAmount,
            BenefitScore = benefitScore
        };
    }
    
    private int CalculateOptimalAmount(Box sourceBox, Box targetBox, Soda.SodaColor color)
    {
        int sourceCount = sourceBox.GetColorCount(color);
        int targetCount = targetBox.GetColorCount(color);
        int targetSpace = targetBox.GetAvailableSpaces();
        
        // Rule 1: Complete a box (highest priority)
        int spaceToComplete = 4 - targetCount;
        if (targetCount > 0 && spaceToComplete > 0 && sourceCount >= spaceToComplete && targetSpace >= spaceToComplete)
        {
            return spaceToComplete; // Complete the box!
        }
        
        // Rule 2: Start a new color group (only if target isn't crowded)
        if (targetCount == 0)
        {
            int targetColorDiversity = targetBox.GetSodaColorCounts().Count;
            if (targetColorDiversity >= 3) return 0; // Target too crowded
            
            // Transfer 1-2 sodas to start new group
            return Mathf.Min(sourceCount, targetSpace, 2);
        }
        
        // Rule 3: Consolidate existing color (medium priority)
        if (targetCount > 0)
        {
            // Transfer enough to balance or complete
            int idealTargetAmount = Mathf.Min(4, targetCount + 2);
            int amountNeeded = idealTargetAmount - targetCount;
            return Mathf.Min(amountNeeded, sourceCount, targetSpace);
        }
        
        return 0;
    }
    
    private float CalculateUniversalBenefitScore(Box sourceBox, Box targetBox, Soda.SodaColor color, int amount)
    {
        float score = 0f;
        
        int targetCurrentCount = targetBox.GetColorCount(color);
        int targetAfterTransfer = targetCurrentCount + amount;
        
        // MASSIVE bonus for completing a box (4 of same color)
        if (targetAfterTransfer == 4)
        {
            score += 10000f; // Highest priority
        }
        
        // Large bonus for getting close to completion
        if (targetAfterTransfer == 3)
        {
            score += 1000f;
        }
        
        // Medium bonus for consolidating existing colors
        if (targetCurrentCount > 0)
        {
            score += 100f * amount; // More transfer = better consolidation
        }
        
        // Bonus for reducing source box diversity (decluttering)
        int sourceDiversity = sourceBox.GetSodaColorCounts().Count;
        if (sourceDiversity > 2)
        {
            score += 50f * sourceDiversity; // Reduce clutter
        }
        
        // Penalty for creating too much diversity in target
        int targetDiversityAfter = targetBox.GetSodaColorCounts().Count;
        if (targetCurrentCount == 0) targetDiversityAfter++; // Will add new color
        
        if (targetDiversityAfter > 3)
        {
            score -= 200f; // Avoid crowded boxes
        }
        
        // Bonus for smart space utilization
        int targetSpaceAfter = targetBox.GetAvailableSpaces() - amount;
        if (targetSpaceAfter == 0 && targetAfterTransfer == 4)
        {
            score += 500f; // Perfect box completion
        }
        
        return score;
    }
    
    private bool CanTransfer(Box sourceBox, Box targetBox)
    {
        // Basic transfer validation
        if (sourceBox == null || targetBox == null) return false;
        if (sourceBox == targetBox) return false;
        if (sourceBox.GetSodasCount() == 0) return false;
        if (targetBox.GetAvailableSpaces() == 0) return false;
        
        // Check cooldown to prevent ping-pong (V2 enhancement)
        //BoxV2Simple sourceV2 = sourceBox.GetComponent<BoxV2Simple>();
        //BoxV2Simple targetV2 = targetBox.GetComponent<BoxV2Simple>();
        
        //if (sourceV2 != null && !sourceV2.CanParticipateInTransfer()) return false;
        //if (targetV2 != null && !targetV2.CanParticipateInTransfer()) return false;
            
        return true;
    }
    
    private IEnumerator ExecuteSingleTransfer(UniversalTransfer transfer)
    {
        Debug.Log($"Transferring {transfer.Amount} {transfer.Color} sodas from {transfer.SourceBox.name} to {transfer.TargetBox.name} (Score: {transfer.BenefitScore})");
        
        int transferred = 0;
        var sodasToTransfer = transfer.SourceBox.Sodas
            .Where(s => s.sodaColor == transfer.Color)
            .Take(transfer.Amount)
            .ToList();
        
        foreach (var soda in sodasToTransfer)
        {
            if (transfer.TargetBox.GetAvailableSpaces() <= 0) break;
            
            // Move soda
            transfer.SourceBox.RemoveSoda(soda);
            transfer.TargetBox.AddSoda(soda);
            soda.transform.parent = null;
            
            // Mark transfer times for V2 boxes (anti-ping-pong)
            //BoxV2Simple sourceV2 = transfer.SourceBox.GetComponent<BoxV2Simple>();
            //BoxV2Simple targetV2 = transfer.TargetBox.GetComponent<BoxV2Simple>();
            
            //if (sourceV2 != null) sourceV2.MarkTransferTime();
            //if (targetV2 != null) targetV2.MarkTransferTime();
            
            // Animate movement
            yield return MoveSodaWithAnimation(soda, transfer.TargetBox);
            
            // Finalize
            soda.transform.parent = transfer.TargetBox.transform;
            transfer.TargetBox.UpdateEmptyPositions();
            transfer.TargetBox.RearrangeSodas();
            
            transferred++;
            yield return new WaitForSeconds(0.1f);
        }
        
        // Add coins
        GameDataManager.instance.AddCoins(transferred * 10);
    }
    
    private IEnumerator MoveSodaWithAnimation(Soda soda, Box targetBox)
    {
        var emptyPositions = targetBox.GetEmptySodaPositions();
        if (emptyPositions.Count == 0) yield break;
        
        Vector3 startPos = soda.transform.position;
        Vector3 endPos = emptyPositions[0].position;
        Vector3 controlPoint = (startPos + endPos) / 2 + Vector3.up * 0.5f;
        
        float elapsedTime = 0;
        
        while (elapsedTime < SODA_MOVE_DURATION)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / SODA_MOVE_DURATION;
            
            soda.transform.position = CalculateParabola(startPos, endPos, controlPoint, t);
            yield return null;
        }
        
        soda.transform.position = endPos;
    }
    
    private Vector3 CalculateParabola(Vector3 start, Vector3 end, Vector3 control, float t)
    {
        return (1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end;
    }
}

[System.Serializable]
public class UniversalTransfer
{
    public Box SourceBox;
    public Box TargetBox;
    public Soda.SodaColor Color;
    public int Amount;
    public float BenefitScore;
}
#endif
