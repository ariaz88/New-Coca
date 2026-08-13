using System.Collections.Generic;

namespace CocaSorting.Levels.Simulation
{
    /// <summary>
    /// Headless mirror of the runtime placement pipeline.
    ///
    /// This is the piece that has to be right. It reproduces
    /// Board.UpdateBoxPosition -> Board.ResolvePlacement ->
    /// UniversalSodaTransferSystem.Resolve -> Board.RetireTerminalBoxes ->
    /// Board.RetireBox exactly, minus the animation waits, and it calls the REAL
    /// TransferAlgorithm rather than a reimplementation of it - so the part of
    /// the logic most likely to drift is shared rather than copied.
    ///
    /// Four details in the runtime loop are easy to miss and silently change the
    /// outcome if they are:
    ///
    ///  1. The visited-signature set handed to TrySelectMove is the SAME set the
    ///     loop uses as its ping-pong guard. It is both, and the planner reads it.
    ///  2. The signature is checked BEFORE a move is selected, and breaks.
    ///  3. RetireTerminalBoxes iterates a copy of the component and retires both
    ///     packed and empty boxes; the iteration order assigns packedMatchSequence,
    ///     which is what blocker damage keys on.
    ///  4. There is a final retire pass after the loop ends.
    /// </summary>
    public static class LevelSimulator
    {
        /// <summary>Board.DirectOffsets, in the same order - it drives BFS order and damage sequencing.</summary>
        private static readonly (int dx, int dy)[] DirectOffsets =
        {
            (0, 1), (1, 0), (0, -1), (-1, 0)
        };

        /// <summary>
        /// Takes the box in a rail slot and places it on a cell, then runs the
        /// full resolution. Returns false if the move was not legal.
        /// </summary>
        public static bool TryPlace(LevelSimState state, int railSlot, int column, int row,
            IReadOnlyList<int[]> queue)
        {
            if (railSlot < 0 || railSlot >= state.Rail.Length)
            {
                return false;
            }

            SimBox box = state.Rail[railSlot];
            if (box == null || !state.CanPlaceAt(column, row))
            {
                return false;
            }

            state.Rail[railSlot] = null;

            box.Column = column;
            box.Row = row;
            box.Id = state.NextId++;
            box.PlacementOrder = ++state.PlacementSequence;
            state.Boxes[state.Index(column, row)] = box;

            Resolve(state, box);
            RefillRail(state, queue);
            return true;
        }

        /// <summary>
        /// Mirrors SpawnContoller.LevelSpawnRoutine: the rail only ever refills
        /// once it is completely empty, which is what lets the queue be modelled
        /// as a flat cursor rather than a spawner that has to be predicted.
        /// </summary>
        public static void RefillRail(LevelSimState state, IReadOnlyList<int[]> queue)
        {
            if (!state.RailIsEmpty())
            {
                return;
            }

            for (int slot = 0; slot < state.Rail.Length && state.RailCursor < queue.Count; slot++)
            {
                int[] recipe = queue[state.RailCursor++];
                SimBox box = new SimBox { Capacity = state.BoxCapacity };
                for (int color = 0; color < SimBox.ColorCount; color++)
                {
                    box.Add(color, recipe[color]);
                }

                // SpawnContoller.DrawFromQueue skips malformed empty recipes
                // rather than shipping a box that retires on placement.
                if (box.Total <= 0)
                {
                    slot--;
                    continue;
                }

                state.Rail[slot] = box;
            }
        }

        // ------------------------------------------------------------ resolve

