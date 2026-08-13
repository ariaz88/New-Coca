using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the Hidden Bombs mechanic for one level: picks the layout, plays the
/// level-start preview, owns the scanner charges, and keeps the defuser dock
/// stocked.
///
/// It is a separate component rather than more of Board because Board already
/// carries the grid, the blockers, placement, resolution and retirement; what
/// belongs there is the per-cell bomb state that placement has to consult, and
/// that is all that went there. Sequencing, UI and economy live here.
///
/// Added at runtime by BombHud so it reaches all 25 baked scenes without a
/// re-bake, the same way OrderPanelSkin and LevelHudLayout do.
/// </summary>
[DisallowMultipleComponent]
public sealed class BombDirector : MonoBehaviour
{
    public static BombDirector instance;

    private Board board;
    private BombLevelSettings settings;
    private readonly List<Defuser> liveDefusers = new List<Defuser>();

    private int defusersRemaining;
    private int scannerChargesRemaining;
    private bool inputFrozen;
    private Coroutine scanRoutine;

    /// <summary>Raised when a charge is spent or a defuser is used, so the HUD can repaint.</summary>
    public event System.Action StateChanged;

    public int ScannerChargesRemaining => scannerChargesRemaining;
    public int DefusersRemaining => defusersRemaining + liveDefusers.Count;
    public bool IsBusy => inputFrozen;
    public bool HasBombs => settings.IsActive;

    private void Awake()
    {
        instance = this;
        board = Board.instance != null ? Board.instance : FindFirstObjectByType<Board>();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        // The spawner outlives this component on a scene change, and a stale
        // delegate would keep claiming slots for a director that no longer exists.
        if (SpawnContoller.instance != null)
        {
            SpawnContoller.instance.RailSlotClaim = null;
            SpawnContoller.instance.RailSlotFactory = null;
        }

        // A level that ends mid-preview must not leave the board refusing drops.
        ThawInput();
    }

    private void Start()
    {
        if (board == null)
        {
            enabled = false;
            return;
        }

        settings = board.BombSettings;
        if (!settings.IsActive)
        {
            enabled = false;
            return;
        }

        defusersRemaining = Mathf.Max(0, settings.defuserCount);
        scannerChargesRemaining = Mathf.Max(0, settings.scannerCharges);
        StartCoroutine(LevelStartRoutine());
    }

    /// <summary>
    /// Chooses this attempt's layout, installs it, and plays the preview.
    ///
    /// The leading frame wait is load-bearing: this project sets no custom script
    /// execution order, so Board.Start - which is what builds the grid the bombs
    /// need - may not have run yet when this Start does.
    /// </summary>
    private IEnumerator LevelStartRoutine()
    {
        yield return null;

        InstallLayoutForThisAttempt();
        if (board.LiveBombCount == 0)
        {
            enabled = false;
            yield break;
        }

        if (SpawnContoller.instance != null)
        {
            SpawnContoller.instance.RailSlotClaim = ClaimRailSlots;
            SpawnContoller.instance.RailSlotFactory = CreateRailDefuser;
        }

        StateChanged?.Invoke();

        yield return PreviewRoutine();
    }

    /// <summary>
    /// Picks a verified layout by attempt number, so a restart is a genuinely
    /// different board and still provably winnable.
    ///
    /// Falling back to a runtime shuffle when the pool is empty is deliberate: a
    /// level whose pool failed to bake should still be playable in the editor
    /// rather than silently having no bombs, and the warning says which one.
    /// </summary>
    private void InstallLayoutForThisAttempt()
    {
        IReadOnlyList<BombLayout> pool = board.BombLayoutPool;
        if (pool != null && pool.Count > 0)
        {
            int attempt = GameDataManager.instance != null ? GameDataManager.instance.LevelAttempt : 1;
            BombLayout layout = pool[Mathf.Abs(attempt) % pool.Count];
            board.InstallBombLayout(layout.Cells);
            return;
        }

        Debug.LogWarning(
            "This level has bombs enabled but no verified layout pool; falling back to a random " +
            "layout, which has NOT been proved solvable. Re-bake the campaign.", this);
        board.InstallBombLayout(BombLayoutGenerator.GenerateRuntimeFallback(board, settings.bombCount));
    }

