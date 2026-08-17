using System.Collections.Generic;
using System.Text;

namespace CocaSorting.Levels.Simulation
{
    /// <summary>Cell state in the simulation. Mirrors BoardCellKind plus the cracked frozen step.</summary>
    public enum SimCell : byte
    {
        Playable = 0,
        Removed = 1,
        Blocker = 2,
        Frozen = 3,
        FrozenCracked = 4
    }

    /// <summary>
    /// One box in the simulation.
    ///
    /// Colour counts live in a fixed six-slot array rather than a dictionary
    /// because the solver clones this structure millions of times; a Dictionary
    /// per box would dominate both allocation and compare cost.
    /// </summary>
    public sealed class SimBox
    {
        // Must equal Soda.SpawnableColors.Length. Deliberately NOT the enum's
        // length: Soda.SodaColor carries an unspawnable Yellow for the new Coke
        // Pack art, and every colour here is used as a raw array index, so a
        // seventh member would be an out-of-range crash in the solver rather
        // than a wider board. If a colour is ever made spawnable, raise this.
        public const int ColorCount = 6;

        public int Id;
        public int Column;
        public int Row;
        public int Capacity;
        public long PlacementOrder;
        public bool IsRetired;

        public readonly int[] Counts = new int[ColorCount];
        public int Total;
        public int Distinct;

        public bool IsPacked => Total == Capacity && Distinct == 1;
        public bool IsEmpty => Total == 0;
        public int FreeSlots => Capacity - Total;

        public void Add(int color, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (Counts[color] == 0)
            {
                Distinct++;
            }

            Counts[color] += amount;
            Total += amount;
        }

        public void Remove(int color, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Counts[color] -= amount;
            Total -= amount;
            if (Counts[color] == 0)
            {
                Distinct--;
            }
        }

        /// <summary>The single colour of a packed box, or -1.</summary>
        public int SoleColor()
        {
            if (Distinct != 1)
            {
                return -1;
            }

            for (int color = 0; color < ColorCount; color++)
            {
                if (Counts[color] > 0)
                {
                    return color;
                }
            }

            return -1;
        }

        public SimBox Clone()
        {
            SimBox clone = new SimBox
            {
                Id = Id,
                Column = Column,
                Row = Row,
                Capacity = Capacity,
                PlacementOrder = PlacementOrder,
                IsRetired = IsRetired,
                Total = Total,
                Distinct = Distinct
            };

            System.Array.Copy(Counts, clone.Counts, ColorCount);
            return clone;
        }

        public string Describe()
        {
            StringBuilder builder = new StringBuilder();
            for (int color = 0; color < ColorCount; color++)
            {
                if (Counts[color] > 0)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append('+');
                    }

                    builder.Append((Soda.SodaColor)color).Append('x').Append(Counts[color]);
                }
            }

