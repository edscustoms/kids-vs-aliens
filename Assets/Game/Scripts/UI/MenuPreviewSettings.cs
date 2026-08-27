using UnityEngine;

/// <summary>
/// Optional per-prefab presentation overrides for the generic menu preview stage.
/// If this component is absent, the preview system auto-frames the object from its Renderer bounds.
/// </summary>
public class MenuPreviewSettings : MonoBehaviour
{
    [Header("Model")]
    public Vector3 localOffset = Vector3.zero;
    public Vector3 localEulerAngles = Vector3.zero;
    [Min(0.01f)] public float scaleMultiplier = 1f;

    [Header("Camera")]
    public Vector3 cameraTargetOffset = Vector3.zero;
    [Min(0.1f)] public float cameraDistanceMultiplier = 1f;

    [Header("Interaction")]
    [Min(0.01f)] public float rotationSensitivity = 0.25f;
}
