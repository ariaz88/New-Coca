using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click scene setup for the Orders panel.
///
/// Building this card by hand means creating roughly a dozen nested RectTransforms
/// with exact anchors, then wiring six serialized references without a mistake.
/// This tool generates the whole hierarchy with placeholder art from Unity's own
/// built-in sprites, so the feature can be played and tested before any final
/// artwork exists. Artists then replace the sprites in place; no script has to
/// change afterwards.
///
/// It never overwrites: if a panel already exists under the chosen Canvas the
/// tool selects it and stops.
/// </summary>
public static class OrderPanelBuilder
{
    private const string PanelName = "OrdersPanel";

    // The reference card's palette, sampled from the gameplay video.
    private static readonly Color CardBorderColor = new Color(0.78f, 0.13f, 0.16f, 1f);
    private static readonly Color CardFillColor = new Color(0.98f, 0.93f, 0.78f, 1f);
    private static readonly Color LabelColor = new Color(0.78f, 0.13f, 0.16f, 1f);
    private static readonly Color SeparatorColor = new Color(0.55f, 0.44f, 0.28f, 1f);
    private static readonly Color CountColor = new Color(0.20f, 0.16f, 0.12f, 1f);
    private static readonly Color CheckColor = new Color(0.25f, 0.72f, 0.27f, 1f);

    [MenuItem("Tools/Coca Sorting/Create Orders Panel", false, 100)]
    public static void CreateOrdersPanel()
    {
        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog(
                "Create Orders Panel",
                "No Canvas was found in the open scene. Select a Canvas, or create one, then run this again.",
                "OK");
            return;
        }

