using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gives each enemy a stable approach slot around the same target.
/// This prevents a group from all requesting Amy's exact position.
/// NavMeshAgent local avoidance still handles the final local movement.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyApproachPlanner : MonoBehaviour
{
    private static readonly Dictionary<
        Transform,
        HashSet<int>>
        ClaimedSlots = new();

    private const float GoldenAngleDegrees =
        137.507764f;

    [Header("Approach ring")]
    [Tooltip(
        "Approach slots are only used near the target. " +
        "At longer range the enemy follows the target's actual NavMesh position."
    )]
    [SerializeField, Min(0.5f)]
    private float slotActivationDistance = 2.4f;

    [SerializeField, Min(1)]
    private int slotsPerRing = 12;

    [SerializeField, Min(0.1f)]
    private float baseRadius = 1.05f;

    [SerializeField, Min(0f)]
    private float extraRingSpacing = 0.65f;

    [Header("Natural variation")]
    [SerializeField, Range(0f, 30f)]
    private float angleJitterDegrees = 10f;

    [SerializeField, Min(0f)]
    private float radiusJitter = 0.10f;

    [Header("NavMesh")]
    [SerializeField, Min(0.05f)]
    private float sampleRadius = 0.8f;

    private Transform currentTarget;
    private int slotIndex = -1;

    private float angleJitter;
    private float localRadiusJitter;

    private void Awake()
    {
        angleJitter =
            Random.Range(
                -angleJitterDegrees,
                angleJitterDegrees);

        localRadiusJitter =
            Random.Range(
                -radiusJitter,
                radiusJitter);
    }

    private void OnDisable()
    {
        ReleaseSlot();
    }

    public void SetTarget(
        Transform target)
    {
        if (target == currentTarget &&
            slotIndex >= 0)
        {
            return;
        }

        ReleaseSlot();

        if (target == null)
            return;

        currentTarget = target;
        slotIndex =
            ClaimSlot(target);
    }

    public void ClearTarget()
    {
        ReleaseSlot();
    }

    public bool TryGetChasePosition(
        Transform target,
        out Vector3 position)
    {
        position =
            target != null
                ? target.position
                : transform.position;

        if (target == null)
            return false;

        Vector3 delta =
            target.position -
            transform.position;

        delta.y = 0f;

        // Normal level navigation:
        // follow Amy's actual reachable NavMesh position.
        if (delta.magnitude >
            slotActivationDistance)
        {
            if (NavMesh.SamplePosition(
                    target.position,
                    out NavMeshHit directHit,
                    sampleRadius,
                    NavMesh.AllAreas))
            {
                position =
                    directHit.position;

                return true;
            }

            position =
                target.position;

            return true;
        }

        // Final approach:
        // spread enemies around Amy instead of stacking.
        return TryGetApproachPosition(
            target,
            out position);
    }

    public bool TryGetApproachPosition(
        Transform target,
        out Vector3 position)
    {
        position =
            target != null
                ? target.position
                : transform.position;

        if (target == null)
            return false;

        SetTarget(target);

        if (slotIndex < 0)
            return false;

        int ring =
            slotIndex /
            Mathf.Max(
                1,
                slotsPerRing);

        float angle =
            slotIndex *
            GoldenAngleDegrees +
            ring * 17f +
            angleJitter;

        float radius =
            Mathf.Max(
                0.15f,
                baseRadius +
                ring *
                extraRingSpacing +
                localRadiusJitter);

        Quaternion rotation =
            Quaternion.Euler(
                0f,
                angle,
                0f);

        Vector3 desired =
            target.position +
            rotation *
            Vector3.forward *
            radius;

        if (NavMesh.SamplePosition(
                desired,
                out NavMeshHit hit,
                sampleRadius,
                NavMesh.AllAreas))
        {
            position = hit.position;
            return true;
        }

        position = desired;
        return true;
    }

    private static int ClaimSlot(
        Transform target)
    {
        if (!ClaimedSlots.TryGetValue(
                target,
                out HashSet<int> slots))
        {
            slots =
                new HashSet<int>();

            ClaimedSlots.Add(
                target,
                slots);
        }

        int slot = 0;

        while (slots.Contains(slot))
            slot++;

        slots.Add(slot);

        return slot;
    }

    private void ReleaseSlot()
    {
        Transform oldTarget =
            currentTarget;

        int oldSlot =
            slotIndex;

        currentTarget = null;
        slotIndex = -1;

        if (ReferenceEquals(
                oldTarget,
                null) ||
            oldSlot < 0)
        {
            return;
        }

        if (!ClaimedSlots.TryGetValue(
                oldTarget,
                out HashSet<int> slots))
        {
            return;
        }

        slots.Remove(oldSlot);

        if (slots.Count == 0)
            ClaimedSlots.Remove(oldTarget);
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ClaimedSlots.Clear();
    }
}
