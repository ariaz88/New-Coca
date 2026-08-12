using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Boots the game from the persistence scene: decides whether the player goes to the
/// tutorial or straight to the first level, then drives the loading screen while the
/// destination scene streams in.
///
/// All presentation lives in <see cref="LoadingScreenUI"/>; this class only produces a
/// 0..1 progress value and owns the hand-off to the loaded scene.
/// </summary>
public class LoadManager : MonoBehaviour
{
    public static LoadManager instance;

    [Header("View")]
    [SerializeField] private GameObject loadScreen;
    [SerializeField] private LoadingScreenUI view;

    [Header("Initial Route")]
    [SerializeField] private string initialTutorialId = "sorting.initial";
    [SerializeField] private string tutorialSceneName = "TUTORIAL";
    [SerializeField] private string firstLevelSceneName = "Level1";

    [Header("Timing")]
    [Tooltip("Floor on how long the loading screen stays up, so a fast load still reads as a load rather than a flicker.")]
    [SerializeField] private float minimumDuration = 8f;
    [Tooltip("Beat held on a full bar before the screen fades, so completion registers.")]
    [SerializeField] private float completionHold = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.4f;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (loadScreen == null || view == null)
        {
            Debug.LogError("LoadManager: loadScreen or view is not assigned.");
            return;
        }

        LoadGame();
    }

    public void LoadGame()
    {
        // The loading screen is a separate root object, so it has to survive the scene swap
        // itself - otherwise it is destroyed mid-fade and the player sees a hard cut.
        Transform screenRoot = loadScreen.transform.root;
        DontDestroyOnLoad(screenRoot.gameObject);

        loadScreen.SetActive(true);
        view.ResetView();

        ITutorialCompletionStore tutorialStore = new PlayerPrefsTutorialCompletionStore();

        // firstLevelSceneName is serialized in PersistanceScene and predates the
        // Level1 -> Level01 rename, so a stale value is expected here. Fall back to
        // whichever spelling of level 1 is actually in the build.
        string firstLevel = firstLevelSceneName;
        if (!Application.CanStreamedLevelBeLoaded(firstLevel) &&
            LevelNaming.TryResolveLoadableSceneName(1, out string resolvedFirstLevel))
        {
            firstLevel = resolvedFirstLevel;
        }

        string destination = tutorialStore.IsCompleted(initialTutorialId)
            ? firstLevel
            : tutorialSceneName;

        StartCoroutine(LoadSceneRoutine(destination, screenRoot.gameObject));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, GameObject screenRoot)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        asyncLoad.allowSceneActivation = false;

        float elapsed = 0f;

        // Gate on whichever is slower: the real load, or the minimum on-screen duration.
        // Both terms only ever increase, so the reported progress never walks backwards.
        while (elapsed < minimumDuration || asyncLoad.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;

            float byTime = minimumDuration > 0f ? elapsed / minimumDuration : 1f;
            float byLoad = asyncLoad.progress / 0.9f; // Unity stalls async progress at 0.9 until activation.
            view.SetProgress(Mathf.Min(byTime, byLoad));

            yield return null;
        }

        view.SetProgress(1f);

        // Let the eased fill actually reach the end before we tear the screen down.
        while (view.DisplayedProgress < 0.999f)
            yield return null;

        yield return new WaitForSecondsRealtime(completionHold);
        yield return view.FadeOut(fadeOutDuration);

        asyncLoad.allowSceneActivation = true;
        while (!asyncLoad.isDone)
            yield return null;

        Destroy(screenRoot);
    }
}
