using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation for the loading screen. Owns every moving part of the view so that
/// <see cref="LoadManager"/> only has to push a raw 0..1 progress value at it.
///
/// The artwork is three layers of one painted composition - background, bubbles, title.
/// The background is still; the title breathes and the bubbles drift with it.
/// </summary>
public class LoadingScreenUI : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private RectTransform background;
    [Tooltip("Shows the complete portrait artwork on every portrait screen by stretching it to the canvas. This deliberately avoids AspectRatioFitter cropping.")]
    [SerializeField] private bool stretchBackgroundToScreen = true;
    [Tooltip("Minimum canvas-space gap above the logo on short/wide portrait displays.")]
    [SerializeField] private float titleTopSafeMargin = 36f;

    [Header("Bar")]
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform fillArea;
    [Tooltip("Left-pivoted rect with a RectMask2D, resized to the filled width so the shine cannot spill onto the empty track.")]
    [SerializeField] private RectTransform fillClip;
    [SerializeField] private RectTransform shine;
    [SerializeField] private TMP_Text percentText;

    [Header("Copy")]
    [SerializeField] private TMP_Text loadingLabel;
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private string loadingWord = "LOADING";
    [SerializeField] private float tipInterval = 3.2f;
    [SerializeField]
    private string[] tips =
    {
        "Match three sodas of the same colour to clear them.",
        "A full carrier box ships out on its own.",
        "Watch the order panel - it tells you which colours are wanted.",
        "Empty a slot before it locks you out of a match.",
        "Plan two moves ahead - the queue never stops.",
    };

    [Header("Title")]
    [Tooltip("Logo layer. The sprite is cropped tight, so it scales about its own centre.")]
    [SerializeField] private RectTransform title;
    [Range(0.5f, 1f)]
    [SerializeField] private float titleMinScale = 0.88f;
    [SerializeField] private float titlePulsePeriod = 2.4f;

    [Header("Bubbles")]
    [SerializeField] private RectTransform bubbles;
    [SerializeField] private CanvasGroup bubblesGroup;
    [Tooltip("Very small movement for the painted reference layer. Individual droplets provide the visible motion.")]
    [SerializeField] private float bubbleRise = 9f;
    [SerializeField] private float bubbleSway = 5f;
    [SerializeField] private float bubbleSwayPeriod = 7.2f;
    [Range(0f, 1f)]
    [SerializeField] private float bubbleMinAlpha = 0.72f;
    [Tooltip("A restrained number of independently animated bubbles around the blue logo silhouette.")]
    [Range(0, 12)]
    [SerializeField] private int bubbleDropletCount = 7;
    [SerializeField] private Vector2 bubbleDropletSize = new Vector2(22f, 40f);
    [SerializeField] private float bubbleDropletCycle = 4.8f;
    [SerializeField] private float bubbleDropletSpread = 46f;
    [SerializeField] private float bubbleDropletDrip = 30f;
    [SerializeField] private Color bubbleDropletTint = new Color(0.72f, 0.93f, 1f, 0.9f);

    [Header("Feel")]
    [Tooltip("Seconds the bar takes to catch up to a jump in progress. Keeps the fill from snapping.")]
    [SerializeField] private float fillSmoothing = 0.25f;
    [Tooltip("Seconds for one shine pass across the filled portion of the bar.")]
    [SerializeField] private float shinePeriod = 1.4f;

    // Built once: avoids both per-frame allocation and TMP's placeholder-precision defaults,
    // which would happily render "62.0%".
    private static readonly string[] PercentLabels = BuildPercentLabels();

    private static string[] BuildPercentLabels()
    {
        var labels = new string[101];
        for (int i = 0; i <= 100; i++) labels[i] = i + "%";
        return labels;
    }

    private CanvasGroup canvasGroup;
    private float targetProgress;
    private float shownProgress;
    private float fillVelocity;
    private int lastShownPercent = -1;
    private int lastShownDots = -1;
    private string[] labelFrames;
    private Coroutine tipRoutine;
    private Vector2 bubbleBasePosition;
    private RectTransform fillRect;
    private BubbleDroplet[] bubbleDroplets;
    private RectTransform bubbleDropletRoot;
    private Sprite[] bubbleDropletSprites;
    private float animationStartTime;
    private Vector2 titleDesignPosition;
    private bool titleDesignPositionCaptured;

    private sealed class BubbleDroplet
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 Origin;
        public Vector2 Direction;
        public float Size;
        public float Cycle;
        public float Phase;
        public float WobblePhase;
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyResponsiveLayout();
        SetupProgressBar();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (bubbles != null) bubbleBasePosition = bubbles.anchoredPosition;
        EnsureBubbleDroplets();
    }

    private void OnEnable()
    {
        animationStartTime = Time.unscaledTime;
        ResetView();
        if (tips != null && tips.Length > 0 && tipText != null)
            tipRoutine = StartCoroutine(CycleTips());
    }

    private void OnDisable()
    {
        if (tipRoutine != null)
        {
            StopCoroutine(tipRoutine);
            tipRoutine = null;
        }
    }

    private void OnDestroy()
    {
        if (bubbleDropletSprites != null)
        {
            foreach (Sprite sprite in bubbleDropletSprites)
            {
                if (sprite == null) continue;
                if (Application.isPlaying) Destroy(sprite);
                else DestroyImmediate(sprite);
            }
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled) ApplyResponsiveLayout();
    }

    /// <summary>Snaps the view back to an empty bar. Called whenever the screen is shown again.</summary>
    public void ResetView()
    {
        targetProgress = 0f;
        shownProgress = 0f;
        fillVelocity = 0f;
        lastShownPercent = -1;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        ApplyProgress(0f);
    }

    /// <summary>Raw loading progress, 0..1. The bar eases toward it rather than jumping.</summary>
    public void SetProgress(float value)
    {
        targetProgress = Mathf.Clamp01(value);
    }

    /// <summary>Progress the player can actually see, which lags <see cref="SetProgress"/> slightly.</summary>
    public float DisplayedProgress => shownProgress;

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        shownProgress = Mathf.SmoothDamp(shownProgress, targetProgress, ref fillVelocity, fillSmoothing, Mathf.Infinity, dt);
        if (targetProgress - shownProgress < 0.001f) shownProgress = targetProgress;
        ApplyProgress(shownProgress);

        AnimateTitle(Time.unscaledTime);
        AnimateShine(Time.unscaledTime);
        AnimateLabel(Time.unscaledTime);
    }

