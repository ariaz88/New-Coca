using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent shared coordinator for Tutorial registration, selection, ownership,
/// completion state, and scene-safe cleanup. It contains no sorting rules.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialManager : MonoBehaviour
{
    private static TutorialManager instance;

#if UNITY_EDITOR
    [Header("Editor Testing")]
    [SerializeField, Tooltip("Stable ID of the Tutorial controlled by the Editor testing options.")]
    private string editorTestingTutorialId = "sorting.initial";

    [SerializeField, Tooltip("Runs the selected Tutorial in the Unity Editor without reading or writing its saved completion state.")]
    private bool forceTutorialInEditor;
#endif

    private readonly List<TutorialControllerBase> registeredTutorials =
        new List<TutorialControllerBase>();
    private readonly HashSet<TutorialControllerBase> warnedInvalidTutorials =
        new HashSet<TutorialControllerBase>();
    private readonly HashSet<TutorialControllerBase> cancelledTutorials =
        new HashSet<TutorialControllerBase>();
    private readonly HashSet<TutorialControllerBase> completedTutorials =
        new HashSet<TutorialControllerBase>();

    private ITutorialCompletionStore completionStore;
    private TutorialControllerBase activeTutorial;
    private TutorialControllerBase handOwner;
    private HandAnimation ownedHand;
    private Coroutine evaluateRoutine;
    private bool activeTutorialWasEditorForced;

    public static bool HasInstance => instance != null;

    public static TutorialManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject managerObject = new GameObject(nameof(TutorialManager));
                instance = managerObject.AddComponent<TutorialManager>();
            }

            return instance;
        }
    }

    public TutorialControllerBase ActiveTutorial => activeTutorial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBeforeSceneLoad()
    {
        _ = Instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
#if UNITY_EDITOR
            instance.ApplyEditorTestingSettingsFrom(this);
#endif
            Destroy(gameObject);
            return;
        }

        instance = this;
        completionStore = new PlayerPrefsTutorialCompletionStore();
        DontDestroyOnLoad(gameObject);
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        instance = null;
    }

    public void Register(TutorialControllerBase tutorial)
    {
        if (tutorial == null || registeredTutorials.Contains(tutorial))
        {
            return;
        }

        registeredTutorials.Add(tutorial);
        cancelledTutorials.Remove(tutorial);
        completedTutorials.Remove(tutorial);
        ScheduleEvaluation();
    }

    public void Unregister(TutorialControllerBase tutorial)
    {
        if (tutorial == null)
        {
            return;
        }

        if (activeTutorial == tutorial)
        {
            CancelTutorial(tutorial, "The Tutorial controller was disabled or unloaded.");
        }

        registeredTutorials.Remove(tutorial);
        warnedInvalidTutorials.Remove(tutorial);
        cancelledTutorials.Remove(tutorial);
        completedTutorials.Remove(tutorial);
    }

    public bool IsCompleted(string tutorialId)
    {
        EnsureStore();
        return completionStore.IsCompleted(tutorialId);
    }

    public void ResetCompletion(string tutorialId)
    {
        EnsureStore();
        completionStore.Reset(tutorialId);
        completedTutorials.RemoveWhere(item =>
            item == null || string.Equals(item.TutorialId, tutorialId?.Trim()));
        cancelledTutorials.RemoveWhere(item =>
            item == null || string.Equals(item.TutorialId, tutorialId?.Trim()));
        ScheduleEvaluation();
    }

    public bool TryAcquireHand(TutorialControllerBase owner, HandAnimation hand)
    {
        if (owner == null || hand == null || activeTutorial != owner)
        {
            return false;
        }

        if (handOwner != null && handOwner != owner)
        {
            return false;
        }

        handOwner = owner;
        ownedHand = hand;
        return hand.Acquire(owner);
    }

    public void ReleaseHand(TutorialControllerBase owner)
    {
        if (owner == null || handOwner != owner)
        {
            return;
        }

        if (ownedHand != null)
        {
            ownedHand.Release(owner);
        }

        handOwner = null;
        ownedHand = null;
    }

    public void CompleteTutorial(TutorialControllerBase tutorial)
    {
        if (tutorial == null || activeTutorial != tutorial)
        {
            return;
        }

        if (!activeTutorialWasEditorForced)
        {
            EnsureStore();
            completionStore.MarkCompleted(tutorial.TutorialId);
        }

        completedTutorials.Add(tutorial);
        ReleaseHand(tutorial);
        activeTutorial = null;
        activeTutorialWasEditorForced = false;
        tutorial.InternalComplete();
        ScheduleEvaluation();
    }

    public void CancelTutorial(TutorialControllerBase tutorial, string reason)
    {
        if (tutorial == null || activeTutorial != tutorial)
        {
            return;
        }

        ReleaseHand(tutorial);
        activeTutorial = null;
        activeTutorialWasEditorForced = false;
        cancelledTutorials.Add(tutorial);
        tutorial.InternalCancel(reason);

        if (!string.IsNullOrWhiteSpace(reason))
        {
            Debug.LogWarning($"Tutorial '{tutorial.TutorialId}' was cancelled: {reason}", tutorial);
        }

        ScheduleEvaluation();
    }

    private void ScheduleEvaluation()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || evaluateRoutine != null)
        {
            return;
        }

        evaluateRoutine = StartCoroutine(EvaluateNextFrame());
    }

    private IEnumerator EvaluateNextFrame()
    {
        yield return null;
        evaluateRoutine = null;

        if (activeTutorial != null)
        {
            yield break;
        }

        registeredTutorials.RemoveAll(item => item == null);
        registeredTutorials.Sort((left, right) => right.Priority.CompareTo(left.Priority));

        foreach (TutorialControllerBase tutorial in registeredTutorials)
        {
            if (tutorial == null || cancelledTutorials.Contains(tutorial) ||
                completedTutorials.Contains(tutorial) ||
                !tutorial.AutoStart || !tutorial.InternalCanStart())
            {
                continue;
            }

            if (!tutorial.InternalValidate(out string reason))
            {
                if (warnedInvalidTutorials.Add(tutorial))
                {
                    Debug.LogWarning($"Tutorial '{tutorial.TutorialId}' cannot start: {reason}", tutorial);
                }

                continue;
            }

            if (!ShouldRun(tutorial, out bool editorForced))
            {
                continue;
            }

            activeTutorial = tutorial;
            activeTutorialWasEditorForced = editorForced;
            tutorial.InternalBegin();
            yield break;
        }
    }

    private bool ShouldRun(TutorialControllerBase tutorial, out bool editorForced)
    {
        editorForced = IsForcedInEditor(tutorial);
        if (editorForced)
        {
            return true;
        }

        if (tutorial.RepeatBehavior == TutorialRepeatMode.Always)
        {
            return true;
        }

        bool developmentBuild = Debug.isDebugBuild || Application.isEditor;
        if (tutorial.RepeatBehavior == TutorialRepeatMode.DevelopmentOnly && developmentBuild)
        {
            return true;
        }

        if (tutorial.ForceStartInDevelopment && developmentBuild)
        {
            return true;
        }

        return !IsCompleted(tutorial.TutorialId);
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        if (activeTutorial != null)
        {
            CancelTutorial(activeTutorial, $"Scene changed from '{previousScene.name}' to '{nextScene.name}'.");
        }

        registeredTutorials.RemoveAll(item => item == null);
        warnedInvalidTutorials.RemoveWhere(item => item == null);
        cancelledTutorials.RemoveWhere(item => item == null);
        completedTutorials.RemoveWhere(item => item == null);
        ScheduleEvaluation();
    }

    private bool IsForcedInEditor(TutorialControllerBase tutorial)
    {
#if UNITY_EDITOR
        return forceTutorialInEditor && tutorial != null &&
               !string.IsNullOrWhiteSpace(editorTestingTutorialId) &&
               string.Equals(tutorial.TutorialId, editorTestingTutorialId.Trim());
#else
        return false;
#endif
    }

#if UNITY_EDITOR
    private void ApplyEditorTestingSettingsFrom(TutorialManager sceneManager)
    {
        if (sceneManager == null)
        {
            return;
        }

        editorTestingTutorialId = sceneManager.editorTestingTutorialId;
        forceTutorialInEditor = sceneManager.forceTutorialInEditor;
    }

    [ContextMenu("Reset Tutorial Progress")]
    private void ResetTutorialProgressFromInspector()
    {
        string tutorialId = editorTestingTutorialId == null
            ? string.Empty
            : editorTestingTutorialId.Trim();
        if (string.IsNullOrWhiteSpace(tutorialId))
        {
            Debug.LogWarning(
                "Tutorial progress was not reset because Editor Testing Tutorial ID is empty.",
                this);
            return;
        }

        ResetCompletion(tutorialId);
        Debug.Log($"Reset saved Tutorial progress for '{tutorialId}'.", this);
    }
#endif

    private void EnsureStore()
    {
        if (completionStore == null)
        {
            completionStore = new PlayerPrefsTutorialCompletionStore();
        }
    }
}
