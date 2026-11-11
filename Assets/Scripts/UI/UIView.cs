using System.Collections;
using UnityEngine;

/// <summary>
/// Base class for a screen controlled by UIManager. Shows and hides itself by
/// fading a CanvasGroup (using unscaled time, so it still animates while paused).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public abstract class UIView : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Deactivate the GameObject once fully hidden so it costs nothing to render.")]
    [SerializeField] private bool deactivateWhenHidden = true;

    Coroutine fadeRoutine;
    bool initialized;

    protected UIManager Manager { get; private set; }
    public bool IsVisible { get; private set; }

    CanvasGroup Group
    {
        get
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            return canvasGroup;
        }
    }

    /// <summary>Called once by UIManager in its Awake, even if this view starts inactive.</summary>
    public void Initialize(UIManager manager)
    {
        if (initialized) return;
        initialized = true;
        Manager = manager;
        OnInitialize();
    }

    public void Show(float duration)
    {
        IsVisible = true;
        gameObject.SetActive(true);
        Group.interactable = true;
        Group.blocksRaycasts = true;
        OnShow();
        FadeTo(1f, duration);
    }

    public void Hide(float duration)
    {
        bool wasVisible = IsVisible;
        IsVisible = false;
        // Stop input immediately so the outgoing screen can't be clicked mid-fade.
        Group.interactable = false;
        Group.blocksRaycasts = false;
        if (wasVisible) OnHide();
        FadeTo(0f, duration);
    }

    /// <summary>Set up button listeners and event subscriptions here.</summary>
    protected virtual void OnInitialize() { }
    protected virtual void OnShow() { }
    protected virtual void OnHide() { }

    void OnDisable()
    {
        // Unity stops coroutines on disable; forget the handle.
        fadeRoutine = null;
    }

    void FadeTo(float target, float duration)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        // Scale the time by how far we have to go, so interrupted fades stay smooth.
        float scaledDuration = duration * Mathf.Abs(target - Group.alpha);
        if (scaledDuration <= 0f || !isActiveAndEnabled)
        {
            Group.alpha = target;
            OnFadeComplete();
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(target, scaledDuration));
    }

    IEnumerator FadeRoutine(float target, float duration)
    {
        float start = Group.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Group.alpha = Mathf.Lerp(start, target, t * t * (3f - 2f * t)); // smoothstep
            yield return null;
        }
        Group.alpha = target;
        fadeRoutine = null;
        OnFadeComplete();
    }

    void OnFadeComplete()
    {
        if (!IsVisible && deactivateWhenHidden)
        {
            gameObject.SetActive(false);
        }
    }
}
