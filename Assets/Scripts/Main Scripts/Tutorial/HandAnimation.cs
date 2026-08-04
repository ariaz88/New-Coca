using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Reusable Hand presentation. It knows how to display a source-to-target loop,
/// but it deliberately does not decide which gameplay action is correct.
/// </summary>
[DisallowMultipleComponent]
public class HandAnimation : MonoBehaviour
{
    // Hand_Tutorial.png uses a centered RectTransform pivot, while the visible
    // fingertip is roughly 70 screen pixels above it. Keep this intrinsic sprite
    // correction out of scene serialization so already-loaded scenes cannot
    // retain the old, uncorrected value after a script reload.
    private static readonly Vector2 FingertipPivotCompensation = new Vector2(0f, -70f);

    public static HandAnimation instance;

    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject handImage;
    [SerializeField, Min(1f)] private float pixelsPerSecond = 530f;
    [SerializeField, Min(0f)] private float pauseAtTarget = 0.5f;
    [SerializeField] private Vector2 screenOffset = new Vector2(70f, 0f);
    [SerializeField] private Ease movementEase = Ease.InOutSine;

    public bool isAnimationRunning;

    private TutorialControllerBase currentOwner;
    private Sequence handSequence;
    private Coroutine delayedRestart;
    private Vector3 lastSourceWorld;
    private Vector3 lastTargetWorld;
    private bool hasLastInstruction;

    public TutorialControllerBase CurrentOwner => currentOwner;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("More than one HandAnimation exists in the scene.", this);
        }

        instance = this;
        HideImmediate();
    }

    private void OnEnable()
    {
        instance = this;
    }

    private void OnDisable()
    {
        StopPresentation();
        if (instance == this)
        {
            instance = null;
        }
    }

    public bool Acquire(TutorialControllerBase owner)
    {
        if (owner == null || (currentOwner != null && currentOwner != owner))
        {
            return false;
        }

        currentOwner = owner;
        return true;
    }

    public bool Play(TutorialControllerBase owner, Transform source, Transform target)
    {
        if (source == null || target == null)
        {
            return false;
        }

        return Play(owner, source.position, target.position);
    }

    public bool Play(TutorialControllerBase owner, Vector3 sourceWorld, Vector3 targetWorld)
    {
        if (owner == null || owner != currentOwner)
        {
            return false;
        }

        if (!TryConvertWorldToHandSpace(sourceWorld, out Vector2 source) ||
            !TryConvertWorldToHandSpace(targetWorld, out Vector2 target))
        {
            HideImmediate();
            return false;
        }

        lastSourceWorld = sourceWorld;
        lastTargetWorld = targetWorld;
        hasLastInstruction = true;

        StopTweenAndDelay();

        RectTransform handRect = handImage != null ? handImage.GetComponent<RectTransform>() : null;
        if (handRect == null)
        {
            Debug.LogWarning("HandAnimation requires a UI GameObject with a RectTransform.", this);
            HideImmediate();
            return false;
        }

        handImage.SetActive(true);
        handRect.anchoredPosition = source;

        float duration = Mathf.Max(0.05f, Vector2.Distance(source, target) / pixelsPerSecond);
        handSequence = DOTween.Sequence()
            .Append(handRect.DOAnchorPos(target, duration).SetEase(movementEase))
            .AppendInterval(pauseAtTarget)
            .Append(handRect.DOAnchorPos(source, duration).SetEase(movementEase))
            .SetLoops(-1, LoopType.Restart)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        isAnimationRunning = true;
        return true;
    }

    public void Stop(TutorialControllerBase owner, bool hide = true)
    {
        if (owner != currentOwner)
        {
            return;
        }

        StopTweenAndDelay();
        if (hide)
        {
            HideImmediate();
        }
    }

    public void Restart(TutorialControllerBase owner, float delay = 0f)
    {
        if (owner != currentOwner || !hasLastInstruction)
        {
            return;
        }

        StopTweenAndDelay();
        delayedRestart = StartCoroutine(RestartAfterDelay(owner, Mathf.Max(0f, delay)));
    }

    public void Release(TutorialControllerBase owner)
    {
        if (owner != currentOwner)
        {
            return;
        }

        StopPresentation();
        currentOwner = null;
        hasLastInstruction = false;
    }

    public void HideHande()
    {
        HideImmediate();
    }

    public void ShowHand()
    {
        if (handImage != null)
        {
            handImage.SetActive(true);
        }
    }

    public void KillAnim()
    {
        StopTweenAndDelay();
        HideImmediate();
    }

    public void DeactivateAnimation()
    {
        KillAnim();
    }

    public void RunAnimation()
    {
        if (currentOwner != null && hasLastInstruction)
        {
            Play(currentOwner, lastSourceWorld, lastTargetWorld);
        }
    }

    /// <summary>
    /// Compatibility entry point retained for older gameplay scripts. The Hand
    /// only restarts when a registered Tutorial already owns an instruction.
    /// </summary>
    public void TryRestartAnimation(float delay)
    {
        if (currentOwner != null)
        {
            Restart(currentOwner, delay);
        }
    }

    public static Vector2 WorldToRectTransformPosition(
        Vector3 worldPosition,
        RectTransform targetRect,
        Camera camera = null)
    {
        if (targetRect == null)
        {
            return Vector2.zero;
        }

        Camera worldCamera = camera != null ? camera : Camera.main;
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(worldCamera, worldPosition);
        Canvas canvas = targetRect.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetRect,
            screenPosition,
            uiCamera,
            out Vector2 localPosition);
        return localPosition;
    }

    private IEnumerator RestartAfterDelay(TutorialControllerBase owner, float delay)
    {
        HideImmediate();
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        delayedRestart = null;
        if (owner == currentOwner && hasLastInstruction)
        {
            Play(owner, lastSourceWorld, lastTargetWorld);
        }
    }

    private bool TryConvertWorldToHandSpace(Vector3 worldPosition, out Vector2 localPosition)
    {
        localPosition = Vector2.zero;
        if (handImage == null)
        {
            Debug.LogWarning("Hand Image is not assigned.", this);
            return false;
        }

        RectTransform handRect = handImage.GetComponent<RectTransform>();
        RectTransform parentRect = handRect != null ? handRect.parent as RectTransform : null;
        if (handRect == null || parentRect == null)
        {
            Debug.LogWarning("The Hand Image must be parented below a RectTransform.", this);
            return false;
        }

        Camera worldCamera = mainCamera != null ? mainCamera : Camera.main;
        if (worldCamera == null)
        {
            Debug.LogWarning("HandAnimation could not find a world Camera.", this);
            return false;
        }

        Vector2 effectiveScreenOffset = screenOffset + FingertipPivotCompensation;
        Vector2 screenPosition =
            worldCamera.WorldToScreenPoint(worldPosition) + (Vector3)effectiveScreenOffset;
        Canvas canvas = handImage.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            screenPosition,
            uiCamera,
            out localPosition);
    }

    private void StopPresentation()
    {
        StopTweenAndDelay();
        HideImmediate();
    }

    private void StopTweenAndDelay()
    {
        if (delayedRestart != null)
        {
            StopCoroutine(delayedRestart);
            delayedRestart = null;
        }

        if (handSequence != null && handSequence.IsActive())
        {
            handSequence.Kill();
        }

        handSequence = null;
        isAnimationRunning = false;
    }

    private void HideImmediate()
    {
        if (handImage != null)
        {
            handImage.SetActive(false);
        }
    }
}
