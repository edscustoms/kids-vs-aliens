using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ElectricGrenadeEffect :
    GrenadeEffect
{
    private const int OverlapCapacity = 32;
    private const int CoverHitCapacity = 32;
    private const int VisualTargetCapacity = 8;

    private readonly Collider[] overlapHits =
        new Collider[OverlapCapacity];

    private readonly RaycastHit[] coverHits =
        new RaycastHit[CoverHitCapacity];

    private readonly HashSet<IDamageable> resolvedTargets =
        new HashSet<IDamageable>(
            OverlapCapacity);

    private readonly Vector3[] visualTargetPositions =
        new Vector3[VisualTargetCapacity];

    public override bool Supports(
        GrenadeEffectData data)
    {
        return data is ElectricGrenadeEffectData;
    }

    public override void Activate(
        GrenadeEffectData data,
        GrenadeEffectContext context)
    {
        if (!(data is ElectricGrenadeEffectData electricData))
        {
            Debug.LogError(
                $"{name}: ElectricGrenadeEffect requires ElectricGrenadeEffectData.",
                this);

            return;
        }

        resolvedTargets.Clear();

        int visualTargetCount = 0;

        int hitCount =
            Physics.OverlapSphereNonAlloc(
                context.Origin,
                electricData.radius,
                overlapHits,
                electricData.targetMask,
                QueryTriggerInteraction.Ignore);

        for (int i = 0;
             i < hitCount;
             i++)
        {
            Collider candidate =
                overlapHits[i];

            if (candidate == null)
                continue;

            IDamageable damageable =
                candidate.GetComponentInParent<IDamageable>();

            if (damageable == null ||
                resolvedTargets.Contains(damageable))
            {
                continue;
            }

            Vector3 targetPoint =
                candidate.bounds.center;

            if (!TryGetVisibleHit(
                    context,
                    electricData,
                    damageable,
                    targetPoint,
                    out Vector3 hitPoint,
                    out Vector3 hitNormal))
            {
                continue;
            }

            resolvedTargets.Add(
                damageable);

            Vector3 direction =
                hitPoint -
                context.Origin;

            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
            }
            else
            {
                direction =
                    Vector3.up;
            }

            HitInfo hit =
                new HitInfo(
                    electricData.damage,
                    hitPoint,
                    hitNormal,
                    direction,
                    context.Owner);

            IHitReaction reaction =
                CombatHitResolver.Resolve(
                    candidate,
                    hit);

            reaction?.ReceiveHit(
                hit);

            IStunnable stunnable =
                candidate.GetComponentInParent<IStunnable>();

            stunnable?.ApplyStun(
                electricData.stunDuration,
                context.Owner);

            if (visualTargetCount <
                visualTargetPositions.Length)
            {
                visualTargetPositions[visualTargetCount] =
                    hitPoint;

                visualTargetCount++;
            }
        }

        SpawnBurst(
            electricData,
            context.Origin,
            visualTargetCount);
    }

    private bool TryGetVisibleHit(
        GrenadeEffectContext context,
        ElectricGrenadeEffectData data,
        IDamageable target,
        Vector3 targetPoint,
        out Vector3 hitPoint,
        out Vector3 hitNormal)
    {
        hitPoint =
            targetPoint;

        Vector3 direction =
            targetPoint -
            context.Origin;

        float distance =
            direction.magnitude;

        if (distance <= 0.001f)
        {
            hitNormal = Vector3.up;
            return true;
        }

        direction /= distance;

        int hitCount =
            Physics.RaycastNonAlloc(
                context.Origin,
                direction,
                coverHits,
                distance + 0.05f,
                data.obstructionMask,
                QueryTriggerInteraction.Ignore);

        bool foundBlocker = false;
        RaycastHit closestHit = default;
        float closestDistance =
            Mathf.Infinity;

        for (int i = 0;
             i < hitCount;
             i++)
        {
            RaycastHit hit =
                coverHits[i];

            if (hit.collider == null ||
                IsOwnedBy(
                    hit.collider,
                    context.Source) ||
                IsOwnedBy(
                    hit.collider,
                    context.Owner) ||
                hit.distance >= closestDistance)
            {
                continue;
            }

            closestHit = hit;
            closestDistance =
                hit.distance;

            foundBlocker = true;
        }

        if (!foundBlocker)
        {
            hitNormal =
                -direction;

            return true;
        }

        IDamageable hitDamageable =
            closestHit.collider.GetComponentInParent<IDamageable>();

        if (!ReferenceEquals(
                hitDamageable,
                target))
        {
            hitNormal = default;
            return false;
        }

        hitPoint =
            closestHit.point;

        hitNormal =
            closestHit.normal;

        return true;
    }

    private static bool IsOwnedBy(
        Collider collider,
        GameObject owner)
    {
        if (collider == null ||
            owner == null)
        {
            return false;
        }

        Transform colliderTransform =
            collider.transform;

        Transform ownerTransform =
            owner.transform;

        return colliderTransform == ownerTransform ||
               colliderTransform.IsChildOf(ownerTransform);
    }

    private void SpawnBurst(
        ElectricGrenadeEffectData data,
        Vector3 origin,
        int visualTargetCount)
    {
        if (data.burstPrefab == null)
            return;

        ElectricGrenadeBurstVFX burst =
            VfxPool.Spawn(
                data.burstPrefab,
                origin,
                Quaternion.identity);

        if (burst == null)
            return;

        burst.Play(
            visualTargetPositions,
            visualTargetCount,
            data.radius,
            data.effectColor);
    }
}
