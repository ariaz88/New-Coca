using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Presentation-only UI used by the sorting Tutorial. State progression is owned
/// by SortingTutorialController, so UI animation timing cannot advance gameplay.
/// </summary>
[DisallowMultipleComponent]
public class ToolTipTutorial : MonoBehaviour
{
    public static ToolTipTutorial instance;

    [SerializeField] private GameObject nextBox;
    [SerializeField] private GameObject completedIteration;
    [SerializeField] private RectTransform endPos;
    [SerializeField] private RectTransform startPos;
    [SerializeField] private TextMeshProUGUI firstBoxText;
    [SerializeField] private Button StartLevel1;
    [SerializeField] private Button RestartTutorial;
    [SerializeField] private GameObject SuccessPanel;
    [SerializeField] private string tutorialIdToReset = "sorting.initial";

    private Sequence stepSequence;
    private Sequence completeSequence;
    private Sequence panelSequence;
    private bool completionPanelShown;
    private bool sceneLoadRequested;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("More than one ToolTipTutorial exists in the scene.", this);
        }

        instance = this;
    }

    private void OnEnable()
    {
        instance = this;

        if (StartLevel1 != null)
        {
            StartLevel1.onClick.RemoveListener(GoToLevel1);
            StartLevel1.onClick.AddListener(GoToLevel1);
        }

        if (RestartTutorial != null)
        {
            RestartTutorial.onClick.RemoveListener(ResetTutorial);
            RestartTutorial.onClick.AddListener(ResetTutorial);
        }
    }

    private void Start()
    {
        ResetPresentation();
    }

    private void OnDisable()
    {
        if (StartLevel1 != null)
        {
            StartLevel1.onClick.RemoveListener(GoToLevel1);
        }

        if (RestartTutorial != null)
        {
            RestartTutorial.onClick.RemoveListener(ResetTutorial);
        }

        KillSequences();
        if (instance == this)
        {
            instance = null;
        }
    }

    public void ShowMovePrompt(bool isFirstMove)
    {
        KillStepSequences();

        if (nextBox == null)
        {
            return;
        }

        if (firstBoxText != null)
        {
            firstBoxText.text = isFirstMove ? "MOVE FIRST BOX" : "MOVE THE BOX";
        }

        completedIteration?.SetActive(false);
        nextBox.SetActive(true);
        nextBox.transform.localScale = Vector3.zero;
        if (startPos != null)
        {
            nextBox.transform.position = startPos.position;
        }

        stepSequence = DOTween.Sequence()
            .Append(nextBox.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack))
            .AppendInterval(0.3f);

        if (endPos != null)
        {
            stepSequence.Append(nextBox.transform.DOMove(endPos.position, 0.25f).SetEase(Ease.InOutSine));
        }

        stepSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    public void ShowPlacementComplete()
    {
        KillStepSequences();
        nextBox?.SetActive(false);

        if (completedIteration == null)
        {
            return;
        }

        completedIteration.SetActive(true);
        completedIteration.transform.localScale = Vector3.zero;
        if (endPos != null)
        {
            completedIteration.transform.position = endPos.position;
        }

        completeSequence = DOTween.Sequence()
            .Append(completedIteration.transform.DOScale(Vector3.one, 0.45f).SetEase(Ease.OutBack))
            .AppendInterval(0.55f)
            .Append(completedIteration.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack))
            .OnComplete(() => completedIteration?.SetActive(false))
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    public void HideStepPresentation()
    {
        KillStepSequences();
        if (nextBox != null)
        {
            nextBox.SetActive(false);
            if (startPos != null)
            {
                nextBox.transform.position = startPos.position;
            }
        }

        completedIteration?.SetActive(false);
    }

    public void ShowCompletionPanel()
    {
        if (completionPanelShown || SuccessPanel == null)
        {
            return;
        }

        completionPanelShown = true;
        HideStepPresentation();
        SuccessPanel.SetActive(true);
        SuccessPanel.transform.localScale = Vector3.zero;

        panelSequence = DOTween.Sequence()
            .Append(SuccessPanel.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack))
            .OnComplete(() =>
            {
                if (SuccessPanel != null && SuccessPanel.transform.childCount > 0)
                {
                    SuccessPanel.transform.GetChild(0).gameObject.SetActive(true);
                }
            })
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    public void ResetPresentation()
    {
        KillSequences();
        completionPanelShown = false;
        sceneLoadRequested = false;
        nextBox?.SetActive(false);
        completedIteration?.SetActive(false);
        SuccessPanel?.SetActive(false);
    }

    public void GoToLevel1()
    {
        LoadSceneOnce("Level1");
    }

    public void ResetTutorial()
    {
        if (TutorialManager.HasInstance)
        {
            TutorialManager.Instance.ResetCompletion(tutorialIdToReset);
        }

        LoadSceneOnce("TUTORIAL");
    }

    // Compatibility wrappers for existing Inspector UnityEvents and older scripts.
    public void NextMoveAnim()
    {
        ShowMovePrompt(false);
    }

    public void CompleteAnim()
    {
        ShowPlacementComplete();
    }

    public void PanelAnimation()
    {
        ShowCompletionPanel();
    }

    private void LoadSceneOnce(string sceneName)
    {
        if (sceneLoadRequested)
        {
            return;
        }

        sceneLoadRequested = true;
        SceneManager.LoadScene(sceneName);
    }

    private void KillStepSequences()
    {
        if (stepSequence != null && stepSequence.IsActive())
        {
            stepSequence.Kill();
        }

        if (completeSequence != null && completeSequence.IsActive())
        {
            completeSequence.Kill();
        }

        stepSequence = null;
        completeSequence = null;
    }

    private void KillSequences()
    {
        KillStepSequences();
        if (panelSequence != null && panelSequence.IsActive())
        {
            panelSequence.Kill();
        }

        panelSequence = null;
    }
}
