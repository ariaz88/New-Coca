using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CocaSorting.Levels.Simulation
{
    public enum SolveOutcome
    {
        Solved,
        NotFoundWithinBudget,
        ProvenDead,
        Skipped
    }

    public readonly struct SolveMove
    {
        public readonly int RailSlot;
        public readonly int Column;
        public readonly int Row;
        public readonly string Recipe;

        public SolveMove(int railSlot, int column, int row, string recipe)
        {
            RailSlot = railSlot;
            Column = column;
            Row = row;
            Recipe = recipe;
        }

        public override string ToString() => $"rail[{RailSlot}] {Recipe} -> ({Column}, {Row})";
    }

    public sealed class SolveOptions
    {
        public int MaxNodes = 150000;
        public double MaxSeconds = 8.0;
        public int MaxDepth = -1;
        public int RandomRestarts = 4;
        public bool ReplayVerify = true;

        /// <summary>Above this, a level is winnable but its line is probably too narrow to find on a phone.</summary>
        public int NarrowSolutionNodeWarning = 50000;
    }

    public sealed class LevelSolveReport
    {
        public SolveOutcome Outcome = SolveOutcome.Skipped;
        public List<SolveMove> WinningLine = new List<SolveMove>();
        public int NodesExplored;
        public int MaxDepthReached;
        public double Seconds;
        public int BestOrdersRemaining = int.MaxValue;
        public string Message = string.Empty;

        public bool IsNarrow(SolveOptions options) =>
            Outcome == SolveOutcome.Solved && NodesExplored > options.NarrowSolutionNodeWarning;

        public string Describe(string levelName)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(levelName).Append(": ").Append(Outcome)
                   .Append(" in ").Append(NodesExplored).Append(" nodes, ")
                   .Append(Seconds.ToString("0.00")).Append("s");

            if (Outcome == SolveOutcome.Solved)
            {
                builder.Append(", ").Append(WinningLine.Count).Append(" moves");
            }
            else
            {
                builder.Append(", best left ").Append(BestOrdersRemaining).Append(" order boxes");
            }

            if (!string.IsNullOrEmpty(Message))
            {
                builder.Append(" - ").Append(Message);
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Proves a level can actually be finished from its authored material.
    ///
    /// Ordered greedy depth-first search with a node budget and seeded restarts,
    /// deliberately not IDA*: every move costs the same, the binding resource is
    /// board space rather than distance, so an admissible heuristic is close to
    /// zero and IDA* degenerates into repeated DFS with extra bookkeeping. The
    /// goal is also only to find A winning line, not the shortest one.
    ///
    /// What makes it tractable is not the search but the shape of the problem:
    /// authored levels are short, the rail is a fixed sequence so there is no
    /// branching on what arrives, and resolution is deterministic - one successor
    /// per move, no chance nodes.
    /// </summary>
    public static class LevelSolver
    {
        public static LevelSolveReport Solve(
            LevelSimState initial,
            IReadOnlyList<int[]> queue,
            SolveOptions options)
        {
            LevelSolveReport report = new LevelSolveReport();
            Stopwatch clock = Stopwatch.StartNew();

            int playableCells = 0;
            for (int index = 0; index < initial.Cells.Length; index++)
            {
                if (initial.Cells[index] != SimCell.Removed)
                {
                    playableCells++;
                }
            }

            int maxDepth = options.MaxDepth > 0
                ? options.MaxDepth
                : playableCells + queue.Count + 8;

            for (int attempt = 0; attempt <= options.RandomRestarts; attempt++)
            {
                LevelSimState root = initial.Clone();
                LevelSimulator.RefillRail(root, queue);

                HashSet<string> seen = new HashSet<string>();
                List<SolveMove> line = new List<SolveMove>();
                System.Random shuffle = attempt == 0 ? null : new System.Random(7919 * attempt);

                if (Search(root, queue, options, report, clock, seen, line, 0, maxDepth, shuffle))
                {
                    report.Outcome = SolveOutcome.Solved;
                    report.WinningLine = new List<SolveMove>(line);
                    report.Seconds = clock.Elapsed.TotalSeconds;

                    if (options.ReplayVerify && !ReplayVerify(initial, queue, report.WinningLine))
                    {
                        report.Outcome = SolveOutcome.NotFoundWithinBudget;
                        report.Message = "REPLAY MISMATCH: the recorded line did not reproduce a win.";
                    }

                    return report;
                }

                if (report.NodesExplored >= options.MaxNodes || clock.Elapsed.TotalSeconds >= options.MaxSeconds)
                {
                    break;
                }
            }

            report.Seconds = clock.Elapsed.TotalSeconds;
            report.Outcome = report.NodesExplored >= options.MaxNodes ||
                             clock.Elapsed.TotalSeconds >= options.MaxSeconds
                ? SolveOutcome.NotFoundWithinBudget
                : SolveOutcome.ProvenDead;

            if (report.Outcome == SolveOutcome.ProvenDead)
            {
                report.Message = "Search exhausted every reachable position without a win.";
            }

            return report;
        }

        private static bool Search(
            LevelSimState state,
            IReadOnlyList<int[]> queue,
            SolveOptions options,
            LevelSolveReport report,
            Stopwatch clock,
            HashSet<string> seen,
            List<SolveMove> line,
            int depth,
            int maxDepth,
            System.Random shuffle)
        {
            if (state.IsWon())
            {
                return true;
            }

            if (depth >= maxDepth ||
                report.NodesExplored >= options.MaxNodes ||
                clock.Elapsed.TotalSeconds >= options.MaxSeconds)
            {
                return false;
            }

            report.NodesExplored++;
            if (depth > report.MaxDepthReached)
            {
                report.MaxDepthReached = depth;
            }

            int remaining = 0;
            for (int color = 0; color < SimBox.ColorCount; color++)
            {
                remaining += System.Math.Max(0, state.OrdersRemaining[color]);
            }

            if (remaining < report.BestOrdersRemaining)
            {
                report.BestOrdersRemaining = remaining;
            }

            if (!seen.Add(state.BuildSignature()))
            {
                return false;
            }

            if (IsDead(state, queue))
            {
                return false;
            }

            foreach (Candidate candidate in GenerateCandidates(state, queue, shuffle))
            {
                LevelSimState next = state.Clone();
                if (!LevelSimulator.TryPlace(next, candidate.RailSlot, candidate.Column, candidate.Row, queue))
                {
                    continue;
                }

                line.Add(new SolveMove(candidate.RailSlot, candidate.Column, candidate.Row, candidate.Recipe));

                if (Search(next, queue, options, report, clock, seen, line, depth + 1, maxDepth, shuffle))
                {
                    return true;
                }

                line.RemoveAt(line.Count - 1);

                if (report.NodesExplored >= options.MaxNodes ||
                    clock.Elapsed.TotalSeconds >= options.MaxSeconds)
                {
                    return false;
                }
            }

            return false;
        }

        // ------------------------------------------------------------ pruning

        /// <summary>
        /// Admissible dead-end tests. Every one of these must be conservative: a
        /// false positive here reports a good level as unsolvable.
        /// </summary>
        private static bool IsDead(LevelSimState state, IReadOnlyList<int[]> queue)
        {
            if (state.IsBoardFull())
            {
                return true;
            }

            // Colour-supply bound. Sodas are never created, so if fewer exist than
            // the outstanding orders need, the level is over.
            //
            // Deliberately counts sodas sitting in full mixed boxes as live supply.
            // They LOOK stranded, but TransferAlgorithm.GenerateUnlockCandidates
            // exists precisely to drain a full mixed box one soda at a time, so
            // excluding them would make this bound inadmissible and the tool would
            // start declaring perfectly good levels impossible.
            for (int color = 0; color < SimBox.ColorCount; color++)
            {
                int needed = state.OrdersRemaining[color];
                if (needed <= 0)
                {
                    continue;
                }

                if (state.CountSupply(color, queue) < needed * state.BoxCapacity)
                {
                    return true;
                }
            }

            // Nothing left to place and nothing on the board can still complete.
            if (state.RailIsEmpty() && state.QueueExhausted(queue.Count))
            {
                return true;
            }

            return false;
        }

        private readonly struct Candidate
        {
            public readonly int RailSlot;
            public readonly int Column;
            public readonly int Row;
            public readonly string Recipe;
            public readonly int Score;

            public Candidate(int railSlot, int column, int row, string recipe, int score)
            {
                RailSlot = railSlot;
                Column = column;
                Row = row;
                Recipe = recipe;
                Score = score;
            }
        }

        /// <summary>
        /// Successors are (live rail slot) x (empty playable cell), ordered so the
        /// most promising placement is tried first. Move ordering, not the search
        /// algorithm, is what actually finds solutions here.
        /// </summary>
        private static List<Candidate> GenerateCandidates(
            LevelSimState state, IReadOnlyList<int[]> queue, System.Random shuffle)
        {
            List<Candidate> candidates = new List<Candidate>();

            // Rail dedup: two slots holding the same colours are the same choice.
            HashSet<string> railSeen = new HashSet<string>();
            List<int> usableSlots = new List<int>();
            for (int slot = 0; slot < state.Rail.Length; slot++)
            {
                SimBox box = state.Rail[slot];
                if (box == null)
                {
                    continue;
                }

                StringBuilder key = new StringBuilder(8);
                for (int color = 0; color < SimBox.ColorCount; color++)
                {
                    key.Append(box.Counts[color]).Append('.');
                }

                if (railSeen.Add(key.ToString()))
                {
                    usableSlots.Add(slot);
                }
            }

            // Inert-cell reduction, the single biggest win. A cell touching
            // neither a box nor a breakable blocker resolves to nothing and only
            // consumes space, so every such cell is interchangeable this move and
            // just one representative is kept.
            int inertRepresentative = -1;
            List<int> liveCells = new List<int>();

            for (int row = 0; row < state.Height; row++)
            {
                for (int column = 0; column < state.Width; column++)
                {
                    if (!state.CanPlaceAt(column, row))
                    {
                        continue;
                    }

                    int index = state.Index(column, row);
                    if (IsInteresting(state, column, row))
                    {
                        liveCells.Add(index);
                    }
                    else if (inertRepresentative < 0)
                    {
                        inertRepresentative = index;
                    }
                }
            }

            if (inertRepresentative >= 0)
            {
                liveCells.Add(inertRepresentative);
            }

            foreach (int slot in usableSlots)
            {
                SimBox box = state.Rail[slot];
                foreach (int index in liveCells)
                {
                    int column = index % state.Width;
                    int row = index / state.Width;
                    candidates.Add(new Candidate(
                        slot, column, row, box.Describe(), ScorePlacement(state, box, column, row)));
                }
            }

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            // Restarts shuffle only the top few, so a bad greedy basin can be
            // escaped without throwing the heuristic away entirely.
            if (shuffle != null && candidates.Count > 2)
            {
                int window = System.Math.Min(3, candidates.Count);
                for (int index = window - 1; index > 0; index--)
                {
                    int swap = shuffle.Next(0, index + 1);
                    (candidates[index], candidates[swap]) = (candidates[swap], candidates[index]);
                }
            }

            return candidates;
        }

        private static bool IsInteresting(LevelSimState state, int column, int row)
        {
            foreach ((int dx, int dy) in Offsets)
            {
                int nextColumn = column + dx;
                int nextRow = row + dy;
                if (!state.IsInside(nextColumn, nextRow))
                {
                    continue;
                }

                int index = state.Index(nextColumn, nextRow);
                if (state.Boxes[index] != null)
                {
                    return true;
                }

                SimCell kind = state.Cells[index];
                if (kind == SimCell.Blocker || kind == SimCell.Frozen || kind == SimCell.FrozenCracked)
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly (int dx, int dy)[] Offsets = { (0, 1), (1, 0), (0, -1), (-1, 0) };

        private static int ScorePlacement(LevelSimState state, SimBox box, int column, int row)
        {
            int score = 0;

            foreach ((int dx, int dy) in Offsets)
            {
                int nextColumn = column + dx;
                int nextRow = row + dy;
                if (!state.IsInside(nextColumn, nextRow))
                {
                    continue;
                }

                int index = state.Index(nextColumn, nextRow);
                SimBox neighbour = state.Boxes[index];

                if (neighbour != null)
                {
                    for (int color = 0; color < SimBox.ColorCount; color++)
                    {
                        if (box.Counts[color] <= 0 || neighbour.Counts[color] <= 0)
                        {
                            continue;
                        }

                        // Shared colour with a neighbour is the whole engine of the
                        // game, and a neighbour close to completing an ordered
                        // colour is worth far more than a generic match.
                        score += 40;
                        int combined = box.Counts[color] + neighbour.Counts[color];
                        if (combined >= box.Capacity)
                        {
                            score += state.OrdersRemaining[color] > 0 ? 300 : 80;
                        }
                    }
                }

                SimCell kind = state.Cells[index];
                if (kind == SimCell.Blocker || kind == SimCell.Frozen || kind == SimCell.FrozenCracked)
                {
                    score += 15;
                }
            }

            // Prefer keeping the board open.
            score += state.Boxes.Length - CountOccupied(state);
            return score;
        }

        private static int CountOccupied(LevelSimState state)
        {
            int total = 0;
            for (int index = 0; index < state.Boxes.Length; index++)
            {
                if (state.Boxes[index] != null)
                {
                    total++;
                }
            }

            return total;
        }

        // ------------------------------------------------------------- replay

        /// <summary>
        /// Replays a winning line from scratch and confirms it still wins.
        ///
        /// The transposition table keys on a canonical signature that folds
        /// placement order down to a rank, which is a lossy step: two states the
        /// table treats as equal could in principle resolve differently if a
        /// TransferAlgorithm tie-break flipped. Replaying turns that from a silent
        /// wrong answer into a caught error, for the price of one linear pass.
        /// </summary>
        private static bool ReplayVerify(
            LevelSimState initial, IReadOnlyList<int[]> queue, List<SolveMove> line)
        {
            LevelSimState state = initial.Clone();
            LevelSimulator.RefillRail(state, queue);

            foreach (SolveMove move in line)
            {
                if (!LevelSimulator.TryPlace(state, move.RailSlot, move.Column, move.Row, queue))
                {
                    return false;
                }
            }

            return state.IsWon();
        }
    }
}
