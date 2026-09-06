using UnityEngine;

/// <summary>
/// Marker for solid world geometry that should NOT block weapon/projectile rays.
///
/// This does not change normal physics collision or NavMesh behavior.
/// A fence can therefore still block player/enemy movement while allowing
/// plasma shots to pass through it.
///
/// Keep this separate from VisionTransparentObstacle so future surfaces such
/// as glass may be visible-through while still blocking projectiles.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProjectilePassThroughObstacle : MonoBehaviour
{
}
