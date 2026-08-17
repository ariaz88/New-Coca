using UnityEngine;
using DG.Tweening;

/// <summary>
/// Tuning for the animation a packed box plays as it leaves the board.
/// Serialized on <see cref="Board"/> so it can be tweaked in the Inspector.
/// </summary>
[System.Serializable]
public class PackedBoxExitSettings
{
    [Tooltip("Pause after the box closes, before it lifts. Gives the burst room to read.")]
    [Min(0f)] public float settleDuration = 0.25f;

    [Tooltip("How far the box rises off the board before pulling back, in its own height. " +
             "The level cameras are pitched about 48 degrees, which costs a third of any " +
             "vertical move on screen, so this needs to be larger than it sounds.")]
    [Min(0f)] public float liftBoxHeights = 0.85f;

    [Tooltip("Time taken by the lift.")]
    [Min(0.01f)] public float liftDuration = 0.18f;

    [Tooltip("How far the box pulls back, measured in its own length.")]
    [Min(0f)] public float recoilBoxLengths = 0.9f;

    [Tooltip("Time taken by the pull-back.")]
    [Min(0.01f)] public float recoilDuration = 0.2f;

    [Tooltip("Dead stop between pulling back and launching.")]
    [Min(0f)] public float holdDuration = 0.1f;

    [Tooltip("Time from launch until the box is fully off screen. Larger is slower.")]
    [Min(0.01f)] public float exitDuration = 0.43f;

    [Tooltip("Degrees the leading (right) edge lifts as the box launches.")]
    public float tiltAngle = 10f;

    [Tooltip("Time taken to reach the tilt. Shorter than the exit so the box is angled early in its travel.")]
    [Min(0.01f)] public float tiltDuration = 0.15f;
}

/// <summary>
/// Drives a packed box off the right-hand edge of the screen: a short pull back to the
/// left, a beat of stillness, then a faster launch to the right with the leading edge
/// lifted. Motion is measured through the camera rather than along world axes, so it
/// reads as horizontal no matter how a level's camera is framed.
/// </summary>
public static class PackedBoxExit
{
    // Viewport x = 1 is the right edge; the margin past it guarantees the box - which is
    // tilted, and so reaches further than its flat width - is fully out of view.
    private const float OffScreenViewportX = 1.35f;

    // Only used when there is no camera to measure the screen edge against.
    private const float FallbackDistance = 12f;

    // Drawn after everything else in the scene. Below UI, which lives on its own canvas.
    private const int OnTopRenderQueue = 3900;

    // UnityEngine.Rendering.CompareFunction.Always
    private const float ZTestAlways = 8f;

    public static void Play(GameObject box, Camera cam, PackedBoxExitSettings settings)
    {
        if (box == null) return;
        if (settings == null) settings = new PackedBoxExitSettings();

        Material[] owned = RenderOnTop(box);

        Transform t = box.transform;
        Vector3 start = t.position;
        Quaternion startRotation = t.rotation;

        Vector3 right = ScreenRight(cam);
        float boxLength = MeasureAlong(box, right);
        float boxHeight = MeasureAlong(box, Vector3.up);

        // The box lifts clear of the board first, then everything after happens at that
        // raised height - it never settles back down before launching.
        Vector3 liftTarget = start + Vector3.up * (boxHeight * settings.liftBoxHeights);
        Vector3 recoilTarget = liftTarget - right * (boxLength * settings.recoilBoxLengths);
        Vector3 exitTarget = OffScreenRight(cam, recoilTarget, right);

        // Rotating about (right x up) lifts the +right edge, which is the box's leading
        // face once it is travelling right.
        Vector3 tiltAxis = Vector3.Cross(right, Vector3.up).normalized;
        Quaternion tiltRotation = tiltAxis == Vector3.zero
            ? startRotation
            : Quaternion.AngleAxis(settings.tiltAngle, tiltAxis) * startRotation;

        Sequence seq = DOTween.Sequence();
        seq.SetTarget(box);

        // Unscaled, to match the order effect and the delayed coin award, both of which
        // run unscaled so they still finish if an end panel freezes gameplay.
        seq.SetUpdate(true);

        if (settings.settleDuration > 0f)
        {
            seq.AppendInterval(settings.settleDuration);
        }

        seq.Append(t.DOMove(liftTarget, settings.liftDuration).SetEase(Ease.OutQuad));
        seq.Append(t.DOMove(recoilTarget, settings.recoilDuration).SetEase(Ease.OutQuad));

        if (settings.holdDuration > 0f)
        {
            seq.AppendInterval(settings.holdDuration);
        }

        seq.Append(t.DOMove(exitTarget, settings.exitDuration).SetEase(Ease.InQuad));
        seq.Join(t.DORotateQuaternion(tiltRotation, settings.tiltDuration).SetEase(Ease.OutQuad));

        seq.OnComplete(() =>
        {
            if (box != null)
            {
                Object.Destroy(box);
            }

            // Renderer.materials hands back copies, and those copies are not cleaned up
            // when their GameObject goes. Releasing them here keeps a level's worth of
            // packed boxes from leaking a material each.
            foreach (Material m in owned)
            {
                if (m != null) Object.Destroy(m);
            }
        });
    }

    /// <summary>
    /// Makes this box draw over the rest of the scene rather than through it, by pushing
    /// its materials to a late render queue and switching depth testing off.
    ///
    /// The materials are instanced first - touching the shared ones would put every box
    /// in the game on top. Returns the instances so the caller can release them.
    /// </summary>
    private static Material[] RenderOnTop(GameObject box)
    {
        var instances = new System.Collections.Generic.List<Material>();

        foreach (Renderer r in box.GetComponentsInChildren<Renderer>(true))
        {
            // Reading .materials is what creates the per-instance copies.
            Material[] mats = r.materials;

            foreach (Material m in mats)
            {
                if (m == null) continue;
                m.renderQueue = OnTopRenderQueue;
                if (m.HasProperty("_ZTest")) m.SetFloat("_ZTest", ZTestAlways);
                instances.Add(m);
            }

            r.materials = mats;
            r.sortingOrder = 32000;
        }

        return instances.ToArray();
    }

    /// <summary>Screen-right, flattened onto the horizontal plane so the box travels level.</summary>
    public static Vector3 ScreenRight(Camera cam)
    {
        if (cam == null) return Vector3.right;

        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
        return right.sqrMagnitude < 0.0001f ? Vector3.right : right.normalized;
    }

    private static Vector3 OffScreenRight(Camera cam, Vector3 from, Vector3 right)
    {
        if (cam == null) return from + right * FallbackDistance;

        Vector3 viewport = cam.WorldToViewportPoint(from);
        Vector3 target = cam.ViewportToWorldPoint(new Vector3(OffScreenViewportX, viewport.y, viewport.z));

        // Pin the height so the box leaves flat instead of drifting up or down on its way out.
        target.y = from.y;
        return target;
    }

    /// <summary>Full world-space size of the box measured along <paramref name="dir"/>.</summary>
    private static float MeasureAlong(GameObject box, Vector3 dir)
    {
        Renderer[] renderers = box.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 0f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        // Bounds are axis-aligned, so project their extents onto the direction.
        Vector3 e = bounds.extents;
        return 2f * (Mathf.Abs(e.x * dir.x) + Mathf.Abs(e.y * dir.y) + Mathf.Abs(e.z * dir.z));
    }
}