    // ---------------------------------------------------------------- preview

    /// <summary>
    /// "Memorize the bombs!": freeze input, reveal every bomb with a sweep and a
    /// pulse, then hide them and hand the board back.
    /// </summary>
    private IEnumerator PreviewRoutine()
    {
        FreezeInput();
        BombHud.instance?.ShowBanner("MEMORIZE THE BOMBS!");
        BombHud.instance?.SetDim(true);

        yield return SweepRoutine();

        foreach (BombRuntime bomb in board.AllBombs)
        {
            if (!bomb.IsLive)
            {
                continue;
            }

            bomb.IsRevealed = true;
            BombCellVisuals.SetVisible(bomb, true);
            StartCoroutine(BombCellVisuals.PulseRoutine(bomb, settings.SafePreviewSeconds));
        }

        CameraShaker.Shake(0.25f);
        yield return new WaitForSeconds(settings.SafePreviewSeconds);

        HideUnarmedBombs();
        BombHud.instance?.SetDim(false);
        BombHud.instance?.HideBanner();
        ThawInput();
    }

    // ---------------------------------------------------------------- scanner

    /// <summary>
    /// Spends one charge to show the bombs that are still live for about a
    /// second. Returns false when there is nothing to spend or the board is
    /// already mid-sweep, so the button can just call it.
    /// </summary>
    public bool TryUseScanner()
    {
        if (!settings.IsActive || scannerChargesRemaining <= 0 || inputFrozen || board.LiveBombCount == 0)
        {
            return false;
        }

        if (GameManager.instance != null &&
            (GameManager.instance.gameOver || GameManager.instance.gameEnded))
        {
            return false;
        }

        scannerChargesRemaining--;
        StateChanged?.Invoke();
        scanRoutine = StartCoroutine(ScanRoutine());
        return true;
    }

    private IEnumerator ScanRoutine()
    {
        FreezeInput();
        BombHud.instance?.SetDim(true);

        yield return SweepRoutine();

        List<BombRuntime> revealed = new List<BombRuntime>();
        foreach (BombRuntime bomb in board.AllBombs)
        {
            if (!bomb.IsLive)
            {
                continue;
            }

            revealed.Add(bomb);
            BombCellVisuals.SetVisible(bomb, true);
            StartCoroutine(BombCellVisuals.PulseRoutine(bomb, 1f));
        }

        yield return new WaitForSeconds(1f);

        foreach (BombRuntime bomb in revealed)
        {
            // An armed bomb stays visible: its fuse is already burning and the
            // player can see the count, so hiding it would be hiding a timer.
            if (!bomb.IsArmed)
            {
                BombCellVisuals.SetVisible(bomb, false);
            }
        }

        BombHud.instance?.SetDim(false);
        ThawInput();
        scanRoutine = null;
    }

    /// <summary>The beam that runs up the board before a reveal.</summary>
    private IEnumerator SweepRoutine()
    {
        yield return BombHud.instance != null
            ? BombHud.instance.SweepRoutine(0.55f)
            : (object)new WaitForSeconds(0.2f);
    }

    private void HideUnarmedBombs()
    {
        foreach (BombRuntime bomb in board.AllBombs)
        {
            if (bomb.IsLive && !bomb.IsArmed)
            {
                bomb.IsRevealed = false;
                BombCellVisuals.SetVisible(bomb, false);
            }
        }
    }

    // ---------------------------------------------------------------- defusers

