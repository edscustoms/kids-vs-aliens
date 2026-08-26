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
    private float minimumDisplayTime = 1f;

    private IEnumerator Start()
    {
        SetProgress(0f);

        float startTime = Time.unscaledTime;

        AsyncOperation operation = SceneManager.LoadSceneAsync(targetSceneName);

        if (operation == null)
        {
            Debug.LogError($"Could not load scene: {targetSceneName}");

            yield break;
        }

        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            SetProgress(progress);

            yield return null;
        }

        SetProgress(1f);

        float elapsed = Time.unscaledTime - startTime;

        if (elapsed < minimumDisplayTime)
        {
            yield return new WaitForSecondsRealtime(minimumDisplayTime - elapsed);
        }

        operation.allowSceneActivation = true;
    }

    private void SetProgress(float progress)
    {
        if (progressFill == null)
            return;

        Vector3 scale = progressFill.localScale;

        scale.x = progress;

        progressFill.localScale = scale;
    }
}
