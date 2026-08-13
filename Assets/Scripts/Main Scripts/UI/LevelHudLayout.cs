using TMPro;
using UnityEngine;

/// <summary>
/// Arranges the top of the gameplay HUD at runtime.
///
/// Applied in code rather than serialized into each scene, for the same reason
/// OrderPanelSkin is: the HUD is identical in all 25 level scenes, and editing
/// 25 scenes to move two objects invites 25 chances to get one of them wrong.
///
/// Nothing is deleted. The progress bar is deactivated, so its component,
/// references and artwork all survive and re-enabling it is a single flag.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelHudLayout : MonoBehaviour
{
    [Header("Progress bar")]
    [SerializeField, Tooltip("Hides the level progress bar. Deactivated only - nothing is destroyed, so turning this off restores it exactly as it was.")]
    private bool hideProgressBar = true;

    [SerializeField, Tooltip("Canvas child holding the progress bar.")]
    private string progressBarName = "LevelProgressionBar";

    [Header("Level label")]
    [SerializeField, Tooltip("Moves the level number into the empty strip at the very top of the screen.")]
    private bool moveLevelLabel = true;

    [SerializeField] private string levelHolderName = "LevelHolder";

    [SerializeField, Tooltip("Distance from the top of the screen to the centre of the level label, in canvas units.")]
    private float levelLabelTopMargin = 62f;

    [SerializeField, Min(0f), Tooltip("Font size for the level label once it is on its own at the top.")]
    private float levelLabelFontSize = 46f;

    private void Awake()
    {
        Apply();
    }

    public void Apply()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Transform root = canvas.transform;

        if (hideProgressBar)
        {
            Transform progressBar = root.Find(progressBarName);
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(false);
            }
        }

        if (!moveLevelLabel)
        {
            return;
        }

        RectTransform levelHolder = root.Find(levelHolderName) as RectTransform;
        if (levelHolder == null)
        {
            return;
        }

        // Anchored to top-centre rather than given an absolute position, so the
        // label stays centred and the same distance from the top edge on any
        // aspect ratio the phone happens to have.
        levelHolder.anchorMin = new Vector2(0.5f, 1f);
        levelHolder.anchorMax = new Vector2(0.5f, 1f);
        levelHolder.pivot = new Vector2(0.5f, 0.5f);
        levelHolder.anchoredPosition = new Vector2(0f, -levelLabelTopMargin);

        levelHolder.sizeDelta = new Vector2(420f, 70f);

        foreach (TextMeshProUGUI label in levelHolder.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            label.fontSize = levelLabelFontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;

            // The label carried its own offset inside the holder, placed for the
            // old top-right corner. Moving only the holder therefore dragged the
            // text off to the side instead of centring it, so the child is
            // stretched flat over the holder and its offset cleared.
            RectTransform labelRect = label.rectTransform;
            if (labelRect == levelHolder)
            {
                continue;
            }

            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.localScale = Vector3.one;
        }
    }
}
