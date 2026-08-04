using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class MouseUIRaycaster : MonoBehaviour
{
    public static MouseUIRaycaster instance;

    private RectTransform currentHoveredRectTransform;

    /// <summary>
    /// Gets the RectTransform of the UI element under the mouse or touch.
    /// </summary>
    public RectTransform CurrentHoveredRectTransform
    {
        get { return currentHoveredRectTransform; }
    }

    private void Awake()
    {
        // Ensure only one instance of this script exists
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        UpdateHoveredUIElement();
    }

    private void UpdateHoveredUIElement()
    {
        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition // Mouse or touch position
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);

        currentHoveredRectTransform = null; // Reset before checking

        foreach (RaycastResult result in results)
        {
            if (result.gameObject != null)
            {
                RectTransform rectTransform = result.gameObject.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    currentHoveredRectTransform = rectTransform;
                    break; // Stop at the first valid result
                }
            }
        }
    }

    /// <summary>
    /// Converts the mouse position into a local position relative to the hovered RectTransform.
    /// </summary>
    public Vector2 GetMousePositionInHoveredRect()
    {
        if (currentHoveredRectTransform == null)
            return Vector2.zero;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            currentHoveredRectTransform,
            Input.mousePosition,
            Camera.main,
            out Vector2 localPoint
        );

        return localPoint;
    }
}
