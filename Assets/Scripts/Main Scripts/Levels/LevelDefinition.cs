using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum LevelDifficulty
{
    Tutorial = 0,
    Easy = 1,
    Medium = 2,
    Hard = 3,
    Boss = 4
}

/// <summary>
/// Result of the last headless solve, stamped onto the definition so the level
/// list can show whether a level is still known-good without re-solving all 25.
/// The checksum is what makes it trustworthy: it goes stale the moment the level
/// is edited.
/// </summary>
[System.Serializable]
public struct LevelVerificationStamp
{
    public string checksum;
    public bool solved;
    public int moves;
    public int nodes;
    public string utcStamp;

    public bool MatchesCurrent(string currentChecksum)
    {
        return solved &&
               !string.IsNullOrEmpty(checksum) &&
               checksum == currentChecksum;
    }
}

/// <summary>
/// The authored source of truth for one campaign level.
///
/// Levels live in scenes, and they still do: this asset is baked INTO a scene's
/// Board and SpawnContoller by LevelSceneGenerator, and nothing reads it at
/// runtime. That split is deliberate. Keeping the scene authoritative at runtime
/// means there is never a question of which of the two wins, the existing Board
/// inspector keeps working, QA can open a scene and press play, and no level
/// data has to be loaded on device. What the asset buys is everything the scene
/// cannot do: bulk generation, validation, diffing, and headless solving.
/// </summary>
[CreateAssetMenu(menuName = "Coca Sorting/Level Definition", fileName = "Level00")]
public sealed class LevelDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField, Min(1)] private int levelNumber = 1;

    [Header("Board")]
    [SerializeField, Min(1)] private int width = 4;
    [SerializeField, Min(1)] private int height = 5;
    [SerializeField, Tooltip("Sparse layout. A coordinate absent from this list is a plain playable cell.")]
    private List<BoardCellEntry> cellStates = new List<BoardCellEntry>();
    [SerializeField, Tooltip("Cells that already hold a box when the level opens.")]
    private List<InitialBoardBoxData> initialBoxes = new List<InitialBoardBoxData>();

    [Header("Goal")]
    [SerializeField] private List<LevelOrderData> orders = new List<LevelOrderData>();

    [Header("Rail")]
    [SerializeField, Tooltip("Colours this level draws from. Also used by the random fallback.")]
    private List<Soda.SodaColor> palette = new List<Soda.SodaColor>();
    [SerializeField, Tooltip("Boxes delivered to the rail, in order. Consumed in batches.")]
    private List<TutorialBoxRecipe> railQueue = new List<TutorialBoxRecipe>();
    [SerializeField, Min(1), Tooltip("Rail slots filled per batch. The rail only refills once it is empty.")]
    private int railBatchSize = 3;
    [SerializeField, Tooltip("What happens once the queue is spent. Levels are verified against the queue alone regardless of this.")]
    private RailExhaustionPolicy railExhaustionPolicy = RailExhaustionPolicy.RandomFallback;

    [Header("Design Notes")]
    [SerializeField] private LevelDifficulty difficulty = LevelDifficulty.Easy;
    [SerializeField, Range(1, 10)] private int difficultyRating = 1;
    [SerializeField, Min(0f), Tooltip("Designer estimate of first-time completion time, in seconds.")]
    private float expectedSeconds = 60f;
    [SerializeField, Tooltip("The one thing this level is trying to teach or test.")]
    private string mainChallenge = string.Empty;
    [SerializeField, TextArea(2, 6)] private string designerNotes = string.Empty;

    [SerializeField, HideInInspector] private string bakedChecksum = string.Empty;
    [SerializeField, HideInInspector] private LevelVerificationStamp lastVerification;

    public int LevelNumber => Mathf.Max(1, levelNumber);
    public int Width => Mathf.Max(1, width);
    public int Height => Mathf.Max(1, height);
    public IReadOnlyList<BoardCellEntry> CellStates => cellStates;
    public IReadOnlyList<InitialBoardBoxData> InitialBoxes => initialBoxes;
    public IReadOnlyList<LevelOrderData> Orders => orders;
    public IReadOnlyList<Soda.SodaColor> Palette => palette;
    public IReadOnlyList<TutorialBoxRecipe> RailQueue => railQueue;
    public int RailBatchSize => Mathf.Max(1, railBatchSize);
    public RailExhaustionPolicy RailExhaustionPolicy => railExhaustionPolicy;
    public LevelDifficulty Difficulty => difficulty;
    public int DifficultyRating => Mathf.Clamp(difficultyRating, 1, 10);
    public float ExpectedSeconds => Mathf.Max(0f, expectedSeconds);
    public string MainChallenge => mainChallenge;
    public string DesignerNotes => designerNotes;

    public string SceneName => LevelNaming.GetSceneName(LevelNumber);
    public string ScenePath => LevelNaming.GetScenePath(LevelNumber);

    public string BakedChecksum => bakedChecksum;
    public LevelVerificationStamp LastVerification => lastVerification;

    /// <summary>True when the generated scene still matches this definition.</summary>
    public bool IsBakedCurrent => !string.IsNullOrEmpty(bakedChecksum) && bakedChecksum == ComputeChecksum();

    /// <summary>True when the last successful solve still applies to this content.</summary>
    public bool IsVerifiedCurrent => lastVerification.MatchesCurrent(ComputeChecksum());

    public int PlayableCellCount
    {
        get
        {
            int blocked = 0;
            foreach (BoardCellEntry entry in cellStates)
            {
                if (entry.kind != BoardCellKind.Playable &&
                    entry.coordinate.x >= 0 && entry.coordinate.x < Width &&
                    entry.coordinate.y >= 0 && entry.coordinate.y < Height)
                {
                    blocked++;
                }
            }

            return Width * Height - blocked;
        }
    }

    public BoardCellKind GetCellKind(Vector2Int coordinate)
    {
        foreach (BoardCellEntry entry in cellStates)
        {
            if (entry.coordinate == coordinate)
            {
                return entry.kind;
            }
        }

        return BoardCellKind.Playable;
    }

    public int CountCellsOfKind(BoardCellKind kind)
    {
        if (kind == BoardCellKind.Playable)
        {
            return PlayableCellCount;
        }

        int total = 0;
        foreach (BoardCellEntry entry in cellStates)
        {
            if (entry.kind == kind)
            {
                total++;
            }
        }

        return total;
    }

    /// <summary>
    /// Every soda of a colour the level can ever produce: what starts on the
    /// board plus everything the authored queue will deliver. The random fallback
    /// is deliberately excluded, because a level must be winnable without it.
    /// </summary>
    public int CountAvailableSodas(Soda.SodaColor color)
    {
        int total = 0;

        foreach (InitialBoardBoxData box in initialBoxes)
        {
            if (box?.startingSodas == null)
            {
                continue;
            }

            foreach (Soda.SodaColor soda in box.startingSodas)
            {
                if (soda == color)
                {
                    total++;
                }
            }
        }

        foreach (TutorialBoxRecipe recipe in railQueue)
        {
            if (recipe != null && recipe.ToDictionary().TryGetValue(color, out int count))
            {
                total += count;
            }
        }

        return total;
    }

    /// <summary>
    /// Stable hash over every authored field, used to detect scene drift and to
    /// invalidate a stale solve. Deliberately excludes the design notes: renaming
    /// the challenge text should not un-verify a level.
    /// </summary>
    public string ComputeChecksum()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(levelNumber).Append('|').Append(width).Append('x').Append(height).Append('|');

        // Sorted so a reordered list that describes the same board does not read
        // as a change.
        List<BoardCellEntry> sortedCells = new List<BoardCellEntry>(cellStates);
        sortedCells.Sort((a, b) =>
        {
            int compare = a.coordinate.x.CompareTo(b.coordinate.x);
            if (compare != 0) return compare;
            compare = a.coordinate.y.CompareTo(b.coordinate.y);
            return compare != 0 ? compare : ((int)a.kind).CompareTo((int)b.kind);
        });

        foreach (BoardCellEntry entry in sortedCells)
        {
            builder.Append(entry.coordinate.x).Append(',').Append(entry.coordinate.y)
                   .Append(':').Append((int)entry.kind).Append(';');
        }

        builder.Append('|');
        foreach (InitialBoardBoxData box in initialBoxes)
        {
            if (box == null) continue;
            builder.Append(box.coordinate.x).Append(',').Append(box.coordinate.y).Append(':');
            if (box.startingSodas != null)
            {
                foreach (Soda.SodaColor soda in box.startingSodas)
                {
                    builder.Append((int)soda).Append('.');
                }
            }
            builder.Append(';');
        }

        builder.Append('|');
        foreach (LevelOrderData order in orders)
        {
            if (order == null) continue;
            builder.Append((int)order.kind).Append(':')
                   .Append((int)order.color).Append('x').Append(order.requiredCount).Append(';');
        }

        builder.Append('|');
        foreach (Soda.SodaColor color in palette)
        {
            builder.Append((int)color).Append('.');
        }

        builder.Append('|');
        foreach (TutorialBoxRecipe recipe in railQueue)
        {
            if (recipe == null)
            {
                builder.Append(';');
                continue;
            }

            // Sorted by colour so two spellings of the same box hash alike.
            List<KeyValuePair<Soda.SodaColor, int>> amounts =
                new List<KeyValuePair<Soda.SodaColor, int>>(recipe.ToDictionary());
            amounts.Sort((a, b) => ((int)a.Key).CompareTo((int)b.Key));
            foreach (KeyValuePair<Soda.SodaColor, int> amount in amounts)
            {
                builder.Append((int)amount.Key).Append('x').Append(amount.Value).Append('.');
            }

            builder.Append(';');
        }

        builder.Append('|').Append(railBatchSize).Append('|').Append((int)railExhaustionPolicy);

        return Hash(builder.ToString());
    }

    private static string Hash(string value)
    {
        // FNV-1a. Not cryptographic - this only has to notice edits, and it has to
        // stay identical across editor sessions, which string.GetHashCode does not.
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= prime;
            }

            return hash.ToString("x8") + "-" + value.Length.ToString("x");
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor-only writers used by the generator and the level tooling.</summary>
    public void EditorSetIdentity(int number)
    {
        levelNumber = Mathf.Max(1, number);
    }

    public void EditorSetBoard(
        int boardWidth,
        int boardHeight,
        List<BoardCellEntry> cells,
        List<InitialBoardBoxData> boxes)
    {
        width = Mathf.Max(1, boardWidth);
        height = Mathf.Max(1, boardHeight);
        cellStates = cells ?? new List<BoardCellEntry>();
        initialBoxes = boxes ?? new List<InitialBoardBoxData>();
    }

    public void EditorSetOrders(List<LevelOrderData> newOrders)
    {
        orders = newOrders ?? new List<LevelOrderData>();
    }

    public void EditorSetRail(
        List<Soda.SodaColor> newPalette,
        List<TutorialBoxRecipe> queue,
        int batchSize,
        RailExhaustionPolicy policy)
    {
        palette = newPalette ?? new List<Soda.SodaColor>();
        railQueue = queue ?? new List<TutorialBoxRecipe>();
        railBatchSize = Mathf.Max(1, batchSize);
        railExhaustionPolicy = policy;
    }

    public void EditorSetDesignNotes(
        LevelDifficulty newDifficulty,
        int rating,
        float seconds,
        string challenge,
        string notes)
    {
        difficulty = newDifficulty;
        difficultyRating = Mathf.Clamp(rating, 1, 10);
        expectedSeconds = Mathf.Max(0f, seconds);
        mainChallenge = challenge ?? string.Empty;
        designerNotes = notes ?? string.Empty;
    }

    public void EditorMarkBaked()
    {
        bakedChecksum = ComputeChecksum();
    }

    public void EditorSetVerification(bool solved, int moves, int nodes)
    {
        lastVerification = new LevelVerificationStamp
        {
            checksum = ComputeChecksum(),
            solved = solved,
            moves = moves,
            nodes = nodes,
            utcStamp = System.DateTime.UtcNow.ToString("u")
        };
    }
#endif
}
