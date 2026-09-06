using UnityEngine;
using UnityEngine.UI;

public sealed class ManualPauseButton : MonoBehaviour
{
    [SerializeField]
    private GameplaySuspensionController suspension;

    [SerializeField]
    private StarterAssets.StarterAssetsInputs input;

    [SerializeField]
    private Button button;

    [SerializeField]
    private GameObject pauseIcon;

    [SerializeField]
    private GameObject playIcon;
    private GameplaySuspensionController.Lease lease;

    public bool OwnsManualPause => lease != null && lease.IsActive;

    private void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(Toggle);
        if (input != null)
            input.PauseRequested += Toggle;
        if (suspension != null)
            suspension.SuspensionChanged += HandleSuspensionChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(Toggle);
        if (input != null)
            input.PauseRequested -= Toggle;
        if (suspension != null)
            suspension.SuspensionChanged -= HandleSuspensionChanged;
        lease?.Dispose();
        lease = null;
    }

    public void Toggle()
    {
        if (suspension == null || !suspension.isActiveAndEnabled || suspension.HasBlockingModal)
            return;
        if (!OwnsManualPause)
            lease = suspension.Acquire(SuspensionReason.ManualPause);
        else
        {
            lease.Dispose();
            lease = null;
        }
        Refresh();
    }

    private void Refresh()
    {
        if (pauseIcon != null)
            pauseIcon.SetActive(!OwnsManualPause);
        if (playIcon != null)
            playIcon.SetActive(OwnsManualPause);
    }

    private void HandleSuspensionChanged(bool suspended) => Refresh();
}
