using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The authored 25-level campaign, expressed as data.
///
/// Board shapes are written as text rows so a level reads as a picture in the
/// source rather than as a coordinate list:
///   '.' playable   '#' permanent hole   'X' X blocker   'F' frozen blocker
/// The first string is the TOP row, matching how the Board inspector draws the
/// grid, so what is written here is what a designer sees in the editor.
///
/// Rail queues are generated rather than hand-listed. Supply is the one thing a
/// level cannot get wrong - an order needing 3 packed boxes needs exactly 12
/// sodas of that colour - so the counts are derived from the orders and the
/// starting board, and every colour is supplied in whole multiples of the box
/// capacity. That last rule matters even for colours no order wants: a
/// single-colour full box always packs and clears itself, so a colour delivered
/// in multiples of four can always be cleaned off the board, while a stray
/// remainder would permanently poison a cell.
/// </summary>
public static class CampaignAuthoring
{
    private const int BoxCapacity = 4;

    private const Soda.SodaColor R = Soda.SodaColor.Red;
    private const Soda.SodaColor B = Soda.SodaColor.Blue;
    private const Soda.SodaColor O = Soda.SodaColor.Orange;
    private const Soda.SodaColor Y = Soda.SodaColor.Yellow;
    private const Soda.SodaColor G = Soda.SodaColor.Green;
    private const Soda.SodaColor P = Soda.SodaColor.Purple;

    private struct Start
    {
        public int X;
        public int Y;
        public string Sodas;

        public Start(int x, int y, string sodas)
        {
            X = x;
            Y = y;
            Sodas = sodas;
        }
    }

    private struct Order
    {
        public Soda.SodaColor Color;
        public int Count;

        public Order(Soda.SodaColor color, int count)
        {
            Color = color;
            Count = count;
        }
    }

    private sealed class LevelSpec
    {
        public int Number;
        public string[] Shape;
        public Start[] Starts = new Start[0];
        public Order[] Orders = new Order[0];
        public Soda.SodaColor[] Palette = new Soda.SodaColor[0];

        /// <summary>Extra full box-sets of an ordered colour, beyond what the orders demand.</summary>
        public int SlackSets = 1;

        /// <summary>Palette colours with no order, supplied as this many full sets each.</summary>
        public Soda.SodaColor[] Distractors = new Soda.SodaColor[0];
        public int DistractorSets = 1;

        /// <summary>Highest distinct colour count allowed inside one rail box.</summary>
        public int MaxColorsPerRailBox = 1;

        public LevelDifficulty Difficulty = LevelDifficulty.Easy;
        public int Rating = 1;
        public float Seconds = 120f;
        public string Challenge = string.Empty;
        public string Notes = string.Empty;
    }

    // ------------------------------------------------------------------ table

    private static readonly string[] Open4x5 =
    {
        "....",
        "....",
        "....",
        "....",
        "...."
    };

