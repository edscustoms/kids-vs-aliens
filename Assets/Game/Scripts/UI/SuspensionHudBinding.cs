using UnityEngine;

// View binding only. The suspension owner has no knowledge of HUD composition.
public sealed class SuspensionHudBinding : MonoBehaviour
{
    [SerializeField]
    private GameplaySuspensionController suspension;

    [SerializeField]
    private GameObject inputBlocker;

    [SerializeField]
    private GameplayFeedbackPresenter feedback;

    private void OnEnable()
    {
        if (suspension != null)
            suspension.ControlBlockChanged += Apply;
        Apply(suspension != null && suspension.BlocksControls);
    }

    private void OnDisable()
    {
        if (suspension != null)
            suspension.ControlBlockChanged -= Apply;
    }

    private void Apply(bool paused)
    {
        if (inputBlocker != null)
            inputBlocker.SetActive(paused);
        if (feedback != null)
            feedback.SetSuppressed(paused);
    }
}
