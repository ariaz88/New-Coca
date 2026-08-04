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
    public static SpawnContoller instance;

    [SerializeField] private List<Transform> spawnPositions;
    [SerializeField] private Transform[] startPos;
    [SerializeField] private Transform[] endPos;
    [SerializeField, Min(0f)] private float spawnDelay = 0.03f;
    [SerializeField, Min(0.01f)] private float moveDuration = 0.5f;
    [SerializeField, Min(1)] private int maxBoxCount = 6;

    public float speed = 1f;
    public GameObject boxPrefab;
    public GameObject sodaPrefab;
    public float respawnDelay = 2f;
    public List<GameObject> spawnedBoxes = new List<GameObject>();
    public bool stopSpawn;
    public bool isTutorialState;

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
            StartCoroutine(LevelSpawnRoutine());
        }
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

        for (int i = 0; i < recipes.Count; i++)
        {
            Vector3 spawnWorld = startPos[i].position + new Vector3(0f, 0.2965f, 0f);
            Box box = CreateConfiguredBox(spawnWorld, recipes[i], true);
            if (box == null)
            {
                Debug.LogWarning($"Tutorial box {i} could not be created.", this);
                onCompleted?.Invoke(new List<Box>());
                yield break;
            }

            result.Add(box);
            if (spawnDelay > 0f)
            {
                yield return new WaitForSeconds(spawnDelay);
            }
        }

        List<Coroutine> movement = new List<Coroutine>();
        for (int i = 0; i < result.Count; i++)
        {
            movement.Add(StartCoroutine(MoveBox(result[i].gameObject, endPos[i].position)));
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
            yield return SpawnRandomRailBatch();
            yield return new WaitUntil(() => stopSpawn || IsGameEnded() || NoBoxInList());

            if (!stopSpawn && !IsGameEnded() && respawnDelay > 0f)
            {
                yield return new WaitForSeconds(respawnDelay);
            }
        }
    }

    private IEnumerator SpawnRandomRailBatch()
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
            Debug.LogWarning($"Normal rail batch was not spawned: {reason}", this);
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

        List<Transform> available = new List<Transform>(slots);
        int sodaCount = UnityEngine.Random.Range(1, Mathf.Min(3, available.Count) + 1);
        for (int i = 0; i < sodaCount; i++)
        {
            int index = UnityEngine.Random.Range(0, available.Count);
            Transform slot = available[index];
            available.RemoveAt(index);
            SpawnSodaAtSlot(box, slot, GetRandomSodaColor());
        }

        box.RefreshContents();
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

    private static Soda.SodaColor GetRandomSodaColor()
    {
        Soda.SodaColor[] colors =
            (Soda.SodaColor[])Enum.GetValues(typeof(Soda.SodaColor));
        return colors[UnityEngine.Random.Range(0, colors.Length)];
    }
}
