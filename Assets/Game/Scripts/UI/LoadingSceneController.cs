using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[DefaultExecutionOrder(-32000)]
public class LoadingSceneController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField]
    private string targetSceneName = "GamePoc";

    [Header("Loading Bar")]
    [SerializeField]
    private RectTransform progressFill;

    [Header("Timing")]
    [SerializeField]
    private float minimumDisplayTime = 1.5f;

    [SerializeField]
    private float fullBarHoldTime = 0.15f;

    // The crossover reuses this exact scene/canvas. Default startup below is unchanged.
    public static bool PresentationOnlyRequested { private get; set; }
    public static LoadingSceneController Presentation { get; private set; }
    public bool IsPresentationOnly { get; private set; }
    public bool IsVisible => presentationCanvas != null && presentationCanvas.enabled;
    public bool IsFullyVisible => IsVisible && presentationOpacity.alpha >= .999f;
    public float DisplayedProgress { get; private set; }
    private Canvas presentationCanvas;
    private CanvasGroup presentationOpacity;
    private TMP_Text destinationLabel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPresentation() { PresentationOnlyRequested = false; Presentation = null; }

    private void Awake()
    {
        if (!PresentationOnlyRequested) return;
        PresentationOnlyRequested = false;
        IsPresentationOnly = true; Presentation = this;
        presentationCanvas = progressFill.GetComponentInParent<Canvas>();
        presentationOpacity = presentationCanvas.gameObject.AddComponent<CanvasGroup>();
        presentationCanvas.enabled = false;
        presentationCanvas.sortingOrder = short.MaxValue;
        // These belong only to the loading scene. The travelling rig remains authoritative.
        foreach (var root in gameObject.scene.GetRootGameObjects())
            if (root.GetComponentInChildren<Camera>(true) != null
                || root.GetComponentInChildren<EventSystem>(true) != null
                || root.GetComponentInChildren<Light>(true) != null) root.SetActive(false);
    }
    public void ConfigurePresentation(string destinationTitle, string loadingText, Sprite artwork)
    {
        if (!IsPresentationOnly) throw new System.InvalidOperationException("Startup loading cannot be externally driven.");
        if (artwork != null)
            foreach (var image in presentationCanvas.GetComponentsInChildren<Image>(true))
                if (image.sprite != null) { image.sprite = artwork; break; }
        if (destinationLabel == null)
        {
            var go = new GameObject("Destination loading label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(progressFill.parent, false);
            destinationLabel = go.GetComponent<TextMeshProUGUI>();
            destinationLabel.alignment = TextAlignmentOptions.Center;
            destinationLabel.fontSize = 24; destinationLabel.color = Color.white;
            destinationLabel.outlineWidth = .2f; destinationLabel.outlineColor = Color.black;
            destinationLabel.raycastTarget = false;
            var rect = destinationLabel.rectTransform;
            rect.anchorMin = new Vector2(0,1); rect.anchorMax = new Vector2(1,1);
            rect.pivot = new Vector2(.5f,0); rect.anchoredPosition = new Vector2(0,16); rect.sizeDelta = new Vector2(0,80);
        }
        destinationLabel.text = destinationTitle + "\n" + loadingText;
    }
    public void ShowPresentation(bool visible) { if (presentationCanvas != null) presentationCanvas.enabled = visible; }
    public IEnumerator FadeOutPresentation(float duration)
    {
        if (!IsPresentationOnly) yield break;
        float elapsed = 0;
        while (elapsed < duration)
        {
            presentationOpacity.alpha = 1f - Mathf.SmoothStep(0f, 1f, elapsed / duration);
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        ShowPresentation(false);
        presentationOpacity.alpha = 1f;
    }
    public void SetActualProgress(float progress) { if (IsPresentationOnly) SetProgress(progress); }
    private void OnDestroy() { if (Presentation == this) Presentation = null; }

    private IEnumerator Start()
    {
        if (IsPresentationOnly) yield break;
        SetProgress(0f);

        // Make sure the loading screen actually renders once
        // before we start loading the next scene.
        Canvas.ForceUpdateCanvases();
        yield return null;

        float startTime = Time.unscaledTime;

        AsyncOperation operation = SceneManager.LoadSceneAsync(targetSceneName);

        if (operation == null)
        {
            Debug.LogError($"Could not load scene: {targetSceneName}");

            yield break;
        }

        operation.allowSceneActivation = false;

        while (true)
        {
            // Unity async loading stops around 0.9
            // until scene activation is allowed.
            float actualProgress = Mathf.Clamp01(operation.progress / 0.9f);

            float elapsed = Time.unscaledTime - startTime;

            float timedProgress =
                minimumDisplayTime <= 0f ? 1f : Mathf.Clamp01(elapsed / minimumDisplayTime);

            // Fast phone:
            // animate over minimumDisplayTime.
            //
            // Slow phone:
            // never visually outrun the real load.
            float displayedProgress = Mathf.Min(actualProgress, timedProgress);

            SetProgress(displayedProgress);

            bool sceneLoaded = operation.progress >= 0.9f;

            bool minimumTimeReached = elapsed >= minimumDisplayTime;

            if (sceneLoaded && minimumTimeReached)
                break;

            yield return null;
        }

        SetProgress(1f);

        // Let 100% actually be visible briefly.
        yield return new WaitForSecondsRealtime(fullBarHoldTime);

        operation.allowSceneActivation = true;
    }

    private void SetProgress(float progress)
    {
        DisplayedProgress = Mathf.Clamp01(progress);
        if (progressFill == null)
            return;

        Vector3 scale = progressFill.localScale;

        scale.x = Mathf.Clamp01(progress);

        progressFill.localScale = scale;
    }
}
