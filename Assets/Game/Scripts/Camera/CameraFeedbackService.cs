using System;
using UnityEngine;

/// <summary>Local-player presentation requests only; owns no camera pose or saved state.</summary>
public static class CameraFeedbackService
{
    public static event Action<CameraFeedbackProfile, float, Vector3> Requested;
    private static CameraFeedbackConfig config;
    public static CameraFeedbackConfig Config
    {
        get
        {
            if (config == null) config = Resources.Load<CameraFeedbackConfig>("CameraFeedback/Config");
            return config;
        }
    }
    public static void Play(CameraFeedbackProfile profile, float strength = 1f, Vector3 direction = default)
    {
        if (profile == null || !CameraFeedbackSettings.Enabled || !(strength > 0f)) return;
        Requested?.Invoke(profile, Mathf.Clamp01(strength), direction);
    }
    public static void PlayerDamaged(Vector3 direction = default)
    {
        if (CameraFeedbackSettings.Enabled && Config != null) Play(Config.playerDamage, 1f, direction);
    }
    public static void Landed(float downwardSpeed)
    {
        if (CameraFeedbackSettings.Enabled && Config != null)
            Play(Config.hardLanding, Config.LandingStrength(downwardSpeed));
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Requested = null; config = null; }
}
