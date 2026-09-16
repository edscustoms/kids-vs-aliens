using System;
using TMPro;
using UnityEngine;

/// <summary>Owns one manual suspension lease across both in-game screens.</summary>
[DisallowMultipleComponent]
public sealed class InGameMenuController : MonoBehaviour
{
    [SerializeField]
    private GameplaySuspensionController suspension;

    [SerializeField]
    private GameObject menuScreen;

    [SerializeField]
    private GameObject optionsScreen;

    [SerializeField]
    private TMP_Text currentCameraMode;
    private GameplaySuspensionController.Lease lease;
    private static readonly GameplayCameraMode[] Modes = (GameplayCameraMode[])
        Enum.GetValues(typeof(GameplayCameraMode));

    public bool IsOpen => lease != null && lease.IsActive;

    public void Configure(
        GameplaySuspensionController owner,
        GameObject menu,
        GameObject options,
        TMP_Text label
    )
    {
        suspension = owner;
        menuScreen = menu;
        optionsScreen = options;
        currentCameraMode = label;
    }

    private void OnEnable()
    {
        GameplayCameraSettings.Changed += RefreshCameraMode;
        if (suspension != null)
            suspension.SuspensionChanged += OnSuspensionChanged;
        SetScreen(null);
        RefreshCameraMode(GameplayCameraSettings.Mode);
    }

    private void OnDisable()
    {
        GameplayCameraSettings.Changed -= RefreshCameraMode;
        if (suspension != null)
            suspension.SuspensionChanged -= OnSuspensionChanged;
        SetScreen(null);
        lease?.Dispose();
        lease = null;
    }

    public void OpenMenu()
    {
        if (
            IsOpen
            || suspension == null
            || !suspension.isActiveAndEnabled
            || suspension.HasBlockingModal
        )
            return;
        lease = suspension.Acquire(SuspensionReason.ManualPause);
        SetScreen(menuScreen);
    }

    public void ShowOptions()
    {
        if (!IsOpen)
            return;
        RefreshCameraMode(GameplayCameraSettings.Mode);
        SetScreen(optionsScreen);
    }

    public void ShowMenu()
    {
        if (IsOpen)
            SetScreen(menuScreen);
    }

    public void ResumeGame()
    {
        SetScreen(null);
        // Releasing our lease never releases a Knowledge/modal owner.
        lease?.Dispose();
        lease = null;
    }

    public void PreviousCamera() => CycleCamera(-1);

    public void NextCamera() => CycleCamera(1);

    private void CycleCamera(int direction)
    {
        if (
            !IsOpen
            || optionsScreen == null
            || !optionsScreen.activeSelf
            || suspension.HasBlockingModal
        )
            return;
        int index = Array.IndexOf(Modes, GameplayCameraSettings.Mode);
        GameplayCameraSettings.Mode = Modes[(index + direction + Modes.Length) % Modes.Length];
    }

    private void RefreshCameraMode(GameplayCameraMode mode)
    {
        if (currentCameraMode != null)
            currentCameraMode.text = mode.ToString().ToUpperInvariant();
    }

    private void OnSuspensionChanged(bool paused)
    {
        if (!paused)
        {
            lease = null;
            SetScreen(null);
        }
    }

    private void SetScreen(GameObject screen)
    {
        if (menuScreen != null)
            menuScreen.SetActive(screen == menuScreen);
        if (optionsScreen != null)
            optionsScreen.SetActive(screen == optionsScreen);
    }
}
