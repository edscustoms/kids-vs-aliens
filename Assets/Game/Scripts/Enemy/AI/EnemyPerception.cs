using UnityEngine;

/// <summary>
/// Periodic enemy perception.
///
/// Initial acquisition:
/// - normal range + FOV + LOS
/// - close-awareness range ignores FOV but NEVER ignores walls
///
/// Existing target:
/// - LOS + lose-sight range
///
/// LOS:
/// - samples several parts of Amy instead of one center point
/// - ignores this enemy and other enemies as vision blockers
/// - solid world geometry still blocks vision
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyPerception : MonoBehaviour
{
    [Header("Target")]
    [SerializeField]
    private string targetTag = "Player";

    [Header("Initial detection")]
    [SerializeField, Min(0.1f)]
    private float detectionRange = 8f;

    [SerializeField, Range(1f, 360f)]
    private float fieldOfViewDegrees = 160f;

    [Tooltip(
        "Very close targets may be noticed regardless of facing direction. " +
        "Walls still block awareness."
    )]
    [SerializeField, Min(0f)]
    private float closeAwarenessRange = 1.8f;

    [Header("Existing target")]
    [SerializeField, Min(0.1f)]
    private float loseSightRange = 13f;

    [Header("Line of sight")]
    [SerializeField, Min(0f)]
    private float eyeHeight = 1.35f;

    [SerializeField]
    private LayerMask lineOfSightMask = ~0;

    [Header("Ticking")]
    [SerializeField]
    private Vector2 senseIntervalRange =
        new Vector2(0.12f, 0.25f);

    [SerializeField, Min(0.1f)]
    private float missingTargetSearchInterval = 1f;

    private readonly RaycastHit[] lineOfSightHits =
        new RaycastHit[24];

    private readonly Vector3[] targetSamples =
        new Vector3[5];

    private Transform candidate;
    private Transform target;

    private CharacterController targetController;

    private bool canSeeTarget;
    private Vector3 lastKnownPosition;
    private float lastSeenTime =
        float.NegativeInfinity;

    private float nextSenseTime;
    private float nextCandidateSearchTime;

    public Transform Target => target;
    public bool HasTarget => target != null;
    public bool CanSeeTarget => canSeeTarget;
    public Vector3 LastKnownPosition => lastKnownPosition;

    public float SecondsSinceLastSeen =>
        float.IsNegativeInfinity(lastSeenTime)
            ? float.PositiveInfinity
            : Time.time - lastSeenTime;

    private void Start()
    {
        ScheduleNextSense();
        TryFindCandidate();
    }

    private void Update()
    {
        if (Time.time < nextSenseTime)
            return;

        Sense();
        ScheduleNextSense();
    }

    public void ForgetTarget()
    {
        target = null;
        targetController = null;
        canSeeTarget = false;
    }

    private void Sense()
    {
        if (target == null)
        {
            if (candidate == null)
            {
                if (Time.time >=
                    nextCandidateSearchTime)
                {
                    TryFindCandidate();
                }

                if (candidate == null)
                    return;
            }

            if (!CanInitiallyDetect(candidate))
                return;

            AcquireTarget(candidate);
            return;
        }

        canSeeTarget =
            CanSeeExistingTarget(target);

        if (!canSeeTarget)
            return;

        lastKnownPosition =
            target.position;

        lastSeenTime =
            Time.time;
    }

    private void AcquireTarget(
        Transform newTarget)
    {
        target = newTarget;

        targetController =
            target.GetComponent<CharacterController>();

        canSeeTarget = true;

        lastKnownPosition =
            target.position;

        lastSeenTime =
            Time.time;
    }

    private bool CanInitiallyDetect(
        Transform candidateTarget)
    {
        Vector3 toTarget =
            candidateTarget.position -
            transform.position;

        toTarget.y = 0f;

        float distance =
            toTarget.magnitude;

        if (distance > detectionRange)
            return false;

        bool isVeryClose =
            closeAwarenessRange > 0f &&
            distance <= closeAwarenessRange;

        if (!isVeryClose &&
            distance > 0.001f)
        {
            float angle =
                Vector3.Angle(
                    transform.forward,
                    toTarget / distance);

            if (angle >
                fieldOfViewDegrees * 0.5f)
            {
                return false;
            }
        }

        return HasAnyLineOfSight(
            candidateTarget);
    }

    private bool CanSeeExistingTarget(
        Transform existingTarget)
    {
        Vector3 delta =
            existingTarget.position -
            transform.position;

        delta.y = 0f;

        if (delta.magnitude >
            loseSightRange)
        {
            return false;
        }

        return HasAnyLineOfSight(
            existingTarget);
    }

    private bool HasAnyLineOfSight(
        Transform targetTransform)
    {
        BuildTargetSamples(
            targetTransform);

        Vector3 origin =
            transform.position +
            Vector3.up *
            eyeHeight;

        for (int i = 0;
             i < targetSamples.Length;
             i++)
        {
            if (HasLineOfSightToPoint(
                    targetTransform,
                    origin,
                    targetSamples[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void BuildTargetSamples(
        Transform targetTransform)
    {
        CharacterController controller =
            target == targetTransform
                ? targetController
                : targetTransform
                    .GetComponent<CharacterController>();

        if (controller != null)
        {
            Bounds bounds =
                controller.bounds;

            Vector3 center =
                bounds.center;

            float height =
                bounds.extents.y;

            float width =
                Mathf.Max(
                    bounds.extents.x,
                    bounds.extents.z);

            Vector3 right =
                targetTransform.right;

            targetSamples[0] =
                center;

            targetSamples[1] =
                center +
                Vector3.up *
                (height * 0.65f);

            targetSamples[2] =
                center +
                Vector3.up *
                (height * 0.25f) +
                right *
                (width * 0.65f);

            targetSamples[3] =
                center +
                Vector3.up *
                (height * 0.25f) -
                right *
                (width * 0.65f);

            targetSamples[4] =
                center -
                Vector3.up *
                (height * 0.45f);

            return;
        }

        Vector3 fallbackCenter =
            targetTransform.position +
            Vector3.up * 0.9f;

        targetSamples[0] =
            fallbackCenter;

        targetSamples[1] =
            fallbackCenter +
            Vector3.up * 0.55f;

        targetSamples[2] =
            fallbackCenter +
            targetTransform.right * 0.25f;

        targetSamples[3] =
            fallbackCenter -
            targetTransform.right * 0.25f;

        targetSamples[4] =
            fallbackCenter -
            Vector3.up * 0.45f;
    }

    private bool HasLineOfSightToPoint(
        Transform targetTransform,
        Vector3 origin,
        Vector3 samplePoint)
    {
        Vector3 direction =
            samplePoint -
            origin;

        float distance =
            direction.magnitude;

        if (distance <= 0.001f)
            return true;

        direction /= distance;

        int hitCount =
            Physics.RaycastNonAlloc(
                origin,
                direction,
                lineOfSightHits,
                distance + 0.15f,
                lineOfSightMask,
                QueryTriggerInteraction.Ignore);

        if (hitCount == 0)
            return true;

        float closestBlockingDistance =
            float.PositiveInfinity;

        bool targetHitBeforeBlocker =
            false;

        for (int i = 0;
             i < hitCount;
             i++)
        {
            RaycastHit hit =
                lineOfSightHits[i];

            Collider collider =
                hit.collider;

            if (collider == null)
                continue;

            if (IsOwnCollider(collider))
                continue;

            if (IsOtherEnemyCollider(
                    collider))
            {
                continue;
            }

            if (IsVisionTransparent(
                    collider))
            {
                continue;
            }

            if (BelongsToTarget(
                    collider,
                    targetTransform))
            {
                if (hit.distance <
                    closestBlockingDistance)
                {
                    targetHitBeforeBlocker =
                        true;
                }

                continue;
            }

            if (hit.distance <
                closestBlockingDistance)
            {
                closestBlockingDistance =
                    hit.distance;

                targetHitBeforeBlocker =
                    false;
            }
        }

        // If the ray reached the intended sample without a real blocker,
        // this is also valid even if the player's collider did not occupy
        // that exact sample point.
        if (float.IsPositiveInfinity(
                closestBlockingDistance))
        {
            return true;
        }

        return targetHitBeforeBlocker;
    }

    private bool IsOwnCollider(
        Collider collider)
    {
        Transform hitTransform =
            collider.transform;

        return
            hitTransform == transform ||
            hitTransform.IsChildOf(transform);
    }

    private static bool IsOtherEnemyCollider(
        Collider collider)
    {
        return
            collider.GetComponentInParent<
                EnemyActor>() != null;
    }

    private static bool IsVisionTransparent(
        Collider collider)
    {
        return
            collider.GetComponentInParent<
                VisionTransparentObstacle>() != null;
    }

    private static bool BelongsToTarget(
        Collider collider,
        Transform targetTransform)
    {
        Transform hitTransform =
            collider.transform;

        return
            hitTransform == targetTransform ||
            hitTransform.IsChildOf(
                targetTransform);
    }

    private void TryFindCandidate()
    {
        nextCandidateSearchTime =
            Time.time +
            missingTargetSearchInterval;

        GameObject targetObject =
            GameObject.FindGameObjectWithTag(
                targetTag);

        candidate =
            targetObject != null
                ? targetObject.transform
                : null;
    }

    private void ScheduleNextSense()
    {
        float min =
            Mathf.Max(
                0.02f,
                Mathf.Min(
                    senseIntervalRange.x,
                    senseIntervalRange.y));

        float max =
            Mathf.Max(
                min,
                Mathf.Max(
                    senseIntervalRange.x,
                    senseIntervalRange.y));

        nextSenseTime =
            Time.time +
            Random.Range(
                min,
                max);
    }

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        Gizmos.color =
            new Color(
                1f,
                0.85f,
                0.1f,
                0.8f);

        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange);

        Gizmos.color =
            new Color(
                0.2f,
                1f,
                0.35f,
                0.75f);

        Gizmos.DrawWireSphere(
            transform.position,
            closeAwarenessRange);

        Gizmos.color =
            new Color(
                1f,
                0.2f,
                0.2f,
                0.5f);

        Gizmos.DrawWireSphere(
            transform.position,
            loseSightRange);
    }

#endif
}
