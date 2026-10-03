using UnityEngine;

/// <summary>Nearest real blocker, excluding the shooter and explicit projectile-pass-through geometry.</summary>
public static class WeaponShotQuery
{
    public static bool Cast(Transform owner, Vector3 origin, Vector3 direction, float distance, RaycastHit[] buffer, out RaycastHit closest)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, buffer, distance, ~0, QueryTriggerInteraction.Ignore);
        // A full non-alloc buffer is ambiguous; a rare allocating fallback must not shoot through omitted cover.
        var hits = count == buffer.Length ? Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore) : buffer;
        if (hits != buffer) count = hits.Length;
        closest = default; float best = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider == null || hit.transform == owner || hit.transform.IsChildOf(owner)
                || hit.collider.GetComponentInParent<ProjectilePassThroughObstacle>() != null || hit.distance >= best) continue;
            best = hit.distance; closest = hit;
        }
        return closest.collider != null;
    }
    public static bool Clear(Transform owner, Transform target, Vector3 start, Vector3 end, RaycastHit[] buffer)
    {
        Vector3 delta = end - start;
        return !Cast(owner, start, delta.normalized, delta.magnitude, buffer, out var hit)
            || hit.transform == target || hit.transform.IsChildOf(target)
            || PlayerBikeRider.IsOccupiedTargetCollider(target, hit.collider);
    }
}