    /// <summary>
    /// Decides how many of this batch's rail slots become Defusers, and which.
    ///
    /// Defusers ride the rail in place of a box rather than sitting at a dock of
    /// their own. The dock was safe but inert: it was always there, always the
    /// same, and it read as a permanent button rather than as a resource. Coming
    /// down the rail means a Defuser arrives when it arrives, sometimes two or
    /// three together, sometimes not for several batches - so holding one back
    /// for a bomb you have not found yet is an actual decision.
    ///
    /// A claimed slot does not draw from the authored queue, so the box that
    /// would have filled it is delayed, never lost, and the level's supply is
    /// unchanged.
    /// </summary>
    private List<int> ClaimRailSlots(int slotCount)
    {
        List<int> claimed = new List<int>();
        if (defusersRemaining <= 0 || slotCount <= 0 || board.LiveBombCount == 0)
        {
            return claimed;
        }

        // Never the whole batch. A batch of nothing but Defusers stalls the level,
        // because the board can only progress when boxes arrive.
        int most = Mathf.Min(defusersRemaining, slotCount - 1, 3);
        if (most <= 0)
        {
            return claimed;
        }

        // Weighted low: one is the common case, two occasional, three rare.
        int roll = Random.Range(0, 100);
        int wanted = roll < 45 ? 0 : roll < 82 ? 1 : roll < 95 ? 2 : 3;
        wanted = Mathf.Min(wanted, most);
        if (wanted <= 0)
        {
            return claimed;
        }

        List<int> pool = new List<int>();
        for (int slot = 0; slot < slotCount; slot++)
        {
            pool.Add(slot);
        }

        for (int i = 0; i < wanted && pool.Count > 0; i++)
        {
            int index = Random.Range(0, pool.Count);
            claimed.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return claimed;
    }

    /// <summary>Builds the Defuser that fills a claimed rail slot.</summary>
    private GameObject CreateRailDefuser(int slot, Vector3 spawnPosition)
    {
        if (defusersRemaining <= 0)
        {
            return null;
        }

        Defuser defuser = DefuserFactory.Create(spawnPosition);
        if (defuser == null)
        {
            return null;
        }

        defusersRemaining--;
        defuser.ConsumeOnWrongCell = settings.wrongDefuserIsConsumed;
        defuser.Consumed += HandleDefuserConsumed;
        liveDefusers.Add(defuser);
        StateChanged?.Invoke();
        return defuser.gameObject;
    }

    /// <summary>
    /// A spent Defuser leaves the pool. When the level forgives a miss the charge
    /// comes back instead, so a fresh one can ride down with a later batch - the
    /// item itself never returns to the rail once it has touched the board.
    /// </summary>
    private void HandleDefuserConsumed(Defuser defuser, bool refund)
    {
        defuser.Consumed -= HandleDefuserConsumed;
        liveDefusers.Remove(defuser);

        if (refund)
        {
            defusersRemaining++;
        }

        StateChanged?.Invoke();
    }

    // ------------------------------------------------------------ input gates

    /// <summary>
    /// Stops every drag and every drop using the owner-scoped constraints that
    /// already exist.
    ///
    /// Deliberately none of the obvious alternatives: IsResolving also gates
    /// CanPlaceAt, the win check and rail exhaustion; gameEnded/gameOver kill
    /// win and lose permanently; and Time.timeScale = 0 stops our own
    /// WaitForSeconds and every board tween while not stopping OnMouseDown at
    /// all.
    /// </summary>
    private void FreezeInput()
    {
        if (inputFrozen)
        {
            return;
        }

        inputFrozen = true;
        board.TrySetPlacementConstraint(this, (box, column, row) => false);

        foreach (Box box in FindObjectsByType<Box>(FindObjectsSortMode.None))
        {
            if (box != null && !box.IsOnBoard)
            {
                box.TrySetDragConstraint(this, _ => false);
            }
        }

        foreach (Defuser defuser in liveDefusers)
        {
            if (defuser != null)
            {
                defuser.TrySetDragConstraint(this, _ => false);
            }
        }
    }

    private void ThawInput()
    {
        if (!inputFrozen)
        {
            return;
        }

        inputFrozen = false;
        if (board != null)
        {
            board.ClearPlacementConstraint(this);
        }

        foreach (Box box in FindObjectsByType<Box>(FindObjectsSortMode.None))
        {
            if (box != null)
            {
                box.ClearDragConstraint(this);
            }
        }

        foreach (Defuser defuser in liveDefusers)
        {
            if (defuser != null)
            {
                defuser.ClearDragConstraint(this);
            }
        }
    }
}
