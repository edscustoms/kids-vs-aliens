using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Thin NavMeshAgent wrapper.
/// Dynamic chase destinations are updated by EnemyBrain.
/// Idle/wander destinations are set once per wander decision.
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
    private bool movementLocked;

    public NavMeshAgent Agent => agent;
    public bool MovementLocked => movementLocked;

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

        ApplyPerEnemyVariation();
    }

    public bool SetDestination(
        Vector3 destination)
    {
        if (movementLocked ||
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
        if (movementLocked)
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

    public void SetMovementLocked(
        bool locked)
    {
        movementLocked = locked;

        if (locked)
        {
            Stop();
        }
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
