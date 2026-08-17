using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns live rail boxes and rail replenishment. Tutorial progression is handled
/// elsewhere; this component only provides deterministic spawn services to it.
/// </summary>
[DisallowMultipleComponent]
public class SpawnContoller : MonoBehaviour
{
    /// <summary>Highest distinct-color count the mix chances below can express.</summary>
    private const int MaxSupportedColorMix = 3;

    public static SpawnContoller instance;

    [SerializeField] private List<Transform> spawnPositions;
    [SerializeField] private Transform[] startPos;
    [SerializeField] private Transform[] endPos;

    /// <summary>
    /// Where the rail's boxes come to rest. Exposed so anything that has to line
    /// up with the rail can read its real positions rather than guessing a world
    /// offset - the first Defuser dock was a hand-written offset from the board
    /// origin and landed off screen on the portrait camera.
    /// </summary>
    public IReadOnlyList<Transform> RailRestPositions => endPos;

    /// <summary>
    /// Asked, once per batch, which slots something other than a Box wants.
    ///
    /// This is how Defusers arrive on the rail instead of at a dock of their own.
    /// A claimed slot does NOT draw from the authored queue - the cursor only
    /// advances for boxes - so a level's supply is delayed by a batch rather than
    /// reduced, and the queue is still consumed in exactly the authored order,
    /// which is what lets the headless solver model the rail as a flat index.
    /// </summary>
    public System.Func<int, List<int>> RailSlotClaim { get; set; }

    /// <summary>
    /// Builds whatever occupies a claimed slot, at the given spawn position.
    /// Returning null leaves the slot empty and the batch simply runs short.
    /// </summary>
    public System.Func<int, Vector3, GameObject> RailSlotFactory { get; set; }
    [SerializeField, Min(0f)] private float spawnDelay = 0.03f;
    [SerializeField, Min(0.01f)] private float moveDuration = 0.5f;

    [Header("Per-Level Rail Batch")]
    [SerializeField, Min(1)]
    [Tooltip("How many boxes this level spawns per rail batch. Clamped by the number of Start/End rail Transforms.")]
    private int maxBoxCount = 6;

    [Header("Per-Level Rail Source")]
    [SerializeField, Tooltip("Authored Queue makes the level a designed puzzle and lets the solver verify it. Random Per Batch is the original behaviour and stays the default for legacy scenes.")]
    private RailMode railMode = RailMode.RandomPerBatch;

    [SerializeField, Tooltip("Boxes delivered to the rail in order, used only in Authored Queue mode. Baked from the level's LevelDefinition asset.")]
    private List<TutorialBoxRecipe> railQueue = new List<TutorialBoxRecipe>();

    [SerializeField, Tooltip("What happens once the authored queue is spent.")]
    private RailExhaustionPolicy railExhaustionPolicy = RailExhaustionPolicy.RandomFallback;

    [SerializeField, Tooltip("Seed for the random fallback, so an exhausted queue still replays identically. 0 uses an unseeded roll.")]
    private int fallbackSeed;

    [Header("Per-Level Soda Palette")]
    [Tooltip("Colors that may spawn on this level's rail boxes. Leave empty to allow every Soda.SodaColor.")]
    [SerializeField] private List<Soda.SodaColor> allowedColors = new List<Soda.SodaColor>();

    [SerializeField, Min(1f), Tooltip("How much more likely a colour an unfinished order still wants is to be picked, versus a colour no order needs. 1 = no bias.")]
    private float orderColorWeight = 2f;

    [Header("Per-Level Box Fill")]
    [SerializeField, Min(1)]
    [Tooltip("Minimum sodas placed in a spawned box. A rail box is never spawned empty.")]
    private int minSodasPerBox = 1;

    [SerializeField, Min(1)]
    [Tooltip("Maximum sodas placed in a spawned box. Always clamped to (box capacity - 1) so a rail box is never spawned full.")]
    private int maxSodasPerBox = 3;