        private static void Resolve(LevelSimState state, SimBox trigger)
        {
            int triggerId = trigger.Id;
            List<SimBox> component = GetConnectedComponent(state, trigger);
            HashSet<string> visited = new HashSet<string>();

            while (true)
            {
                component.RemoveAll(box => box == null || box.IsRetired);
                if (component.Count == 0)
                {
                    break;
                }

                RetireTerminal(state, component);

                component.RemoveAll(box => box == null || box.IsRetired);
                if (component.Count == 0)
                {
                    break;
                }

                List<TransferAlgorithm.BoxState> states = BuildStates(component);
                List<TransferAlgorithm.Edge> edges = BuildDirectEdges(state, component);
                string signature = TransferAlgorithm.BuildSignature(states);

                // Checked before selection, and breaks - same as the runtime.
                if (!visited.Add(signature))
                {
                    break;
                }

                if (!TransferAlgorithm.TrySelectMove(
                        states, edges, triggerId, visited, out TransferAlgorithm.Decision decision))
                {
                    break;
                }

                SimBox source = component.Find(box => box.Id == decision.SourceId);
                SimBox target = component.Find(box => box.Id == decision.TargetId);
                if (source == null || target == null || !AreAdjacent(source, target))
                {
                    break;
                }

                int color = (int)decision.Color;
                int amount = System.Math.Min(decision.Amount,
                    System.Math.Min(source.Counts[color], target.FreeSlots));
                if (amount <= 0)
                {
                    break;
                }

                source.Remove(color, amount);
                target.Add(color, amount);
            }

            component.RemoveAll(box => box == null || box.IsRetired);
            RetireTerminal(state, component);
        }

        /// <summary>
        /// Mirrors Board.RetireTerminalBoxes: iterate a COPY, retire every box
        /// that is packed or empty. Order matters because each packed box takes
        /// the next packedMatchSequence, and blocker damage is keyed on it.
        /// </summary>
        private static void RetireTerminal(LevelSimState state, List<SimBox> component)
        {
            if (component.Count == 0)
            {
                return;
            }

            SimBox[] snapshot = component.ToArray();
            foreach (SimBox box in snapshot)
            {
                if (box == null || box.IsRetired)
                {
                    continue;
                }

                bool packed = box.IsPacked;
                if (!packed && !box.IsEmpty)
                {
                    continue;
                }

                RetireBox(state, box, packed);
            }
        }

        private static void RetireBox(LevelSimState state, SimBox box, bool packed)
        {
            box.IsRetired = true;

            int index = state.Index(box.Column, box.Row);
            if (state.Boxes[index] == box)
            {
                state.Boxes[index] = null;
            }

            if (!packed)
            {
                return;
            }

            // Sequence first, then damage - Board.RetireBox does the same, and the
            // order is what makes the one-hit-per-match guard work at all.
            long damageSequence = ++state.PackedSequence;
            ApplyPackedMatchDamage(state, box.Column, box.Row, damageSequence);

            int color = box.SoleColor();
            if (color >= 0 && state.OrdersRemaining[color] > 0)
            {
                state.OrdersRemaining[color]--;
            }
        }

        /// <summary>
        /// Mirrors Board.ApplyPackedMatchDamage. Instant here - the runtime plays
        /// a break animation, but the end state is identical.
        /// </summary>
        private static void ApplyPackedMatchDamage(LevelSimState state, int column, int row, long sequence)
        {
            foreach ((int dx, int dy) in DirectOffsets)
            {
                int nextColumn = column + dx;
                int nextRow = row + dy;
                if (!state.IsInside(nextColumn, nextRow))
                {
                    continue;
                }

                int index = state.Index(nextColumn, nextRow);
                SimCell kind = state.Cells[index];

                bool breakable = kind == SimCell.Blocker ||
                                 kind == SimCell.Frozen ||
                                 kind == SimCell.FrozenCracked;
                if (!breakable || state.LastDamage[index] == sequence)
                {
                    continue;
                }

                state.LastDamage[index] = sequence;

                switch (kind)
                {
                    case SimCell.Blocker:
                        state.Cells[index] = SimCell.Playable;
                        OnBlockOpened(state);
                        break;
                    case SimCell.Frozen:
                        // Cracked only. A half-broken blocker has not opened, so it
                        // must not advance a "open N locked blocks" order - the same
                        // rule Board applies by reporting only from UnlockBlockedCell.
                        state.Cells[index] = SimCell.FrozenCracked;
                        break;
                    case SimCell.FrozenCracked:
                        state.Cells[index] = SimCell.Playable;
                        OnBlockOpened(state);
                        break;
                }
            }
        }

