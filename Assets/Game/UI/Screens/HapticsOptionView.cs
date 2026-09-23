using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared ON/OFF setting; changing it never plays a haptic.</summary>
[DisallowMultipleComponent]
public sealed class HapticsOptionView : MonoBehaviour
{
    private Button button;
    private TMP_Text label;

    public static void Ensure(Transform options, bool inGame)
    {
        var existing = options.Find("HapticsEnabled");
        if (existing != null) return;
        // Keep existing authored controls intact. Menu's reset row has room to its right.
        var control = InterfaceFactory.Button(options, "HapticsEnabled", "HAPTICS: ON",
            inGame ? new Vector2(.34f, .46f) : new Vector2(.70f, .24f),
            inGame ? new Vector2(.66f, .53f) : new Vector2(.95f, .32f), null);
        control.gameObject.AddComponent<HapticsOptionView>();
        // Confirmation overlays must continue to intercept all controls.
        var confirmation = options.Find("ResetProgressConfirmation");
        if (confirmation != null) confirmation.SetAsLastSibling();
    }

    private void OnEnable()
    {
        button = GetComponent<Button>();
        label = GetComponentInChildren<TMP_Text>(true);
        button.onClick.AddListener(Toggle);
        HapticSettings.Changed += Refresh;
        Refresh(HapticSettings.Enabled);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(Toggle);
        HapticSettings.Changed -= Refresh;
    }

    private void Toggle() => HapticSettings.Enabled = !HapticSettings.Enabled;
    private void Refresh(bool enabled) { if (label != null) label.text = enabled ? "HAPTICS: ON" : "HAPTICS: OFF"; }
}
