using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CoinManager : MonoBehaviour
{
    public static CoinManager instance;

    [Header("UI References")]
    [SerializeField] private GameObject animatedCoinPrefab;
    [SerializeField] private GameObject animatedGemPrefab;
    [SerializeField] RectTransform coinTargetUI;
    [SerializeField] RectTransform gemTargetUI;

    [Header("Available Items")]
    [SerializeField] int MaxCoin;
    Queue<GameObject> coinsQueue = new Queue<GameObject>();
    Queue<GameObject> gemsQueue = new Queue<GameObject>();

    [SerializeField] Ease easeType;
    [SerializeField] float spread = 0.15f;
    float minAnimDuration = 0.25f;
    float maxAnimDuration = 0.42f;

    private void Awake()
    {
        instance = this;

        PrepareItems(animatedCoinPrefab, coinsQueue, MaxCoin);
        PrepareItems(animatedGemPrefab, gemsQueue, MaxCoin); // Initialize gems queue with MaxCoin items
    }

    private void PrepareItems(GameObject prefab, Queue<GameObject> queue, int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject item = Instantiate(prefab, transform);
            item.SetActive(false);
            queue.Enqueue(item);
        }
    }

    private void Animate(Vector3 collectedItemPosition, RectTransform targetUI, Queue<GameObject> itemQueue, int amount)
    {
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) return;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        for (int i = 0; i < amount; i++)
        {
            if (itemQueue.Count > 0)
            {
                GameObject item = itemQueue.Dequeue();
                item.SetActive(true);

                // Get the local position in the canvas RectTransform
                Vector2 uiPosition = WorldToRectTransformPosition(collectedItemPosition, canvasRect);

                // Set the item's parent and position
                item.transform.SetParent(canvas.transform, false);
                item.GetComponent<RectTransform>().anchoredPosition = uiPosition;

                item.transform.localPosition += new Vector3(Random.Range(-spread, spread), Random.Range(-spread, spread), 0);

                float duration = Random.Range(minAnimDuration, maxAnimDuration);

                // Tween Animation
                item.transform.DOMove(targetUI.position, duration)
                .SetEase(easeType)
                .OnComplete(() =>
                {
                    item.SetActive(false);
                    itemQueue.Enqueue(item);
                });
            }
        }
    }

    public static Vector2 WorldToRectTransformPosition(Vector3 worldPosition, RectTransform canvasRectTransform, Camera camera = null)
    {
        if (camera == null)
            camera = Camera.main;

        // Convert world position to screen point
        Vector2 screenPosition = camera.WorldToScreenPoint(worldPosition);

        // Convert screen point to local position in RectTransform
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform,
            screenPosition,
            camera,
            out Vector2 localPosition
        );

        return localPosition;
    }

    public void AddCoins(Vector3 collectedCoinPosition, int amount)
    {
        Animate(collectedCoinPosition, coinTargetUI, coinsQueue, amount);
    }

    public void AddGems(Vector3 collectedGemPosition, int amount)
    {
        Animate(collectedGemPosition, gemTargetUI, gemsQueue, amount); // Use gem target UI and gem queue
    }
}
