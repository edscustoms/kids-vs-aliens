using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private IEnumerator Start()
    {
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
        if (progressFill == null)
            return;

        Vector3 scale = progressFill.localScale;

        scale.x = Mathf.Clamp01(progress);

        progressFill.localScale = scale;
    }
}
