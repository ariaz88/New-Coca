using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure, deterministic transfer planner. It has no timing, animation, or scene
/// dependencies, which makes the rules testable without running coroutines.
/// </summary>
public static class TransferAlgorithm_version2
{
    public sealed class BoxState
    {
        public int Id;
        public int Column;
        public int Row;
        public int Capacity;
        public long PlacementOrder;
        public readonly Dictionary<Soda.SodaColor, int> Colors =
            new Dictionary<Soda.SodaColor, int>();

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
            var clone = new BoxState
            {
                Id = Id,
                Column = Column,
                Row = Row,
                Capacity = Capacity,
                PlacementOrder = PlacementOrder
            };

            foreach (var pair in Colors)
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

        public Decision(
            int sourceId,
            int targetId,
            Soda.SodaColor color,
            int amount,
            bool isUnlockMove)
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

    /// <summary>
    /// Finds one move. Every normal move makes measurable progress. When no
    /// normal move exists, a one-soda unlock move is allowed only if it exposes
    /// an immediate progress move from a full mixed source box.
    /// </summary>
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
        List<Candidate> progress = GenerateProgressCandidates(
            byId, edges, triggerId, visitedStates);

        if (progress.Count > 0)
        {
            decision = progress.OrderBy(candidate => candidate, CandidateComparer.Instance)
                               .First()
                               .Decision;
            return true;
        }

        List<Candidate> unlocks = GenerateUnlockCandidates(
            byId, edges, triggerId, visitedStates);

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

    public static List<BoxState> ApplyForSimulation(
        IReadOnlyList<BoxState> states,
        Decision decision)
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
        var result = new List<Candidate>();

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

        foreach (var pair in source.Colors.OrderBy(pair => pair.Key))
        {
            Soda.SodaColor color = pair.Key;
            int sourceColorCount = pair.Value;
            int targetColorCount = target.GetCount(color);

            // Core game rule: a color may move only when both adjacent boxes
            // already contain that color.
            if (sourceColorCount <= 0 || targetColorCount <= 0)
            {
                continue;
            }

            int amount = Math.Min(sourceColorCount, target.FreeSlots);
            if (amount <= 0)
            {
                continue;
            }

            Candidate candidate = CreateCandidate(
                source, target, color, amount, false, allStates, triggerId);

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
        var result = new List<Candidate>();

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

        foreach (var pair in source.Colors.OrderBy(pair => pair.Key))
        {
            Soda.SodaColor color = pair.Key;
            if (pair.Value <= 0 || target.GetCount(color) <= 0)
            {
                continue;
            }

            Candidate unlock = CreateCandidate(
                source, target, color, 1, true, allStates, triggerId);

            if (IsVisited(unlock.ResultSignature, visitedStates))
            {
                continue;
            }

            List<BoxState> simulated = ApplyForSimulation(
                allStates.Values.OrderBy(state => state.Id).ToList(),
                unlock.Decision);
            Dictionary<int, BoxState> simulatedById =
                simulated.ToDictionary(state => state.Id);

            // An unlock is legal only when it immediately enables a genuine
            // progress move. This replaces the old case-specific ping-pong code.
            List<Candidate> followUps = GenerateProgressCandidates(
                simulatedById, edges, triggerId, visitedStates);
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

            unlock.FollowUp = followUps
                .OrderBy(candidate => candidate, CandidateComparer.Instance)
                .First();
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
        bool createsBlockedMixed =
            targetTotalAfter == target.Capacity && !completesTarget;

        long before =
            (long)sourceColorBefore * sourceColorBefore +
            (long)targetColorBefore * targetColorBefore;
        long after =
            (long)sourceColorAfter * sourceColorAfter +
            (long)targetColorAfter * targetColorAfter;

        var decision = new Decision(
            source.Id, target.Id, color, amount, isUnlock);
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

        int sourceDistance =
            Math.Abs(source.Column - trigger.Column) +
            Math.Abs(source.Row - trigger.Row);
        int targetDistance =
            Math.Abs(target.Column - trigger.Column) +
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

        // Stable default priority: right, up, left, down. For chain moves,
        // coordinates provide the final deterministic ordering.
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

    private static void SetCount(
        BoxState state,
        Soda.SodaColor color,
        int count)
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
