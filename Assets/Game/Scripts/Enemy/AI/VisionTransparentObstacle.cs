using UnityEngine;

/// <summary>
/// Marker for solid world geometry that should NOT block enemy vision.
///
/// Examples:
/// - chain-link fences
/// - grates
/// - transparent force fields
///
/// This changes enemy perception only.
/// Physics collision, bullets and NavMesh baking remain separate concerns.
/// </summary>
[DisallowMultipleComponent]
public sealed class VisionTransparentObstacle : MonoBehaviour
{
}
