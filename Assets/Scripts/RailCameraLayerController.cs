using UnityEngine;

/// <summary>
/// Keeps the conveyor and its waiting boxes on the camera-only RailView layer.
/// Add this component only to scenes that use a dedicated rail camera.
/// </summary>
public sealed class RailCameraLayerController : MonoBehaviour
{
    [SerializeField] private string railObjectName = "rail";
    [SerializeField] private string railLayerName = "RailView";
    [SerializeField] private float railZoneMaxZ = -0.45f;

    private int railLayer;
    private Transform railRoot;

    private void Awake()
    {
        railLayer = LayerMask.NameToLayer(railLayerName);
        GameObject rail = GameObject.Find(railObjectName);
        railRoot = rail != null ? rail.transform : null;

        if (railLayer < 0)
        {
            Debug.LogError($"Layer '{railLayerName}' is missing.", this);
            enabled = false;
            return;
        }

        if (railRoot != null)
        {
            SetLayerRecursively(railRoot, railLayer);
        }
    }

    private void LateUpdate()
    {
        // Boxes switch cameras as they cross from the rail into the board. This
        // keeps a dragged box visible and makes pointer projection follow the
        // camera viewport currently underneath it.
        foreach (Box box in FindObjectsOfType<Box>())
        {
            if (box == null)
            {
                continue;
            }

            int targetLayer = !box.IsOnBoard && box.transform.position.z < railZoneMaxZ
                ? railLayer
                : 0;
            SetLayerRecursively(box.transform, targetLayer);
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root.gameObject.layer != layer)
        {
            root.gameObject.layer = layer;
        }

        foreach (Transform child in root)
        {
            SetLayerRecursively(child, layer);
        }
    }
}