    [SerializeField, Min(1)]
    [Tooltip("Minimum number of distinct colors inside a spawned box. Ignored while Use Color Mix Chances is on.")]
    private int minDistinctColorsPerBox = 1;

    [SerializeField, Min(1)]
    [Tooltip("Maximum number of distinct colors inside a spawned box. 1 = single-color levels, 2 or 3 = mixed / hardest levels. Ignored while Use Color Mix Chances is on.")]
    private int maxDistinctColorsPerBox = 1;

    [Header("Per-Level Color Mix Chance")]
    [Tooltip("When on, the number of distinct colors is drawn from the chances below instead of uniformly from the Min/Max range. The Min/Max range still acts as a hard clamp.")]
    [SerializeField] private bool useColorMixChances = false;

    [SerializeField, Min(0f)]
    [Tooltip("Chance in percent of spawning a single-color box. The three chances should add up to 100.")]
    private float singleColorChance = 100f;

    [SerializeField, Min(0f)]
    [Tooltip("Chance in percent of spawning a box with exactly 2 distinct colors.")]
    private float twoColorChance = 0f;

    [SerializeField, Min(0f)]
    [Tooltip("Chance in percent of spawning a box with exactly 3 distinct colors.")]
    private float threeColorChance = 0f;

    public float speed = 1f;
    public GameObject boxPrefab;
    public GameObject sodaPrefab;
    public float respawnDelay = 2f;
    public List<GameObject> spawnedBoxes = new List<GameObject>();
    public bool stopSpawn;
    private Coroutine spawnRoutine;
    public bool isTutorialState;

    private int railQueueCursor;
    private int fallbackDrawCount;

    /// <summary>
    /// True once an authored queue has been fully consumed on a level that does
    /// not fall back. Board.CheckBoardFill reads this to end a level whose
    /// material has run out, which is otherwise an unloseable stalemate.
    /// </summary>
    public bool IsQueueExhausted =>
        railMode == RailMode.AuthoredQueue &&
        railExhaustionPolicy == RailExhaustionPolicy.StopSpawning &&
        railQueueCursor >= (railQueue != null ? railQueue.Count : 0);

    /// <summary>Recipes not yet delivered. Used by the level tooling.</summary>
    public int RemainingQueuedBoxes =>
        railMode == RailMode.AuthoredQueue && railQueue != null
            ? Mathf.Max(0, railQueue.Count - railQueueCursor)
            : 0;

