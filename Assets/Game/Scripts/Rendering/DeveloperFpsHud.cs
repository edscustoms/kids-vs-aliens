#if UNITY_EDITOR || DEVELOPMENT_BUILD
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Attached only to the persistent graphics owner; no input or raycast surface.</summary>
public sealed class DeveloperFpsHud : MonoBehaviour
{
    private TMP_Text label;
    private double start;
    private int frames;
    private void Start()
    {
        var canvas = new GameObject("Developer FPS", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder = 32000;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var safe = InterfaceFactory.Rect(canvas.transform, "Safe Area", Vector2.zero, Vector2.one);
        safe.gameObject.AddComponent<SafeAreaPanel>();
        label = InterfaceFactory.Text(safe, "FPS", "FPS", new Vector2(.44f, .95f), new Vector2(.56f, 1), 28, Color.green, TextAlignmentOptions.Center);
        start = Time.realtimeSinceStartupAsDouble;
    }
    public static Color ColorFor(float fps) => fps >= 60 ? Color.green : fps >= 30 ? Color.yellow : Color.red;
    private void Update()
    {
        if (label == null) return;
        frames++;
        double elapsed = Time.realtimeSinceStartupAsDouble - start;
        if (elapsed < .5) return;
        int fps = Mathf.FloorToInt((float)(frames / elapsed));
        label.SetText("{0:0} FPS", fps); label.color = ColorFor(fps);
        frames = 0; start = Time.realtimeSinceStartupAsDouble;
    }
    private void OnApplicationPause(bool paused) { frames = 0; start = Time.realtimeSinceStartupAsDouble; }
}
#endif
