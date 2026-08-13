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
    private Image scannerPlate;
    private Image scannerGlyph;

    private static readonly Color ScannerEnabledPlate = new Color(0.98f, 0.55f, 0.16f, 1f);
    private static readonly Color ScannerSpentPlate = new Color(0.55f, 0.57f, 0.62f, 1f);
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
        if (Board.instance != null)
        {
            Board.instance.BombStateChanged -= Repaint;
        }

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

        // Also driven by the Board, so the scanner greys out the instant the last
        // bomb is defused rather than waiting for the next director event.
        if (Board.instance != null)
        {
            Board.instance.BombStateChanged += Repaint;
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

    /// <summary>
    /// The scanner button, built to match the SWAP powerups it sits beside: a
    /// rounded plate, a drawn glyph, a count badge in the corner and a green
    /// caption pill under it. The first version was a flat blue square with the
    /// word SCAN in it, which read as debug UI next to the game's own buttons.
    /// </summary>
    private void BuildScannerButton()
    {
        GameObject host = NewUIObject("ScannerButton", transform);
        RectTransform rect = host.GetComponent<RectTransform>();

        // Right edge, not left: the left column already carries settings, no-ads
        // and two SWAP powerups, and anchoring there put the scanner on top of
        // them. The right side at this height is empty in every level.
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-30f, -170f);
        rect.sizeDelta = new Vector2(150f, 150f);

        scannerPlate = host.AddComponent<Image>();
        scannerPlate.sprite = BombHudTextures.Plate;
        scannerPlate.type = Image.Type.Sliced;
        scannerPlate.color = ScannerEnabledPlate;

        scannerButton = host.AddComponent<Button>();
        scannerButton.targetGraphic = scannerPlate;
        scannerButton.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = scannerButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 1f);
        scannerButton.colors = colors;
        scannerButton.onClick.AddListener(HandleScannerPressed);

        GameObject glyphHost = NewUIObject("Glyph", host.transform);
        scannerGlyph = glyphHost.AddComponent<Image>();
        scannerGlyph.sprite = BombHudTextures.RadarGlyph;
        scannerGlyph.raycastTarget = false;
        scannerGlyph.color = Color.white;
        RectTransform glyphRect = glyphHost.GetComponent<RectTransform>();
        glyphRect.anchorMin = new Vector2(0.5f, 0.5f);
        glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
        glyphRect.pivot = new Vector2(0.5f, 0.5f);
        glyphRect.anchoredPosition = new Vector2(0f, 8f);
        glyphRect.sizeDelta = new Vector2(92f, 92f);

        // Count badge, top-right, the same idiom the SWAP buttons use.
        GameObject badgeHost = NewUIObject("Count", host.transform);
        Image badge = badgeHost.AddComponent<Image>();
        badge.sprite = BombHudTextures.Pill;
        badge.type = Image.Type.Sliced;
        badge.color = new Color(1f, 0.72f, 0.18f, 1f);
        badge.raycastTarget = false;
        RectTransform badgeRect = badgeHost.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(1f, 1f);
        badgeRect.anchorMax = new Vector2(1f, 1f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(-6f, -6f);
        badgeRect.sizeDelta = new Vector2(56f, 56f);

        GameObject countHost = NewUIObject("CountText", badgeHost.transform);
        scannerLabel = countHost.AddComponent<TextMeshProUGUI>();
        scannerLabel.alignment = TextAlignmentOptions.Center;
        scannerLabel.fontSize = 34f;
        scannerLabel.fontStyle = FontStyles.Bold;
        scannerLabel.color = new Color(0.22f, 0.12f, 0.02f, 1f);
        scannerLabel.raycastTarget = false;
        Stretch(countHost.GetComponent<RectTransform>());

        GameObject captionHost = NewUIObject("Caption", host.transform);
        Image caption = captionHost.AddComponent<Image>();
        caption.sprite = BombHudTextures.Pill;
        caption.type = Image.Type.Sliced;
        caption.color = new Color(0.26f, 0.74f, 0.24f, 1f);
        caption.raycastTarget = false;
        RectTransform captionRect = captionHost.GetComponent<RectTransform>();
        captionRect.anchorMin = new Vector2(0.5f, 0f);
        captionRect.anchorMax = new Vector2(0.5f, 0f);
        captionRect.pivot = new Vector2(0.5f, 0.5f);
        captionRect.anchoredPosition = new Vector2(0f, 6f);
        captionRect.sizeDelta = new Vector2(132f, 42f);

        GameObject captionTextHost = NewUIObject("Text", captionHost.transform);
        TextMeshProUGUI captionText = captionTextHost.AddComponent<TextMeshProUGUI>();
        captionText.text = "SCAN";
        captionText.alignment = TextAlignmentOptions.Center;
        captionText.fontSize = 26f;
        captionText.fontStyle = FontStyles.Bold;
        captionText.color = Color.white;
        captionText.raycastTarget = false;
        Stretch(captionTextHost.GetComponent<RectTransform>());
    }

    private void BuildDefuserBadge()
    {
        GameObject host = NewUIObject("DefuserBadge", transform);
        defuserBadge = host.GetComponent<RectTransform>();
        defuserBadge.anchorMin = new Vector2(1f, 0.5f);
        defuserBadge.anchorMax = new Vector2(1f, 0.5f);
        defuserBadge.pivot = new Vector2(1f, 0.5f);
        defuserBadge.anchoredPosition = new Vector2(-28f, -310f);
        defuserBadge.sizeDelta = new Vector2(220f, 86f);

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

        // Once every bomb is defused there is nothing left to scan, so the button
        // greys out rather than sitting there inviting a wasted charge.
        bool anythingToFind = Board.instance != null && Board.instance.LiveBombCount > 0;
        bool usable = director.ScannerChargesRemaining > 0 && anythingToFind;

        if (scannerLabel != null)
        {
            scannerLabel.text = director.ScannerChargesRemaining.ToString();
        }

        if (scannerButton != null)
        {
            scannerButton.interactable = usable && !director.IsBusy;
        }

        if (scannerPlate != null)
        {
            scannerPlate.color = usable ? ScannerEnabledPlate : ScannerSpentPlate;
        }

        if (scannerGlyph != null)
        {
            scannerGlyph.color = usable ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        }

        if (defuserLabel != null)
        {
            // A counter, not a button. The tool rides down the rail in place of a
            // box and is dragged onto the bomb; the first version read
            // "DEFUSERS 3" beside two real buttons and was tapped as if it were
            // one, so it now says where the thing actually is.
            defuserLabel.text = $"DEFUSE ×{director.DefusersRemaining}\n<size=60%>arrives on the rail</size>";
        }

        if (defuserBadge != null)
        {
            defuserBadge.gameObject.SetActive(director.DefusersRemaining > 0);
        }
    }
}