    private static List<LevelSpec> BuildCampaign()
    {
        List<LevelSpec> levels = new List<LevelSpec>();

        // -------------------------------------------------- 1-5  core gameplay

        levels.Add(new LevelSpec
        {
            Number = 1,
            Shape = Open4x5,
            Starts = new[]
            {
                new Start(1, 4, "BB"), new Start(2, 4, "GG"),
                new Start(1, 0, "GG"), new Start(2, 0, "BB")
            },
            Orders = new[] { new Order(G, 3), new Order(B, 3) },
            Palette = new[] { G, B },
            SlackSets = 1,
            Difficulty = LevelDifficulty.Tutorial,
            Rating = 1,
            Seconds = 90f,
            Challenge = "Place a box, watch matching sodas gather, complete a colour.",
            Notes = "Unchanged from the shipped Level 1. Known-good opener."
        });

        levels.Add(new LevelSpec
        {
            Number = 2,
            Shape = Open4x5,
            Starts = new[]
            {
                new Start(0, 4, "GGG"), new Start(3, 4, "BBB"),
                new Start(1, 2, "BG")
            },
            Orders = new[] { new Order(G, 3), new Order(B, 3) },
            Palette = new[] { G, B },
            SlackSets = 1,
            Difficulty = LevelDifficulty.Easy,
            Rating = 2,
            Seconds = 120f,
            Challenge = "Two nearly-complete boxes in opposite corners, and one mixed box to unpick.",
            Notes = "Redesigned. The shipped Level 2 was a byte-identical copy of Level 1."
        });

        levels.Add(new LevelSpec
        {
            Number = 3,
            Shape = Open4x5,
            Starts = new[]
            {
                new Start(0, 4, "RR"), new Start(3, 4, "YY"),
                new Start(0, 0, "YY"), new Start(3, 0, "RR")
            },
            Orders = new[] { new Order(R, 3), new Order(Y, 3) },
            Palette = new[] { R, Y },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Easy,
            Rating = 3,
            Seconds = 150f,
            Challenge = "First mixed-colour rail boxes: a box can now carry two colours at once.",
        });

        levels.Add(new LevelSpec
        {
            Number = 4,
            Shape = Open4x5,
            Starts = new[]
            {
                new Start(1, 4, "OO"), new Start(2, 4, "PP"),
                new Start(1, 1, "PO")
            },
            Orders = new[] { new Order(O, 3), new Order(P, 3) },
            Palette = new[] { O, P },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Easy,
            Rating = 4,
            Seconds = 180f,
            Challenge = "Fewer starting anchors, so the board has to be built up before it pays out.",
        });

        levels.Add(new LevelSpec
        {
            Number = 5,
            Shape = Open4x5,
            Starts = new[]
            {
                new Start(0, 4, "RR"), new Start(3, 4, "GG"), new Start(1, 2, "BB")
            },
            Orders = new[] { new Order(R, 3), new Order(G, 3), new Order(B, 2) },
            Palette = new[] { R, G, B },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Medium,
            Rating = 5,
            Seconds = 210f,
            Challenge = "First mastery test: three orders competing for the same board space.",
        });

        // ------------------------------------------------ 6-10  X blockers

        levels.Add(new LevelSpec
        {
            Number = 6,
            Shape = new[]
            {
                "....",
                ".X..",
                "XGX.",   // the tutorial set-piece: one free neighbour, three blockers
                "....",
                "...."
            },
            Starts = new[] { new Start(1, 2, "GGG") },
            Orders = new[] { new Order(G, 2) },
            Palette = new[] { G },
            SlackSets = 1,
            Difficulty = LevelDifficulty.Tutorial,
            Rating = 2,
            Seconds = 120f,
            Challenge = "TUTORIAL: completing a box breaks the X blockers touching it.",
            Notes = "The 'G' in the shape row is the starting box cell, kept playable. " +
                    "Blockers sit north, west and east of it; only the south cell is free."
        });

        levels.Add(new LevelSpec
        {
            Number = 7,
            Shape = new[]
            {
                "....",
                ".XX.",
                "....",
                ".XX.",
                "...."
            },
            Starts = new[] { new Start(0, 4, "GG"), new Start(3, 0, "BB") },
            Orders = new[] { new Order(G, 3), new Order(B, 3) },
            Palette = new[] { G, B },
            SlackSets = 1,
            Difficulty = LevelDifficulty.Easy,
            Rating = 3,
            Seconds = 180f,
            Challenge = "Blockers split the board into lanes until they are broken open.",
        });

        levels.Add(new LevelSpec
        {
            Number = 8,
            Shape = new[]
            {
                ".XX.",
                "....",
                ".XX.",
                "....",
                ".XX."
            },
            Starts = new[] { new Start(0, 3, "YY"), new Start(3, 1, "OO") },
            Orders = new[] { new Order(Y, 3), new Order(O, 3) },
            Palette = new[] { Y, O },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Medium,
            Rating = 4,
            Seconds = 210f,
            Challenge = "Six blockers and a narrow working area: order of operations starts to matter.",
        });

        levels.Add(new LevelSpec
        {
            Number = 9,
            Shape = new[]
            {
                "X..X",
                ".XX.",
                "....",
                ".XX.",
                "X..X"
            },
            Starts = new[] { new Start(1, 2, "RR"), new Start(2, 2, "PP") },
            Orders = new[] { new Order(R, 3), new Order(P, 3) },
            Palette = new[] { R, P },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Medium,
            Rating = 5,
            Seconds = 240f,
            Challenge = "Work outward from the centre: the corners only open once the ring falls.",
        });

        levels.Add(new LevelSpec
        {
            Number = 10,
            Shape = new[]
            {
                "XX.X",
                "....",
                "X.XX",
                "....",
                "XX.X"
            },
            Starts = new[] { new Start(0, 3, "GG"), new Start(3, 1, "BB"), new Start(1, 1, "YY") },
            Orders = new[] { new Order(G, 3), new Order(B, 3), new Order(Y, 2) },
            Palette = new[] { G, B, Y },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Hard,
            Rating = 6,
            Seconds = 270f,
            Challenge = "X-blocker mastery: eight blockers, three orders, very little free space.",
        });

        // -------------------------------------------- 11-12  consolidation

        levels.Add(new LevelSpec
        {
            Number = 11,
            Shape = new[]
            {
                "#..#",
                "....",
                ".XX.",
                "....",
                "#..#"
            },
            Starts = new[] { new Start(1, 3, "OO"), new Start(2, 1, "PP") },
            Orders = new[] { new Order(O, 3), new Order(P, 3) },
            Palette = new[] { O, P },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Medium,
            Rating = 5,
            Seconds = 270f,
            Challenge = "First permanent holes. Unlike blockers, these corners never open.",
            Notes = "Teaches the hole/blocker distinction before both appear together."
        });

        levels.Add(new LevelSpec
        {
            Number = 12,
            Shape = new[]
            {
                "#.X.",
                "....",
                "X..X",
                "....",
                ".X.#"
            },
            Starts = new[] { new Start(1, 3, "RR"), new Start(2, 1, "YY"), new Start(1, 1, "GG") },
            Orders = new[] { new Order(R, 3), new Order(Y, 3), new Order(G, 2) },
            Palette = new[] { R, Y, G },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Hard,
            Rating = 6,
            Seconds = 300f,
            Challenge = "Holes and blockers together, on an asymmetric board.",
        });

        // ---------------------------------------------- 13-18  frozen blockers

        levels.Add(new LevelSpec
        {
            Number = 13,
            Shape = new[]
            {
                "....",
                "....",
                ".GF.",
                "....",
                "...."
            },
            Starts = new[] { new Start(1, 2, "GGG") },
            Orders = new[] { new Order(G, 3) },
            Palette = new[] { G },
            SlackSets = 1,
            Difficulty = LevelDifficulty.Tutorial,
            Rating = 3,
            Seconds = 150f,
            Challenge = "TUTORIAL: frozen blockers need two completions - the first only cracks.",
            Notes = "One frozen blocker east of the starting box. The tutorial makes the player " +
                    "complete twice next to it and shows the cell is still blocked after the first."
        });

        levels.Add(new LevelSpec
        {
            Number = 14,
            Shape = new[]
            {
                "....",
                ".FF.",
                "....",
                ".FF.",
                "...."
            },
            Starts = new[] { new Start(0, 4, "BB"), new Start(3, 0, "OO") },
            Orders = new[] { new Order(B, 3), new Order(O, 3) },
            Palette = new[] { B, O },
            SlackSets = 1,
            Difficulty = LevelDifficulty.Medium,
            Rating = 5,
            Seconds = 240f,
            Challenge = "Four frozen blockers. Plan to complete twice beside each one.",
        });

        levels.Add(new LevelSpec
        {
            Number = 15,
            Shape = new[]
            {
                "....",
                ".XF.",
                "....",
                ".FX.",
                "...."
            },
            Starts = new[] { new Start(0, 3, "PP"), new Start(3, 1, "YY") },
            Orders = new[] { new Order(P, 3), new Order(Y, 3) },
            Palette = new[] { P, Y },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Medium,
            Rating = 5,
            Seconds = 270f,
            Challenge = "Both blocker types side by side: one cheap to open, one expensive.",
        });

        levels.Add(new LevelSpec
        {
            Number = 16,
            Shape = new[]
            {
                "F..F",
                ".XX.",
                "....",
                ".XX.",
                "F..F"
            },
            Starts = new[] { new Start(1, 2, "RR"), new Start(2, 2, "GG") },
            Orders = new[] { new Order(R, 3), new Order(G, 3) },
            Palette = new[] { R, G },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Hard,
            Rating = 6,
            Seconds = 300f,
            Challenge = "The cheap blockers are inside, the expensive ones out at the corners.",
        });

        levels.Add(new LevelSpec
        {
            Number = 17,
            Shape = new[]
            {
                "#F.X",
                "....",
                "X..F",
                "....",
                "F.X#"
            },
            Starts = new[] { new Start(1, 3, "BB"), new Start(2, 1, "OO"), new Start(2, 3, "YY") },
            Orders = new[] { new Order(B, 3), new Order(O, 3), new Order(Y, 2) },
            Palette = new[] { B, O, Y },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Hard,
            Rating = 7,
            Seconds = 330f,
            Challenge = "Every cell type at once, with three orders to satisfy.",
        });

        levels.Add(new LevelSpec
        {
            Number = 18,
            Shape = new[]
            {
                "FX.XF",
                ".....",
                "X.F.X",
                ".....",
                "FX.XF"
            },
            Starts = new[] { new Start(2, 3, "PP"), new Start(2, 1, "GG"), new Start(0, 1, "RR") },
            Orders = new[] { new Order(P, 3), new Order(G, 3), new Order(R, 3) },
            Palette = new[] { P, G, R },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Boss,
            Rating = 8,
            Seconds = 360f,
            Challenge = "Frozen mastery: a wider board, nine blockers, five of them frozen.",
        });

        // ------------------------------------------------ 19-21  combined

        levels.Add(new LevelSpec
        {
            Number = 19,
            Shape = new[]
            {
                "#.X.#",
                ".F.F.",
                "X...X",
                ".F.F.",
                "#.X.#"
            },
            Starts = new[] { new Start(1, 4, "YY"), new Start(3, 0, "BB"), new Start(2, 2, "OO") },
            Orders = new[] { new Order(Y, 3), new Order(B, 3), new Order(O, 3) },
            Palette = new[] { Y, B, O },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Hard,
            Rating = 7,
            Seconds = 360f,
            Challenge = "A lattice board: almost every free cell touches a blocker.",
        });

        levels.Add(new LevelSpec
        {
            Number = 20,
            Shape = new[]
            {
                "..X..",
                "#.F.#",
                "X...X",
                "#.F.#",
                "..X.."
            },
            Starts = new[] { new Start(1, 2, "RR"), new Start(3, 2, "GG"), new Start(0, 4, "PP") },
            Orders = new[] { new Order(R, 3), new Order(G, 3), new Order(P, 3) },
            Palette = new[] { R, G, P },
            SlackSets = 1,
            MaxColorsPerRailBox = 3,
            Difficulty = LevelDifficulty.Hard,
            Rating = 8,
            Seconds = 390f,
            Challenge = "Three-colour rail boxes arrive for the first time on a tight board.",
        });

        levels.Add(new LevelSpec
        {
            Number = 21,
            Shape = new[]
            {
                "#X..X#",
                "..F...",
                "X....X",
                "...F..",
                "#X..X#"
            },
            Starts = new[] { new Start(1, 3, "OO"), new Start(2, 1, "YY"), new Start(1, 2, "BB") },
            Orders = new[] { new Order(O, 3), new Order(Y, 3), new Order(B, 3) },
            Palette = new[] { O, Y, B },
            SlackSets = 1,
            MaxColorsPerRailBox = 3,
            Difficulty = LevelDifficulty.Boss,
            Rating = 8,
            Seconds = 420f,
            Challenge = "A six-wide board where the useful space is a narrow cross.",
        });

        // -------------------------------------------------- 22-25  expert

        levels.Add(new LevelSpec
        {
            Number = 22,
            Shape = new[]
            {
                "#.XX.#",
                ".F..F.",
                "X....X",
                ".F..F.",
                "#.XX.#"
            },
            Starts = new[] { new Start(2, 2, "RR"), new Start(3, 2, "BB") },
            Orders = new[] { new Order(R, 3), new Order(B, 3), new Order(G, 2) },
            Palette = new[] { R, B, G },
            SlackSets = 1,
            MaxColorsPerRailBox = 3,
            Difficulty = LevelDifficulty.Boss,
            Rating = 9,
            Seconds = 420f,
            Challenge = "Only two anchors on a heavily blocked board: the opening is the whole puzzle.",
        });

        levels.Add(new LevelSpec
        {
            Number = 23,
            Shape = new[]
            {
                "X#..#X",
                ".FF...",
                "..XX..",
                "...FF.",
                "X#..#X"
            },
            Starts = new[] { new Start(0, 3, "YY"), new Start(5, 1, "PP"), new Start(3, 3, "OO") },
            Orders = new[] { new Order(Y, 3), new Order(P, 3), new Order(O, 3) },
            Palette = new[] { Y, P, O },
            SlackSets = 1,
            MaxColorsPerRailBox = 3,
            Difficulty = LevelDifficulty.Boss,
            Rating = 9,
            Seconds = 450f,
            Challenge = "Asymmetric frozen pairs: the two halves of the board open at different rates.",
        });

        levels.Add(new LevelSpec
        {
            Number = 24,
            Shape = new[]
            {
                "#XF.X#",
                "......",
                "F.XX.F",
                "......",
                "#X.FX#"
            },
            Starts = new[] { new Start(1, 3, "GG"), new Start(4, 1, "RR"), new Start(2, 1, "BB") },
            Orders = new[] { new Order(G, 3), new Order(R, 3), new Order(B, 3) },
            Palette = new[] { G, R, B },
            Distractors = new[] { Y },
            DistractorSets = 1,
            SlackSets = 1,
            MaxColorsPerRailBox = 3,
            Difficulty = LevelDifficulty.Boss,
            Rating = 10,
            Seconds = 480f,
            Challenge = "A colour no order wants shows up and must be cleared to free the space.",
            Notes = "The distractor is supplied as a whole set of four, so it can always be packed " +
                    "away - a remainder would permanently poison a cell."
        });

        levels.Add(new LevelSpec
        {
            Number = 25,
            Shape = new[]
            {
                "#XF..FX#",
                "..X..X..",
                "F......F",
                "..X..X..",
                "#XF..FX#"
            },
            Starts = new[]
            {
                new Start(3, 2, "PP"), new Start(4, 2, "OO"),
                new Start(1, 2, "YY"), new Start(6, 2, "GG")
            },
            Orders = new[] { new Order(P, 3), new Order(O, 3), new Order(Y, 3), new Order(G, 3) },
            Palette = new[] { P, O, Y, G },
            SlackSets = 1,
            MaxColorsPerRailBox = 3,
            Difficulty = LevelDifficulty.Boss,
            Rating = 10,
            Seconds = 540f,
            Challenge = "Campaign finale: eight columns, four orders, sixteen blockers.",
            Notes = "Widest board in the campaign. The four anchors sit on one row, so every " +
                    "order has to be grown outward through the blocker lattice."
        });

        return levels;
    }

