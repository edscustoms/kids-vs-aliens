using UnityEngine;

public static class CombatHitResolver
{
    /// <summary>
    /// Applies gameplay damage immediately and returns an optional reaction
    /// that can be played later when the visual projectile reaches the hit.
    /// </summary>
    public static IHitReaction Resolve(
        Collider collider,
        HitInfo hit
    )
    {
        if (collider == null)
            return null;

        IDamageable damageable =
            collider.GetComponentInParent<IDamageable>();

        damageable?.ReceiveDamage(hit);

        return collider.GetComponentInParent<IHitReaction>();
    }
}