        private static void OnBlockOpened(LevelSimState state)
        {
            if (state.BlocksOrderRemaining > 0)
            {
                state.BlocksOrderRemaining--;
            }
        }

        // ------------------------------------------------------------ helpers

        /// <summary>BFS in DirectOffsets order, matching Board.GetConnectedComponent.</summary>
        private static List<SimBox> GetConnectedComponent(LevelSimState state, SimBox origin)
        {
            List<SimBox> result = new List<SimBox>();
            if (origin == null || origin.IsRetired)
            {
                return result;
            }

            HashSet<int> visited = new HashSet<int> { origin.Id };
            Queue<SimBox> queue = new Queue<SimBox>();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                SimBox current = queue.Dequeue();
                result.Add(current);

                foreach ((int dx, int dy) in DirectOffsets)
                {
                    int nextColumn = current.Column + dx;
                    int nextRow = current.Row + dy;
                    if (!state.IsPlayable(nextColumn, nextRow))
                    {
                        continue;
                    }

                    SimBox neighbour = state.Boxes[state.Index(nextColumn, nextRow)];
                    if (neighbour != null && !neighbour.IsRetired && visited.Add(neighbour.Id))
                    {
                        queue.Enqueue(neighbour);
                    }
                }
            }

            return result;
        }

        private static bool AreAdjacent(SimBox first, SimBox second)
        {
            return System.Math.Abs(first.Column - second.Column) +
                   System.Math.Abs(first.Row - second.Row) == 1;
        }

        /// <summary>Ordered by Id, matching UniversalSodaTransferSystem.BuildStates.</summary>
        private static List<TransferAlgorithm.BoxState> BuildStates(List<SimBox> component)
        {
            List<SimBox> ordered = new List<SimBox>(component);
            ordered.Sort((a, b) => a.Id.CompareTo(b.Id));

            List<TransferAlgorithm.BoxState> result = new List<TransferAlgorithm.BoxState>(ordered.Count);
            foreach (SimBox box in ordered)
            {
                if (box == null || box.IsRetired)
                {
                    continue;
                }

                TransferAlgorithm.BoxState boxState = new TransferAlgorithm.BoxState
                {
                    Id = box.Id,
                    Column = box.Column,
                    Row = box.Row,
                    Capacity = box.Capacity,
                    PlacementOrder = box.PlacementOrder
                };

                for (int color = 0; color < SimBox.ColorCount; color++)
                {
                    if (box.Counts[color] > 0)
                    {
                        boxState.Colors.Add((Soda.SodaColor)color, box.Counts[color]);
                    }
                }

                result.Add(boxState);
            }

            return result;
        }

        /// <summary>Ordered by Id, each pair emitted once with lower Id first - matching BuildDirectEdges.</summary>
        private static List<TransferAlgorithm.Edge> BuildDirectEdges(LevelSimState state, List<SimBox> component)
        {
            HashSet<int> included = new HashSet<int>();
            foreach (SimBox box in component)
            {
                if (box != null && !box.IsRetired)
                {
                    included.Add(box.Id);
                }
            }

            List<SimBox> ordered = new List<SimBox>(component);
            ordered.Sort((a, b) => a.Id.CompareTo(b.Id));

            List<TransferAlgorithm.Edge> result = new List<TransferAlgorithm.Edge>();
            foreach (SimBox box in ordered)
            {
                if (box == null || box.IsRetired)
                {
                    continue;
                }

                foreach ((int dx, int dy) in DirectOffsets)
                {
                    int nextColumn = box.Column + dx;
                    int nextRow = box.Row + dy;
                    if (!state.IsPlayable(nextColumn, nextRow))
                    {
                        continue;
                    }

                    SimBox neighbour = state.Boxes[state.Index(nextColumn, nextRow)];
                    if (neighbour == null || neighbour.IsRetired || !included.Contains(neighbour.Id))
                    {
                        continue;
                    }

                    if (box.Id < neighbour.Id)
                    {
                        result.Add(new TransferAlgorithm.Edge(box.Id, neighbour.Id));
                    }
                }
            }

            return result;
        }
    }
}