        Transform existing = canvas.transform.Find(PanelName);
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorUtility.DisplayDialog(
                "Create Orders Panel",
                "This Canvas already has an OrdersPanel. It has been selected instead of creating a second one.",
                "OK");
            return;
        }

        GameObject panelObject = BuildPanel(canvas);
        Undo.RegisterCreatedObjectUndo(panelObject, "Create Orders Panel");
        Selection.activeGameObject = panelObject;

        EnsureOrderManager();

        Debug.Log(
            "Orders panel created with placeholder art. Assign a SodaVisualLibrary asset on " +
            "OrderPanelUI, then replace the placeholder sprites with the real card artwork.",
            panelObject);
    }

    private static GameObject BuildPanel(Canvas canvas)
    {
        // Root: anchored to the top centre of the canvas, matching the reference.
        GameObject panelObject = CreateUIObject(PanelName, canvas.transform);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -60f);

        // Tighter than before. The card used to be far wider than its contents,
        // which left the drinks looking small and stranded; the reference card
        // hugs its slots.
        panelRect.sizeDelta = new Vector2(360f, 150f);

        // Border and fill are two stacked images so the red outline thickness can
        // be tuned without a nine-sliced custom sprite.
        GameObject borderObject = CreateImage("Border", panelRect, CardBorderColor);
        Stretch(borderObject.GetComponent<RectTransform>());

        GameObject fillObject = CreateImage("Fill", borderObject.GetComponent<RectTransform>(), CardFillColor);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        Stretch(fillRect);
        fillRect.offsetMin = new Vector2(8f, 8f);
        fillRect.offsetMax = new Vector2(-8f, -8f);

        // "Orders" tab sitting on the top edge of the card.
        GameObject labelObject = CreateImage("LabelTab", panelRect, LabelColor);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(168f, 50f);
        labelRect.anchoredPosition = new Vector2(0f, 4f);

        TextMeshProUGUI labelText = CreateText("Text", labelRect, "Orders", 32f, Color.white);
        labelText.fontStyle = FontStyles.Bold;
        Stretch(labelText.rectTransform);

        // Horizontal strip that receives the generated slots.
        GameObject containerObject = CreateUIObject("SlotContainer", panelRect);
        RectTransform containerRect = containerObject.GetComponent<RectTransform>();
        Stretch(containerRect);
        containerRect.offsetMin = new Vector2(16f, 14f);
        containerRect.offsetMax = new Vector2(-16f, -24f);

        HorizontalLayoutGroup layout = containerObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 10f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject separatorTemplate = BuildSeparatorTemplate(panelRect);
        OrderSlotUI slotTemplate = BuildSlotTemplate(panelRect);

        OrderPanelUI panelUI = panelObject.AddComponent<OrderPanelUI>();
        SerializedObject serializedPanel = new SerializedObject(panelUI);
        serializedPanel.FindProperty("slotPrefab").objectReferenceValue = slotTemplate;
        serializedPanel.FindProperty("slotContainer").objectReferenceValue = containerRect;
        serializedPanel.FindProperty("separatorPrefab").objectReferenceValue = separatorTemplate;

        // Wired here so a panel created after the icons were baked works without
        // anyone remembering to drag the asset in.
        SodaVisualLibrary library = FindVisualLibrary();
        if (library != null)
        {
            serializedPanel.FindProperty("visualLibrary").objectReferenceValue = library;
        }

        serializedPanel.ApplyModifiedPropertiesWithoutUndo();

        // Added to the same object, because OrderPanelUI finds its impact
        // presenter by searching its own hierarchy. Its streak and sparkle
        // sprites are generated at runtime, so only the tint source is wired.
        OrderVfxDirector director = panelObject.AddComponent<OrderVfxDirector>();
        if (library != null)
        {
            SerializedObject serializedDirector = new SerializedObject(director);
            serializedDirector.FindProperty("visualLibrary").objectReferenceValue = library;
            serializedDirector.ApplyModifiedPropertiesWithoutUndo();
        }

        return panelObject;
    }

    /// <summary>
    /// The slot template stays an inactive child of the panel rather than a
    /// prefab asset, so the whole setup lives in the scene and needs no folder
    /// decisions from the person running the tool.
    /// </summary>
    private static OrderSlotUI BuildSlotTemplate(RectTransform panelRect)
    {
        GameObject slotObject = CreateUIObject("SlotTemplate", panelRect);
        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(104f, 104f);

        // Glow sits behind the icon and starts fully transparent. Its sprite and
        // final size are replaced at runtime by OrderSlotUI with a generated
        // radial halo: the built-in rounded rectangle used here reads as a
        // yellow box and its hard edge spilled past the card border.
        GameObject glowObject = CreateImage("Glow", slotRect, new Color(1f, 0.92f, 0.55f, 0f));
        RectTransform glowRect = glowObject.GetComponent<RectTransform>();
        glowRect.sizeDelta = new Vector2(118f, 118f);

        Image glowImage = glowObject.GetComponent<Image>();
        glowImage.raycastTarget = false;
        glowImage.type = Image.Type.Simple;

        GameObject iconObject = CreateImage("Icon", slotRect, Color.white);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(88f, 88f);
        iconRect.anchoredPosition = new Vector2(0f, 6f);
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = null;
        iconImage.enabled = false;
        iconImage.raycastTarget = false;

        // A soda is taller than it is wide. Without this the square slot would
        // stretch the baked sprite into a rectangle.
        iconImage.type = Image.Type.Simple;
        iconImage.preserveAspect = true;

        TextMeshProUGUI countText = CreateText("Count", slotRect, "1", 40f, CountColor);
        RectTransform countRect = countText.rectTransform;
        countRect.anchorMin = new Vector2(1f, 0f);
        countRect.anchorMax = new Vector2(1f, 0f);
        countRect.pivot = new Vector2(1f, 0f);
        countRect.sizeDelta = new Vector2(46f, 46f);
        countRect.anchoredPosition = new Vector2(2f, 0f);
        countText.alignment = TextAlignmentOptions.BottomRight;
        countText.fontStyle = FontStyles.Bold;

        // OrderSlotUI replaces this sprite at runtime with a generated tick. The
        // placeholder used to be a plain green square, which was the most
        // obvious difference from the reference once an order completed.
        GameObject checkObject = CreateImage("Checkmark", slotRect, CheckColor);
        RectTransform checkRect = checkObject.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(1f, 0f);
        checkRect.anchorMax = new Vector2(1f, 0f);
        checkRect.pivot = new Vector2(1f, 0f);
        checkRect.sizeDelta = new Vector2(42f, 42f);
        checkRect.anchoredPosition = new Vector2(2f, -2f);

        Image checkImage = checkObject.GetComponent<Image>();
        checkImage.raycastTarget = false;
        checkImage.type = Image.Type.Simple;
        checkImage.preserveAspect = true;
        checkObject.SetActive(false);

        OrderSlotUI slotUI = slotObject.AddComponent<OrderSlotUI>();
        SerializedObject serializedSlot = new SerializedObject(slotUI);
        serializedSlot.FindProperty("iconImage").objectReferenceValue = iconImage;
        serializedSlot.FindProperty("iconTransform").objectReferenceValue = iconRect;
        serializedSlot.FindProperty("countText").objectReferenceValue = countText;
        serializedSlot.FindProperty("checkmarkRoot").objectReferenceValue = checkObject;
        serializedSlot.FindProperty("glowImage").objectReferenceValue = glowObject.GetComponent<Image>();
        serializedSlot.ApplyModifiedPropertiesWithoutUndo();

        slotObject.SetActive(false);
        return slotUI;
    }

    private static GameObject BuildSeparatorTemplate(RectTransform panelRect)
    {
        // Wider and darker than the first version, which was a 2 px line in a
        // low-contrast tan that was invisible on screen.
        GameObject separatorObject = CreateImage("SeparatorTemplate", panelRect, SeparatorColor);
        RectTransform separatorRect = separatorObject.GetComponent<RectTransform>();
        separatorRect.sizeDelta = new Vector2(4f, 82f);
        separatorObject.GetComponent<Image>().raycastTarget = false;
        separatorObject.SetActive(false);
        return separatorObject;
    }

    /// <summary>
    /// Adds an OrderManager to the scene when none exists, because the panel
    /// renders nothing without one and the omission is easy to miss.
    /// </summary>
    private static void EnsureOrderManager()
    {
        if (Object.FindFirstObjectByType<OrderManager>() != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("OrderManager");
        managerObject.AddComponent<OrderManager>();
        Undo.RegisterCreatedObjectUndo(managerObject, "Create Order Manager");
    }

    private static SodaVisualLibrary FindVisualLibrary()
    {
        string[] found = AssetDatabase.FindAssets("t:SodaVisualLibrary");
        return found.Length > 0
            ? AssetDatabase.LoadAssetAtPath<SodaVisualLibrary>(AssetDatabase.GUIDToAssetPath(found[0]))
            : null;
    }

    private static Canvas ResolveCanvas()
    {
        if (Selection.activeGameObject != null)
        {
            Canvas selected = Selection.activeGameObject.GetComponentInParent<Canvas>();
            if (selected != null)
            {
                return selected.rootCanvas;
            }
        }

        Canvas found = Object.FindFirstObjectByType<Canvas>();
        return found != null ? found.rootCanvas : null;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject created = CreateUIObject(name, parent);
        Image image = created.AddComponent<Image>();
        image.color = color;

        // Unity's built-in rounded sprite. Keeps placeholders from looking like
        // hard rectangles while real art is still being produced.
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        return created;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string content,
        float size,
        Color color)
    {
        GameObject created = CreateUIObject(name, parent);
        TextMeshProUGUI text = created.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
