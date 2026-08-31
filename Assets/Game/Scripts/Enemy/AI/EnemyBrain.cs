using UnityEngine;

public enum EnemyBrainState
{
    Idle,
    Wander,
    Chase,
    Attack,
    Investigate,
    Dead
}

/// <summary>
/// Reusable first-pass enemy state machine.
///
/// Investigation can now start from:
/// - last known target position after LOS is lost
/// - an incoming shot direction, without revealing the shooter's position
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyBrain : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private EnemyActor actor;

    [SerializeField]
    private EnemyMotor motor;

    [SerializeField]
    private EnemyPerception perception;

    [SerializeField]
    private EnemyApproachPlanner approachPlanner;

    [SerializeField]
    private EnemyWanderPlanner wanderPlanner;

    [SerializeField]
    private EnemyInvestigationPlanner investigationPlanner;

    [SerializeField]
    private EnemyMeleeAttack meleeAttack;

    [Header("Dynamic chase")]
    [SerializeField]
    private Vector2 chaseRepathIntervalRange =
        new Vector2(0.15f, 0.35f);

    [Header("Idle")]
    [SerializeField]
    private Vector2 idleWaitRange =
        new Vector2(1.8f, 4.5f);

    [SerializeField, Range(0f, 1f)]
    private float wanderChance = 0.70f;

    [SerializeField, Min(1f)]
    private float wanderTravelTimeout = 12f;

    [Header("Investigation")]
    [SerializeField]
    private Vector2 investigatePointWaitRange =
        new Vector2(0.6f, 1.4f);

    [SerializeField, Min(1f)]
    private float investigateTotalTimeout = 10f;

    [Header("Incoming shot investigation")]
    [Tooltip(
        "How far the enemy initially moves in the direction the shot came from. " +
        "This is directional knowledge only; it does not use the shooter's position."
    )]
    [SerializeField, Min(0.5f)]
    private float incomingShotInvestigationDistance = 4f;

    public EnemyBrainState State { get; private set; }

    private float nextChaseRepathTime;
    private float nextIdleDecisionTime;
    private float wanderGiveUpTime;

    private float investigateGiveUpTime;
    private float investigateWaitUntil;
    private bool waitingAtInvestigationPoint;

    private Vector3 investigationAnchor;

    private bool incomingShotPending;
    private Vector3 pendingIncomingShotDirection;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void Start()
    {
        EnterIdle();
    }

    private void Update()
    {
        if (actor == null ||
            !actor.IsAlive)
        {
            EnterDead();
            return;
        }

        // If Amy is actually visible, real perception always wins.
        if (perception != null &&
            perception.HasTarget &&
            perception.CanSeeTarget)
        {
            incomingShotPending = false;

            SyncActorTarget(
                perception.Target);

            if (meleeAttack != null &&
                meleeAttack.CanAttack(
                    perception.Target))
            {
                UpdateAttack(
                    perception.Target);
            }
            else
            {
                UpdateChase(
                    perception.Target);
            }

            return;
        }

        // A hit reaction movement-locks the enemy.
        // Keep the direction in memory, but do not start walking until the
        // reaction animation has actually finished.
        if (incomingShotPending)
        {
            if (motor != null &&
                motor.MovementLocked)
            {
                return;
            }

            if (BeginIncomingShotInvestigation())
            {
                return;
            }

            incomingShotPending = false;
        }

        // Existing target, but LOS is lost:
        // investigate its last known position.
        if (perception != null &&
            perception.HasTarget)
        {
            SyncActorTarget(
                perception.Target);

            if (State !=
                EnemyBrainState.Investigate)
            {
                BeginInvestigation(
                    perception.LastKnownPosition);
            }

            UpdateInvestigation();
            return;
        }

        // Directional investigation has no target object at all.
        if (State ==
            EnemyBrainState.Investigate)
        {
            UpdateInvestigation();
            return;
        }

        ClearTargetState();
        UpdateIdleOrWander();
    }

    /// <summary>
    /// Stores only the incoming projectile direction.
    ///
    /// HitInfo.Direction points from shooter -> enemy.
    /// Investigation later uses the opposite direction to move toward
    /// where the shot came from.
    /// </summary>
    public void QueueIncomingShotInvestigation(
        Vector3 incomingShotDirection)
    {
        if (actor != null &&
            !actor.IsAlive)
        {
            return;
        }

        incomingShotDirection.y = 0f;

        if (incomingShotDirection.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        pendingIncomingShotDirection =
            incomingShotDirection.normalized;

        incomingShotPending =
            true;
    }

    private bool BeginIncomingShotInvestigation()
    {
        incomingShotPending =
            false;

        if (investigationPlanner == null)
            return false;

        Vector3 towardShotSource =
            -pendingIncomingShotDirection;

        if (!investigationPlanner.TryGetDirectionalAnchor(
                towardShotSource,
                incomingShotInvestigationDistance,
                out Vector3 anchor))
        {
            return false;
        }

        // Do not magically acquire Amy.
        perception?.ForgetTarget();
        actor?.ClearCurrentTarget();

        BeginInvestigation(
            anchor);

        return true;
    }

    private void UpdateChase(
        Transform target)
    {
        if (State !=
            EnemyBrainState.Chase)
        {
            State =
                EnemyBrainState.Chase;

            nextChaseRepathTime =
                0f;

            approachPlanner?.SetTarget(
                target);
        }

        if (Time.time <
            nextChaseRepathTime)
        {
            return;
        }

        Vector3 destination =
            target.position;

        if (approachPlanner != null)
        {
            approachPlanner.TryGetChasePosition(
                target,
                out destination);
        }

        motor?.SetDestination(
            destination);

        nextChaseRepathTime =
            Time.time +
            GetRandomRange(
                chaseRepathIntervalRange,
                0.05f);
    }

    private void UpdateAttack(
        Transform target)
    {
        if (State !=
            EnemyBrainState.Attack)
        {
            State =
                EnemyBrainState.Attack;

            motor?.Stop();
        }

        motor?.FacePosition(
            target.position);

        meleeAttack?.TryAttack(
            target);
    }

    private void BeginInvestigation(
        Vector3 anchor)
    {
        State =
            EnemyBrainState.Investigate;

        approachPlanner?.ClearTarget();

        investigationAnchor =
            anchor;

        investigateGiveUpTime =
            Time.time +
            investigateTotalTimeout;

        waitingAtInvestigationPoint =
            false;

        investigationPlanner?.BuildSearch(
            investigationAnchor);

        // First move to the actual investigation anchor.
        if (motor != null &&
            motor.SetDestination(
                investigationAnchor))
        {
            return;
        }

        MoveToNextInvestigationPoint();
    }

    private void UpdateInvestigation()
    {
        if (Time.time >=
            investigateGiveUpTime)
        {
            FinishInvestigation();
            return;
        }

        if (motor == null)
        {
            FinishInvestigation();
            return;
        }

        if (waitingAtInvestigationPoint)
        {
            if (Time.time <
                investigateWaitUntil)
            {
                motor.FacePosition(
                    investigationAnchor);

                return;
            }

            waitingAtInvestigationPoint =
                false;

            MoveToNextInvestigationPoint();
            return;
        }

        if (!motor.HasReachedDestination)
            return;

        motor.Stop();

        waitingAtInvestigationPoint =
            true;

        investigateWaitUntil =
            Time.time +
            GetRandomRange(
                investigatePointWaitRange,
                0.05f);
    }

    private void MoveToNextInvestigationPoint()
    {
        if (investigationPlanner != null &&
            investigationPlanner.TryGetNextPoint(
                out Vector3 nextPoint))
        {
            if (motor != null &&
                motor.SetDestination(
                    nextPoint))
            {
                return;
            }
        }

        FinishInvestigation();
    }

    private void FinishInvestigation()
    {
        perception?.ForgetTarget();

        actor?.ClearCurrentTarget();

        approachPlanner?.ClearTarget();

        EnterIdle();
    }

    private void UpdateIdleOrWander()
    {
        if (State ==
            EnemyBrainState.Wander)
        {
            if (motor == null ||
                motor.HasReachedDestination ||
                Time.time >=
                wanderGiveUpTime)
            {
                EnterIdle();
            }

            return;
        }

        if (State !=
            EnemyBrainState.Idle)
        {
            EnterIdle();
        }

        if (Time.time <
            nextIdleDecisionTime)
        {
            return;
        }

        bool shouldWander =
            wanderPlanner != null &&
            Random.value <
                wanderChance;

        if (shouldWander &&
            wanderPlanner.TryGetRandomPoint(
                out Vector3 destination))
        {
            if (motor != null &&
                motor.SetDestination(
                    destination))
            {
                State =
                    EnemyBrainState.Wander;

                wanderGiveUpTime =
                    Time.time +
                    wanderTravelTimeout;

                return;
            }
        }

        ScheduleNextIdleDecision();
    }

    private void EnterIdle()
    {
        State =
            EnemyBrainState.Idle;

        motor?.Stop();

        ScheduleNextIdleDecision();
    }

    private void EnterDead()
    {
        if (State ==
            EnemyBrainState.Dead)
        {
            return;
        }

        incomingShotPending =
            false;

        State =
            EnemyBrainState.Dead;

        motor?.Stop();

        approachPlanner?.ClearTarget();
    }

    private void SyncActorTarget(
        Transform target)
    {
        if (actor != null &&
            actor.CurrentTarget != target)
        {
            actor.SetCurrentTarget(
                target);
        }

        if (State !=
            EnemyBrainState.Investigate)
        {
            approachPlanner?.SetTarget(
                target);
        }
    }

    private void ClearTargetState()
    {
        if (actor != null &&
            actor.CurrentTarget != null)
        {
            actor.ClearCurrentTarget();
        }

        approachPlanner?.ClearTarget();
    }

    private void ScheduleNextIdleDecision()
    {
        nextIdleDecisionTime =
            Time.time +
            GetRandomRange(
                idleWaitRange,
                0.05f);
    }

    private static float GetRandomRange(
        Vector2 range,
        float minimum)
    {
        float min =
            Mathf.Max(
                minimum,
                Mathf.Min(
                    range.x,
                    range.y));

        float max =
            Mathf.Max(
                min,
                Mathf.Max(
                    range.x,
                    range.y));

        return
            Random.Range(
                min,
                max);
    }

    private void CacheReferences()
    {
        if (actor == null)
            actor =
                GetComponent<EnemyActor>();

        if (motor == null)
            motor =
                GetComponent<EnemyMotor>();

        if (perception == null)
            perception =
                GetComponent<EnemyPerception>();

        if (approachPlanner == null)
            approachPlanner =
                GetComponent<EnemyApproachPlanner>();

        if (wanderPlanner == null)
            wanderPlanner =
                GetComponent<EnemyWanderPlanner>();

        if (investigationPlanner == null)
            investigationPlanner =
                GetComponent<EnemyInvestigationPlanner>();

        if (meleeAttack == null)
            meleeAttack =
                GetComponent<EnemyMeleeAttack>();
    }
}
