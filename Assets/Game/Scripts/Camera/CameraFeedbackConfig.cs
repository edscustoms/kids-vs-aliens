using UnityEngine;

[CreateAssetMenu(menuName = "Game/Camera/Feedback Config")]
public sealed class CameraFeedbackConfig : ScriptableObject
{
    public CameraFeedbackProfile playerDamage;
    public CameraFeedbackProfile hardLanding;
    [Header("Landing speed (m/s, downward)")]
    [Min(0)] public float minimumLandingSpeed = 8f;
    [Min(0)] public float fullLandingSpeed = 16f;
    [Header("Combined feedback limits")]
    [Min(0)] public float maximumRotation = 1.5f;
    [Min(0)] public float maximumPosition = .06f;

    public float LandingStrength(float downwardSpeed) =>
        Mathf.InverseLerp(minimumLandingSpeed, Mathf.Max(minimumLandingSpeed + .01f, fullLandingSpeed), downwardSpeed);
}