#if UNITY_EDITOR
    /// <summary>Editor-only hook so the breathing can be inspected without entering play mode.</summary>
    public void EditorPoseAt(float time, float progress = -1f)
    {
        ResolveReferences();
        ApplyResponsiveLayout();
        SetupProgressBar();
        if (bubbles != null) bubbleBasePosition = bubbles.anchoredPosition;
        EnsureBubbleDroplets();
        if (progress >= 0f)
        {
            shownProgress = Mathf.Clamp01(progress);
            targetProgress = shownProgress;
            ApplyProgress(shownProgress);
            AnimateShine(time);
        }
        AnimateTitle(time);
    }
#endif

    private void ApplyProgress(float p)
    {
        if (fillImage != null)
        {
            if (fillRect == null) SetupProgressBar();

            float fullWidth = fillArea != null
                ? fillArea.rect.width
                : ((RectTransform)fillImage.transform.parent).rect.width;
            float width = Mathf.Max(0f, fullWidth * p);
            fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            fillImage.enabled = width > 0.5f;
        }

        if (percentText != null)
        {
            int percent = Mathf.Clamp(Mathf.RoundToInt(p * 100f), 0, 100);
            if (percent != lastShownPercent)
            {
                lastShownPercent = percent;
                percentText.text = PercentLabels[percent];
            }
        }
    }

    /// <summary>
    /// Breathes the logo between <see cref="titleMinScale"/> and 1, eased at both ends, and drifts
    /// the bubbles with it. A raw sine eases correctly but never dwells at the extremes; a
    /// smoothstepped triangle pauses fractionally at each end, which is what reads as a breath.
    /// </summary>
    private void AnimateTitle(float t)
    {
        float triangle = Mathf.PingPong(t / Mathf.Max(0.1f, titlePulsePeriod) * 2f, 1f);
        float eased = triangle * triangle * (3f - 2f * triangle);

        if (title != null)
        {
            float scale = Mathf.Lerp(titleMinScale, 1f, eased);
            title.localScale = new Vector3(scale, scale, 1f);
        }

        if (bubbles != null)
        {
            // Rises as the logo swells. The sway runs on its own period so the two never
            // line up into an obvious repeat.
            float sway = Mathf.Sin(t / Mathf.Max(0.1f, bubbleSwayPeriod) * Mathf.PI * 2f) * bubbleSway;
            bubbles.anchoredPosition = bubbleBasePosition
                + new Vector2(sway, bubbleRise * (eased - 0.5f) * 2f);
        }

        if (bubblesGroup != null)
            bubblesGroup.alpha = Mathf.Lerp(bubbleMinAlpha, 1f, eased);

        AnimateBubbleDroplets(t);
    }

    private void AnimateShine(float t)
    {
        if (shine == null || fillArea == null || fillClip == null) return;

        // The clip rect tracks the filled width, so the sweep is cut off exactly at the
        // leading edge of the fill instead of running on over the empty track.
        float filledWidth = fillRect != null ? fillRect.rect.width : fillArea.rect.width * shownProgress;
        fillClip.sizeDelta = new Vector2(filledWidth, fillClip.sizeDelta.y);

        bool visible = filledWidth > 1f;
        if (shine.gameObject.activeSelf != visible) shine.gameObject.SetActive(visible);
        if (!visible) return;

        float phase = Mathf.Repeat(t / Mathf.Max(0.01f, shinePeriod), 1f);
        float travel = filledWidth + shine.rect.width;
        shine.anchoredPosition = new Vector2(-shine.rect.width * 0.5f + travel * phase, 0f);
    }

    private void AnimateLabel(float t)
    {
        if (loadingLabel == null) return;

        int dots = Mathf.FloorToInt(Mathf.Repeat(t * 2f, 4f));
        if (dots == lastShownDots) return;
        lastShownDots = dots;

        // Pre-built so the ticking dots don't allocate a string every frame.
        if (labelFrames == null)
        {
            labelFrames = new string[4];
            for (int i = 0; i < 4; i++) labelFrames[i] = loadingWord + new string('.', i);
        }
        loadingLabel.text = labelFrames[dots];
    }

    private void ResolveReferences()
    {
        if (background == null)
        {
            Transform found = transform.Find("BG");
            if (found != null) background = found as RectTransform;
        }

        if (title == null)
        {
            Transform found = transform.Find("Title");
            if (found != null) title = found as RectTransform;
        }

        if (bubbles == null)
        {
            Transform found = transform.Find("BG/Bubbles");
            if (found != null) bubbles = found as RectTransform;
        }

        if (title != null && !titleDesignPositionCaptured)
        {
            titleDesignPosition = title.anchoredPosition;
            titleDesignPositionCaptured = true;
        }
    }

    /// <summary>
    /// The reference art is already composed as one portrait image. Stretching the BG rect is
    /// intentional here: it is the only mode that both shows the entire image and leaves no
    /// uncovered pixels on differing portrait aspect ratios.
    /// </summary>
    private void ApplyResponsiveLayout()
    {
        if (stretchBackgroundToScreen && background != null)
        {
            var fitter = background.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.enabled = false;

            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.pivot = new Vector2(0.5f, 0.5f);
            background.anchoredPosition = Vector2.zero;
            background.sizeDelta = Vector2.zero;
            background.localScale = Vector3.one;

            var image = background.GetComponent<Image>();
            if (image != null) image.preserveAspect = false;
        }

        KeepTitleInsideCanvas();
    }

    private void KeepTitleInsideCanvas()
    {
        if (title == null || !titleDesignPositionCaptured) return;
        var parent = title.parent as RectTransform;
        if (parent == null || parent.rect.height <= 0f) return;

        title.anchoredPosition = titleDesignPosition;

        // Anchored position is relative to the title's anchor point, not to the parent's centre.
        // Compute the true upper edge so this also stays correct if the canvas pivot changes.
        float anchorY = Mathf.Lerp(parent.rect.yMin, parent.rect.yMax, title.anchorMin.y);
        float titleTop = anchorY + title.anchoredPosition.y + title.rect.height * (1f - title.pivot.y);
        float allowedTop = parent.rect.yMax - Mathf.Max(0f, titleTopSafeMargin);
        if (titleTop > allowedTop)
        {
            Vector2 position = title.anchoredPosition;
            position.y -= titleTop - allowedTop;
            title.anchoredPosition = position;
        }
    }

    /// <summary>
    /// Width-resizes a sliced fill instead of using Image.fillAmount. Image.fillAmount cuts a
    /// rounded sprite through the middle and produces the square/misaligned corners seen before.
    /// </summary>
    private void SetupProgressBar()
    {
        if (fillImage == null) return;

        fillRect = fillImage.rectTransform;
        fillImage.type = Image.Type.Sliced;
        fillImage.fillAmount = 1f;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(fillRect.sizeDelta.x, 0f);
    }

    private void EnsureBubbleDroplets()
    {
        if (title == null || bubbleDropletCount <= 0 || bubbleDroplets != null) return;

        var rootObject = new GameObject("BubbleDroplets", typeof(RectTransform));
        rootObject.hideFlags = HideFlags.DontSave;
        bubbleDropletRoot = rootObject.GetComponent<RectTransform>();
        bubbleDropletRoot.SetParent(title, false);
        bubbleDropletRoot.anchorMin = Vector2.zero;
        bubbleDropletRoot.anchorMax = Vector2.one;
        bubbleDropletRoot.pivot = new Vector2(0.5f, 0.5f);
        bubbleDropletRoot.anchoredPosition = Vector2.zero;
        bubbleDropletRoot.sizeDelta = Vector2.zero;
        bubbleDropletRoot.SetAsLastSibling();

        bubbleDropletSprites = CreateBubbleDropletSprites();
        if (bubbleDropletSprites == null || bubbleDropletSprites.Length == 0) return;
        bubbleDroplets = new BubbleDroplet[bubbleDropletCount];

        // Normalised points follow the actual opaque logo bounds. LS_Logo's lower 42% is
        // transparent, so centring these on the source texture would place them far too low.
        Vector2[] outlinePoints =
        {
            new Vector2(0.035f, 0.68f),
            new Vector2(0.14f, 0.89f),
            new Vector2(0.34f, 0.975f),
            new Vector2(0.61f, 0.985f),
            new Vector2(0.84f, 0.90f),
            new Vector2(0.97f, 0.70f),
            new Vector2(0.82f, 0.43f),
            new Vector2(0.18f, 0.43f),
        };
        Vector2[] directions =
        {
            new Vector2(-1f, 0.05f),
            new Vector2(-0.45f, 0.7f),
            new Vector2(-0.12f, 1f),
            new Vector2(0.16f, 1f),
            new Vector2(0.55f, 0.7f),
            new Vector2(1f, 0.02f),
            new Vector2(0.42f, -0.85f),
            new Vector2(-0.38f, -0.9f),
        };

        int count = bubbleDroplets.Length;
        for (int i = 0; i < count; i++)
        {
            var particleObject = new GameObject("Bubble_" + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            particleObject.hideFlags = HideFlags.DontSave;

            var rect = particleObject.GetComponent<RectTransform>();
            rect.SetParent(bubbleDropletRoot, false);
            rect.anchorMin = outlinePoints[i % outlinePoints.Length];
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);

            float hash = Mathf.Repeat(i * 0.6180339f, 1f);
            float size = Mathf.Lerp(bubbleDropletSize.x, bubbleDropletSize.y, hash);
            rect.sizeDelta = new Vector2(size, size);

            var image = particleObject.GetComponent<Image>();
            image.sprite = bubbleDropletSprites[i % bubbleDropletSprites.Length];
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = new Color(bubbleDropletTint.r, bubbleDropletTint.g, bubbleDropletTint.b, 0f);

            bubbleDroplets[i] = new BubbleDroplet
            {
                Rect = rect,
                Image = image,
                Origin = Vector2.zero,
                Direction = directions[i % directions.Length].normalized,
                Size = size,
                Cycle = Mathf.Max(2.5f, bubbleDropletCycle * Mathf.Lerp(0.88f, 1.16f, hash)),
                Phase = i / (float)count,
                WobblePhase = hash * Mathf.PI * 2f,
            };
        }
    }

    private void AnimateBubbleDroplets(float t)
    {
        if (bubbleDroplets == null) return;

        float elapsed = Mathf.Max(0f, t - animationStartTime);
        for (int i = 0; i < bubbleDroplets.Length; i++)
        {
            BubbleDroplet bubble = bubbleDroplets[i];
            float life = Mathf.Repeat(elapsed / bubble.Cycle + bubble.Phase, 1f);

            // Only part of each cycle is visible. This staggered rest keeps the effect sparse.
            const float visiblePart = 0.72f;
            if (life >= visiblePart)
            {
                Color hidden = bubble.Image.color;
                hidden.a = 0f;
                bubble.Image.color = hidden;
                continue;
            }

            float u = life / visiblePart;
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.18f, u));
            float disappear = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 1f, u));
            float alpha = appear * disappear * bubbleDropletTint.a;
            float travel = u * u * (3f - 2f * u);
            float wobble = Mathf.Sin(u * Mathf.PI * 2f + bubble.WobblePhase) * 5f * Mathf.Sin(u * Mathf.PI);

            Vector2 offset = bubble.Direction * bubbleDropletSpread * travel;
            offset.x += wobble;
            if (bubble.Direction.y < -0.2f)
                offset.y -= bubbleDropletDrip * travel * travel;
            else
                offset.y += 7f * Mathf.Sin(u * Mathf.PI);

            bubble.Rect.anchoredPosition = bubble.Origin + offset;
            float scale = Mathf.Lerp(0.55f, 1f, appear) * Mathf.Lerp(1f, 0.72f, travel);
            bubble.Rect.localScale = new Vector3(scale, scale, 1f);

            Color color = bubble.Image.color;
            color.a = alpha;
            bubble.Image.color = color;
        }
    }

    /// <summary>
    /// Reuses isolated, hand-painted bubbles from LS_Bubbles_Art. This keeps the animated
    /// droplets visually identical to the supplied reference instead of introducing a second
    /// procedural art style.
    /// </summary>
    private Sprite[] CreateBubbleDropletSprites()
    {
        Image sourceImage = bubbles != null ? bubbles.GetComponent<Image>() : null;
        Texture2D sourceTexture = sourceImage != null && sourceImage.sprite != null
            ? sourceImage.sprite.texture
            : null;
        if (sourceTexture == null) return null;

        // Pixel-space crops (bottom-left origin) of isolated bubbles in LS_Bubbles_Art.png.
        // A small transparent pad retains their antialiased rim.
        Rect[] sourceRects =
        {
            new Rect(764f, 1195f, 146f, 123f),
            new Rect(733f, 1300f, 66f, 68f),
            new Rect(175f, 1210f, 48f, 49f),
            new Rect(218f, 1264f, 47f, 47f),
            new Rect(872f, 1294f, 54f, 37f),
            new Rect(385f, 1314f, 35f, 36f),
        };

        var sprites = new Sprite[sourceRects.Length];
        for (int i = 0; i < sourceRects.Length; i++)
        {
            Rect rect = sourceRects[i];
            rect.x = Mathf.Clamp(rect.x, 0f, sourceTexture.width - 1f);
            rect.y = Mathf.Clamp(rect.y, 0f, sourceTexture.height - 1f);
            rect.width = Mathf.Min(rect.width, sourceTexture.width - rect.x);
            rect.height = Mathf.Min(rect.height, sourceTexture.height - rect.y);

            Sprite sprite = Sprite.Create(
                sourceTexture,
                rect,
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "Loading Bubble Crop " + (i + 1);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            sprites[i] = sprite;
        }
        return sprites;
    }

    private IEnumerator CycleTips()
    {
        int index = 0;
        while (true)
        {
            yield return FadeText(tipText, 0f, 0.25f);
            tipText.text = tips[index % tips.Length];
            index++;
            yield return FadeText(tipText, 1f, 0.35f);
            yield return new WaitForSecondsRealtime(tipInterval);
        }
    }

    private static IEnumerator FadeText(TMP_Text text, float to, float duration)
    {
        Color c = text.color;
        float from = c.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(from, to, elapsed / duration);
            text.color = c;
            yield return null;
        }
        c.a = to;
        text.color = c;
    }

    /// <summary>Fades the whole screen out before the loaded scene is revealed.</summary>
    public IEnumerator FadeOut(float duration)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        float from = canvasGroup.alpha;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, 0f, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }
}
