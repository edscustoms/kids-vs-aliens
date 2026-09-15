using UnityEngine;

public sealed class OptionsScreenController : MonoBehaviour
{
    [SerializeField]
    private UISegmentedControl gameplayCamera;

    public void Configure(UISegmentedControl control) => gameplayCamera = control;

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
