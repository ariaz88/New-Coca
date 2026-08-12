using System;
using UnityEngine;

/// <summary>
/// What occupies a Board cell before any box is placed on it.
///
/// These used to be one concept. A coordinate in Board.removedCells rendered a
/// taped-X cardboard box AND broke open when a box packed next to it, so a level
/// designer had no way to carve a permanent hole in the board: every hole was a
/// blocker that the first adjacent match would dissolve.
/// </summary>
public enum BoardCellKind : byte
{
    /// <summary>Ordinary cell. Never stored - an absent coordinate means Playable.</summary>
    Playable = 0,

    /// <summary>
    /// A permanent hole. No Node, no visual, immune to damage, never placeable.
    /// This is what shapes a board into something other than a rectangle.
    /// </summary>
    Removed = 1,

    /// <summary>Taped-X cardboard box. One orthogonally adjacent packed match breaks it open.</summary>
    Blocker = 2,

    /// <summary>
    /// Frost-covered box. Two orthogonally adjacent packed matches: the first
    /// visibly cracks it, the second breaks it open.
    /// </summary>
    Frozen = 3
}

/// <summary>
/// One authored non-playable cell. Stored sparsely: only cells that are not
/// plain Playable appear in the list.
///
/// A struct rather than a class so value equality makes de-duplication and
/// editor lookups trivial, and so there is no null-check surface in the
/// validation paths.
/// </summary>
[Serializable]
public struct BoardCellEntry : IEquatable<BoardCellEntry>
{
    public Vector2Int coordinate;
    public BoardCellKind kind;

    public BoardCellEntry(Vector2Int coordinate, BoardCellKind kind)
    {
        this.coordinate = coordinate;
        this.kind = kind;
    }

    public bool Equals(BoardCellEntry other)
    {
        return coordinate == other.coordinate && kind == other.kind;
    }

    public override bool Equals(object obj)
    {
        return obj is BoardCellEntry other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (coordinate.GetHashCode() * 397) ^ (int)kind;
    }

    public override string ToString()
    {
        return $"({coordinate.x}, {coordinate.y}) {kind}";
    }
}

/// <summary>
/// The rules that separate the cell kinds. Kept in one place so the Board, the
/// level validator, and the headless solver cannot drift apart on what a
/// "frozen" cell costs to open.
/// </summary>
public static class BoardCellRules
{
    /// <summary>
    /// Orthogonally adjacent packed matches needed to open the cell.
    /// Playable is already open; Removed never opens, and reports 0 so callers
    /// that treat 0 as "not a damage target" behave correctly.
    /// </summary>
    public static int GetRequiredHits(BoardCellKind kind)
    {
        switch (kind)
        {
            case BoardCellKind.Blocker: return 1;
            case BoardCellKind.Frozen: return 2;
            default: return 0;
        }
    }

    /// <summary>True when adjacent packed matches can eventually open this cell.</summary>
    public static bool IsBreakable(BoardCellKind kind)
    {
        return kind == BoardCellKind.Blocker || kind == BoardCellKind.Frozen;
    }

    /// <summary>True when a box cannot currently be dropped on this cell.</summary>
    public static bool BlocksPlacement(BoardCellKind kind)
    {
        return kind != BoardCellKind.Playable;
    }

    /// <summary>True when the cell shows a physical box that has to be rendered.</summary>
    public static bool HasVisual(BoardCellKind kind)
    {
        return IsBreakable(kind);
    }

    /// <summary>Short glyph used by the Board inspector grid and validation messages.</summary>
    public static string GetGlyph(BoardCellKind kind)
    {
        switch (kind)
        {
            case BoardCellKind.Removed: return "-";
            case BoardCellKind.Blocker: return "X";
            case BoardCellKind.Frozen: return "*";
            default: return "O";
        }
    }

    public static string GetDisplayName(BoardCellKind kind)
    {
        switch (kind)
        {
            case BoardCellKind.Removed: return "Hole";
            case BoardCellKind.Blocker: return "X Blocker";
            case BoardCellKind.Frozen: return "Frozen";
            default: return "Playable";
        }
    }

    /// <summary>Advances the authoring cycle: Playable -> Removed -> Blocker -> Frozen -> Playable.</summary>
    public static BoardCellKind Next(BoardCellKind kind)
    {
        switch (kind)
        {
            case BoardCellKind.Playable: return BoardCellKind.Removed;
            case BoardCellKind.Removed: return BoardCellKind.Blocker;
            case BoardCellKind.Blocker: return BoardCellKind.Frozen;
            default: return BoardCellKind.Playable;
        }
    }

    public static BoardCellKind Previous(BoardCellKind kind)
    {
        switch (kind)
        {
            case BoardCellKind.Playable: return BoardCellKind.Frozen;
            case BoardCellKind.Removed: return BoardCellKind.Playable;
            case BoardCellKind.Blocker: return BoardCellKind.Removed;
            default: return BoardCellKind.Blocker;
        }
    }

    /// <summary>Colour used for this kind in the Scene-view gizmo and the inspector grid.</summary>
    public static Color GetEditorColor(BoardCellKind kind)
    {
        switch (kind)
        {
            case BoardCellKind.Removed: return new Color(0.13f, 0.13f, 0.15f, 0.85f);
            case BoardCellKind.Blocker: return new Color(0.38f, 0.18f, 0.07f, 0.75f);
            case BoardCellKind.Frozen: return new Color(0.42f, 0.78f, 0.92f, 0.8f);
            default: return new Color(0.2f, 0.9f, 0.35f, 0.9f);
        }
    }
}
