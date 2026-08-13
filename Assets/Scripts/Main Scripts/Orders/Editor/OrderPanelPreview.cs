using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the Orders card to a PNG without entering Play Mode.
///
/// Follows LoadingScreenPreview's precedent. A UI skin that is only visible by
/// launching the game and reaching a level is a skin that gets tuned by guesswork;
/// this makes the card inspectable in one click, at the real reference resolution,
/// with a representative set of orders.
/// </summary>
public static class OrderPanelPreview
{
    private const string OutputFolder = "Builds/Previews";
    private static readonly Vector2Int ReferenceResolution = new Vector2Int(1125, 2436);

    [MenuItem("Tools/Coca Sorting/Preview Orders Panel", priority = 103)]
    public static void RenderPreview()
    {
        string path = Render(new[]
        {
            (Soda.SodaColor.Pink, 2),
            (Soda.SodaColor.Orange, 3),
            (Soda.SodaColor.Green, 0)
        });

        if (!string.IsNullOrEmpty(path))
        {
            Debug.Log("Orders panel preview written to " + path);
            EditorUtility.RevealInFinder(path);
        }
    }

    /// <summary>
    /// Builds a throwaway canvas holding just the card, renders it, and tears the
    /// whole rig down. A count of 0 renders that slot in its completed state.
    /// </summary>
    public static string Render((Soda.SodaColor color, int remaining)[] orders, string fileName = "OrdersPanel.png")
    {
        GameObject rig = new GameObject("~OrderPanelPreviewRig");
        rig.hideFlags = HideFlags.HideAndDontSave;

        RenderTexture target = null;
        Texture2D readback = null;

        try
        {
            // World space, not screen space. A manual Camera.Render() outside Play
            // Mode does not draw screen-space canvases - the UI pass is injected
            // elsewhere in the frame - so the rig would come back empty. A
            // world-space canvas is ordinary geometry and renders reliably.
            const float unitsPerPixel = 0.01f;

            Canvas canvas = rig.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            // Far from any real level geometry, so the open scene's board and
            // trucks cannot appear behind the card. Same trick SodaIconBaker uses.
            Vector3 rigOrigin = new Vector3(0f, -10000f, 0f);

            RectTransform canvasRect = rig.GetComponent<RectTransform>();
            canvasRect.sizeDelta = ReferenceResolution;
            canvasRect.localScale = Vector3.one * unitsPerPixel;
            canvasRect.position = rigOrigin;

            GameObject cameraObject = new GameObject("PreviewCamera");
            cameraObject.transform.SetParent(rig.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;

            // A mid blue stands in for the board, so the card is judged against
            // roughly what sits behind it in game rather than against black.
            camera.backgroundColor = new Color(0.44f, 0.62f, 0.86f, 1f);
            camera.orthographic = true;
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            canvas.worldCamera = camera;

            GameObject panel = BuildCard(canvas.transform, orders);

            OrderPanelSkin skin = panel.GetComponent<OrderPanelSkin>();
            if (skin == null)
            {
                skin = panel.AddComponent<OrderPanelSkin>();
            }

            skin.Apply();
            Canvas.ForceUpdateCanvases();
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            Canvas.ForceUpdateCanvases();

            // Frame the card rather than the whole phone screen: at full-screen
            // scale the card is a thumbnail and its detail cannot be judged.
            Vector3[] corners = new Vector3[4];
            panelRect.GetWorldCorners(corners);
            Vector3 centre = (corners[0] + corners[2]) * 0.5f;
            float cardWidth = Vector3.Distance(corners[0], corners[3]);
            float cardHeight = Vector3.Distance(corners[0], corners[1]);

            const float padding = 1.35f;
            float aspect = 16f / 9f;
            camera.orthographicSize = Mathf.Max(cardHeight, cardWidth / aspect) * 0.5f * padding;
            camera.transform.position = new Vector3(centre.x, centre.y, centre.z - 10f);

            int height = 640;
            target = new RenderTexture(Mathf.RoundToInt(height * aspect), height, 24)
            {
                antiAliasing = 8
            };
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(OutputFolder);
            string path = Path.Combine(OutputFolder, fileName);
            File.WriteAllBytes(path, readback.EncodeToPNG());
            return Path.GetFullPath(path);
        }
        catch (System.Exception exception)
        {
            Debug.LogError("Orders panel preview failed: " + exception);
            return null;
        }
        finally
        {
            if (target != null)
            {
                // Cleared first: releasing a texture still bound as a camera target
                // logs an error.
                foreach (Camera camera in rig.GetComponentsInChildren<Camera>(true))
                {
                    camera.targetTexture = null;
                }

                target.Release();
                Object.DestroyImmediate(target);
            }

            if (readback != null)
            {
                Object.DestroyImmediate(readback);
            }

            Object.DestroyImmediate(rig);
        }
    }

    /// <summary>
    /// Recreates the hierarchy OrderPanelBuilder produces, so the preview exercises
    /// the same structure the skin meets in a real scene.
    /// </summary>
    private static GameObject BuildCard(Transform parent, (Soda.SodaColor color, int remaining)[] orders)
    {
        GameObject panel = NewRect("OrdersPanel", parent);

        GameObject border = NewImage("Border", panel.transform, Color.white);
        NewImage("Fill", border.transform, Color.white);

        GameObject tab = NewImage("LabelTab", panel.transform, Color.white);
        GameObject labelObject = NewRect("Text", tab.transform);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "ORDERS";
        label.alignment = TextAlignmentOptions.Center;

        GameObject container = NewRect("SlotContainer", panel.transform);
        HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        SodaVisualLibrary library = SodaVisualLibrary.Resolve();

        for (int index = 0; index < orders.Length; index++)
        {
            if (index > 0)
            {
                GameObject separator = NewImage("Separator", container.transform, Color.white);
                separator.GetComponent<RectTransform>().sizeDelta = new Vector2(3f, 58f);
            }

            GameObject slotObject = NewRect("Slot" + index, container.transform);
            OrderSlotUI slot = slotObject.AddComponent<OrderSlotUI>();

            GameObject glow = NewImage("Glow", slotObject.transform, new Color(1f, 1f, 1f, 0f));
            GameObject icon = NewImage("Icon", slotObject.transform, Color.white);
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(74f, 74f);
            iconRect.anchoredPosition = new Vector2(0f, 10f);

            Image iconImage = icon.GetComponent<Image>();
            iconImage.preserveAspect = true;
            if (library != null)
            {
                iconImage.sprite = library.GetIcon(orders[index].color);
            }

            GameObject countObject = NewRect("Count", slotObject.transform);
            TextMeshProUGUI count = countObject.AddComponent<TextMeshProUGUI>();
            count.text = orders[index].remaining.ToString();
            count.alignment = TextAlignmentOptions.Center;
            count.fontSize = 34f;
            count.fontStyle = FontStyles.Bold;
            countObject.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 40f);
            countObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -36f);

            GameObject check = NewImage("Checkmark", slotObject.transform, new Color(0.24f, 0.72f, 0.26f, 1f));
            check.GetComponent<Image>().sprite = OrderVfxTextures.Checkmark;
            check.GetComponent<RectTransform>().sizeDelta = new Vector2(44f, 44f);
            check.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -36f);

            bool complete = orders[index].remaining <= 0;
            check.SetActive(complete);
            countObject.SetActive(!complete);

            SerializedObject serialized = new SerializedObject(slot);
            serialized.FindProperty("iconImage").objectReferenceValue = iconImage;
            serialized.FindProperty("iconTransform").objectReferenceValue = iconRect;
            serialized.FindProperty("countText").objectReferenceValue = count;
            serialized.FindProperty("checkmarkRoot").objectReferenceValue = check;
            serialized.FindProperty("glowImage").objectReferenceValue = glow.GetComponent<Image>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        return panel;
    }

    private static GameObject NewRect(string name, Transform parent)
    {
        GameObject created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    private static GameObject NewImage(string name, Transform parent, Color color)
    {
        GameObject created = NewRect(name, parent);
        Image image = created.AddComponent<Image>();
        image.color = color;
        return created;
    }
}
