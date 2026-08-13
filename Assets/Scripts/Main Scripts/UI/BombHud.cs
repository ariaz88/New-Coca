using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The on-screen half of Hidden Bombs: the preview banner, the screen dim, the
/// scanner beam, the scanner button and the counters.
///
/// Built entirely in code and attached at runtime, which is this project's
/// established way of changing the HUD - OrderPanelSkin and LevelHudLayout do
/// the same. The 25 level scenes were baked before bombs existed, and hand-adding
/// a button to each of them would be twenty-five edits that then have to be kept
/// in step; this reaches every scene at once and survives a re-bake.
///
/// It also installs BombDirector, so a level with bombs enabled needs nothing
/// added to its scene at all.
/// </summary>
[DisallowMultipleComponent]
public sealed class BombHud : MonoBehaviour
{
    public static BombHud instance;

    private const int DimSortOrder = 400;
    private const int ContentSortOrder = 401;

    private Canvas canvas;
    private Image dimImage;
    private RectTransform beam;
    private TextMeshProUGUI bannerText;
    private RectTransform bannerRoot;
    private TextMeshProUGUI scannerLabel;
    private Button scannerButton;
    private TextMeshProUGUI defuserLabel;
    private RectTransform defuserBadge;

    private BombDirector director;

    /// <summary>
    /// Creates the HUD after the level scene has loaded.
    ///
    /// RuntimeInitializeOnLoadMethod rather than a hook in UIManager because the
    /// bomb HUD has to appear in level scenes only, and sceneLoaded is the one
    /// signal that is guaranteed to arrive after Board exists in the scene but
    /// before the player can touch anything.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
        TryCreateForActiveScene();
    }

    private static void HandleSceneLoaded(
        UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        TryCreateForActiveScene();
    }

    private static void TryCreateForActiveScene()
    {
        if (instance != null)
        {
            return;
        }

        Board board = FindFirstObjectByType<Board>();
        if (board == null || !board.BombSettings.IsActive)
        {
            return;
        }

        GameObject host = new GameObject("BombHud");
        host.AddComponent<BombHud>();
    }

    private void Awake()
    {
        instance = this;
        Build();
        director = gameObject.AddComponent<BombDirector>();
    }

    private void OnEnable()
    {
        if (director != null)
        {
            director.StateChanged += Repaint;
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.StateChanged -= Repaint;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Start()
    {
        if (director != null)
        {
            director.StateChanged += Repaint;
        }

        Repaint();
    }

    // ------------------------------------------------------------------ build

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = ContentSortOrder;
        gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1125f, 2436f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildDim();
        BuildBeam();
        BuildBanner();
        BuildScannerButton();
        BuildDefuserBadge();
    }

    private void BuildDim()
    {
        GameObject host = NewUIObject("Dim", transform);
        dimImage = host.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0f);

        // The dim must never eat a tap. Input during the preview is stopped by the
        // Board and drag constraints, not by a full-screen raycast blocker, so
        // that a cancelled preview can never leave the screen unclickable.
        dimImage.raycastTarget = false;
        Stretch(host.GetComponent<RectTransform>());
    }

    private void BuildBeam()
    {
        GameObject host = NewUIObject("ScanBeam", transform);
        Image image = host.AddComponent<Image>();
        image.color = new Color(0.35f, 1f, 0.6f, 0.30f);
        image.raycastTarget = false;

        beam = host.GetComponent<RectTransform>();
        beam.anchorMin = new Vector2(0f, 0.5f);
        beam.anchorMax = new Vector2(1f, 0.5f);
        beam.pivot = new Vector2(0.5f, 0.5f);
        beam.sizeDelta = new Vector2(0f, 26f);
        host.SetActive(false);
    }

    private void BuildBanner()
    {
        GameObject host = NewUIObject("Banner", transform);
        bannerRoot = host.GetComponent<RectTransform>();
        bannerRoot.anchorMin = new Vector2(0.5f, 0.72f);
        bannerRoot.anchorMax = new Vector2(0.5f, 0.72f);
        bannerRoot.pivot = new Vector2(0.5f, 0.5f);
        bannerRoot.sizeDelta = new Vector2(880f, 130f);

        Image plate = host.AddComponent<Image>();
        plate.color = new Color(0.05f, 0.05f, 0.10f, 0.82f);
        plate.raycastTarget = false;

        GameObject textHost = NewUIObject("Text", host.transform);
        bannerText = textHost.AddComponent<TextMeshProUGUI>();
        bannerText.alignment = TextAlignmentOptions.Center;
        bannerText.fontSize = 58f;
        bannerText.fontStyle = FontStyles.Bold;
        bannerText.color = new Color(1f, 0.86f, 0.35f, 1f);
        bannerText.raycastTarget = false;
        Stretch(textHost.GetComponent<RectTransform>());

        host.SetActive(false);
    }

    private void BuildScannerButton()
    {
        GameObject host = NewUIObject("ScannerButton", transform);
        RectTransform rect = host.GetComponent<RectTransform>();

        // Under the existing powerup column on the left, using the same anchor so
        // it lands in the same place on every aspect ratio.
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(28f, -230f);
        rect.sizeDelta = new Vector2(170f, 170f);

        Image background = host.AddComponent<Image>();
        background.color = new Color(0.16f, 0.45f, 0.78f, 0.96f);

        scannerButton = host.AddComponent<Button>();
        scannerButton.targetGraphic = background;
        scannerButton.onClick.AddListener(HandleScannerPressed);

        GameObject textHost = NewUIObject("Label", host.transform);
        scannerLabel = textHost.AddComponent<TextMeshProUGUI>();
        scannerLabel.alignment = TextAlignmentOptions.Center;
        scannerLabel.fontSize = 34f;
        scannerLabel.fontStyle = FontStyles.Bold;
        scannerLabel.color = Color.white;
        scannerLabel.raycastTarget = false;
        Stretch(textHost.GetComponent<RectTransform>());
    }

    private void BuildDefuserBadge()
    {
        GameObject host = NewUIObject("DefuserBadge", transform);
        defuserBadge = host.GetComponent<RectTransform>();
        defuserBadge.anchorMin = new Vector2(0f, 0.5f);
        defuserBadge.anchorMax = new Vector2(0f, 0.5f);
        defuserBadge.pivot = new Vector2(0f, 0.5f);
        defuserBadge.anchoredPosition = new Vector2(28f, -420f);
        defuserBadge.sizeDelta = new Vector2(170f, 90f);

        Image background = host.AddComponent<Image>();
        background.color = new Color(0.12f, 0.55f, 0.32f, 0.94f);
        background.raycastTarget = false;

        GameObject textHost = NewUIObject("Label", host.transform);
        defuserLabel = textHost.AddComponent<TextMeshProUGUI>();
        defuserLabel.alignment = TextAlignmentOptions.Center;
        defuserLabel.fontSize = 30f;
        defuserLabel.fontStyle = FontStyles.Bold;
        defuserLabel.color = Color.white;
        defuserLabel.raycastTarget = false;
        Stretch(textHost.GetComponent<RectTransform>());
    }

    private static GameObject NewUIObject(string name, Transform parent)
    {
        GameObject host = new GameObject(name, typeof(RectTransform));
        host.transform.SetParent(parent, false);
        return host;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ----------------------------------------------------------------- public

    public void ShowBanner(string message)
    {
        if (bannerRoot == null)
        {
            return;
        }

        bannerText.text = message;
        bannerRoot.gameObject.SetActive(true);
    }

    public void HideBanner()
    {
        if (bannerRoot != null)
        {
            bannerRoot.gameObject.SetActive(false);
        }
    }

    public void SetDim(bool dimmed)
    {
        if (dimImage != null)
        {
            dimImage.color = new Color(0f, 0f, 0f, dimmed ? 0.45f : 0f);
        }
    }

    /// <summary>Runs the beam from the bottom of the board to the top.</summary>
    public IEnumerator SweepRoutine(float seconds)
    {
        if (beam == null)
        {
            yield break;
        }

        beam.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            beam.anchoredPosition = new Vector2(0f, Mathf.Lerp(-700f, 500f, t));
            yield return null;
        }

        beam.gameObject.SetActive(false);
    }

    private void HandleScannerPressed()
    {
        if (director == null || !director.TryUseScanner())
        {
            return;
        }

        Repaint();
    }

    private void Repaint()
    {
        if (director == null)
        {
            return;
        }

        if (scannerLabel != null)
        {
            scannerLabel.text = $"SCAN\n{director.ScannerChargesRemaining}";
        }

        if (scannerButton != null)
        {
            scannerButton.interactable = director.ScannerChargesRemaining > 0 && !director.IsBusy;
        }

        if (defuserLabel != null)
        {
            defuserLabel.text = $"DEFUSERS {director.DefusersRemaining}";
        }

        if (defuserBadge != null)
        {
            defuserBadge.gameObject.SetActive(director.DefusersRemaining > 0);
        }
    }
}
