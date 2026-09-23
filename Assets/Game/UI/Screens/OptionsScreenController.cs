using UnityEngine;
using TMPro;

public sealed class OptionsScreenController : MonoBehaviour
{
    [SerializeField]
    private UISegmentedControl gameplayCamera;
    [SerializeField] private GameObject resetConfirmation;
    [SerializeField] private TMP_Text resetMessage;

    public void Configure(UISegmentedControl control) => gameplayCamera = control;
    public void ConfigureReset(GameObject confirmation, TMP_Text message)
    {
        resetConfirmation = confirmation;
        resetMessage = message;
    }
    private void Start()
    {
        HapticsOptionView.Ensure(transform, false);
        if (resetConfirmation != null) resetConfirmation.SetActive(false);
        else ProgressResetView.Build(transform,new(.34f,.24f),new(.66f,.31f));
    }
    public void ShowResetConfirmation() => resetConfirmation.SetActive(true);
    public void CancelReset() => resetConfirmation.SetActive(false);
    public void ConfirmReset() => ProgressResetView.ConfirmReset(resetMessage);

    private void OnEnable()
    {
        if (gameplayCamera == null)
            return;
        gameplayCamera.Select((int)GameplayCameraSettings.Mode, false);
        gameplayCamera.SelectionChanged.AddListener(SelectCamera);
        GameplayCameraSettings.Changed += Refresh;
    }

    private void OnDisable()
    {
        if (gameplayCamera != null)
            gameplayCamera.SelectionChanged.RemoveListener(SelectCamera);
        GameplayCameraSettings.Changed -= Refresh;
    }

    private void SelectCamera(int index) => GameplayCameraSettings.Mode = (GameplayCameraMode)index;

    private void Refresh(GameplayCameraMode mode) => gameplayCamera.Select((int)mode, false);
}
