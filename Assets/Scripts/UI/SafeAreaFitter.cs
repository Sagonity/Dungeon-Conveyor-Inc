using UnityEngine;

/// <summary>
/// Fits this RectTransform to Screen.safeArea so UI stays clear of notches,
/// camera cutouts and rounded corners. Re-applies when the device rotates.
/// Put screen content under a child with this component; keep full-screen
/// backgrounds on the parent so they still reach the screen edges.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform rectTransform;
    Rect appliedSafeArea;
    Vector2Int appliedScreenSize;

    void Awake()
    {
        rectTransform = (RectTransform)transform;
    }

    void OnEnable()
    {
        Apply();
    }

    void Update()
    {
        if (Screen.safeArea != appliedSafeArea
            || Screen.width != appliedScreenSize.x
            || Screen.height != appliedScreenSize.y)
        {
            Apply();
        }
    }

    void Apply()
    {
        if (rectTransform == null) rectTransform = (RectTransform)transform;

        Rect safeArea = Screen.safeArea;
        appliedSafeArea = safeArea;
        appliedScreenSize = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 min = safeArea.position;
        Vector2 max = safeArea.position + safeArea.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        rectTransform.anchorMin = min;
        rectTransform.anchorMax = max;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