    public RailMode RailMode => railMode;
    public IReadOnlyList<TutorialBoxRecipe> RailQueue => railQueue;
    public RailExhaustionPolicy RailExhaustionPolicy => railExhaustionPolicy;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("More than one SpawnContoller exists in the scene.", this);
        }

        instance = this;
        isTutorialState = SceneManager.GetActiveScene().name == "TUTORIAL";
    }

    private void Start()
    {
        if (!isTutorialState)
        {
            spawnRoutine = StartCoroutine(LevelSpawnRoutine());
        }
    }

    /// <summary>
    /// Restarts rail replenishment after a revive. LevelSpawnRoutine's while loop
    /// exits for good once stopSpawn or gameEnded goes true, so clearing those
    /// flags is not enough to resume spawning; the routine has to start again.
    /// </summary>
    public void RestartSpawning()
    {
        if (isTutorialState)
        {
            return;
        }

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
        }

        spawnRoutine = StartCoroutine(LevelSpawnRoutine());
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// Spawns one deterministic Tutorial batch, animates it onto the rail, and
    /// returns the stable Box instances. spawnedBoxes remains a live rail list.
    /// </summary>
    public IEnumerator SpawnTutorialBatch(
        IReadOnlyList<Dictionary<Soda.SodaColor, int>> recipes,
        Action<List<Box>> onCompleted)
    {
        CleanupLiveRailList();

        List<Box> result = new List<Box>();
        if (!ValidateSpawnConfiguration(recipes != null ? recipes.Count : 0, out string reason))
        {
            Debug.LogWarning($"Tutorial rail batch was not spawned: {reason}", this);
            onCompleted?.Invoke(result);
            yield break;
        }

        if (spawnedBoxes.Count > 0)
        {
            Debug.LogWarning("Tutorial rail batch requested while live rail boxes still exist.", this);
            onCompleted?.Invoke(result);
            yield break;
        }

        yield return SpawnBatchInternal(recipes, onCompleted);
    }

    /// <summary>
    /// Spawns one batch of exactly-specified boxes and animates them onto the
    /// rail. Shared by the tutorial batches and the authored level queue, which
    /// differ only in where their recipes come from and in the guards they apply
    /// before getting here.
    /// </summary>
    private IEnumerator SpawnBatchInternal(
        IReadOnlyList<Dictionary<Soda.SodaColor, int>> recipes,
        Action<List<Box>> onCompleted,
        IReadOnlyList<int> claimedSlots = null)
    {
        List<Box> result = new List<Box>();
        List<GameObject> arrivals = new List<GameObject>();
        List<int> arrivalSlots = new List<int>();

        // A set, not List.Contains: on IReadOnlyList<int> that binds to the
        // MemoryExtensions span overload rather than the one you expect.
        HashSet<int> claimed = null;
        if (claimedSlots != null && claimedSlots.Count > 0)
        {
            claimed = new HashSet<int>();
            for (int i = 0; i < claimedSlots.Count; i++)
            {
                claimed.Add(claimedSlots[i]);
            }
        }

        int slotCount = recipes.Count + (claimed != null ? claimed.Count : 0);
        int recipeCursor = 0;

        for (int slot = 0; slot < slotCount; slot++)
        {
            Vector3 spawnWorld = startPos[slot].position + new Vector3(0f, 0.2965f, 0f);

            if (claimed != null && claimed.Contains(slot))
            {
                GameObject claimedItem = RailSlotFactory?.Invoke(slot, spawnWorld);
                if (claimedItem != null)
                {
                    // Claimed items join the live rail list like any box. That is
                    // safe because the prune asks IRailItem.IsConsumed rather than
                    // "is this a Box" - the test that used to make an unused
                    // Defuser pin the rail forever.
                    spawnedBoxes.Add(claimedItem);
                    arrivals.Add(claimedItem);
                    arrivalSlots.Add(slot);
                }
            }
            else if (recipeCursor < recipes.Count)
            {
                Box box = CreateConfiguredBox(spawnWorld, recipes[recipeCursor++], true);
                if (box == null)
                {
                    Debug.LogWarning($"Configured rail box {slot} could not be created.", this);
                    onCompleted?.Invoke(new List<Box>());
                    yield break;
                }

                result.Add(box);
                arrivals.Add(box.gameObject);
                arrivalSlots.Add(slot);
            }

            SoundManager.instance?.PlayBoxSpawn();

            if (spawnDelay > 0f)
            {
                yield return new WaitForSeconds(spawnDelay);
            }
        }

        List<Coroutine> movement = new List<Coroutine>();
        for (int i = 0; i < arrivals.Count; i++)
        {
            movement.Add(StartCoroutine(MoveBox(arrivals[i], endPos[arrivalSlots[i]].position)));
        }

        foreach (Coroutine coroutine in movement)
        {
            yield return coroutine;
        }

        onCompleted?.Invoke(result);
    }

    /// <summary>Creates a Box with exact soda counts for a Tutorial setup.</summary>
    public Box CreateConfiguredBox(
        Vector3 worldPosition,
        IReadOnlyDictionary<Soda.SodaColor, int> recipe,
        bool addToRailList = false)
    {
        if (boxPrefab == null || sodaPrefab == null)
        {
            Debug.LogWarning("Box Prefab and Soda Prefab must be assigned.", this);
            return null;
        }

        GameObject boxObject = Instantiate(
            boxPrefab,
            worldPosition,
            Quaternion.Euler(-90f, 0f, 0f));
        Box box = boxObject.GetComponent<Box>();
        if (box == null)
        {
            Debug.LogWarning("The configured Box Prefab does not contain Box.", boxObject);
            Destroy(boxObject);
            return null;
        }

        if (!PopulateConfiguredSodas(box, recipe))
        {
            Destroy(boxObject);
            return null;
        }

        box.RefreshContents();
        if (addToRailList)
        {
            spawnedBoxes.Add(boxObject);
        }

        return box;
    }

    private IEnumerator LevelSpawnRoutine()
    {
        HandAnimation hand = FindFirstObjectByType<HandAnimation>();
        if (hand != null)
        {
            hand.HideHande();
            hand.DeactivateAnimation();
        }

        yield return new WaitForSeconds(0.1f);

        while (!stopSpawn && !IsGameEnded())
        {
            yield return SpawnRailBatch();
            yield return new WaitUntil(() => stopSpawn || IsGameEnded() || NoBoxInList());

            if (!stopSpawn && !IsGameEnded() && respawnDelay > 0f)
            {
                yield return new WaitForSeconds(respawnDelay);
            }
        }
    }

    private IEnumerator SpawnRailBatch()
    {
        CleanupLiveRailList();
        if (stopSpawn || IsGameEnded() || spawnedBoxes.Count > 0)
        {
            yield break;
        }

        int count = Mathf.Min(maxBoxCount, startPos != null ? startPos.Length : 0,
            endPos != null ? endPos.Length : 0);
        if (!ValidateSpawnConfiguration(count, out string reason))
        {
            Debug.LogWarning($"Rail batch was not spawned: {reason}", this);
            yield break;
        }

        if (railMode == RailMode.AuthoredQueue)
        {
            // Claims come first: a slot taken by a Defuser must not pull a recipe
            // off the queue, or that box would be lost rather than delayed.
            List<int> claimed = RailSlotClaim != null ? RailSlotClaim(count) : null;
            int boxSlots = count - (claimed != null ? claimed.Count : 0);

            List<Dictionary<Soda.SodaColor, int>> queued = boxSlots > 0
                ? DrawFromQueue(boxSlots)
                : new List<Dictionary<Soda.SodaColor, int>>();

            if (queued.Count == 0 && (claimed == null || claimed.Count == 0))
            {
                // Queue spent under StopSpawning. LevelSpawnRoutine's WaitUntil
                // then parks on an empty rail, and Board.CheckBoardFill ends the
                // level rather than leaving an unloseable stalemate.
                yield break;
            }

            yield return SpawnBatchInternal(queued, null, claimed);
            yield break;
        }

        List<GameObject> batch = new List<GameObject>();
        for (int i = 0; i < count; i++)
        {
            GameObject boxObject = Instantiate(
                boxPrefab,
                startPos[i].position + new Vector3(0f, 0.2965f, 0f),
                Quaternion.Euler(-90f, 0f, 0f));
            spawnedBoxes.Add(boxObject);
            batch.Add(boxObject);
            SpawnRandomSodas(boxObject);
            SoundManager.instance?.PlayBoxSpawn();

            if (spawnDelay > 0f)
            {
                yield return new WaitForSeconds(spawnDelay);
            }
        }

        List<Coroutine> movement = new List<Coroutine>();
        for (int i = 0; i < batch.Count; i++)
        {
            movement.Add(StartCoroutine(MoveBox(batch[i], endPos[i].position)));
        }

        foreach (Coroutine coroutine in movement)
        {
            yield return coroutine;
        }
    }

    /// <summary>
    /// Takes the next batch of authored recipes off the queue and applies the
    /// exhaustion policy when the queue cannot fill it.
    ///
    /// Because the rail only ever refills once it is completely empty, the cursor
    /// advances in lockstep with play and the queue is consumed in exactly the
    /// authored order. That is what lets the headless solver model the rail as a
    /// flat index rather than having to predict a spawner.
    /// </summary>
    private List<Dictionary<Soda.SodaColor, int>> DrawFromQueue(int count)
    {
        List<Dictionary<Soda.SodaColor, int>> batch = new List<Dictionary<Soda.SodaColor, int>>();
        int queueLength = railQueue != null ? railQueue.Count : 0;

        while (batch.Count < count && queueLength > 0 && railQueueCursor < queueLength)
        {
            TutorialBoxRecipe recipe = railQueue[railQueueCursor];
            railQueueCursor++;

            // A malformed empty recipe would spawn an empty box that instantly
            // retires on placement, so it is skipped rather than shipped. The
            // level validator reports these at authoring time.
            if (recipe == null || recipe.TotalCount <= 0)
            {
                continue;
            }

            batch.Add(recipe.ToDictionary());
        }

        if (batch.Count >= count)
        {
            return batch;
        }

        switch (railExhaustionPolicy)
        {
            case RailExhaustionPolicy.LoopQueue:
                if (queueLength > 0)
                {
                    railQueueCursor = 0;
                    while (batch.Count < count)
                    {
                        TutorialBoxRecipe recipe = railQueue[railQueueCursor];
                        railQueueCursor = (railQueueCursor + 1) % queueLength;

                        if (recipe != null && recipe.TotalCount > 0)
                        {
                            batch.Add(recipe.ToDictionary());
                        }
                        else if (AllQueuedRecipesAreEmpty())
                        {
                            // Every entry is unusable; looping would spin forever.
                            break;
                        }
                    }
                }

                break;

            case RailExhaustionPolicy.RandomFallback:
                int capacity = GetPrefabBoxCapacity();
                while (batch.Count < count)
                {
                    Dictionary<Soda.SodaColor, int> generated = BuildSeededFallbackRecipe(capacity);
                    if (generated == null || generated.Count == 0)
                    {
                        break;
                    }

                    batch.Add(generated);
                }

                break;

            case RailExhaustionPolicy.StopSpawning:
            default:
                // Deliberately returns a short or empty batch: the authored queue
                // is the whole of this level's material.
                break;
        }

        return batch;
    }

    private bool AllQueuedRecipesAreEmpty()
    {
        foreach (TutorialBoxRecipe recipe in railQueue)
        {
            if (recipe != null && recipe.TotalCount > 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// One fallback box, drawn from the level's normal random settings but with a
    /// per-level seed so an exhausted queue still replays identically between
    /// attempts. Levels are never designed to need this - it exists so a player
    /// who wastes the authored queue is not left staring at an empty rail.
    /// </summary>
    private Dictionary<Soda.SodaColor, int> BuildSeededFallbackRecipe(int capacity)
    {
        // BuildRandomRecipe reads UnityEngine.Random, which is global. Saving and
        // restoring the state around the call keeps the seeding local to the rail
        // instead of quietly re-seeding every other system's rolls.
        UnityEngine.Random.State previousState = default;
        bool seeded = fallbackSeed != 0;
        if (seeded)
        {
            previousState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(fallbackSeed + fallbackDrawCount);
        }

        fallbackDrawCount++;

        List<Soda.SodaColor> colors = BuildRandomRecipe(capacity);

        if (seeded)
        {
            UnityEngine.Random.state = previousState;
        }

        Dictionary<Soda.SodaColor, int> recipe = new Dictionary<Soda.SodaColor, int>();
        foreach (Soda.SodaColor color in colors)
        {
            recipe.TryGetValue(color, out int existing);
            recipe[color] = existing + 1;
        }

        return recipe;
    }

    /// <summary>
    /// Box capacity read off the prefab, so a fallback recipe can be sized before
    /// any box exists. Falls back to the project's four-slot box.
    /// </summary>
    private int GetPrefabBoxCapacity()
    {
        Box prefabBox = boxPrefab != null ? boxPrefab.GetComponent<Box>() : null;
        int capacity = prefabBox != null ? prefabBox.DiscoverableCapacity : 0;
        return capacity > 0 ? capacity : 4;
    }

    private bool PopulateConfiguredSodas(
        Box box,
        IReadOnlyDictionary<Soda.SodaColor, int> recipe)
    {
        if (box == null || recipe == null)
        {
            Debug.LogWarning("A Tutorial recipe or Box is missing.", this);
            return false;
        }

        Transform[] slots = box.GetSodaPositions();
        int requiredSlots = 0;
        foreach (KeyValuePair<Soda.SodaColor, int> entry in recipe)
        {
            requiredSlots += Mathf.Max(0, entry.Value);
        }

        if (requiredSlots > slots.Length)
        {
            Debug.LogWarning(
                $"Recipe requires {requiredSlots} sodas, but '{box.name}' has {slots.Length} slots.",
                box);
            return false;
        }

        int slotIndex = 0;
        foreach (KeyValuePair<Soda.SodaColor, int> entry in recipe)
        {
            for (int count = 0; count < Mathf.Max(0, entry.Value); count++)
            {
                SpawnSodaAtSlot(box, slots[slotIndex++], entry.Key);
            }
        }

        return true;
    }

    private void SpawnRandomSodas(GameObject boxObject)
    {
        Box box = boxObject != null ? boxObject.GetComponent<Box>() : null;
        if (box == null)
        {
            return;
        }

        Transform[] slots = box.GetSodaPositions();
        if (slots.Length == 0)
        {
            return;
        }

        List<Soda.SodaColor> recipe = BuildRandomRecipe(slots.Length);
        List<Transform> available = new List<Transform>(slots);
        for (int i = 0; i < recipe.Count; i++)
        {
            int index = UnityEngine.Random.Range(0, available.Count);
            Transform slot = available[index];
            available.RemoveAt(index);
            SpawnSodaAtSlot(box, slot, recipe[i]);
        }

        box.RefreshContents();
    }

    /// <summary>
    /// Builds one random soda list for a rail box using the per-level inspector
    /// settings. The result is never empty and never fills the box, so a spawned
    /// rail box always leaves the player at least one free slot to work with.
    /// </summary>
    private List<Soda.SodaColor> BuildRandomRecipe(int capacity)
    {
        List<Soda.SodaColor> recipe = new List<Soda.SodaColor>();
        if (capacity <= 0)
        {
            return recipe;
        }

        if (capacity < 2)
        {
            Debug.LogWarning(
                $"Box capacity is {capacity}; a rail box cannot be spawned partially filled.",
                this);
        }

        // Never full: at most capacity - 1 sodas. Never empty: at least one.
        int highestFill = Mathf.Max(1, capacity - 1);
        int maxFill = Mathf.Clamp(maxSodasPerBox, 1, highestFill);
        int minFill = Mathf.Clamp(minSodasPerBox, 1, maxFill);

        // The color count is rolled first so a level asking for 3-color boxes is
        // not silently starved by a small soda count, then the fill count is
        // rolled inside the range that can actually hold those colors.
        List<Soda.SodaColor> palette = GetLevelPalette();

        // With the mix chances on, the chances alone drive the color count; the
        // Min/Max Distinct fields are ignored so they cannot silently veto a
        // 100% three-color level. Only real limits (fill room, palette) clamp it.
        int hardCeiling = Mathf.Min(maxFill, palette.Count);
        int colorCeiling = useColorMixChances
            ? Mathf.Min(MaxSupportedColorMix, hardCeiling)
            : Mathf.Min(maxDistinctColorsPerBox, hardCeiling);
        colorCeiling = Mathf.Max(1, colorCeiling);
        int colorFloor = useColorMixChances
            ? 1
            : Mathf.Clamp(minDistinctColorsPerBox, 1, colorCeiling);
        int distinctCount = RollDistinctColorCount(colorFloor, colorCeiling);

        int sodaCount = UnityEngine.Random.Range(Mathf.Max(minFill, distinctCount), maxFill + 1);

        // Pick the distinct colors for this box without repeats, biased toward the
        // colors the active orders actually want.
        List<Soda.SodaColor> pool = new List<Soda.SodaColor>(palette);
        List<Soda.SodaColor> chosen = new List<Soda.SodaColor>();
        for (int i = 0; i < distinctCount && pool.Count > 0; i++)
        {
            int index = PickWeightedColorIndex(pool);
            chosen.Add(pool[index]);
            pool.RemoveAt(index);
        }

        // Guarantee every chosen color appears at least once, then fill the rest.
        recipe.AddRange(chosen);
        while (recipe.Count < sodaCount)
        {
            recipe.Add(chosen[UnityEngine.Random.Range(0, chosen.Count)]);
        }

        return recipe;
    }

    /// <summary>
    /// Weighted pick from the remaining palette.
    ///
    /// Once a level's rail carries more colours than its orders need, a uniform
    /// draw makes the useful colours rare: with six colours on the rail and two
    /// ordered, only a third of what arrives can advance an order, and the level
    /// turns into a wait rather than a puzzle. Ordered colours therefore get
    /// orderColorWeight times the chance of the rest - enough that progress
    /// stays steady while the distractor colours still show up often enough to
    /// occupy space and force real decisions.
    /// </summary>
    private int PickWeightedColorIndex(List<Soda.SodaColor> pool)
    {
        if (pool.Count == 1)
        {
            return 0;
        }

        float total = 0f;
        for (int index = 0; index < pool.Count; index++)
        {
            total += GetColorWeight(pool[index]);
        }

        if (total <= 0f)
        {
            return UnityEngine.Random.Range(0, pool.Count);
        }

        float roll = UnityEngine.Random.value * total;
        for (int index = 0; index < pool.Count; index++)
        {
            roll -= GetColorWeight(pool[index]);
            if (roll <= 0f)
            {
                return index;
            }
        }

        return pool.Count - 1;
    }

    private float GetColorWeight(Soda.SodaColor color)
    {
        // Only orders that still need boxes count. Weighting a colour that is
        // already fulfilled would keep flooding the rail with a drink the player
        // has no remaining use for.
        OrderManager orders = OrderManager.instance;
        if (orders == null || !orders.HasOrders)
        {
            return 1f;
        }

        OrderRuntimeState state = orders.GetOrder(color);
        bool wanted = state != null && !state.IsFulfilled;
        return wanted ? Mathf.Max(1f, orderColorWeight) : 1f;
    }

    /// <summary>
    /// Picks how many distinct colors a box gets. With the mix chances enabled the
    /// counts are drawn by weight; chances for counts outside [floor, ceiling] are
    /// dropped, and if nothing usable remains the uniform range is used instead.
    /// </summary>
    private int RollDistinctColorCount(int colorFloor, int colorCeiling)
    {
        if (!useColorMixChances)
        {
            return UnityEngine.Random.Range(colorFloor, colorCeiling + 1);
        }

        float[] chances = { singleColorChance, twoColorChance, threeColorChance };
        float total = 0f;
        for (int count = 1; count <= chances.Length; count++)
        {
            if (count >= colorFloor && count <= colorCeiling)
            {
                total += Mathf.Max(0f, chances[count - 1]);
            }
        }

        if (total <= 0f)
        {
            return UnityEngine.Random.Range(colorFloor, colorCeiling + 1);
        }

        float roll = UnityEngine.Random.value * total;
        for (int count = 1; count <= chances.Length; count++)
        {
            if (count < colorFloor || count > colorCeiling)
            {
                continue;
            }

            roll -= Mathf.Max(0f, chances[count - 1]);
            if (roll <= 0f)
            {
                return count;
            }
        }

        return colorCeiling;
    }

    /// <summary>Distinct colors this level is allowed to spawn; empty inspector list means every color.</summary>
    private List<Soda.SodaColor> GetLevelPalette()
    {
        List<Soda.SodaColor> palette = new List<Soda.SodaColor>();
        if (allowedColors != null)
        {
            foreach (Soda.SodaColor color in allowedColors)
            {
                // Soda.Yellow has no supply, no order and no solver coverage.
                // Dropped here rather than trusted not to be authored.
                if (!Soda.IsSpawnable(color) || palette.Contains(color))
                {
                    continue;
                }

                palette.Add(color);
            }
        }

        if (palette.Count == 0)
        {
            // Soda.SpawnableColors, not Enum.GetValues: the latter now includes
            // Yellow, which would put the new art's yellow can into every level
            // that leaves allowedColors empty.
            palette.AddRange(Soda.SpawnableColors);
        }

        return palette;
    }

    private void OnValidate()
    {
        maxBoxCount = Mathf.Max(1, maxBoxCount);
        maxSodasPerBox = Mathf.Max(1, maxSodasPerBox);
        minSodasPerBox = Mathf.Clamp(minSodasPerBox, 1, maxSodasPerBox);
        maxDistinctColorsPerBox = Mathf.Max(1, maxDistinctColorsPerBox);
        minDistinctColorsPerBox = Mathf.Clamp(minDistinctColorsPerBox, 1, maxDistinctColorsPerBox);

        singleColorChance = Mathf.Max(0f, singleColorChance);
        twoColorChance = Mathf.Max(0f, twoColorChance);
        threeColorChance = Mathf.Max(0f, threeColorChance);

        if (useColorMixChances)
        {
            float total = singleColorChance + twoColorChance + threeColorChance;
            if (total <= 0f)
            {
                Debug.LogWarning(
                    "All color mix chances are 0; the Min/Max distinct color range will be used instead.",
                    this);
            }
            else if (Mathf.Abs(total - 100f) > 0.01f)
            {
                Debug.LogWarning(
                    $"Color mix chances add up to {total}, not 100. They will be normalized at runtime.",
                    this);
            }

            if (threeColorChance > 0f && maxSodasPerBox < 3)
            {
                Debug.LogWarning(
                    "Three-color boxes need Max Sodas Per Box of at least 3; the chance will fall back to fewer colors.",
                    this);
            }
            else if (twoColorChance > 0f && maxSodasPerBox < 2)
            {
                Debug.LogWarning(
                    "Two-color boxes need Max Sodas Per Box of at least 2; the chance will fall back to a single color.",
                    this);
            }
        }
    }

    private void SpawnSodaAtSlot(Box box, Transform slot, Soda.SodaColor color)
    {
        GameObject sodaObject = Instantiate(
            sodaPrefab,
            slot.position,
            Quaternion.Euler(-90f, 0f, 0f),
            box.transform);
        Soda soda = sodaObject.GetComponent<Soda>();
        if (soda != null)
        {
            soda.SetColor(color);
        }
    }

    private IEnumerator MoveBox(GameObject box, Vector3 endPosition)
    {
        if (box == null)
        {
            yield break;
        }

        Vector3 startPosition = box.transform.position;
        float elapsedTime = 0f;
        while (elapsedTime < moveDuration && box != null)
        {
            elapsedTime += Time.deltaTime;
            float interpolation = Mathf.Clamp01(elapsedTime / moveDuration);
            box.transform.position = Vector3.Lerp(startPosition, endPosition, interpolation);
            yield return null;
        }

        if (box != null)
        {
            box.transform.position = endPosition;
        }
    }

    private bool ValidateSpawnConfiguration(int requestedCount, out string reason)
    {
        if (requestedCount <= 0)
        {
            reason = "The requested batch is empty.";
            return false;
        }

        if (boxPrefab == null || sodaPrefab == null)
        {
            reason = "Box Prefab or Soda Prefab is not assigned.";
            return false;
        }

        if (startPos == null || endPos == null ||
            requestedCount > startPos.Length || requestedCount > endPos.Length)
        {
            reason = "Start/end rail Transform arrays do not contain enough entries.";
            return false;
        }

        for (int i = 0; i < requestedCount; i++)
        {
            if (startPos[i] == null || endPos[i] == null)
            {
                reason = $"Rail Transform {i} is unassigned.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private void CleanupLiveRailList()
    {
        spawnedBoxes.RemoveAll(item => item == null);
    }

    private bool NoBoxInList()
    {
        CleanupLiveRailList();
        return spawnedBoxes.Count == 0;
    }

    private static bool IsGameEnded()
    {
        return GameManager.instance != null && GameManager.instance.gameEnded;
    }
}
