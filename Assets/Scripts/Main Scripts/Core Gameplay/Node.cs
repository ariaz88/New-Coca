using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Node : MonoBehaviour
{
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    public int row;
    public int column;
    private string carrierTag = "Carrier";
    public bool isOccupied = false;
    private Renderer nodeRenderer;
    private Color originalColor;

    [Header("Highlight Visual")]
    [SerializeField] private Transform highlightVisual;
    [SerializeField, Min(0f)] private float highlightHeightAboveNode = 0.131f;
    public Color highlightColor = new Color(1f, 0.45f, 0.05f, 1f);

    private Renderer[] highlightRenderers;
    private MaterialPropertyBlock highlightProperties;
    private bool warnedMissingHighlightVisual;

    private void Awake()
    {
        CacheHighlightVisual();
    }

    void Start()
    {
        nodeRenderer = GetComponent<Renderer>();
        if (nodeRenderer != null)
        {
            originalColor = nodeRenderer.material.color;
        }

    }

    public void Highlight()
    {
        if (!CacheHighlightVisual())
        {
            return;
        }

        Vector3 visiblePosition = highlightVisual.position;
        visiblePosition.y = transform.position.y + highlightHeightAboveNode;
        highlightVisual.position = visiblePosition;

        ApplyHighlightColor();
        highlightVisual.gameObject.SetActive(true);
    }

    public void Unhighlight()
    {
        if (!CacheHighlightVisual())
        {
            return;
        }

        highlightVisual.gameObject.SetActive(false);

    }

    private bool CacheHighlightVisual()
    {
        if (highlightVisual == null && transform.childCount > 0)
        {
            highlightVisual = transform.GetChild(0);
        }

        if (highlightVisual == null)
        {
            if (!warnedMissingHighlightVisual)
            {
                warnedMissingHighlightVisual = true;
                Debug.LogWarning(
                    $"Node '{name}' has no Highlight visual assigned and no child fallback.",
                    this);
            }

            return false;
        }

        if (highlightRenderers == null || highlightRenderers.Length == 0)
        {
            highlightRenderers = highlightVisual.GetComponentsInChildren<Renderer>(true);
        }

        return true;
    }

    private void ApplyHighlightColor()
    {
        if (highlightRenderers == null)
        {
            return;
        }

        if (highlightProperties == null)
        {
            highlightProperties = new MaterialPropertyBlock();
        }

        foreach (Renderer highlightRenderer in highlightRenderers)
        {
            if (highlightRenderer == null)
            {
                continue;
            }

            Material material = highlightRenderer.sharedMaterial;
            highlightProperties.Clear();
            highlightRenderer.GetPropertyBlock(highlightProperties);

            if (material != null && material.HasProperty(BaseColorProperty))
            {
                highlightProperties.SetColor(BaseColorProperty, highlightColor);
            }

            if (material != null && material.HasProperty(ColorProperty))
            {
                highlightProperties.SetColor(ColorProperty, highlightColor);
            }

            highlightRenderer.SetPropertyBlock(highlightProperties);
            highlightRenderer.enabled = true;
        }
    }

    public void HighLightForHammer()
    {
        if (nodeRenderer != null)
        {
            nodeRenderer.material.color = new Color(238f / 255f, 84f / 255f, 84f / 255f);
        }
    }

    //private void OnTriggerEnter(Collider other)
    //{
    //    if (other.CompareTag(carrierTag))
    //    {
    //        isOccupied = true;
    //    }
    //}
    //private void OnTriggerExit(Collider other)
    //{
    //    if (other.CompareTag(carrierTag))
    //    {
    //        isOccupied = false;
    //    }
    //}
}
