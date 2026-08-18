using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Grey-box menu scene: a bottom bar of five tabs, one flat placeholder panel per
/// tab, and a single "Level N" button on Home that enters the campaign.
///
/// Built entirely in code, like BombHud and OrderPanelSkin, so the scene itself is
/// three objects (camera, EventSystem, this) and the layout is edited here rather
/// than by hand in a scene file. Everything except Home is a coloured rectangle
/// with its own name on it - the tabs are only meant to prove navigation works.
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuSceneUI : MonoBehaviour
{
    /// <summary>Scene name, used by WinPanel to route here after a level.</summary>
    public const string SceneName = "MenuScene";

    /// <summary>
    /// The level the Home button enters: whatever progress says is next.
    ///
    /// WinPanel writes the next level number before routing here, so arriving from
    /// a finished level N shows "Level N+1". Read fresh on every enable rather than
    /// cached, because the menu is returned to after every level.
    /// </summary>
    public static int NextLevel => Mathf.Clamp(
        PlayerPrefs.GetInt("Level", 1), 1, LevelNaming.CampaignLevelCount);

    private const int HomeTabIndex = 2;
    private const float BarHeight = 200f;

    // Left to right, matching the reference layout: Shop and Map sit left of Home,
    // the two right-hand tabs are placeholders we have no plans for yet.
    private static readonly string[] TabNames = { "Shop", "Map", "Home", "Levels", "Settings" };

    private static readonly Color[] PanelColors =
    {
        new Color(0.36f, 0.24f, 0.52f, 1f),   // Shop
        new Color(0.16f, 0.47f, 0.62f, 1f),   // Map
        new Color(0.20f, 0.62f, 0.82f, 1f),   // Home
        new Color(0.52f, 0.33f, 0.20f, 1f),   // Levels
        new Color(0.30f, 0.34f, 0.40f, 1f)    // Settings
    };

    private static readonly Color TabIdle = new Color(0.16f, 0.18f, 0.24f, 1f);
    private static readonly Color TabActive = new Color(0.98f, 0.75f, 0.22f, 1f);
    private static readonly Color TabIdleLabel = new Color(0.82f, 0.85f, 0.90f, 1f);
    private static readonly Color TabActiveLabel = new Color(0.18f, 0.13f, 0.02f, 1f);

    private readonly List<GameObject> panels = new List<GameObject>();
    private readonly List<Image> tabPlates = new List<Image>();
    private readonly List<TextMeshProUGUI> tabLabels = new List<TextMeshProUGUI>();

    private TextMeshProUGUI levelButtonLabel;
    private bool sceneLoadRequested;

    private void Awake()
    {
        EnsureEventSystem();
        Build();
        SelectTab(HomeTabIndex);
        RefreshLevelButton();
    }

    /// <summary>Re-reads progress, so returning to the menu re-labels the button.</summary>
    private void RefreshLevelButton()
    {
        if (levelButtonLabel != null)
        {
            levelButtonLabel.text = "Level " + NextLevel;
        }
    }

    // ------------------------------------------------------------------ build

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1125f, 2436f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform content = BuildContentArea();

        for (int index = 0; index < TabNames.Length; index++)
        {
            panels.Add(BuildPanel(content, index));
        }

        BuildTabBar();
    }

    private RectTransform BuildContentArea()
    {
        GameObject host = NewUIObject("Content", transform);
        RectTransform rect = host.GetComponent<RectTransform>();
        Stretch(rect);

        // The bar owns the bottom strip, so every panel stops above it.
        rect.offsetMin = new Vector2(0f, BarHeight);
        return rect;
    }

    private GameObject BuildPanel(RectTransform parent, int index)
    {
        GameObject host = NewUIObject(TabNames[index] + "Panel", parent);
        Stretch(host.GetComponent<RectTransform>());

        Image background = host.AddComponent<Image>();
        background.color = PanelColors[index];

        GameObject titleHost = NewUIObject("Title", host.transform);
        TextMeshProUGUI title = titleHost.AddComponent<TextMeshProUGUI>();
        title.text = TabNames[index].ToUpperInvariant();
        title.alignment = TextAlignmentOptions.Center;
        title.fontSize = 96f;
        title.fontStyle = FontStyles.Bold;
        title.color = Color.white;
        title.raycastTarget = false;

        RectTransform titleRect = titleHost.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 120f);
        titleRect.sizeDelta = new Vector2(0f, 140f);

        GameObject noteHost = NewUIObject("Note", host.transform);
        TextMeshProUGUI note = noteHost.AddComponent<TextMeshProUGUI>();
        note.text = "placeholder tab";
        note.alignment = TextAlignmentOptions.Center;
        note.fontSize = 44f;
        note.color = new Color(1f, 1f, 1f, 0.75f);
        note.raycastTarget = false;

        RectTransform noteRect = noteHost.GetComponent<RectTransform>();
        noteRect.anchorMin = new Vector2(0f, 0.5f);
        noteRect.anchorMax = new Vector2(1f, 0.5f);
        noteRect.pivot = new Vector2(0.5f, 0.5f);
        noteRect.anchoredPosition = new Vector2(0f, 20f);
        noteRect.sizeDelta = new Vector2(0f, 80f);

        if (index == HomeTabIndex)
        {
            BuildLevelButton(host.transform);
        }

        return host;
    }

    /// <summary>The one real control in the menu: enter the campaign at HomeButtonLevel.</summary>
    private void BuildLevelButton(Transform parent)
    {
        GameObject host = NewUIObject("LevelButton", parent);
        RectTransform rect = host.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 180f);
        rect.sizeDelta = new Vector2(620f, 170f);

        Image plate = host.AddComponent<Image>();
        plate.sprite = BombHudTextures.Plate;
        plate.type = Image.Type.Sliced;
        plate.color = new Color(0.42f, 0.78f, 0.24f, 1f);

        Button button = host.AddComponent<Button>();
        button.targetGraphic = plate;
        button.onClick.AddListener(EnterLevel);

        GameObject labelHost = NewUIObject("Label", host.transform);
        levelButtonLabel = labelHost.AddComponent<TextMeshProUGUI>();
        levelButtonLabel.text = "Level " + NextLevel;
        levelButtonLabel.alignment = TextAlignmentOptions.Center;
        levelButtonLabel.fontSize = 72f;
        levelButtonLabel.fontStyle = FontStyles.Bold;
        levelButtonLabel.color = Color.white;
        levelButtonLabel.raycastTarget = false;
        Stretch(labelHost.GetComponent<RectTransform>());
    }

    private void BuildTabBar()
    {
        GameObject barHost = NewUIObject("TabBar", transform);
        RectTransform barRect = barHost.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0f, BarHeight);

        Image barBackground = barHost.AddComponent<Image>();
        barBackground.color = new Color(0.09f, 0.10f, 0.14f, 1f);

        float step = 1f / TabNames.Length;

        for (int index = 0; index < TabNames.Length; index++)
        {
            int tabIndex = index;

            GameObject host = NewUIObject(TabNames[index] + "Tab", barHost.transform);
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(index * step, 0f);
            rect.anchorMax = new Vector2((index + 1) * step, 1f);
            rect.offsetMin = new Vector2(12f, 18f);
            rect.offsetMax = new Vector2(-12f, -18f);

            Image plate = host.AddComponent<Image>();
            plate.sprite = BombHudTextures.Plate;
            plate.type = Image.Type.Sliced;
            plate.color = TabIdle;
            tabPlates.Add(plate);

            Button button = host.AddComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(() => SelectTab(tabIndex));

            GameObject labelHost = NewUIObject("Label", host.transform);
            TextMeshProUGUI label = labelHost.AddComponent<TextMeshProUGUI>();
            label.text = TabNames[index];
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 34f;
            label.fontStyle = FontStyles.Bold;
            label.color = TabIdleLabel;
            label.raycastTarget = false;
            Stretch(labelHost.GetComponent<RectTransform>());
            tabLabels.Add(label);
        }
    }

    // ----------------------------------------------------------------- behaviour

    public void SelectTab(int index)
    {
        for (int i = 0; i < panels.Count; i++)
        {
            panels[i].SetActive(i == index);
            tabPlates[i].color = i == index ? TabActive : TabIdle;
            tabLabels[i].color = i == index ? TabActiveLabel : TabIdleLabel;
        }
    }

    private void EnterLevel()
    {
        if (sceneLoadRequested)
        {
            return;
        }

        int level = NextLevel;
        if (!LevelNaming.TryResolveLoadableSceneName(level, out string sceneName))
        {
            Debug.LogWarning($"Level {level} is not in Build Settings.", this);
            return;
        }

        sceneLoadRequested = true;
        SceneManager.LoadScene(sceneName);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// A scene with no EventSystem renders the menu and ignores every tap, which
    /// looks like broken buttons rather than a missing object.
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject host = new GameObject("EventSystem");
        host.AddComponent<EventSystem>();
        host.AddComponent<StandaloneInputModule>();
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
}
