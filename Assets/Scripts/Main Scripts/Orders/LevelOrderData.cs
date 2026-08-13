using UnityEngine;

/// <summary>
/// One designer-authored order line for a single level.
///
/// A level's Orders panel is built from a list of these. Each entry means
/// "the player must pack <see cref="requiredCount"/> boxes whose sodas are all
/// <see cref="color"/>". The list lives on the scene's Board component so a
/// level keeps its layout and its orders in the same place, exactly like the
/// existing blocked-cell and initial-box level data.
///
/// This type is intentionally a plain serializable class rather than a struct
/// so Unity's SerializedProperty editing (insert, delete, reorder) behaves the
/// same way it already does for InitialBoardBoxData.
/// </summary>
public enum OrderKind
{
    /// <summary>Pack N boxes of a single soda colour. The original, and the default.</summary>
    Soda = 0,

    /// <summary>
    /// Open N locked blocks. Any breakable blocker counts once, at the moment it
    /// actually opens - a frozen blocker that has only cracked has not opened yet.
    /// </summary>
    Blocks = 1
}

[System.Serializable]
public sealed class LevelOrderData
{
    // Deliberately first and deliberately defaulting to Soda, so every order
    // already serialized in the 25 level scenes keeps meaning exactly what it
    // meant before this field existed.
    [Tooltip("What this order asks for: packing sodas of one colour, or opening locked blocks.")]
    public OrderKind kind = OrderKind.Soda;

    [Tooltip("Which soda color this order asks for. Ignored by Blocks orders.")]
    public Soda.SodaColor color = Soda.SodaColor.Red;

    [Min(1)]
    [Tooltip("How many packed boxes of this color, or how many locked blocks to open.")]
    public int requiredCount = 1;

    public bool IsBlocks => kind == OrderKind.Blocks;

    public LevelOrderData()
    {
    }

    public LevelOrderData(Soda.SodaColor color, int requiredCount)
    {
        kind = OrderKind.Soda;
        this.color = color;
        this.requiredCount = Mathf.Max(1, requiredCount);
    }

    /// <summary>Creates a "open N locked blocks" order.</summary>
    public static LevelOrderData Blocks(int requiredCount)
    {
        return new LevelOrderData
        {
            kind = OrderKind.Blocks,
            requiredCount = Mathf.Max(1, requiredCount)
        };
    }
}
