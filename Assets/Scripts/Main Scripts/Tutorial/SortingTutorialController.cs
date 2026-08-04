using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TutorialBoardDirection
{
    Right,
    Left,
    Down,
    Up
}

/// <summary>
/// Pure, testable orthogonal-cell calculation shared by the sorting Tutorial's
/// highlight and placement-validation paths.
/// </summary>
public static class TutorialAdjacentCellUtility
{
    private static readonly Vector2Int[] OrthogonalOffsets =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.down,
        Vector2Int.up
    };

    public static List<Vector2Int> GetAvailableOrthogonalCells(
        Vector2Int reference,
        int width,
        int height,
        Func<Vector2Int, bool> isAvailable)
    {
        List<Vector2Int> result = new List<Vector2Int>();
        if (width <= 0 || height <= 0 || isAvailable == null)
        {
            return result;
        }

        foreach (Vector2Int offset in OrthogonalOffsets)
        {
            Vector2Int candidate = reference + offset;
            bool isInside = candidate.x >= 0 && candidate.x < width &&
                            candidate.y >= 0 && candidate.y < height;
            if (isInside && isAvailable(candidate))
            {
                result.Add(candidate);
            }
        }

        return result;
    }
}

[Serializable]
public sealed class TutorialSodaAmount
{
    [SerializeField] private Soda.SodaColor color;
    [SerializeField, Min(0)] private int count = 1;

    public Soda.SodaColor Color => color;
    public int Count => Mathf.Max(0, count);
}

[Serializable]
public sealed class TutorialBoxRecipe
{
    [SerializeField] private List<TutorialSodaAmount> sodas = new List<TutorialSodaAmount>();

    public Dictionary<Soda.SodaColor, int> ToDictionary()
    {
        Dictionary<Soda.SodaColor, int> result = new Dictionary<Soda.SodaColor, int>();
        foreach (TutorialSodaAmount amount in sodas)
        {
            if (amount == null || amount.Count <= 0)
            {
                continue;
            }

            if (!result.ContainsKey(amount.Color))
            {
                result.Add(amount.Color, 0);
            }

            result[amount.Color] += amount.Count;
        }

        return result;
    }
}

[Serializable]
public sealed class SortingTutorialStageConfig
{
    [SerializeField] private string stageName = "Tutorial Stage";
    [SerializeField] private bool createReferenceBox;
    [SerializeField] private Vector2Int referenceCell = new Vector2Int(1, 2);
    [SerializeField] private TutorialBoxRecipe referenceBox = new TutorialBoxRecipe();
    [SerializeField] private List<TutorialBoxRecipe> railBoxes = new List<TutorialBoxRecipe>();
    [SerializeField, Min(0)] private int requiredPackedMatches = 1;
    [SerializeField] private bool requireEmptyBoard = true;
    [SerializeField] private Vector2Int initialTargetCell = new Vector2Int(1, 2);

    public string StageName => string.IsNullOrWhiteSpace(stageName) ? "Tutorial Stage" : stageName;
    public bool CreateReferenceBox => createReferenceBox;
    public Vector2Int ReferenceCell => referenceCell;
    public TutorialBoxRecipe ReferenceBox => referenceBox;
    public IReadOnlyList<TutorialBoxRecipe> RailBoxes => railBoxes;
    public int RequiredPackedMatches => requiredPackedMatches;
    public bool RequireEmptyBoard => requireEmptyBoard;
    public Vector2Int InitialTargetCell => initialTargetCell;
}

/// <summary>
/// Feature-specific controller for the game's initial two-stage soda-sorting
/// Tutorial. Progress is stable and never depends on the mutable live rail list.
/// </summary>
[DisallowMultipleComponent]
public sealed class SortingTutorialController : TutorialControllerBase
{
    // The second configured stage is the two-color phase. Its rail Boxes must be
    // consumed visually from left to right.
    private const int OrderedRailSelectionStageIndex = 1;

    private enum SortingTutorialState
    {
        Inactive,
        PreparingStage,
        WaitingForDestination,
        WaitingForPlacement,
        WaitingForResolution,
        TransitioningStage,
        Completing
    }

