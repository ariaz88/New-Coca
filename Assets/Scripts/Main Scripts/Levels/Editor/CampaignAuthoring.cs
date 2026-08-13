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
/// Starting-box contents use one letter per soda:
///   R red   B blue   O orange   K pink   G green   P purple
/// Pink is K because P is already purple.
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
    private const Soda.SodaColor K = Soda.SodaColor.Pink;
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

        /// <summary>Locked blocks the level asks the player to open. 0 = no such order.</summary>
        public int BlocksOrder;

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
                new Start(0, 4, "RR"), new Start(3, 4, "KK"),
                new Start(0, 0, "KK"), new Start(3, 0, "RR")
            },
            Orders = new[] { new Order(R, 3), new Order(K, 3) },
            Palette = new[] { R, K },
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
            Starts = new[] { new Start(0, 3, "KK"), new Start(3, 1, "OO") },
            Orders = new[] { new Order(K, 3), new Order(O, 3) },
            Palette = new[] { K, O },
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
            Starts = new[] { new Start(0, 3, "GG"), new Start(3, 1, "BB"), new Start(1, 1, "KK") },
            Orders = new[] { new Order(G, 3), new Order(B, 3), new Order(K, 2) },
            Palette = new[] { G, B, K },
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

            // From here on the campaign mixes goal types: one of the drink orders
            // is replaced by "open N locked blocks", so clearing the board is a
            // stated objective rather than only a means to free space.
            Orders = new[] { new Order(O, 3) },
            BlocksOrder = 2,
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
            Starts = new[] { new Start(1, 3, "RR"), new Start(2, 1, "KK"), new Start(1, 1, "GG") },
            Orders = new[] { new Order(R, 3), new Order(K, 3) },
            BlocksOrder = 2,
            Palette = new[] { R, K, G },
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

            // Two drink colours from here on. A card of one drink plus one block
            // order let the player treat the whole board as a single pipeline;
            // a second colour means the two goals compete for the same cells.
            Orders = new[] { new Order(B, 3), new Order(O, 3) },
            BlocksOrder = 2,
            Palette = new[] { B, O },
            SlackSets = 1,
            Difficulty = LevelDifficulty.Medium,
            Rating = 6,
            Seconds = 300f,
            Challenge = "Four frozen blockers, and two drinks competing for the space between them.",
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
            Starts = new[] { new Start(0, 3, "PP"), new Start(3, 1, "KK") },
            Orders = new[] { new Order(P, 3), new Order(K, 3) },
            BlocksOrder = 2,
            Palette = new[] { P, K },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Hard,
            Rating = 6,
            Seconds = 330f,
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
            BlocksOrder = 2,
            Palette = new[] { R, G },
            SlackSets = 1,
            MaxColorsPerRailBox = 2,
            Difficulty = LevelDifficulty.Hard,
            Rating = 7,
            Seconds = 360f,
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
            Starts = new[] { new Start(1, 3, "BB"), new Start(2, 1, "OO"), new Start(2, 3, "KK") },
            Orders = new[] { new Order(B, 3), new Order(O, 3) },
            BlocksOrder = 2,
            Palette = new[] { B, O, K },
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
                "FX.X",
                "....",
                "X.F.",
                "....",
                "FX.X"
            },
            Starts = new[] { new Start(2, 3, "PP"), new Start(2, 1, "GG"), new Start(0, 1, "RR") },
            Orders = new[] { new Order(P, 3), new Order(G, 3) },
            BlocksOrder = 3,
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
            // The first draft of this board was a true checkerboard lattice, and
            // the solver proved it unplayable: 8 of its 13 open cells had no open
            // neighbour at all, and two of the starting boxes sat on such cells,
            // so they could never receive a soda or grow toward a match. The
            // vertical blocker pairs are now single blockers, which keeps the
            // woven look while leaving every open cell connected to the rest.
            Shape = new[]
            {
                "#.X.",
                "..F.",
                "X...",
                "..F.",
                "#.X."
            },
            Starts = new[] { new Start(1, 3, "KK"), new Start(3, 1, "BB"), new Start(2, 2, "OO") },
            Orders = new[] { new Order(K, 3), new Order(B, 3) },
            BlocksOrder = 3,
            Palette = new[] { K, B, O },
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
                ".X..",
                "#.F.",
                "..X.",
                "#.F.",
                ".X.."
            },
            Starts = new[] { new Start(1, 2, "RR"), new Start(3, 2, "GG"), new Start(2, 4, "PP") },
            Orders = new[] { new Order(R, 3), new Order(G, 3) },
            BlocksOrder = 3,
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
                "#X.X",
                "..F.",
                "X...",
                ".F..",
                "#X.X"
            },
            Starts = new[] { new Start(1, 3, "OO"), new Start(2, 1, "KK"), new Start(1, 2, "BB") },
            Orders = new[] { new Order(O, 3), new Order(K, 3) },
            BlocksOrder = 3,
            Palette = new[] { O, K, B },
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
            // The corner holes were removed and a third anchor added: with only two
            // anchors on a board this blocked, the solver could reach 7 of the 8
            // required packs and no further. The blocker pattern is unchanged, so
            // the level still reads as the tightest board so far.
            Shape = new[]
            {
                ".XX.",
                "....",
                "FX.F",
                "....",
                ".XX."
            },
            Starts = new[] { new Start(2, 2, "RR"), new Start(3, 1, "BB"), new Start(2, 3, "GG") },
            Orders = new[] { new Order(R, 3), new Order(B, 3) },
            BlocksOrder = 3,
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
                "X#.X",
                ".FF.",
                "....",
                ".FF.",
                "X#.X"
            },
            Starts = new[] { new Start(0, 3, "KK"), new Start(3, 1, "PP"), new Start(3, 3, "OO") },
            Orders = new[] { new Order(K, 3), new Order(P, 3) },
            BlocksOrder = 4,
            Palette = new[] { K, P, O },
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
                "#XF.",
                "....",
                "F.XX",
                "....",
                "#X.F"
            },
            Starts = new[] { new Start(1, 3, "GG"), new Start(3, 1, "RR"), new Start(2, 1, "BB") },
            Orders = new[] { new Order(G, 3), new Order(R, 3) },
            BlocksOrder = 4,
            Palette = new[] { G, R, B },
            Distractors = new[] { K },
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
                "#XF.",
                "....",
                "F..F",
                "....",
                ".FX#"
            },
            Starts = new[]
            {
                new Start(0, 3, "KK"), new Start(3, 3, "OO"), new Start(1, 2, "PP")
            },

            // Two drink orders rather than four. On a 4x5 board the finale only has
            // twelve open cells to work in, and four simultaneous goals left no room
            // to manoeuvre at all - the difficulty came from having nowhere to put
            // anything, which is not the same as being hard.
            Orders = new[] { new Order(P, 3), new Order(O, 3) },

            // Three blocks, not four. The finale has only twelve playable cells and
            // now carries all six drinks; with four blocks on top of two drink
            // orders the solver stalled one goal short however much slack it was
            // given, and a level that needs eight packs on twelve cells is not
            // hard so much as airless.
            BlocksOrder = 3,
            Palette = new[] { P, O, K, G },
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

        // Placed last so the crate always sits at the right of the card, which
        // keeps the drinks grouped together across the whole campaign.
        if (spec.BlocksOrder > 0)
        {
            orders.Add(LevelOrderData.Blocks(spec.BlocksOrder));
        }

        definition.EditorSetIdentity(spec.Number);
        definition.EditorSetBoard(width, height, cells, boxes);
        definition.EditorSetOrders(orders);

        // The rail carries more colours than the orders need from level 6 on, so
        // the player has to work around drinks that cannot advance anything.
        List<Soda.SodaColor> distractors = ResolveDistractors(spec);
        int maxColorsPerBox = Mathf.Max(spec.MaxColorsPerRailBox, GetMaxColorsPerRailBox(spec.Number));

        Dictionary<Soda.SodaColor, int> railNeeds = ComputeRailNeeds(spec, boxes, distractors);
        List<TutorialBoxRecipe> queue = BuildRailQueue(
            railNeeds, maxColorsPerBox, 7717 + spec.Number * 131, OrderColors(spec), spec.Number);

        List<Soda.SodaColor> palette = new List<Soda.SodaColor>(spec.Palette);
        foreach (Soda.SodaColor distractor in distractors)
        {
            if (!palette.Contains(distractor))
            {
                palette.Add(distractor);
            }
        }

        definition.EditorSetRail(palette, queue, 3, RailExhaustionPolicy.RandomFallback);
        int legalBombCells = BombLayoutGenerator.GetLegalCells(width, height, cells, boxes).Count;
        definition.EditorSetBombs(GetBombSettings(spec.Number, legalBombCells));
        definition.EditorSetDesignNotes(
            spec.Difficulty, spec.Rating, spec.Seconds, spec.Challenge, spec.Notes);
    }

    /// <summary>
    /// The Hidden Bombs curve.
    ///
    /// Bombs start at level 10, once the player has met every cell type and has
    /// had four levels of X blockers - a hidden hazard is only a memory test if
    /// the visible board is already understood, otherwise it is just noise.
    ///
    /// The three dials move in opposite directions on purpose: bomb count climbs
    /// while preview length, scanner charges and defusers fall, so the same board
    /// asks for more memory and offers less insurance as the campaign goes on.
    /// Immediate mode - where a single wrong drop ends the run - is held back to
    /// the last four levels, and those get the most defusers per bomb to match.
    /// </summary>
    private static BombLevelSettings GetBombSettings(int levelNumber, int legalBombCells)
    {
        // 13 is the frozen-blocker tutorial. A level whose whole job is teaching
        // one rule cannot also be hiding bombs under the board - the player would
        // have no way to tell which mechanic just beat them.
        if (levelNumber < 10 || levelNumber == 13)
        {
            return BombLevelSettings.Disabled;
        }

        BombLevelSettings settings = BombLevelSettings.Disabled;
        settings.enabled = true;
        settings.detonationMode = BombDetonationMode.Countdown;
        settings.fuseMoves = 2;

        if (levelNumber == 10)
        {
            // The teaching level: two bombs, a long look, and enough defusers to
            // recover from getting it wrong twice.
            settings.bombCount = CapBombCount(2, legalBombCells);
            settings.defuserCount = 2;
            settings.previewSeconds = 3.5f;
            settings.scannerCharges = 2;
            settings.wrongDefuserIsConsumed = false;
            return settings;
        }

        if (levelNumber <= 12)
        {
            settings.bombCount = CapBombCount(2, legalBombCells);
            settings.defuserCount = 2;
            settings.previewSeconds = 3f;
            settings.scannerCharges = 1;
            settings.wrongDefuserIsConsumed = false;
            return settings;
        }

        if (levelNumber <= 17)
        {
            settings.bombCount = CapBombCount(levelNumber <= 15 ? 3 : 4, legalBombCells);
            settings.defuserCount = 2;
            settings.previewSeconds = 2.5f;
            settings.scannerCharges = 1;
            settings.wrongDefuserIsConsumed = false;
            return settings;
        }

        if (levelNumber <= 21)
        {
            settings.bombCount = CapBombCount(levelNumber <= 19 ? 4 : 5, legalBombCells);
            settings.defuserCount = 2;
            settings.previewSeconds = 2f;
            settings.scannerCharges = 1;
            settings.fuseMoves = 1;

            // From here a wasted Defuser is gone. Guessing has to cost something
            // or the scanner has no job.
            settings.wrongDefuserIsConsumed = true;
            return settings;
        }

        // 22-25: one wrong drop ends the run.
        settings.bombCount = CapBombCount(levelNumber >= 24 ? 6 : 5, legalBombCells);
        settings.defuserCount = 3;
        settings.previewSeconds = 2f;
        settings.scannerCharges = 1;
        settings.detonationMode = BombDetonationMode.Immediate;
        settings.wrongDefuserIsConsumed = true;
        return settings;
    }

    /// <summary>
    /// Trims a bomb count to what the board can actually carry.
    ///
    /// The late boards are the most blocked ones, so the level with the highest
    /// number on the curve is often the one with the least room: level 24 has
    /// nine non-playable cells and three anchors, leaving eight cells a bomb may
    /// legally occupy. Six bombs there left two usable cells and the solver
    /// proved every single layout unwinnable.
    ///
    /// Bombs may take at most 45% of the legal cells, which keeps a working
    /// majority of the board free whatever shape the level is.
    /// </summary>
    private static int CapBombCount(int desired, int legalBombCells)
    {
        int ceiling = Mathf.FloorToInt(legalBombCells * 0.45f);
        return Mathf.Clamp(desired, 0, Mathf.Max(1, ceiling));
    }

    /// <summary>
    /// How many distinct colours this level's rail should carry in total,
    /// including the ordered ones.
    ///
    /// Levels 1-5 stay on their order colours alone: while the player is still
    /// learning that a box packs when it holds four of one drink, a rail full of
    /// colours that cannot pay off just reads as noise. From level 6 the rail
    /// widens, and from level 11 it carries ALL SIX drinks - past that point the
    /// difficulty is meant to come from sorting pressure, not from board shape,
    /// and a rail missing two colours is a much tidier board than a full one.
    /// </summary>
    private static int GetTargetRailColors(int levelNumber)
    {
        if (levelNumber <= 5) return 0;      // orders only
        if (levelNumber <= 9) return 4;
        if (levelNumber == 10) return 5;
        return 6;
    }

    /// <summary>
    /// Most distinct colours allowed inside a single rail box.
    ///
    /// A three-colour box is much harder to place well, because two of its
    /// drinks will usually be stranded wherever it lands, so it is held back
    /// until the player is comfortable with two-colour boxes.
    /// </summary>
    private static int GetMaxColorsPerRailBox(int levelNumber)
    {
        if (levelNumber <= 2) return 1;
        if (levelNumber <= 13) return 2;
        return 3;
    }

    /// <summary>
    /// How strongly the queue front-loads the colours an order actually wants.
    ///
    /// This is an ARRIVAL ORDER bias, not a quantity: the totals are fixed by
    /// ComputeRailNeeds, and this only decides how early each colour shows up.
    /// It used to be a flat 2.0, which meant ordered drinks arrived roughly twice
    /// as often as everything else all the way to level 25 - the distractors were
    /// pushed into a tail the player had already made room for, so a full rail
    /// never actually felt full. The bias now starts modest and tightens toward
    /// even odds, so by the finale the useful drinks are barely more common than
    /// the useless ones.
    /// </summary>
    private static float GetOrderColorBias(int levelNumber)
    {
        if (levelNumber <= 9) return 1.6f;

        // 1.30 at level 10 down to 1.05 at level 25.
        float t = Mathf.InverseLerp(10f, 25f, levelNumber);
        return Mathf.Lerp(1.30f, 1.05f, t);
    }

    /// <summary>
    /// Percent chance that a rail box carries more than one colour.
    ///
    /// A single-colour box is the easy case: it can only ever help, because every
    /// drink in it wants the same destination. Once the player is fluent, most
    /// boxes should be mixed, so placing one always costs something somewhere.
    /// </summary>
    private static int GetMixedBoxChance(int levelNumber)
    {
        if (levelNumber <= 17) return 55;
        if (levelNumber <= 19) return 78;
        return 90;
    }

    /// <summary>
    /// Percent chance that an already-mixed three-slot box takes a THIRD colour,
    /// and how often a box is three slots wide rather than two. Both are needed:
    /// a three-colour box is only possible in a three-slot box, so raising the
    /// colour chance alone leaves the level barely changed.
    /// </summary>
    private static int GetThirdColorChance(int levelNumber)
    {
        if (levelNumber <= 19) return 40;
        return 75;
    }

    private static int GetWideBoxChance(int levelNumber)
    {
        if (levelNumber <= 19) return 50;    // the historic uniform 2-or-3 split
        return 75;
    }

    private static List<Soda.SodaColor> OrderColors(LevelSpec spec)
    {
        List<Soda.SodaColor> colors = new List<Soda.SodaColor>();
        foreach (Order order in spec.Orders)
        {
            if (!colors.Contains(order.Color))
            {
                colors.Add(order.Color);
            }
        }

        return colors;
    }

    /// <summary>
    /// Picks the non-ordered colours this level's rail also carries, topping the
    /// palette up to the level's target colour count. Explicit spec distractors
    /// win; the rest are filled deterministically from the remaining drinks so a
    /// re-author always produces the same level.
    /// </summary>
    private static List<Soda.SodaColor> ResolveDistractors(LevelSpec spec)
    {
        List<Soda.SodaColor> distractors = new List<Soda.SodaColor>(spec.Distractors);

        // A palette colour with no order of its own is a distractor whether or not
        // the spec calls it one, and it needs the same whole-box supply. Without
        // this it was listed in the level's palette, counted toward the colour
        // target, and then never appeared on the rail at all: ComputeRailNeeds only
        // supplies ordered colours, explicit distractors, and colours already on
        // the board. Level 25 named green in its palette and shipped five colours.
        List<Soda.SodaColor> ordered = OrderColors(spec);
        foreach (Soda.SodaColor color in spec.Palette)
        {
            if (!ordered.Contains(color) && !distractors.Contains(color))
            {
                distractors.Add(color);
            }
        }

        int target = GetTargetRailColors(spec.Number);
        if (target <= 0)
        {
            return distractors;
        }

        List<Soda.SodaColor> used = new List<Soda.SodaColor>(spec.Palette);
        foreach (Soda.SodaColor color in distractors)
        {
            if (!used.Contains(color))
            {
                used.Add(color);
            }
        }

        // Rotated by level number so consecutive levels do not all reach for the
        // same spare colour, which would make the added pressure feel identical.
        Soda.SodaColor[] all = { R, B, O, K, G, P };
        for (int offset = 0; offset < all.Length && used.Count < target; offset++)
        {
            Soda.SodaColor candidate = all[(spec.Number + offset) % all.Length];
            if (!used.Contains(candidate))
            {
                used.Add(candidate);
                distractors.Add(candidate);
            }
        }

        return distractors;
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
        LevelSpec spec, List<InitialBoardBoxData> boxes, List<Soda.SodaColor> distractors)
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

        // A "open N locked blocks" order still has to be paid for in sodas.
        //
        // Blockers are only ever opened by packing a box in an orthogonally
        // adjacent cell, and a frozen blocker needs two such packs. Sizing the rail
        // from the drink orders alone therefore under-supplied every level that
        // traded a drink order for a block order: the solver could finish all the
        // drinks and then find nothing left to break the blockers with.
        //
        // Two extra boxes per requested block covers the frozen case; the boxes
        // are spread across the ordered colours so no single drink dominates.
        if (spec.BlocksOrder > 0 && spec.Orders.Length > 0)
        {
            int extraBoxes = spec.BlocksOrder * 2;
            for (int index = 0; index < extraBoxes; index++)
            {
                Soda.SodaColor color = spec.Orders[index % spec.Orders.Length].Color;
                totals.TryGetValue(color, out int existing);
                totals[color] = existing + BoxCapacity;
            }
        }

        // A distractor is supplied as whole boxes so it can always be packed away
        // and its cell recovered. An odd remainder would sit on the board forever.
        foreach (Soda.SodaColor distractor in distractors)
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
        Dictionary<Soda.SodaColor, int> needs,
        int maxColorsPerBox,
        int seed,
        List<Soda.SodaColor> orderColors,
        int levelNumber)
    {
        System.Random random = new System.Random(seed);
        float orderBias = GetOrderColorBias(levelNumber);
        int mixedChance = GetMixedBoxChance(levelNumber);
        int thirdColorChance = GetThirdColorChance(levelNumber);
        int wideBoxChance = GetWideBoxChance(levelNumber);
        List<TutorialBoxRecipe> queue = new List<TutorialBoxRecipe>();

        Dictionary<Soda.SodaColor, int> remaining = new Dictionary<Soda.SodaColor, int>(needs);
        List<Soda.SodaColor> colors = new List<Soda.SodaColor>(remaining.Keys);
        colors.Sort((a, b) => ((int)a).CompareTo((int)b));

        while (true)
        {
            // Drain the largest pool first so no colour is stranded in a tail of
            // single-soda boxes, but weight ordered colours up so they are not
            // back-loaded behind the distractors. How far up is level-dependent -
            // see GetOrderColorBias.
            Soda.SodaColor primary = default;
            int best = 0;
            float bestScore = 0f;
            foreach (Soda.SodaColor color in colors)
            {
                if (!remaining.TryGetValue(color, out int count) || count <= 0)
                {
                    continue;
                }

                float score = count * (orderColors != null && orderColors.Contains(color) ? orderBias : 1f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = count;
                    primary = color;
                }
            }

            if (best <= 0)
            {
                break;
            }

            // 2 or 3, weighted toward 3 on the late levels: a third colour can
            // only fit in a three-slot box, so the wide-box roll has to move with
            // the third-colour roll or the late rail stays two-colour in practice.
            int preferredSize = random.Next(0, 100) < wideBoxChance ? 3 : 2;
            int boxSize = Mathf.Min(best, preferredSize);
            List<(Soda.SodaColor, int)> amounts = new List<(Soda.SodaColor, int)>();

            if (maxColorsPerBox > 1 && boxSize >= 2 && random.Next(0, 100) < mixedChance)
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

                    if (maxColorsPerBox > 2 && boxSize == 3 && others.Count > 1 &&
                        random.Next(0, 100) < thirdColorChance)
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
                case 'K': result.Add(K); break;
                case 'G': result.Add(G); break;
                case 'P': result.Add(P); break;
            }
        }

        return result;
    }
}
