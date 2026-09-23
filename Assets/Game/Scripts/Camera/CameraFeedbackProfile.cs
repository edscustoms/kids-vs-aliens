using UnityEngine;

[CreateAssetMenu(menuName = "Game/Camera/Feedback Profile")]
public sealed class CameraFeedbackProfile : ScriptableObject
{
    [Min(.01f)] public float duration = .15f;
    [Tooltip("Camera-local degrees. Negative X kicks the view upward.")]
    public Vector3 rotation;
    [Tooltip("Camera-local metres. Negative Y produces a landing dip.")]
    public Vector3 position;
    [Tooltip("Optional yaw/roll influence in degrees from an existing incoming hit direction.")]
    [Min(0)] public float directionalInfluence;
}
