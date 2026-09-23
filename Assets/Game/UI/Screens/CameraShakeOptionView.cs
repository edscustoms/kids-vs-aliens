using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CameraShakeOptionView : MonoBehaviour
{
    private Button button;
    private TMP_Text label;
    public static void Ensure(Transform options, bool inGame)
    {
        if (options.Find("CameraShake") != null) return;
        // Split only the original generated default row; preserve hand-tuned controls.
        var haptics = options.Find("HapticsEnabled") as RectTransform;
        if (inGame && haptics != null && haptics.anchorMin == new Vector2(.34f, .46f)
            && haptics.anchorMax == new Vector2(.66f, .53f)
            && haptics.offsetMin == Vector2.zero && haptics.offsetMax == Vector2.zero)
        {
            haptics.anchorMin = new Vector2(.27f, .46f);
            haptics.anchorMax = new Vector2(.49f, .53f);
        }
        var control = InterfaceFactory.Button(options, "CameraShake", "CAMERA SHAKE: ON",
            inGame ? new Vector2(.51f, .46f) : new Vector2(.70f, .14f),
            inGame ? new Vector2(.73f, .53f) : new Vector2(.95f, .22f), null);
        control.gameObject.AddComponent<CameraShakeOptionView>();
        var confirmation = options.Find("ResetProgressConfirmation");
        if (confirmation != null) confirmation.SetAsLastSibling();
    }
    private void OnEnable()
    {
        button = GetComponent<Button>(); label = GetComponentInChildren<TMP_Text>(true);
        button.onClick.AddListener(Toggle);
        CameraFeedbackSettings.Changed += Refresh;
        Refresh(CameraFeedbackSettings.Enabled);
    }
    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(Toggle);
        CameraFeedbackSettings.Changed -= Refresh;
    }
    private void Toggle() => CameraFeedbackSettings.Enabled = !CameraFeedbackSettings.Enabled;
    private void Refresh(bool enabled) { if (label != null) label.text = enabled ? "CAMERA SHAKE: ON" : "CAMERA SHAKE: OFF"; }
}
