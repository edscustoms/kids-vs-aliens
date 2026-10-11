using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GraphicsOptionsView : MonoBehaviour
{
    private Button qualityButton, shadowsButton;
    private TMP_Text qualityLabel, shadowsLabel;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private Button terrainButton;
    private TMP_Text terrainLabel;
#endif
    public static void Ensure(Transform options, bool inGame)
    {
        if (options.Find("GraphicsOptions") != null) return;
        if (inGame)
        {
            // Make room between the existing camera selector and accessibility row.
            MoveRow(options, "CameraLabel", .80f);
            MoveRow(options, "CurrentCameraModeLabel", .68f);
            MoveRow(options, "PreviousCameraButton", .68f);
            MoveRow(options, "NextCameraButton", .68f);
        }
        var root = InterfaceFactory.Rect(options, "GraphicsOptions", Vector2.zero, Vector2.one);
        var view = root.gameObject.AddComponent<GraphicsOptionsView>();
        view.qualityButton = InterfaceFactory.Button(root, "GraphicsQuality", "GRAPHICS: HIGH",
            inGame ? new Vector2(.27f,.55f) : new Vector2(.05f,.24f),
            inGame ? new Vector2(.53f,.605f) : new Vector2(.30f,.32f), view.CycleQuality);
        view.shadowsButton = InterfaceFactory.Button(root, "Shadows", "SHADOWS: ON",
            inGame ? new Vector2(.55f,.55f) : new Vector2(.05f,.14f),
            inGame ? new Vector2(.73f,.605f) : new Vector2(.30f,.22f), view.ToggleShadows);
        view.qualityLabel = view.qualityButton.GetComponentInChildren<TMP_Text>();
        view.shadowsLabel = view.shadowsButton.GetComponentInChildren<TMP_Text>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (inGame)
        {
            MoveRow(options, "CameraLabel", .83f);
            MoveRow(options, "CurrentCameraModeLabel", .73f);
            MoveRow(options, "PreviousCameraButton", .73f);
            MoveRow(options, "NextCameraButton", .73f);
            foreach (var button in new[] { view.qualityButton, view.shadowsButton })
            {
                var rect = (RectTransform)button.transform;
                rect.anchorMin = new Vector2(rect.anchorMin.x, .61f);
                rect.anchorMax = new Vector2(rect.anchorMax.x, .665f);
            }
        }
        view.terrainButton = InterfaceFactory.Button(root, "TerrainRendering", "",
            inGame ? new Vector2(.27f, .54f) : new Vector2(.34f, .265f),
            inGame ? new Vector2(.73f, .595f) : new Vector2(.66f, .335f), view.ToggleTerrain);
        view.terrainLabel = view.terrainButton.GetComponentInChildren<TMP_Text>();
        view.terrainLabel.enableAutoSizing = true;
        view.terrainLabel.fontSizeMin = 16;
        view.terrainLabel.fontSizeMax = 27;
#endif
        view.Refresh();
        options.Find("ResetProgressConfirmation")?.SetAsLastSibling();
    }
    private static void MoveRow(Transform options, string name, float y)
    {
        if (options.Find(name) is not RectTransform rect) return;
        rect.anchorMin = new Vector2(rect.anchorMin.x, y);
        rect.anchorMax = new Vector2(rect.anchorMax.x, y);
    }
    private void OnEnable() { GraphicsPreferences.Changed += Refresh; Refresh(); }
    private void OnDisable() => GraphicsPreferences.Changed -= Refresh;
    private void CycleQuality() => GraphicsPreferences.Quality = (GraphicsQuality)(((int)GraphicsPreferences.Quality + 1) % 3);
    private void ToggleShadows() => GraphicsPreferences.Shadows = !GraphicsPreferences.Shadows;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void ToggleTerrain() => GraphicsPreferences.ExperimentalTerrain = !GraphicsPreferences.ExperimentalTerrain;
#endif
    private void Refresh()
    {
        if (qualityLabel != null) qualityLabel.text = "GRAPHICS: " + GraphicsPreferences.Quality.ToString().ToUpperInvariant();
        if (shadowsLabel != null) shadowsLabel.text = GraphicsPreferences.Shadows ? "SHADOWS: ON" : "SHADOWS: OFF";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (terrainLabel != null) terrainLabel.text = "TERRAIN RENDERING: " +
            (GraphicsPreferences.ExperimentalTerrain ? "EXPERIMENTAL DISTANCE V2" : "ORIGINAL MEDIUM");
        if (terrainButton != null) terrainButton.interactable = GraphicsPreferences.Quality == GraphicsQuality.Medium;
#endif
    }
}
