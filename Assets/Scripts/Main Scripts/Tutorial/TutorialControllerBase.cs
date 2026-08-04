using UnityEngine;

public enum TutorialRepeatMode
{
    Once,
    Always,
    DevelopmentOnly
}

/// <summary>
/// Lifecycle contract shared by feature-specific Tutorials. Gameplay meaning
/// remains in derived controllers; this base only exposes registration metadata
/// and safe start/complete/cancel boundaries.
/// </summary>
public abstract class TutorialControllerBase : MonoBehaviour
{
    [Header("Tutorial Registration")]
    [SerializeField] private string tutorialId = "tutorial.unconfigured";
    [SerializeField, Min(0)] private int requiredLevel;
    [SerializeField] private int priority;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private TutorialRepeatMode repeatBehavior = TutorialRepeatMode.Once;
    [SerializeField] private bool forceStartInDevelopment;

    public string TutorialId => tutorialId == null ? string.Empty : tutorialId.Trim();
    public int RequiredLevel => requiredLevel;
    public int Priority => priority;
    public bool AutoStart => autoStart;
    public TutorialRepeatMode RepeatBehavior => repeatBehavior;
    public bool ForceStartInDevelopment => forceStartInDevelopment;
    public bool IsRunning { get; private set; }

    protected virtual void OnEnable()
    {
        TutorialManager.Instance.Register(this);
    }

    protected virtual void OnDisable()
    {
        if (TutorialManager.HasInstance)
        {
            TutorialManager.Instance.Unregister(this);
        }
    }

    internal bool InternalValidate(out string reason)
    {
        if (string.IsNullOrWhiteSpace(TutorialId))
        {
            reason = "Tutorial ID is empty.";
            return false;
        }

        return ValidateTutorial(out reason);
    }

    internal bool InternalCanStart()
    {
        int currentLevel = PlayerPrefs.GetInt("Level", 1);
        return isActiveAndEnabled && currentLevel >= requiredLevel && CanStartTutorial();
    }

    internal void InternalBegin()
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        OnTutorialStarted();
    }

    internal void InternalComplete()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        OnTutorialCompleted();
    }

    internal void InternalCancel(string reason)
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        OnTutorialCancelled(reason);
    }

    protected void RequestCompletion()
    {
        TutorialManager.Instance.CompleteTutorial(this);
    }

    protected void RequestCancellation(string reason)
    {
        TutorialManager.Instance.CancelTutorial(this, reason);
    }

    [ContextMenu("Reset This Tutorial Completion")]
    private void ResetThisTutorialCompletion()
    {
        TutorialManager.Instance.ResetCompletion(TutorialId);
    }

    protected virtual bool ValidateTutorial(out string reason)
    {
        reason = string.Empty;
        return true;
    }

    protected virtual bool CanStartTutorial()
    {
        return true;
    }

    protected abstract void OnTutorialStarted();

    protected virtual void OnTutorialCompleted()
    {
    }

    protected virtual void OnTutorialCancelled(string reason)
    {
    }
}
