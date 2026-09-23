using UnityEngine;
using UnityEngine.UI;

/// <summary>Observes an accepted uGUI click/submit without changing its action.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class UIAudioButton : MonoBehaviour
{
    private Button button;
    public static void Ensure(Button value)
    {
        if (AllowsClick(value) && value.GetComponent<UIAudioButton>() == null) value.gameObject.AddComponent<UIAudioButton>();
    }
    // Classify by input role, not names or HUD ancestry: navigation and quick slots still click.
    // Include inactive parents because authored screens and self-hiding buttons are supported.
    private static bool AllowsClick(Button value) => value != null
        && value.GetComponentInParent<UIVirtualButton>(true) == null
        && value.GetComponentInParent<UIVirtualJoystick>(true) == null
        && value.GetComponentInParent<UIVirtualTouchZone>(true) == null
        && value.GetComponentInParent<BeamHoistButton>(true) == null;

    private void Awake() { button = GetComponent<Button>(); button.onClick.AddListener(Clicked); }
    // Keep the subscription when the action hides its own screen during onClick.
    private void Clicked()
    {
        // Also cover existing hooks and controls whose input component is added after registration.
        if (AllowsClick(button)) UIAudioFeedback.Click();
    }
    private void OnDestroy() { if (button != null) button.onClick.RemoveListener(Clicked); }
}