    // ---------------------------------------------------------------- building

    [MenuItem("Tools/Coca Sorting/Levels/Author Campaign Definitions", priority = 90)]
    public static void AuthorCampaignDefinitions()
    {
        List<LevelSpec> specs = BuildCampaign();
        Directory.CreateDirectory(LevelSceneGenerator.DefinitionFolder);

        int created = 0;
        int updated = 0;

        foreach (LevelSpec spec in specs)
        {
            string path = $"{LevelSceneGenerator.DefinitionFolder}/{LevelNaming.GetSceneName(spec.Number)}.asset";
            LevelDefinition definition = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(definition, path);
                created++;
            }
            else
            {
                updated++;
            }

            ApplySpec(definition, spec);
            EditorUtility.SetDirty(definition);

            // Per-asset rather than a blanket SaveAssets: this project has
            // read-only package assets that a blanket save fails on, which buries
            // the real output under an unrelated error.
            AssetDatabase.SaveAssetIfDirty(definition);
        }

        AssetDatabase.Refresh();

        Debug.Log($"Campaign definitions authored: {created} created, {updated} updated, " +
                  $"{specs.Count} total in {LevelSceneGenerator.DefinitionFolder}.");
    }

    private static void ApplySpec(LevelDefinition definition, LevelSpec spec)
    {
        int height = spec.Shape.Length;
        int width = 0;
        foreach (string row in spec.Shape)
        {
            width = Mathf.Max(width, row.Length);
        }

        List<BoardCellEntry> cells = new List<BoardCellEntry>();
        for (int rowIndex = 0; rowIndex < height; rowIndex++)
        {
            // Shape rows are written top-first; board rows count up from the bottom.
            int y = height - 1 - rowIndex;
            string row = spec.Shape[rowIndex];

            for (int x = 0; x < width; x++)
            {
                char glyph = x < row.Length ? row[x] : '.';
                BoardCellKind kind;
                switch (glyph)
                {
                    case '#': kind = BoardCellKind.Removed; break;
                    case 'X': kind = BoardCellKind.Blocker; break;
                    case 'F': kind = BoardCellKind.Frozen; break;
                    default: continue;   // '.' and any starting-box marker letter
                }

                cells.Add(new BoardCellEntry(new Vector2Int(x, y), kind));
            }
        }

        List<InitialBoardBoxData> boxes = new List<InitialBoardBoxData>();
        foreach (Start start in spec.Starts)
        {
            boxes.Add(new InitialBoardBoxData
            {
                coordinate = new Vector2Int(start.X, start.Y),
                startingSodas = ParseSodas(start.Sodas)
            });
        }

        List<LevelOrderData> orders = new List<LevelOrderData>();
        foreach (Order order in spec.Orders)
        {
            orders.Add(new LevelOrderData { color = order.Color, requiredCount = order.Count });
        }

        definition.EditorSetIdentity(spec.Number);
        definition.EditorSetBoard(width, height, cells, boxes);
        definition.EditorSetOrders(orders);

        Dictionary<Soda.SodaColor, int> railNeeds = ComputeRailNeeds(spec, boxes);
        List<TutorialBoxRecipe> queue = BuildRailQueue(
            railNeeds, spec.MaxColorsPerRailBox, 7717 + spec.Number * 131);

        List<Soda.SodaColor> palette = new List<Soda.SodaColor>(spec.Palette);
        foreach (Soda.SodaColor distractor in spec.Distractors)
        {
            if (!palette.Contains(distractor))
            {
                palette.Add(distractor);
            }
        }

        definition.EditorSetRail(palette, queue, 3, RailExhaustionPolicy.RandomFallback);
        definition.EditorSetDesignNotes(
            spec.Difficulty, spec.Rating, spec.Seconds, spec.Challenge, spec.Notes);
    }

    /// <summary>
    /// Works out how many sodas of each colour the rail must deliver.
    ///
    /// Every colour is rounded up to a whole number of boxes. For an ordered
    /// colour that is what the orders literally require; for any other colour it
    /// is what makes the colour clearable at all, since a box only packs and frees
    /// its cell when it is full and single-coloured.
    /// </summary>
    private static Dictionary<Soda.SodaColor, int> ComputeRailNeeds(
        LevelSpec spec, List<InitialBoardBoxData> boxes)
    {
        Dictionary<Soda.SodaColor, int> onBoard = new Dictionary<Soda.SodaColor, int>();
        foreach (InitialBoardBoxData box in boxes)
        {
            foreach (Soda.SodaColor soda in box.startingSodas)
            {
                onBoard.TryGetValue(soda, out int existing);
                onBoard[soda] = existing + 1;
            }
        }

        Dictionary<Soda.SodaColor, int> totals = new Dictionary<Soda.SodaColor, int>();

        foreach (Order order in spec.Orders)
        {
            totals.TryGetValue(order.Color, out int existing);
            totals[order.Color] = existing + (order.Count + spec.SlackSets) * BoxCapacity;
        }

        foreach (Soda.SodaColor distractor in spec.Distractors)
        {
            totals.TryGetValue(distractor, out int existing);
            totals[distractor] = existing + spec.DistractorSets * BoxCapacity;
        }

        // Any colour that starts on the board but has no order still has to be
        // clearable, so it is topped up to a whole box.
        foreach (KeyValuePair<Soda.SodaColor, int> pair in onBoard)
        {
            if (!totals.ContainsKey(pair.Key))
            {
                totals[pair.Key] = Mathf.CeilToInt(pair.Value / (float)BoxCapacity) * BoxCapacity;
            }
        }

        Dictionary<Soda.SodaColor, int> needs = new Dictionary<Soda.SodaColor, int>();
        foreach (KeyValuePair<Soda.SodaColor, int> pair in totals)
        {
            onBoard.TryGetValue(pair.Key, out int already);
            int remaining = pair.Value - already;

            // Keep the grand total a whole number of boxes even when the starting
            // board holds an awkward count.
            if (remaining % BoxCapacity != 0)
            {
                remaining += BoxCapacity - (remaining % BoxCapacity);
            }

            if (remaining > 0)
            {
                needs[pair.Key] = remaining;
            }
        }

        return needs;
    }

    /// <summary>
    /// Chunks the required sodas into rail boxes of one to three.
    ///
    /// Never four: a rail box that arrives already full is packed on arrival and
    /// cannot be usefully dragged anywhere, which the level validator treats as an
    /// authoring error.
    /// </summary>
    private static List<TutorialBoxRecipe> BuildRailQueue(
        Dictionary<Soda.SodaColor, int> needs, int maxColorsPerBox, int seed)
    {
        System.Random random = new System.Random(seed);
        List<TutorialBoxRecipe> queue = new List<TutorialBoxRecipe>();

        Dictionary<Soda.SodaColor, int> remaining = new Dictionary<Soda.SodaColor, int>(needs);
        List<Soda.SodaColor> colors = new List<Soda.SodaColor>(remaining.Keys);
        colors.Sort((a, b) => ((int)a).CompareTo((int)b));

        while (true)
        {
            // Always drain the largest pool first, so no colour is left stranded in
            // a tail of single-soda boxes at the end of the level.
            Soda.SodaColor primary = default;
            int best = 0;
            foreach (Soda.SodaColor color in colors)
            {
                if (remaining.TryGetValue(color, out int count) && count > best)
                {
                    best = count;
                    primary = color;
                }
            }

            if (best <= 0)
            {
                break;
            }

            int boxSize = Mathf.Min(best, random.Next(2, BoxCapacity));   // 2 or 3
            List<(Soda.SodaColor, int)> amounts = new List<(Soda.SodaColor, int)>();

            if (maxColorsPerBox > 1 && boxSize >= 2 && random.Next(0, 100) < 55)
            {
                // Mixed box: split between the primary colour and another that
                // still owes sodas.
                List<Soda.SodaColor> others = new List<Soda.SodaColor>();
                foreach (Soda.SodaColor color in colors)
                {
                    if (color != primary && remaining.TryGetValue(color, out int count) && count > 0)
                    {
                        others.Add(color);
                    }
                }

                if (others.Count > 0)
                {
                    Soda.SodaColor secondary = others[random.Next(0, others.Count)];
                    int secondaryCount = 1;

                    if (maxColorsPerBox > 2 && boxSize == 3 && others.Count > 1 && random.Next(0, 100) < 40)
                    {
                        Soda.SodaColor tertiary = others[random.Next(0, others.Count)];
                        if (tertiary != secondary)
                        {
                            amounts.Add((primary, 1));
                            amounts.Add((secondary, 1));
                            amounts.Add((tertiary, 1));
                            Consume(remaining, primary, 1);
                            Consume(remaining, secondary, 1);
                            Consume(remaining, tertiary, 1);
                            queue.Add(new TutorialBoxRecipe(amounts.ToArray()));
                            continue;
                        }
                    }

                    int primaryCount = boxSize - secondaryCount;
                    amounts.Add((primary, primaryCount));
                    amounts.Add((secondary, secondaryCount));
                    Consume(remaining, primary, primaryCount);
                    Consume(remaining, secondary, secondaryCount);
                    queue.Add(new TutorialBoxRecipe(amounts.ToArray()));
                    continue;
                }
            }

            amounts.Add((primary, boxSize));
            Consume(remaining, primary, boxSize);
            queue.Add(new TutorialBoxRecipe(amounts.ToArray()));
        }

        return queue;
    }

    private static void Consume(Dictionary<Soda.SodaColor, int> pool, Soda.SodaColor color, int amount)
    {
        pool.TryGetValue(color, out int existing);
        pool[color] = Mathf.Max(0, existing - amount);
    }

    private static List<Soda.SodaColor> ParseSodas(string sodas)
    {
        List<Soda.SodaColor> result = new List<Soda.SodaColor>();
        if (string.IsNullOrEmpty(sodas))
        {
            return result;
        }

        foreach (char glyph in sodas)
        {
            switch (glyph)
            {
                case 'R': result.Add(R); break;
                case 'B': result.Add(B); break;
                case 'O': result.Add(O); break;
                case 'Y': result.Add(Y); break;
                case 'G': result.Add(G); break;
                case 'P': result.Add(P); break;
            }
        }

        return result;
    }
}