    [Header("Sorting Tutorial References")]
    [SerializeField] private Board board;
    [SerializeField] private SpawnContoller spawnController;
    [SerializeField] private HandAnimation handAnimation;
    [SerializeField] private ToolTipTutorial toolTipTutorial;

    [Header("Adaptive Hand Destination")]
    [SerializeField] private List<TutorialBoardDirection> directionPriority =
        new List<TutorialBoardDirection>
        {
            TutorialBoardDirection.Right,
            TutorialBoardDirection.Left,
            TutorialBoardDirection.Down,
            TutorialBoardDirection.Up
        };
    [SerializeField, Min(0.05f)] private float destinationRetryDelay = 0.25f;

    [Header("Two-Stage Sorting Content")]
    [SerializeField] private List<SortingTutorialStageConfig> stages =
        new List<SortingTutorialStageConfig>();

    private readonly List<Box> stageBoxes = new List<Box>();
    private readonly HashSet<int> stageSourceIds = new HashSet<int>();
    private readonly HashSet<int> acceptedSourceIds = new HashSet<int>();
    private readonly HashSet<long> handledPlacementOrders = new HashSet<long>();
    private readonly HashSet<Vector2Int> highlightedCells = new HashSet<Vector2Int>();
    private readonly List<Node> highlightedNodes = new List<Node>();

    private Coroutine tutorialRoutine;
    private SortingTutorialState state;
    private int currentStageIndex = -1;
    private int packedMatchesThisStage;
    private Box currentSourceBox;
    private Box lastSuccessfullyPlacedBox;
    private Box activeReferenceBox;
    private Box placedBoxAwaitingResolution;
    private Vector3 currentTargetWorld;
    private long pendingPlacementOrder = -1;
    private bool placementAcceptedThisStep;
    private bool resolutionFinishedThisStep;

    public string CurrentState => state.ToString();
    public int CurrentStageNumber => currentStageIndex + 1;
    public IReadOnlyCollection<Vector2Int> HighlightedCells => highlightedCells;

