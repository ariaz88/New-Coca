using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Regression tests for the Board blocker rules: how many adjacent packed matches
/// each cell kind costs, that permanent holes are immune, and above all that a
/// single packed box can never damage the same blocker twice.
///
/// That last rule is the one worth guarding. It is invisible in normal play - a
/// frozen blocker that silently took both of its hits from one match just looks
/// like a slightly easier level - so it will not be caught by playtesting, only
/// by asserting it directly.
///
/// Reflection is used deliberately. The blocker hit counters are private runtime
/// state and must stay that way: the whole point of the refactor was to stop
/// blocker damage being reachable through the serialized level layout. A test
/// reaching in is a better trade than a production API that lets anything
/// mutate it.
/// </summary>
public static class BlockerRulesDiagnostics
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [MenuItem("Tools/Coca Sorting/Tests/Run Blocker Rules Tests")]
    public static void RunBlockerRulesTests()
    {
        List<string> failures = new List<string>();
        StringBuilder log = new StringBuilder();
        int checks = 0;

        // A scratch additive scene keeps every Board built here out of whichever
        // level the designer currently has open.
        Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        try
        {
            RunLayoutCosts(scratch, ref checks, failures, log);
            RunDamageSequenceRules(scratch, ref checks, failures, log);
            RunLegacyMigration(scratch, ref checks, failures, log);
        }
        finally
        {
            EditorSceneManager.CloseScene(scratch, true);
        }

        if (failures.Count == 0)
        {
            Debug.Log($"Blocker rules: {checks} checks passed.\n{log}");
            return;
        }

        Debug.LogError(
            $"Blocker rules: {failures.Count} of {checks} checks FAILED.\n" +
            string.Join("\n", failures) + "\n\n" + log);
    }

    private static void RunLayoutCosts(
        Scene scratch, ref int checks, List<string> failures, StringBuilder log)
    {
        log.AppendLine("-- cell kind costs --");

        Board board = CreateBoard(scratch, 3, 3, new List<BoardCellEntry>
        {
            new BoardCellEntry(new Vector2Int(1, 2), BoardCellKind.Frozen),
            new BoardCellEntry(new Vector2Int(0, 1), BoardCellKind.Blocker),
            new BoardCellEntry(new Vector2Int(2, 1), BoardCellKind.Removed),
            new BoardCellEntry(new Vector2Int(1, 0), BoardCellKind.Frozen)
        });

        Check(ref checks, failures, log, "Blocker costs 1 hit",
            HitsRemaining(board, 0, 1) == 1);
        Check(ref checks, failures, log, "Frozen costs 2 hits",
            HitsRemaining(board, 1, 2) == 2);
        Check(ref checks, failures, log, "Hole costs 0 hits (immune)",
            HitsRemaining(board, 2, 1) == 0);
        Check(ref checks, failures, log, "All four kinds block placement",
            !board.IsPlayableCell(1, 2) && !board.IsPlayableCell(0, 1) &&
            !board.IsPlayableCell(2, 1) && !board.IsPlayableCell(1, 0));
        Check(ref checks, failures, log, "PlayableCellCount is 9-4=5",
            board.PlayableCellCount == 5);
        Check(ref checks, failures, log, "Untouched blocker is not cracked",
            !board.IsBlockerCracked(1, 2));
    }

    private static void RunDamageSequenceRules(
        Scene scratch, ref int checks, List<string> failures, StringBuilder log)
    {
        log.AppendLine("-- damage sequencing --");

        Board board = CreateBoard(scratch, 3, 3, new List<BoardCellEntry>
        {
            new BoardCellEntry(new Vector2Int(1, 2), BoardCellKind.Frozen),
            new BoardCellEntry(new Vector2Int(0, 1), BoardCellKind.Blocker),
            new BoardCellEntry(new Vector2Int(2, 1), BoardCellKind.Removed),
            new BoardCellEntry(new Vector2Int(1, 0), BoardCellKind.Frozen)
        });

        // A packed box at (1,1) touches all four cells above.
        ApplyDamage(board, 1, 1, 1L);

        Check(ref checks, failures, log, "Frozen cracks on first hit",
            board.IsBlockerCracked(1, 2) && HitsRemaining(board, 1, 2) == 1);
        Check(ref checks, failures, log, "Cracked frozen still blocks placement",
            !board.IsPlayableCell(1, 2));
        Check(ref checks, failures, log, "Hole ignores adjacent match",
            HitsRemaining(board, 2, 1) == 0 && !board.IsBlockerCracked(2, 1) &&
            !IsBreaking(board, 2, 1));
        // These boards carry no blocker visual, so BreakBlockedCellRoutine never
        // reaches a yield and completes inline. The cell is therefore already
        // fully open here rather than mid-animation, which is the end state that
        // actually matters: open, off the blocker table, and placeable.
        Check(ref checks, failures, log, "Blocker opens on its single hit",
            IsOpened(board, 0, 1),
            "playable=" + board.IsPlayableCell(0, 1) + " tracked=" + IsTracked(board, 0, 1));
        Check(ref checks, failures, log, "Opened blocker leaves the breaking set",
            !IsBreaking(board, 0, 1) && BreakingCount(board) == 0,
            "breakingCount=" + BreakingCount(board));
        Check(ref checks, failures, log, "Opened blocker raises PlayableCellCount to 6",
            board.PlayableCellCount == 6,
            "PlayableCellCount=" + board.PlayableCellCount);

        // The guard: the same packed box cannot land a second hit.
        ApplyDamage(board, 1, 1, 1L);

        Check(ref checks, failures, log, "One match cannot hit a frozen blocker twice (north)",
            HitsRemaining(board, 1, 2) == 1);
        Check(ref checks, failures, log, "One match cannot hit a frozen blocker twice (south)",
            HitsRemaining(board, 1, 0) == 1);

        // A different packed box carries a new sequence and does land.
        ApplyDamage(board, 1, 1, 2L);

        Check(ref checks, failures, log, "Second distinct match opens the frozen blocker",
            IsOpened(board, 1, 2),
            "playable=" + board.IsPlayableCell(1, 2) + " tracked=" + IsTracked(board, 1, 2));
        // Both frozen blockers touched the same two matches, so both open together.
        Check(ref checks, failures, log, "Both frozen blockers open after two matches",
            IsOpened(board, 1, 2) && IsOpened(board, 1, 0),
            "north=" + IsOpened(board, 1, 2) + " south=" + IsOpened(board, 1, 0));
        Check(ref checks, failures, log, "Hole is still immune after two matches",
            !IsBreaking(board, 2, 1) && !board.IsPlayableCell(2, 1));

        // Cracking must never enter the set that gates ResolvePlacement, or a
        // crack would hang resolution forever.
        Board crackOnly = CreateBoard(scratch, 3, 3, new List<BoardCellEntry>
        {
            new BoardCellEntry(new Vector2Int(0, 1), BoardCellKind.Frozen)
        });
        ApplyDamage(crackOnly, 1, 1, 1L);
        Check(ref checks, failures, log, "A crack never gates resolution",
            BreakingCount(crackOnly) == 0 && crackOnly.IsBlockerCracked(0, 1));
    }

    private static void RunLegacyMigration(
        Scene scratch, ref int checks, List<string> failures, StringBuilder log)
    {
        log.AppendLine("-- legacy removedCells migration --");

        Board board = CreateBoard(scratch, 3, 3, new List<BoardCellEntry>());
        SetPrivate(board, "removedCells", new List<Vector2Int>
        {
            new Vector2Int(0, 0),
            new Vector2Int(2, 2)
        });

        // Before OnValidate: the legacy list alone must already drive play, so a
        // scene never opened since the split still behaves.
        Invoke(board, "RebuildLayoutCache");
        Check(ref checks, failures, log, "Legacy cells play as blockers before migration",
            !board.IsPlayableCell(0, 0) && HitsRemaining(board, 0, 0) == 1);

        Invoke(board, "OnValidate");

        List<BoardCellEntry> migrated = (List<BoardCellEntry>)GetPrivate(board, "cellStates");
        List<Vector2Int> legacy = (List<Vector2Int>)GetPrivate(board, "removedCells");

        Check(ref checks, failures, log, "Migration drains the legacy list",
            legacy.Count == 0);
        Check(ref checks, failures, log, "Migration produces two Blocker entries",
            migrated.Count == 2 &&
            migrated.TrueForAll(entry => entry.kind == BoardCellKind.Blocker));
        Check(ref checks, failures, log, "Migrated cells keep their coordinates",
            migrated.Exists(e => e.coordinate == new Vector2Int(0, 0)) &&
            migrated.Exists(e => e.coordinate == new Vector2Int(2, 2)));

        // Idempotence: a second pass must not duplicate anything.
        Invoke(board, "OnValidate");
        migrated = (List<BoardCellEntry>)GetPrivate(board, "cellStates");
        Check(ref checks, failures, log, "Migration is idempotent",
            migrated.Count == 2);

        // An empty legacy list (every shipped level) must be a total no-op.
        Board clean = CreateBoard(scratch, 3, 3, new List<BoardCellEntry>());
        Invoke(clean, "OnValidate");
        Check(ref checks, failures, log, "Empty legacy list migrates to nothing",
            ((List<BoardCellEntry>)GetPrivate(clean, "cellStates")).Count == 0 &&
            clean.PlayableCellCount == 9);
    }

    // ---- harness ----

    private static Board CreateBoard(Scene scratch, int width, int height, List<BoardCellEntry> states)
    {
        GameObject host = new GameObject("BlockerRulesBoard");
        SceneManager.MoveGameObjectToScene(host, scratch);

        Board board = host.AddComponent<Board>();
        SetPrivate(board, "width", width);
        SetPrivate(board, "height", height);
        SetPrivate(board, "cellStates", states);
        SetPrivate(board, "removedCells", new List<Vector2Int>());
        Invoke(board, "RebuildLayoutCache");
        return board;
    }

    /// <summary>
    /// Applies one packed match's damage. Outside Play Mode the break path cannot
    /// start its coroutine, so that exception is swallowed: every state change the
    /// tests assert on happens before StartCoroutine is reached, and the break
    /// animation itself is not what these tests are about.
    /// </summary>
    private static void ApplyDamage(Board board, int column, int row, long sequence)
    {
        try
        {
            typeof(Board)
                .GetMethod("ApplyPackedMatchDamage", Private)
                .Invoke(board, new object[] { column, row, sequence });
        }
        catch (TargetInvocationException exception)
        {
            // Expected in Edit Mode: coroutines are Play Mode only.
            Debug.Log("  (edit-mode break path stopped at: " +
                      (exception.InnerException != null
                          ? exception.InnerException.GetType().Name + " - " + exception.InnerException.Message
                          : "unknown") + ")");
        }
    }

    private static int HitsRemaining(Board board, int column, int row)
    {
        IDictionary blockers = (IDictionary)GetPrivate(board, "blockers");
        object runtime = blockers[new Vector2Int(column, row)];
        if (runtime == null)
        {
            return -1;
        }

        return (int)runtime.GetType().GetField("HitsRemaining").GetValue(runtime);
    }

    /// <summary>True when the blocker table still tracks this cell.</summary>
    private static bool IsTracked(Board board, int column, int row)
    {
        IDictionary blockers = (IDictionary)GetPrivate(board, "blockers");
        return blockers.Contains(new Vector2Int(column, row));
    }

    /// <summary>
    /// A cell is fully open only when it has left the blocker table AND become
    /// placeable. Checking one without the other would miss a half-torn-down
    /// blocker that still refuses drops.
    /// </summary>
    private static bool IsOpened(Board board, int column, int row)
    {
        return !IsTracked(board, column, row) && board.IsPlayableCell(column, row);
    }

    private static bool IsBreaking(Board board, int column, int row)
    {
        ICollection<Vector2Int> breaking =
            (ICollection<Vector2Int>)GetPrivate(board, "breakingBlockedCells");
        return breaking.Contains(new Vector2Int(column, row));
    }

    private static int BreakingCount(Board board)
    {
        return ((ICollection<Vector2Int>)GetPrivate(board, "breakingBlockedCells")).Count;
    }

    private static object GetPrivate(Board board, string field)
    {
        return typeof(Board).GetField(field, Private).GetValue(board);
    }

    private static void SetPrivate(Board board, string field, object value)
    {
        typeof(Board).GetField(field, Private).SetValue(board, value);
    }

    private static void Invoke(Board board, string method)
    {
        typeof(Board).GetMethod(method, Private).Invoke(board, null);
    }

    private static void Check(
        ref int checks, List<string> failures, StringBuilder log, string name, bool passed,
        string detail = null)
    {
        checks++;
        string suffix = string.IsNullOrEmpty(detail) ? string.Empty : "   [" + detail + "]";
        log.AppendLine((passed ? "  PASS  " : "  FAIL  ") + name + suffix);
        if (!passed)
        {
            failures.Add("FAILED: " + name + suffix);
        }
    }
}
