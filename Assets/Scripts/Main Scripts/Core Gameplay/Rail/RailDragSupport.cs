using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The parts of dragging a rail item that have nothing to do with what the item
/// is: where the pointer lands on the board plane, and how a dragged object is
/// lifted above everything it passes over.
///
/// This was lifted out of Box when the Defuser arrived. The alternative was to
/// re-parent Box onto a shared base class, which would have meant restructuring
/// a 2400-line file that carries the whole soda transfer surface; pulling out the
/// two pieces that are genuinely item-agnostic gets the same reuse without
/// touching how Box works.
/// </summary>
public static class RailDragSupport
{
    /// <summary>
    /// Where the pointer intersects a horizontal plane, in world space.
    /// </summary>
    public static bool TryGetPointerOnPlane(Plane plane, out Vector3 point)
    {
        point = default;
        Camera camera = GetPointerCamera();
        if (camera == null)
        {
            return false;
        }

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (!plane.Raycast(ray, out float distance))
        {
            return false;
        }

        point = ray.GetPoint(distance);
        return true;
    }

    /// <summary>
    /// Uses the topmost camera viewport under the pointer. In a normal scene this
    /// resolves to Camera.main; in a split-view scene it lets rail items use the
    /// dedicated lower camera and board items use the upper camera.
    /// </summary>
    public static Camera GetPointerCamera()
    {
        Vector3 pointer = Input.mousePosition;
        Camera selected = null;
        float selectedDepth = float.NegativeInfinity;

        foreach (Camera camera in Camera.allCameras)
        {
            if (camera == null || !camera.isActiveAndEnabled ||
                !camera.pixelRect.Contains(new Vector2(pointer.x, pointer.y)))
            {
                continue;
            }

            if (selected == null || camera.depth > selectedDepth)
            {
                selected = camera;
                selectedDepth = camera.depth;
            }
        }

        return selected != null ? selected : Camera.main;
    }

    /// <summary>Clears every Node highlight on the board.</summary>
    public static void ClearAllHighlights()
    {
        if (Board.instance == null || Board.instance.grid == null)
        {
            return;
        }

        foreach (Node node in Board.instance.grid)
        {
            if (node != null)
            {
                node.Unhighlight();
            }
        }
    }
}

/// <summary>
/// Gives a dragged object visual priority without moving it.
///
/// It swaps every renderer onto a ToyGloss overlay material and pushes the
/// sorting order to the top, so the item draws over the board it is passing
/// across; the originals are restored the moment the drag ends. Keeping the
/// saved state in an instance means two items can be lifted independently
/// without one restoring the other's materials.
/// </summary>
public sealed class RailDragDisplay
{
    private const string DragOverlayShaderResourcePath = "Shaders/ToyGloss";

    private sealed class RendererState
    {
        public Renderer Renderer;
        public int SortingOrder;
        public Material[] SharedMaterials;
    }

    private static Shader dragOverlayShader;

    private readonly List<RendererState> states = new List<RendererState>();
    private readonly List<Material> overlayMaterials = new List<Material>();

    public void Enable(GameObject target)
    {
        Restore();

        if (target == null)
        {
            return;
        }

        if (dragOverlayShader == null)
        {
            dragOverlayShader = Resources.Load<Shader>(DragOverlayShaderResourcePath);
        }

        foreach (Renderer childRenderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (childRenderer == null)
            {
                continue;
            }

            Material[] originalMaterials = childRenderer.sharedMaterials;
            states.Add(new RendererState
            {
                Renderer = childRenderer,
                SortingOrder = childRenderer.sortingOrder,
                SharedMaterials = originalMaterials
            });

            if (dragOverlayShader != null)
            {
                Material[] swapped = new Material[originalMaterials.Length];
                for (int i = 0; i < originalMaterials.Length; i++)
                {
                    Material originalMaterial = originalMaterials[i];
                    if (originalMaterial == null)
                    {
                        continue;
                    }

                    Material overlayMaterial = new Material(dragOverlayShader)
                    {
                        name = originalMaterial.name + " (Drag Overlay)",
                        hideFlags = HideFlags.HideAndDontSave
                    };

                    // The copy overwrites renderQueue too, and the overlay's own
                    // queue is the whole point of it, so it is put back after.
                    int overlayRenderQueue = overlayMaterial.renderQueue;
                    overlayMaterial.CopyPropertiesFromMaterial(originalMaterial);
                    overlayMaterial.renderQueue = overlayRenderQueue;
                    swapped[i] = overlayMaterial;
                    overlayMaterials.Add(overlayMaterial);
                }

                childRenderer.sharedMaterials = swapped;
            }

            childRenderer.sortingOrder = short.MaxValue;
        }
    }

    public void Restore()
    {
        foreach (RendererState state in states)
        {
            if (state?.Renderer == null)
            {
                continue;
            }

            state.Renderer.sharedMaterials = state.SharedMaterials;
            state.Renderer.sortingOrder = state.SortingOrder;
        }

        states.Clear();

        foreach (Material overlayMaterial in overlayMaterials)
        {
            if (overlayMaterial != null)
            {
                Object.Destroy(overlayMaterial);
            }
        }

        overlayMaterials.Clear();
    }
}
