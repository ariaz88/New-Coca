using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What happens when a normal Box is dropped on a bomb.
///
/// The trigger is the same in both: the bomb is a trap under a cell that looks
/// free, and dropping on it is what sets it off. Only the consequence differs,
/// which is what makes this a difficulty dial rather than two mechanics.
/// </summary>
public enum BombDetonationMode
{
    /// <summary>
    /// The bomb reveals, arms, and burns down a short fuse measured in
    /// placements. A Defuser dropped on it before the fuse runs out saves the
    /// level; otherwise it blows the boxes around it off the board. Costly, but
    /// survivable - this is the teaching mode.
    /// </summary>
    Countdown = 0,

    /// <summary>
    /// The bomb detonates on contact and the level is lost. No fuse, no
    /// counterplay. Reserved for the last levels, where the player already knows
    /// to scan before committing.
    /// </summary>
    Immediate = 1
}

/// <summary>
/// Everything a level needs to know about its bombs.
///
/// A struct rather than loose fields on Board because the same values are
/// authored on LevelDefinition, baked into the scene, read by the runtime, and
/// handed to the layout generator in the editor - four places that would
/// otherwise drift apart one field at a time.
/// </summary>
[Serializable]
public struct BombLevelSettings
{
    [Tooltip("Master switch. Off means this level has no bombs at all and nothing bomb-related is created.")]
    public bool enabled;

    [Min(0), Tooltip("How many bombs are hidden on the board.")]
    public int bombCount;

    [Min(0), Tooltip("Defusers delivered over the course of the level.")]
    public int defuserCount;

    [Min(0f), Tooltip("Seconds the level-start preview holds the bombs visible.")]
    public float previewSeconds;

    [Min(0), Tooltip("Scanner uses granted for the level.")]
    public int scannerCharges;

    [Tooltip("What dropping a Box on a bomb does.")]
    public BombDetonationMode detonationMode;

    [Min(1), Tooltip("Countdown mode only: placements the player has left once a bomb is armed.")]
    public int fuseMoves;

    [Tooltip("Countdown mode only: whether a Defuser dropped on a cell with no bomb is spent anyway.")]
    public bool wrongDefuserIsConsumed;

    public static BombLevelSettings Disabled => new BombLevelSettings
    {
        enabled = false,
        bombCount = 0,
        defuserCount = 0,
        previewSeconds = 2.5f,
        scannerCharges = 0,
        detonationMode = BombDetonationMode.Countdown,
        fuseMoves = 2,
        wrongDefuserIsConsumed = false
    };

    /// <summary>True when this level actually has bombs to place.</summary>
    public bool IsActive => enabled && bombCount > 0;

    public int SafeFuseMoves => Mathf.Max(1, fuseMoves);
    public float SafePreviewSeconds => Mathf.Max(0f, previewSeconds);
}

/// <summary>
/// One bomb's live state.
///
/// A class, not a struct, and deliberately NOT serializable: like BlockerRuntime
/// this is play state only. A bomb that survived into the scene asset would make
/// every restart show the same layout, which is exactly what the level is
/// supposed to re-roll.
/// </summary>
public sealed class BombRuntime
{
    public Vector2Int Coordinate;

    /// <summary>Visible to the player right now - by preview, scanner, or because it armed.</summary>
    public bool IsRevealed;

    /// <summary>Neutralised. The cell is an ordinary playable cell again.</summary>
    public bool IsDefused;

    /// <summary>Fuse burning: a Box landed on it in Countdown mode.</summary>
    public bool IsArmed;

    /// <summary>Placements left before it goes off. Only meaningful while armed.</summary>
    public int MovesUntilDetonation;

    /// <summary>Root of the generated marker. Null until the level draws one.</summary>
    public GameObject Visual;

    /// <summary>True while this bomb is still a threat.</summary>
    public bool IsLive => !IsDefused;
}

/// <summary>
/// A single generated bomb layout, stored on LevelDefinition after the solver
/// has proved the level is still winnable with those cells unusable.
///
/// Serialized as a flat coordinate list because Unity does not serialize
/// nested generic lists; the pool is a list of these.
/// </summary>
[Serializable]
public sealed class BombLayout
{
    [SerializeField] private List<Vector2Int> cells = new List<Vector2Int>();

    public BombLayout()
    {
    }

    public BombLayout(IEnumerable<Vector2Int> coordinates)
    {
        if (coordinates == null)
        {
            return;
        }

        foreach (Vector2Int coordinate in coordinates)
        {
            if (!cells.Contains(coordinate))
            {
                cells.Add(coordinate);
            }
        }
    }

    public IReadOnlyList<Vector2Int> Cells => cells;
    public int Count => cells.Count;

    public override string ToString()
    {
        return string.Join(" ", cells);
    }
}
