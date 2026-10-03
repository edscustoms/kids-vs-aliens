using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Thin NavMeshAgent wrapper.
/// Dynamic chase destinations are updated by EnemyBrain.
/// Idle/wander destinations are set once per wander decision.
/// External impacts temporarily own swept movement and retain a safe ground save pose.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class EnemyMotor : MonoBehaviour
{
    [Header("Per-enemy variation")]
    [SerializeField]
    private Vector2 moveSpeedRange =
        new Vector2(1.8f, 2.3f);

    [SerializeField]
    private Vector2 accelerationRange =
        new Vector2(7f, 10f);

    [SerializeField]
    private Vector2 angularSpeedRange =
        new Vector2(300f, 420f);

    [SerializeField]
    private Vector2Int avoidancePriorityRange =
        new Vector2Int(25, 75);

    [Header("Arrival")]
    [SerializeField, Min(0f)]
    private float destinationTolerance = 0.12f;

    [Header("Facing while stopped")]
    [SerializeField, Min(1f)]
    private float faceTurnSpeed = 540f;

    private NavMeshAgent agent;
    private EnemyMovementLockReason movementLocks;
    private EnemyHealth health;
    private Vector3 impactVelocity, impactGround;
    private Transform impactSource;
    private bool externalImpact, resumeAgent;
    private float impactElapsed;
    private readonly RaycastHit[] impactHits = new RaycastHit[32];
    public bool IsExternallyDisplaced => externalImpact;
    public Vector3 RunPosition => externalImpact ? impactGround : transform.position;

    public NavMeshAgent Agent => agent;
    public bool MovementLocked =>
        movementLocks !=
        EnemyMovementLockReason.None;

    public EnemyMovementLockReason MovementLocks =>
        movementLocks;
    public event System.Action<EnemyMovementLockReason> MovementLocksChanged;

    public Vector3 Velocity =>
        agent != null
            ? agent.velocity
            : Vector3.zero;

    public float MoveSpeed =>
        agent != null
            ? agent.speed
            : 0f;

    public float SpeedNormalized =>
        agent != null &&
        agent.speed > 0.001f
            ? Mathf.Clamp01(
                agent.velocity.magnitude /
                agent.speed)
            : 0f;

    public bool IsReady =>
        agent != null &&
        agent.isActiveAndEnabled &&
        agent.isOnNavMesh;

    public bool HasReachedDestination
    {
        get
        {
            if (!IsReady)
                return false;

            if (agent.pathPending)
                return false;

            if (!agent.hasPath)
                return true;

            float threshold =
                agent.stoppingDistance +
                destinationTolerance;

            return
                agent.remainingDistance <= threshold &&
                agent.velocity.sqrMagnitude <= 0.05f;
        }
    }

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();

        ApplyPerEnemyVariation();
    }

    /// <summary>Temporary swept ballistic movement. Navigation retains the safe ground endpoint.</summary>
    public bool ApplyExternalImpact(Vector3 velocity, Transform source)
    {
        if (!externalImpact && !IsReady) return false;
        if (!externalImpact)
        {
            impactGround = transform.position;
            resumeAgent = agent.enabled;
            SetMovementLock(EnemyMovementLockReason.ExternalImpact, true);
            agent.enabled = false;
        }
        externalImpact = true;
        impactVelocity = velocity;
        impactSource = source;
        impactElapsed = 0;
        return true;
    }

    private void Update()
    {
        if (!externalImpact || Time.deltaTime <= 0) return;
        // Bounded substeps keep the sweep/ground projection useful after a slow mobile frame.
        float remaining = Mathf.Min(Time.deltaTime, .2f);
        while (externalImpact && remaining > 0)
        {
            float dt = Mathf.Min(remaining, .02f);
            remaining -= dt;
            StepExternalImpact(dt);
        }
    }

    private void StepExternalImpact(float dt)
    {
        impactElapsed += dt;
        impactVelocity += Physics.gravity * dt;
        Vector3 step = impactVelocity * dt;
        Vector3 groundCandidate = impactGround + Vector3.ProjectOnPlane(step, Vector3.up);
        // Stay above connected walkable ground. A failed landing probe must never leave a
        // living alien stranded off the NavMesh or save an airborne pose for Continue.
        bool safe = NavMesh.SamplePosition(groundCandidate, out var ground, .5f, agent.areaMask)
            && Vector3.ProjectOnPlane(ground.position - groundCandidate, Vector3.up).sqrMagnitude <= .0025f
            && Mathf.Abs(ground.position.y - impactGround.y) < .4f
            && !NavMesh.Raycast(impactGround, ground.position, out _, agent.areaMask);
        if (!safe) { step.x = step.z = 0; impactVelocity.x = impactVelocity.z = 0; }
        float distance = step.magnitude;
        float radius = Mathf.Max(.1f, agent.radius);
        Vector3 bottom = transform.position + Vector3.up * (radius + .03f);
        Vector3 top = transform.position + Vector3.up * Mathf.Max(radius + .03f, agent.height - radius);
        int count = distance > .00001f ? Physics.CapsuleCastNonAlloc(bottom, top, radius, step / distance,
            impactHits, distance + .02f, ~0, QueryTriggerInteraction.Ignore) : 0;
        float travel = distance;
        if (count == impactHits.Length) travel = 0;
        for (int i = 0; i < count; i++)
        {
            var hit = impactHits[i];
            var obstacle = hit.collider.transform;
            if (obstacle.IsChildOf(transform) || (impactSource != null && obstacle.IsChildOf(impactSource))) continue;
            travel = Mathf.Min(travel, Mathf.Max(0, hit.distance - .02f));
        }
        if (travel < distance)
        {
            transform.position += step.normalized * travel;
            impactVelocity.x = impactVelocity.z = 0;
            if (impactVelocity.y > 0) impactVelocity.y = 0;
        }
        else
        {
            transform.position += step;
            if (safe) impactGround = ground.position;
        }
        if ((impactVelocity.y <= 0 && transform.position.y <= impactGround.y + .04f) || impactElapsed >= 2)
            EndExternalImpact();
    }

    public void EndExternalImpact()
    {
        if (!externalImpact) return;
        externalImpact = false;
        transform.position = impactGround;
        impactVelocity = Vector3.zero;
        impactSource = null;
        if (resumeAgent && (health == null || !health.IsDead))
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.Warp(impactGround);
        }
        SetMovementLock(EnemyMovementLockReason.ExternalImpact, false);
    }

    private void OnDisable() => EndExternalImpact();

    public bool SetDestination(
        Vector3 destination)
    {
        if (MovementLocked ||
            !IsReady)
        {
            return false;
        }

        agent.isStopped = false;

        return agent.SetDestination(
            destination);
    }

    public void Stop()
    {
        if (!IsReady)
            return;

        agent.isStopped = true;

        if (agent.hasPath)
            agent.ResetPath();

        // Kill residual agent velocity immediately so a hit reaction
        // does not slide for a frame after movement is locked.
        agent.velocity = Vector3.zero;
    }

    public void FacePosition(
        Vector3 worldPosition)
    {
        if (MovementLocked)
            return;

        Vector3 direction =
            worldPosition -
            transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion desired =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                desired,
                faceTurnSpeed *
                Time.deltaTime);
    }

    public void SetMovementLock(
        EnemyMovementLockReason reason,
        bool locked)
    {
        if (reason == EnemyMovementLockReason.None)
            return;
        var previous = movementLocks;

        if (locked)
        {
            movementLocks |=
                reason;
        }
        else
        {
            movementLocks &=
                ~reason;
        }

        if (locked)
        {
            Stop();
        }
        if (previous != movementLocks) MovementLocksChanged?.Invoke(movementLocks);
    }

    private void ApplyPerEnemyVariation()
    {
        if (agent == null)
            return;

        float minSpeed =
            Mathf.Min(
                moveSpeedRange.x,
                moveSpeedRange.y);

        float maxSpeed =
            Mathf.Max(
                moveSpeedRange.x,
                moveSpeedRange.y);

        float minAcceleration =
            Mathf.Min(
                accelerationRange.x,
                accelerationRange.y);

        float maxAcceleration =
            Mathf.Max(
                accelerationRange.x,
                accelerationRange.y);

        float minAngular =
            Mathf.Min(
                angularSpeedRange.x,
                angularSpeedRange.y);

        float maxAngular =
            Mathf.Max(
                angularSpeedRange.x,
                angularSpeedRange.y);

        int minPriority =
            Mathf.Clamp(
                Mathf.Min(
                    avoidancePriorityRange.x,
                    avoidancePriorityRange.y),
                0,
                99);

        int maxPriority =
            Mathf.Clamp(
                Mathf.Max(
                    avoidancePriorityRange.x,
                    avoidancePriorityRange.y),
                0,
                99);

        agent.speed =
            Random.Range(
                minSpeed,
                maxSpeed);

        agent.acceleration =
            Random.Range(
                minAcceleration,
                maxAcceleration);

        agent.angularSpeed =
            Random.Range(
                minAngular,
                maxAngular);

        agent.avoidancePriority =
            Random.Range(
                minPriority,
                maxPriority + 1);

        agent.autoBraking = true;
    }
}
