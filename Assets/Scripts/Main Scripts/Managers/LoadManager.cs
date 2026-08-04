using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadManager : MonoBehaviour
{
    public static LoadManager instance;
    public GameObject loadScreen;
    public Slider bar;

    [Header("Initial Route")]
    [SerializeField] private string initialTutorialId = "sorting.initial";
    [SerializeField] private string tutorialSceneName = "TUTORIAL";
    [SerializeField] private string firstLevelSceneName = "Level1";

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
        if (loadScreen == null || bar == null)
        {
            Debug.LogError("LoadScreen or Bar is not assigned.");
            return;
        }

        LoadGame();
    }

    public void LoadGame()
    {
        loadScreen.SetActive(true);
        ITutorialCompletionStore tutorialStore = new PlayerPrefsTutorialCompletionStore();
        string destination = tutorialStore.IsCompleted(initialTutorialId)
            ? firstLevelSceneName
            : tutorialSceneName;
        StartCoroutine(LoadSceneWithMinimumDuration(destination, 8f));
    }

    IEnumerator LoadSceneWithMinimumDuration(string sceneName, float minDuration)
    {
        float elapsedTime = 0f; // Time spent loading
        float progress = 0f;   // Loading bar progress

        // Start loading the scene asynchronously
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        asyncLoad.allowSceneActivation = false; // Prevent scene activation until loading is complete

        // Simulate the loading bar over a fixed duration
        while (progress < 1f || elapsedTime < minDuration)
        {
            elapsedTime += Time.deltaTime;

            // Calculate progress based on both scene loading and time elapsed
            progress = Mathf.Min(elapsedTime / minDuration, asyncLoad.progress / 0.9f); // Normalize async progress (0 to 0.9)
            bar.value = Mathf.Clamp01(progress);

            yield return null;
        }

        // Wait a short pause for smooth transition, if needed
        yield return new WaitForSeconds(0.5f);

        // Activate the scene
        asyncLoad.allowSceneActivation = true;

        // Hide the loading screen
        loadScreen.SetActive(false);
    }
}



#region old version

//public class LoadManager2 : MonoBehaviour
//{
//    public static LoadManager2 instance;
//    public GameObject loadScreen;
//    public Slider bar;
//    private List<AsyncOperation> sceneLoading = new List<AsyncOperation>();
//    private float simulatedLoadProgress = 0f;

//    private void Awake()
//    {
//        if (instance == null)
//        {
//            instance = this;
//            DontDestroyOnLoad(gameObject); // Persist across scenes
//        }
//        else
//        {
//            Destroy(gameObject); // Prevent duplicate managers
//            return;
//        }
//    }
//    private void OnDestroy()
//    {
//        StopAllCoroutines(); // Ensure all coroutines are stopped when this object is destroyed
//    }

//    private void Start()
//    {
//        if (loadScreen == null || bar == null)
//        {
//            //Debug.LogError("LoadScreen or Bar is not assigned.");
//            return;
//        }

//        LoadGame();
//    }

//    public void LoadGame1()
//    {
//        loadScreen.SetActive(true);
//        sceneLoading.Add(SceneManager.LoadSceneAsync((int)SceneIndexces.LEVEL1, LoadSceneMode.Additive));
//        StartCoroutine(GetSceneLoadProgress());
//    }

//    public void LoadGame()
//    {
//        loadScreen.SetActive(true);
//        sceneLoading.Add(SceneManager.LoadSceneAsync((int)SceneIndexces.LEVEL1, LoadSceneMode.Single));
//        StartCoroutine(GetSceneLoadProgress());
//    }

//    IEnumerator GetSceneLoadProgress()
//    {
//        float totalSceneProgress = 0f;

//        // Gradually increase progress for a smooth animation
//        while (simulatedLoadProgress < 1f || !AllScenesLoaded())
//        {
//            totalSceneProgress = 0;

//            foreach (AsyncOperation operation in sceneLoading)
//            {
//                totalSceneProgress += operation.progress; // Unity's actual loading progress (0 to 0.9)
//            }

//            totalSceneProgress = Mathf.Clamp01(totalSceneProgress / sceneLoading.Count); // Normalize to 0-1

//            // Simulate slower progress for a smooth bar fill
//            simulatedLoadProgress = Mathf.MoveTowards(simulatedLoadProgress, totalSceneProgress, Time.deltaTime * 0.8f);

//            bar.value = simulatedLoadProgress;

//            yield return null;
//        }

//        yield return new WaitForSeconds(0.8f); // Brief pause after loading is complete
//        loadScreen.SetActive(false); // Hide the loading screen
//    }

//    private bool AllScenesLoaded()
//    {
//        foreach (AsyncOperation operation in sceneLoading)
//        {
//            if (!operation.isDone) return false;
//        }
//        return true;
//    }
//}

#endregion