    protected override bool ValidateTutorial(out string reason)
    {
        if (board == null || spawnController == null || handAnimation == null || toolTipTutorial == null)
        {
            reason = "Board, Spawn Controller, Hand Animation, and ToolTip Tutorial must all be assigned.";
            return false;
        }

        if (board.grid == null || board.allBoxes == null)
        {
            reason = "The Board has not finished its scene initialization.";
            return false;
        }

        if (stages == null || stages.Count < 2 || stages.Any(stage => stage == null))
        {
            reason = "At least two non-null sorting stage configurations are required.";
            return false;
        }

        if (directionPriority == null || directionPriority.Count == 0)
        {
            reason = "The Hand destination direction-priority list is empty.";
            return false;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            SortingTutorialStageConfig stage = stages[i];
            if (stage.RailBoxes == null || stage.RailBoxes.Count == 0 ||
                stage.RailBoxes.Any(recipe => recipe == null || recipe.ToDictionary().Count == 0))
            {
                reason = $"{stage.StageName} contains an empty or missing rail recipe.";
                return false;
            }

            if (stage.CreateReferenceBox &&
                (stage.ReferenceBox == null || stage.ReferenceBox.ToDictionary().Count == 0))
            {
                reason = $"{stage.StageName} requires a valid reference-box recipe.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    protected override void OnTutorialStarted()
    {
        if (!TutorialManager.Instance.TryAcquireHand(this, handAnimation))
        {
            RequestCancellation("The reusable Hand is already owned or could not be acquired.");
            return;
        }

        if (!board.TrySetPlacementConstraint(this, IsHighlightedTutorialPlacement))
        {
            RequestCancellation("The Board placement constraint is already owned by another system.");
            return;
        }

        board.PlacementAccepted += HandlePlacementAccepted;
        board.PlacementResolutionCompleted += HandlePlacementResolutionCompleted;
        toolTipTutorial.ResetPresentation();
        state = SortingTutorialState.PreparingStage;
        tutorialRoutine = StartCoroutine(RunTutorial());
    }

    protected override void OnTutorialCompleted()
    {
        CleanupRuntimeState();
        toolTipTutorial?.ShowCompletionPanel();
    }

    protected override void OnTutorialCancelled(string reason)
    {
        CleanupRuntimeState();
        toolTipTutorial?.HideStepPresentation();
    }

    private IEnumerator RunTutorial()
    {
        for (int stageIndex = 0; stageIndex < stages.Count; stageIndex++)
        {
            currentStageIndex = stageIndex;
            state = SortingTutorialState.PreparingStage;

            if (!PrepareStage(stages[stageIndex], out string preparationError))
            {
                CancelFromRoutine(preparationError);
                yield break;
            }

            if (stages[stageIndex].CreateReferenceBox)
            {
                yield return CreateReferenceBox(stages[stageIndex]);
                if (!IsRunning)
                {
                    yield break;
                }
            }

            yield return SpawnStageRailBoxes(stages[stageIndex]);
            if (!IsRunning || stageBoxes.Count == 0)
            {
                if (IsRunning)
                {
                    CancelFromRoutine($"{stages[stageIndex].StageName} did not produce any rail boxes.");
                }

                yield break;
            }

            while (acceptedSourceIds.Count < stageSourceIds.Count)
            {
                currentSourceBox = FindNextUnplacedStageBox();
                if (currentSourceBox == null)
                {
                    CancelFromRoutine(
                        $"{stages[stageIndex].StageName} lost an unplaced Tutorial box before Board acceptance.");
                    yield break;
                }

                state = SortingTutorialState.WaitingForDestination;
                while (!TryRefreshHighlightsAndDestination(
                           stages[stageIndex],
                           currentSourceBox,
                           out currentTargetWorld))
                {
                    handAnimation.Stop(this);
                    toolTipTutorial.HideStepPresentation();
                    yield return new WaitForSeconds(destinationRetryDelay);

                    if (!IsRunning)
                    {
                        yield break;
                    }
                }

                placementAcceptedThisStep = false;
                resolutionFinishedThisStep = false;
                pendingPlacementOrder = -1;
                state = SortingTutorialState.WaitingForPlacement;

                bool firstMoveInStage = acceptedSourceIds.Count == 0;
                toolTipTutorial.ShowMovePrompt(firstMoveInStage);
                if (!handAnimation.Play(
                        this,
                        currentSourceBox.transform.position,
                        currentTargetWorld))
                {
                    CancelFromRoutine("The Hand could not present the current source and target.");
                    yield break;
                }

                yield return new WaitUntil(() => !IsRunning || placementAcceptedThisStep);
                if (!IsRunning)
                {
                    yield break;
                }

                state = SortingTutorialState.WaitingForResolution;
                yield return new WaitUntil(() => !IsRunning || resolutionFinishedThisStep);
                if (!IsRunning)
                {
                    yield break;
                }
            }

            UnsubscribeFromStageBoxes();
            SortingTutorialStageConfig completedStage = stages[stageIndex];
            if (packedMatchesThisStage < completedStage.RequiredPackedMatches)
            {
                CancelFromRoutine(
                    $"{completedStage.StageName} resolved only {packedMatchesThisStage} packed match(es); " +
                    $"{completedStage.RequiredPackedMatches} were required.");
                yield break;
            }

            if (completedStage.RequireEmptyBoard && !board.IsBoardEmpty())
            {
                CancelFromRoutine($"{completedStage.StageName} finished its placements, but the Board is not empty.");
                yield break;
            }

            state = SortingTutorialState.TransitioningStage;
            ClearTutorialHighlights();
            handAnimation.Stop(this);
            toolTipTutorial.HideStepPresentation();
            yield return null;
        }

        state = SortingTutorialState.Completing;
        tutorialRoutine = null;
        RequestCompletion();
    }

    private bool PrepareStage(SortingTutorialStageConfig stage, out string error)
    {
        UnsubscribeFromStageBoxes();
        stageBoxes.Clear();
        stageSourceIds.Clear();
        acceptedSourceIds.Clear();
        handledPlacementOrders.Clear();
        packedMatchesThisStage = 0;
        currentSourceBox = null;
        lastSuccessfullyPlacedBox = null;
        activeReferenceBox = null;
        placedBoxAwaitingResolution = null;
        placementAcceptedThisStep = false;
        resolutionFinishedThisStep = false;
        pendingPlacementOrder = -1;

        spawnController.spawnedBoxes.RemoveAll(item => item == null);
        if (spawnController.spawnedBoxes.Count > 0)
        {
            error = $"{stage.StageName} cannot begin while the rail still contains live boxes.";
            return false;
        }

        if (stage.CreateReferenceBox && !board.IsBoardEmpty())
        {
            error = $"{stage.StageName} requires an empty Board before its reference box is created.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private IEnumerator CreateReferenceBox(SortingTutorialStageConfig stage)
    {
        Vector2Int cell = stage.ReferenceCell;
        if (!board.CanPlaceAt(cell.x, cell.y))
        {
            CancelFromRoutine($"{stage.StageName} reference cell {cell} is not available.");
            yield break;
        }

        Box reference = spawnController.CreateConfiguredBox(
            board.GetPlacementWorldPosition(cell.x, cell.y),
            stage.ReferenceBox.ToDictionary());
        if (reference == null || !board.TryPlaceBox(reference, cell.x, cell.y))
        {
            if (reference != null)
            {
                Destroy(reference.gameObject);
            }

            CancelFromRoutine($"{stage.StageName} reference box could not be placed.");
            yield break;
        }

        lastSuccessfullyPlacedBox = reference;
        activeReferenceBox = reference;
        yield return new WaitUntil(() => !IsRunning || !board.IsResolving);
    }

    private IEnumerator SpawnStageRailBoxes(SortingTutorialStageConfig stage)
    {
        List<Dictionary<Soda.SodaColor, int>> recipes = stage.RailBoxes
            .Select(recipe => recipe.ToDictionary())
            .ToList();
        List<Box> spawned = null;

        yield return spawnController.SpawnTutorialBatch(recipes, result => spawned = result);
        if (!IsRunning || spawned == null)
        {
            yield break;
        }

        foreach (Box box in spawned)
        {
            if (box == null)
            {
                continue;
            }

            stageBoxes.Add(box);
            stageSourceIds.Add(box.GetInstanceID());
        }

        bool requiresOrderedSelection =
            currentStageIndex == OrderedRailSelectionStageIndex;
        if (requiresOrderedSelection)
        {
            // World X matches screen-left to screen-right for the Tutorial camera.
            // Sorting spatially keeps the rule correct even if spawn arrays change.
            stageBoxes.Sort((left, right) =>
                left.transform.position.x.CompareTo(right.transform.position.x));
        }

        foreach (Box box in stageBoxes)
        {
            box.DragStarted += HandleStageBoxDragStarted;
            box.DropRejectedOrCancelled += HandleStageBoxDropRejected;

            if (requiresOrderedSelection &&
                !box.TrySetDragConstraint(this, CanBeginOrderedStageDrag))
            {
                CancelFromRoutine(
                    $"{stage.StageName} could not reserve ordered drag input for '{box.name}'.");
                yield break;
            }
        }
    }

    private bool CanBeginOrderedStageDrag(Box box)
    {
        return IsRunning &&
               currentStageIndex == OrderedRailSelectionStageIndex &&
               state == SortingTutorialState.WaitingForPlacement &&
               box != null &&
               box == currentSourceBox &&
               !acceptedSourceIds.Contains(box.GetInstanceID());
    }

    private void HandlePlacementAccepted(BoardPlacementAcceptedEvent placement)
    {
        if (!IsRunning || placement.Box == null)
        {
            return;
        }

        int sourceId = placement.Box.GetInstanceID();
        if (!stageSourceIds.Contains(sourceId) || acceptedSourceIds.Contains(sourceId) ||
            !handledPlacementOrders.Add(placement.PlacementOrder))
        {
            return;
        }

        acceptedSourceIds.Add(sourceId);
        currentSourceBox = placement.Box;
        lastSuccessfullyPlacedBox = placement.Box;
        placedBoxAwaitingResolution = placement.Box;
        pendingPlacementOrder = placement.PlacementOrder;
        placementAcceptedThisStep = true;
        resolutionFinishedThisStep = false;

        ClearTutorialHighlights();
        handAnimation.Stop(this);
        toolTipTutorial.ShowPlacementComplete();
    }

    private void HandlePlacementResolutionCompleted(BoardPlacementResolutionEvent resolution)
    {
        if (!IsRunning || pendingPlacementOrder < 0 ||
            resolution.PlacementOrder != pendingPlacementOrder)
        {
            return;
        }

        packedMatchesThisStage += resolution.PackedMatches;
        activeReferenceBox = SelectSurvivingTargetPack(FindNextUnplacedStageBox());
        placedBoxAwaitingResolution = null;
        pendingPlacementOrder = -1;
        resolutionFinishedThisStep = true;
    }

    private void HandleStageBoxDragStarted(Box box)
    {
        if (!IsRunning || box == null || acceptedSourceIds.Contains(box.GetInstanceID()))
        {
            return;
        }

        handAnimation.Stop(this);
        toolTipTutorial.HideStepPresentation();
    }

    private void HandleStageBoxDropRejected(Box box)
    {
        if (!IsRunning || placementAcceptedThisStep || currentSourceBox == null)
        {
            return;
        }

        RestoreTutorialHighlights();
        toolTipTutorial.ShowMovePrompt(acceptedSourceIds.Count == 0);
        handAnimation.Restart(this, destinationRetryDelay);
    }

    private Box FindNextUnplacedStageBox()
    {
        return stageBoxes.FirstOrDefault(box =>
            box != null &&
            !box.IsRetired &&
            !acceptedSourceIds.Contains(box.GetInstanceID()));
    }

    private bool TryRefreshHighlightsAndDestination(
        SortingTutorialStageConfig stage,
        Box nextSource,
        out Vector3 targetWorld)
    {
        ClearTutorialHighlights();
        targetWorld = Vector3.zero;
        activeReferenceBox = SelectSurvivingTargetPack(nextSource);

        if (activeReferenceBox != null)
        {
            Vector2Int referenceCell =
                new Vector2Int(activeReferenceBox.column, activeReferenceBox.row);
            List<Vector2Int> available =
                TutorialAdjacentCellUtility.GetAvailableOrthogonalCells(
                    referenceCell,
                    board.Width,
                    board.Height,
                    cell => board.CanPlaceAt(cell.x, cell.y));

            foreach (Vector2Int cell in available)
            {
                AddTutorialHighlight(cell);
            }

            foreach (TutorialBoardDirection direction in directionPriority)
            {
                Vector2Int offset = DirectionToOffset(direction);
                Vector2Int candidate = referenceCell + offset;
                if (highlightedCells.Contains(candidate))
                {
                    targetWorld = board.GetPlacementWorldPosition(candidate.x, candidate.y);
                    return true;
                }
            }

            if (highlightedCells.Count > 0)
            {
                Vector2Int fallback = highlightedCells.First();
                targetWorld = board.GetPlacementWorldPosition(fallback.x, fallback.y);
                return true;
            }

            return false;
        }

        Vector2Int initial = stage.InitialTargetCell;
        if (board.CanPlaceAt(initial.x, initial.y))
        {
            AddTutorialHighlight(initial);
            targetWorld = board.GetPlacementWorldPosition(initial.x, initial.y);
            return true;
        }

        return false;
    }

    private bool IsHighlightedTutorialPlacement(Box box, int column, int row)
    {
        if (!IsRunning || box == null)
        {
            return false;
        }

        int sourceId = box.GetInstanceID();
        if (!stageSourceIds.Contains(sourceId))
        {
            // The only non-stage placement performed by this controller is the
            // runtime reference Box while the stage is being prepared.
            return state == SortingTutorialState.PreparingStage && stageSourceIds.Count == 0;
        }

        return state == SortingTutorialState.WaitingForPlacement &&
               !acceptedSourceIds.Contains(sourceId) &&
               highlightedCells.Contains(new Vector2Int(column, row));
    }

    private Box SelectSurvivingTargetPack(Box nextSource)
    {
        if (board == null || board.boardBoxes == null)
        {
            return null;
        }

        List<Box> activePacks = board.boardBoxes
            .Where(box => IsActiveBoardBox(box) && !box.IsEmpty)
            .ToList();
        if (activePacks.Count == 0)
        {
            return null;
        }

        return activePacks
            .OrderByDescending(box => GetSharedColorUsefulness(box, nextSource))
            .ThenByDescending(box => box.SodaCount)
            .ThenByDescending(box => box == activeReferenceBox ? 1 : 0)
            .ThenByDescending(box => box == placedBoxAwaitingResolution ? 1 : 0)
            .ThenByDescending(box => box.PlacementOrder)
            .First();
    }

    private static int GetSharedColorUsefulness(Box pack, Box nextSource)
    {
        if (pack == null || nextSource == null)
        {
            return 0;
        }

        pack.RefreshContents();
        nextSource.RefreshContents();
        Dictionary<Soda.SodaColor, int> packColors = pack.GetSodaColorCounts();
        Dictionary<Soda.SodaColor, int> sourceColors = nextSource.GetSodaColorCounts();
        int score = 0;

        foreach (KeyValuePair<Soda.SodaColor, int> sourceColor in sourceColors)
        {
            if (!packColors.TryGetValue(sourceColor.Key, out int existingCount))
            {
                continue;
            }

            int freeForColor = Mathf.Max(0, pack.Capacity - existingCount);
            score += Mathf.Min(sourceColor.Value, freeForColor) * 10 + existingCount;
        }

        return score;
    }

    private void AddTutorialHighlight(Vector2Int cell)
    {
        if (!board.IsInside(cell.x, cell.y) || board.grid == null)
        {
            return;
        }

        Node node = board.grid[cell.x, cell.y];
        if (node == null || !highlightedCells.Add(cell))
        {
            return;
        }

        highlightedNodes.Add(node);
        node.Highlight();
    }

    private void RestoreTutorialHighlights()
    {
        foreach (Node node in highlightedNodes)
        {
            if (node != null)
            {
                node.Highlight();
            }
        }
    }

    private void ClearTutorialHighlights()
    {
        foreach (Node node in highlightedNodes)
        {
            if (node != null)
            {
                node.Unhighlight();
            }
        }

        highlightedNodes.Clear();
        highlightedCells.Clear();
    }

    private bool IsActiveBoardBox(Box box)
    {
        if (box == null || box.IsRetired || board.allBoxes == null)
        {
            return false;
        }

        int column = box.column;
        int row = box.row;
        return column >= 0 && column < board.Width && row >= 0 && row < board.Height &&
               board.allBoxes[column, row] == box;
    }

    private static Vector2Int DirectionToOffset(TutorialBoardDirection direction)
    {
        switch (direction)
        {
            case TutorialBoardDirection.Left:
                return Vector2Int.left;
            case TutorialBoardDirection.Down:
                return Vector2Int.down;
            case TutorialBoardDirection.Up:
                return Vector2Int.up;
            default:
                return Vector2Int.right;
        }
    }

    private void UnsubscribeFromStageBoxes()
    {
        foreach (Box box in stageBoxes)
        {
            if (box == null)
            {
                continue;
            }

            box.DragStarted -= HandleStageBoxDragStarted;
            box.DropRejectedOrCancelled -= HandleStageBoxDropRejected;
            box.ClearDragConstraint(this);
        }
    }

    private void CleanupRuntimeState()
    {
        if (tutorialRoutine != null)
        {
            StopCoroutine(tutorialRoutine);
            tutorialRoutine = null;
        }

        if (board != null)
        {
            board.PlacementAccepted -= HandlePlacementAccepted;
            board.PlacementResolutionCompleted -= HandlePlacementResolutionCompleted;
            board.ClearPlacementConstraint(this);
        }

        ClearTutorialHighlights();
        UnsubscribeFromStageBoxes();
        if (handAnimation != null)
        {
            handAnimation.Stop(this);
        }

        if (TutorialManager.HasInstance)
        {
            TutorialManager.Instance.ReleaseHand(this);
        }

        stageBoxes.Clear();
        stageSourceIds.Clear();
        acceptedSourceIds.Clear();
        handledPlacementOrders.Clear();
        currentSourceBox = null;
        lastSuccessfullyPlacedBox = null;
        activeReferenceBox = null;
        placedBoxAwaitingResolution = null;
        state = SortingTutorialState.Inactive;
    }

    private void CancelFromRoutine(string reason)
    {
        tutorialRoutine = null;
        RequestCancellation(reason);
    }
}
