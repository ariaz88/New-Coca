using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Draws dragged rail items on top of everything, without touching their
/// materials.
///
/// A dragged Box has to read as "in your hand" - above the board, above other
/// boxes, above the rail. The original approach swapped every material for a
/// copy running a shader that pushed its depth to the near clip plane. That is
/// exact only for CocaSorting/ToyGloss, which the overlay shader is a copy of;
/// applied to the new Coke Pack's Universal Render Pipeline/Lit materials it
/// re-rendered them with the wrong lighting model and visibly changed their
/// colour on every drag. See Box.CanOverlayReproduce.
///
/// This does it the other way round: the item keeps its own materials, and a
/// stacked Overlay camera with Depth-only clear draws the DraggedItem layer
/// after the base camera. Depth is cleared first, so the item cannot be occluded
/// by anything, and because nothing about the material changed the shading is
/// identical by construction.
///
/// Installed at runtime off sceneLoaded, so none of the baked level scenes needed
/// to change - the same trick BombHud uses.
/// </summary>
[DisallowMultipleComponent]
public sealed class DraggedItemOverlay : MonoBehaviour
{
    public const string LayerName = "DraggedItem";

    private static DraggedItemOverlay instance;
    private static int cachedLayer = -1;

    private Camera baseCamera;
    private Camera overlayCamera;

    /// <summary>
    /// The layer dragged items are moved onto, or -1 when the project is missing
    /// it. Callers must check: silently moving items to layer 0 would look like
    /// the overlay simply not working.
    /// </summary>
    public static int Layer
    {
        get
        {
            if (cachedLayer < 0)
            {
                cachedLayer = LayerMask.NameToLayer(LayerName);
            }

            return cachedLayer;
        }
    }

    /// <summary>True when there is a live overlay camera to draw onto.</summary>
    public static bool IsAvailable => instance != null && instance.overlayCamera != null && Layer >= 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Install();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Install();
    }

    private static void Install()
    {
        if (Layer < 0)
        {
            Debug.LogWarning(
                $"Layer '{LayerName}' is missing; dragged items will not be drawn on top.");
            return;
        }

        if (instance != null && instance.overlayCamera != null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        GameObject host = new GameObject("DraggedItemOverlay");
        host.transform.SetParent(mainCamera.transform, false);
        instance = host.AddComponent<DraggedItemOverlay>();
        instance.Build(mainCamera);
    }

    private void Build(Camera mainCamera)
    {
        baseCamera = mainCamera;

        // The base camera must stop drawing the layer itself, or the item renders
        // twice - once occluded, once on top - which reads as a depth artefact.
        baseCamera.cullingMask &= ~(1 << Layer);

        overlayCamera = gameObject.AddComponent<Camera>();
        overlayCamera.cullingMask = 1 << Layer;
        overlayCamera.clearFlags = CameraClearFlags.Depth;
        overlayCamera.depth = baseCamera.depth + 1;

        // No second listener, and it must never win Camera.main.
        gameObject.tag = "Untagged";

        UniversalAdditionalCameraData baseData = baseCamera.GetUniversalAdditionalCameraData();

        UniversalAdditionalCameraData overlayData =
            overlayCamera.GetUniversalAdditionalCameraData();
        overlayData.renderType = CameraRenderType.Overlay;

        // Overlay cameras clear depth by default in URP - clearDepth is read
        // only - so CameraClearFlags.Depth above is what expresses it.
        //
        // Shadow and post settings are COPIED from the base camera rather than
        // set to sensible-looking values. The whole point of this camera is that
        // a dragged item looks exactly like a resting one, and forcing shadows
        // off here would reintroduce the very shading difference the material
        // swap was abandoned for.
        overlayData.renderShadows = baseData.renderShadows;
        overlayData.renderPostProcessing = baseData.renderPostProcessing;
        baseData.renderType = CameraRenderType.Base;
        if (!baseData.cameraStack.Contains(overlayCamera))
        {
            baseData.cameraStack.Add(overlayCamera);
        }

        SyncProjection();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
    }

    /// <summary>
    /// Syncs immediately before the overlay camera draws, rather than in
    /// LateUpdate. CinemachineBrain also runs in LateUpdate, so a LateUpdate copy
    /// races it and can lag the base camera by a frame - which on a moving camera
    /// shows up as the dragged item swimming against the board.
    /// </summary>
    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != overlayCamera || baseCamera == null)
        {
            return;
        }

        SyncProjection();
    }

    /// <summary>
    /// Copies every input the base camera's projection is derived from, and
    /// leaves the matrix itself IMPLICIT so it re-derives per render target.
    ///
    /// Two ways to get this wrong, both of which were shipped and caught:
    ///
    /// Copying fieldOfView alone is not enough. This project's camera runs
    /// usePhysicalProperties with a 1x1 sensor at 6.31mm focal length, while a
    /// fresh Camera defaults to a 36x24 sensor at 151.44mm. Copying only the flag
    /// and the FOV left those defaults in place, giving the overlay a projection
    /// 1.5x narrower - every dragged item drawn at two thirds size and ~80px out.
    ///
    /// Assigning projectionMatrix outright fixes that but FREEZES the matrix: an
    /// explicit matrix does not re-derive when the aspect changes, so the overlay
    /// stretched as soon as anything rendered the camera at another aspect.
    ///
    /// Nothing here assigns projectionMatrix, so it stays implicit on its own.
    /// Do NOT "make sure" of that with ResetProjectionMatrix() - that call also
    /// clears usePhysicalProperties, silently undoing the lens copy below and
    /// putting the size and offset bugs straight back.
    ///
    /// The physical fields must be set before focalLength, because focalLength,
    /// sensorSize and fieldOfView are interdependent once physical mode is on.
    /// </summary>
    private void SyncProjection()
    {
        overlayCamera.orthographic = baseCamera.orthographic;
        overlayCamera.orthographicSize = baseCamera.orthographicSize;
        overlayCamera.nearClipPlane = baseCamera.nearClipPlane;
        overlayCamera.farClipPlane = baseCamera.farClipPlane;
        overlayCamera.rect = baseCamera.rect;

        overlayCamera.usePhysicalProperties = baseCamera.usePhysicalProperties;
        if (baseCamera.usePhysicalProperties)
        {
            overlayCamera.sensorSize = baseCamera.sensorSize;
            overlayCamera.gateFit = baseCamera.gateFit;
            overlayCamera.lensShift = baseCamera.lensShift;
            overlayCamera.focalLength = baseCamera.focalLength;
        }
        else
        {
            overlayCamera.fieldOfView = baseCamera.fieldOfView;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