            return builder.Length > 0 ? builder.ToString() : "(empty)";
        }
    }

    /// <summary>
    /// A complete snapshot of a level mid-play: the board, the rail, the queue
    /// cursor and the outstanding orders.
    ///
    /// This is the search node. Everything the resolver can read has to be in
    /// here, and nothing that cannot affect the outcome should be, or the
    /// transposition table stops collapsing equivalent positions.
    /// </summary>
    public sealed class LevelSimState
    {
        public int Width;
        public int Height;
        public int BoxCapacity = 4;

        public SimCell[] Cells;          // length Width*Height
        public SimBox[] Boxes;           // length Width*Height, null == empty cell

        /// <summary>Live rail slots. A null slot has been consumed.</summary>
        public SimBox[] Rail;
        public int RailCursor;

        public int[] OrdersRemaining = new int[SimBox.ColorCount];

        /// <summary>Locked blocks the level still has to have opened. 0 when unused.</summary>
        public int BlocksOrderRemaining;

        public long PlacementSequence;
        public long PackedSequence;
        public int NextId = 1;

        /// <summary>Damage id last applied to each cell, mirroring BlockerRuntime.LastDamageSequence.</summary>
        public long[] LastDamage;

        public int Index(int column, int row) => row * Width + column;
        public bool IsInside(int column, int row) =>
            column >= 0 && column < Width && row >= 0 && row < Height;

        /// <summary>True when a box can be dropped here: inside, not blocked, and empty.</summary>
        public bool CanPlaceAt(int column, int row)
        {
            if (!IsInside(column, row))
            {
                return false;
            }

            int index = Index(column, row);
            return Cells[index] == SimCell.Playable && Boxes[index] == null;
        }

        public bool IsPlayable(int column, int row) =>
            IsInside(column, row) && Cells[Index(column, row)] == SimCell.Playable;

        public bool IsWon()
        {
            if (BlocksOrderRemaining > 0)
            {
                return false;
            }

            for (int color = 0; color < SimBox.ColorCount; color++)
            {
                if (OrdersRemaining[color] > 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Breakable blockers still standing, and the hits they collectively need.</summary>
        public void CountBreakableBlockers(out int count, out int hitsNeeded)
        {
            count = 0;
            hitsNeeded = 0;

            for (int index = 0; index < Cells.Length; index++)
            {
                switch (Cells[index])
                {
                    case SimCell.Blocker:
                        count++;
                        hitsNeeded += 1;
                        break;
                    case SimCell.Frozen:
                        count++;
                        hitsNeeded += 2;
                        break;
                    case SimCell.FrozenCracked:
                        count++;
                        hitsNeeded += 1;
                        break;
                }
            }
        }

        /// <summary>Mirrors Board.CheckBoardFill: every playable cell holds a box.</summary>
        public bool IsBoardFull()
        {
            bool foundPlayable = false;
            for (int index = 0; index < Cells.Length; index++)
            {
                if (Cells[index] != SimCell.Playable)
                {
                    continue;
                }

                foundPlayable = true;
                if (Boxes[index] == null)
                {
                    return false;
                }
            }

            return foundPlayable;
        }

        public bool RailIsEmpty()
        {
            for (int slot = 0; slot < Rail.Length; slot++)
            {
                if (Rail[slot] != null)
                {
                    return false;
                }
            }

            return true;
        }

        public bool QueueExhausted(int queueLength) => RailCursor >= queueLength;

        /// <summary>Sodas of a colour still reachable: on the board, on the rail, and still queued.</summary>
        public int CountSupply(int color, IReadOnlyList<int[]> queue)
        {
            int total = 0;

            for (int index = 0; index < Boxes.Length; index++)
            {
                if (Boxes[index] != null)
                {
                    total += Boxes[index].Counts[color];
                }
            }

            for (int slot = 0; slot < Rail.Length; slot++)
            {
                if (Rail[slot] != null)
                {
                    total += Rail[slot].Counts[color];
                }
            }

            for (int index = RailCursor; index < queue.Count; index++)
            {
                total += queue[index][color];
            }

            return total;
        }

        public LevelSimState Clone()
        {
            LevelSimState clone = new LevelSimState
            {
                Width = Width,
                Height = Height,
                BoxCapacity = BoxCapacity,
                Cells = (SimCell[])Cells.Clone(),
                Boxes = new SimBox[Boxes.Length],
                Rail = new SimBox[Rail.Length],
                RailCursor = RailCursor,
                OrdersRemaining = (int[])OrdersRemaining.Clone(),
                BlocksOrderRemaining = BlocksOrderRemaining,
                PlacementSequence = PlacementSequence,
                PackedSequence = PackedSequence,
                NextId = NextId,
                LastDamage = (long[])LastDamage.Clone()
            };

            for (int index = 0; index < Boxes.Length; index++)
            {
                clone.Boxes[index] = Boxes[index]?.Clone();
            }

            for (int slot = 0; slot < Rail.Length; slot++)
            {
                clone.Rail[slot] = Rail[slot]?.Clone();
            }

            return clone;
        }

        /// <summary>
        /// Canonical key for the transposition table.
        ///
        /// Placement order is folded in as a RANK, not as the raw counter. The
        /// raw value grows without bound, so including it would make every state
        /// unique and defeat the table entirely - but it cannot simply be dropped,
        /// because TransferAlgorithm's tie-breakers compare placement order, so
        /// two positions differing only in relative age can resolve differently.
        /// The rank is exactly the information the comparer actually consumes.
        /// </summary>
        public string BuildSignature()
        {
            List<SimBox> occupied = new List<SimBox>();
            for (int index = 0; index < Boxes.Length; index++)
            {
                if (Boxes[index] != null)
                {
                    occupied.Add(Boxes[index]);
                }
            }

            List<SimBox> byAge = new List<SimBox>(occupied);
            byAge.Sort((a, b) => a.PlacementOrder.CompareTo(b.PlacementOrder));
            Dictionary<int, int> rank = new Dictionary<int, int>(byAge.Count);
            for (int position = 0; position < byAge.Count; position++)
            {
                rank[byAge[position].Id] = position;
            }

            StringBuilder builder = new StringBuilder(128);

            for (int index = 0; index < Cells.Length; index++)
            {
                builder.Append((int)Cells[index]);
            }

            builder.Append('|');
            for (int index = 0; index < Boxes.Length; index++)
            {
                SimBox box = Boxes[index];
                if (box == null)
                {
                    builder.Append('.');
                    continue;
                }

                builder.Append(index).Append(':');
                for (int color = 0; color < SimBox.ColorCount; color++)
                {
                    builder.Append(box.Counts[color]);
                }

                builder.Append('@').Append(rank[box.Id]).Append(';');
            }

            // Rail slots are sorted: two rail arrangements holding the same boxes
            // are the same position, because the player may take any slot.
            builder.Append('|');
            List<string> railKeys = new List<string>();
            for (int slot = 0; slot < Rail.Length; slot++)
            {
                if (Rail[slot] == null)
                {
                    continue;
                }

                StringBuilder key = new StringBuilder(8);
                for (int color = 0; color < SimBox.ColorCount; color++)
                {
                    key.Append(Rail[slot].Counts[color]);
                }

                railKeys.Add(key.ToString());
            }

            railKeys.Sort(System.StringComparer.Ordinal);
            builder.Append(string.Join(",", railKeys));

            builder.Append('|').Append(RailCursor).Append('|');
            for (int color = 0; color < SimBox.ColorCount; color++)
            {
                builder.Append(OrdersRemaining[color]).Append('.');
            }

            builder.Append('|').Append(BlocksOrderRemaining);

            return builder.ToString();
        }
    }
}
