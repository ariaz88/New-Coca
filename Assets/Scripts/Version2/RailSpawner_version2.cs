using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Optional capacity-independent replacement for the old rail spawner. Use it
/// when boxes may have more than four SodaPosition slots.
/// </summary>
[DisallowMultipleComponent]
public sealed class RailSpawner_version2 : MonoBehaviour
{
    [SerializeField] private Box_version2 boxPrefab;
    [SerializeField] private Soda sodaPrefab;
    [SerializeField] private Transform[] railStartPositions;
    [SerializeField] private Transform[] railEndPositions;
    [SerializeField] private List<Soda.SodaColor> availableColors =
        new List<Soda.SodaColor>();
    [SerializeField, Min(0)] private int minimumInitialSodas = 1;
    [Tooltip("Zero means Capacity - 1.")]
    [SerializeField, Min(0)] private int maximumInitialSodas;
    [SerializeField, Min(0.01f)] private float railMoveDuration = 0.5f;
    [SerializeField, Min(0f)] private float nextBatchDelay = 1f;
    [SerializeField] private bool spawnContinuously = true;

    private readonly List<Box_version2> activeRailBoxes =
        new List<Box_version2>();

    private void Start()
    {
        if (availableColors.Count == 0)
        {
            availableColors.AddRange(
                System.Enum.GetValues(typeof(Soda.SodaColor))
                    .Cast<Soda.SodaColor>());
        }

        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        do
        {
            yield return SpawnBatch();

            if (!spawnContinuously) yield break;
            yield return new WaitUntil(() =>
            {
                activeRailBoxes.RemoveAll(box => box == null || box.IsPlaced);
                return activeRailBoxes.Count == 0;
            });

            if (nextBatchDelay > 0f)
            {
                yield return new WaitForSeconds(nextBatchDelay);
            }
        }
        while (spawnContinuously);
    }

    private IEnumerator SpawnBatch()
    {
        if (boxPrefab == null || sodaPrefab == null)
        {
            Debug.LogError(
                "RailSpawner_version2 requires box and soda prefabs.",
                this);
            yield break;
        }

        int count = Mathf.Min(
            railStartPositions?.Length ?? 0,
            railEndPositions?.Length ?? 0);

        for (int i = 0; i < count; i++)
        {
            if (railStartPositions[i] == null || railEndPositions[i] == null)
            {
                continue;
            }

            Box_version2 box = Instantiate(
                boxPrefab,
                railStartPositions[i].position,
                railStartPositions[i].rotation);
            box.DiscoverSlots();
            PopulateBox(box);
            box.RefreshContents();
            activeRailBoxes.Add(box);
            StartCoroutine(MoveAlongRail(box, railEndPositions[i]));
            yield return null;
        }
    }

    private void PopulateBox(Box_version2 box)
    {
        int capacity = box.Capacity;
        if (capacity <= 0 || availableColors.Count == 0) return;

        int maximum = maximumInitialSodas <= 0
            ? Mathf.Max(1, capacity - 1)
            : Mathf.Min(maximumInitialSodas, capacity);
        int minimum = Mathf.Clamp(minimumInitialSodas, 0, maximum);
        int sodaCount = Random.Range(minimum, maximum + 1);

        List<int> availableSlots =
            Enumerable.Range(0, capacity).ToList();
        for (int i = 0; i < sodaCount; i++)
        {
            int randomListIndex = Random.Range(0, availableSlots.Count);
            int slotIndex = availableSlots[randomListIndex];
            availableSlots.RemoveAt(randomListIndex);

            Transform slot = box.GetSlot(slotIndex);
            Soda soda = Instantiate(
                sodaPrefab,
                slot.position,
                slot.rotation,
                box.transform);
            Soda.SodaColor color =
                availableColors[Random.Range(0, availableColors.Count)];
            soda.SetColor(color);
        }
    }

    private IEnumerator MoveAlongRail(Box_version2 box, Transform destination)
    {
        if (box == null || destination == null) yield break;

        Vector3 startPosition = box.transform.position;
        Quaternion startRotation = box.transform.rotation;
        float elapsed = 0f;

        while (elapsed < railMoveDuration && box != null && !box.IsPlaced)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / railMoveDuration));
            box.transform.position =
                Vector3.Lerp(startPosition, destination.position, t);
            box.transform.rotation =
                Quaternion.Slerp(startRotation, destination.rotation, t);
            yield return null;
        }

        if (box != null && !box.IsPlaced)
        {
            box.transform.SetPositionAndRotation(
                destination.position,
                destination.rotation);
        }
    }
}
