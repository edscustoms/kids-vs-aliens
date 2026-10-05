using System;
using UnityEngine;

/// <summary>AI intentions only. Movement, support, jump, turbo and collisions stay in AlienBikeController.</summary>
[DefaultExecutionOrder(-15), DisallowMultipleComponent, RequireComponent(typeof(AlienBikeController), typeof(EnemyActor))]
public sealed class EnemyBikeDriver : MonoBehaviour, IRunStateParticipant
{
    public enum DriveState { Dormant, FrontPass, TurnBack, Pursuit, Disengaged }
    public enum DistanceBand { CatchUp, Pursuit, Attack, Recover, Flee }
    public enum VehicleAttack { Sideswipe, RearQuarter, CutAcross, Block }
    [SerializeField] private BikeRouteGuide guide;
    [SerializeField] private PlayerBikeRider player;
    [SerializeField] private EnemyBikeDriver[] peers = Array.Empty<EnemyBikeDriver>();
    [Header("Stable rider variation")]
    [SerializeField] private float preferredSide = 1;
    [SerializeField, Min(0)] private float reactionInterval = .18f;
    [SerializeField, Min(0)] private float fireDelay = .2f;
    [SerializeField, Min(1)] private float preferredDistance = 11;
    [SerializeField, Range(.8f, 1.2f)] private float throttleAggression = 1;
    [SerializeField] private float jumpTimingOffset;
    [Header("BikeRoute chase pressure")]
    [SerializeField, Min(20)] private float catchUpDistance = 40;
    [SerializeField, Range(1, 1.8f)] private float catchUpMultiplier = 1.15f;
    [SerializeField, Range(1, 1.6f)] private float pursuitMultiplier = 1;
    [SerializeField, Min(5)] private float attackDistance = 17;
    [SerializeField, Min(.5f)] private float attackSeconds = 3.6f;
    [SerializeField, Min(0)] private float committedRamDamage;
    [SerializeField, Min(1)] private float attackRecoverySeconds = 1.8f;
    [Header("Lead blocker (ordinary drive input)")]
    [SerializeField, Min(4)] private float leadHoldSeconds = 6;
    [SerializeField, Range(10, 25)] private float leadDistance = 16;
    [SerializeField, Min(1)] private float leadRecoverySeconds = 4;
    [Header("Engagement envelope (relative to Amy)")]
    [SerializeField, Min(1)] private float envelopeRear = 8;
    [SerializeField, Min(1)] private float envelopeFront = 6;
    [SerializeField, Min(1)] private float overshootDistance = 10;
    [SerializeField, Range(.4f, .8f)] private float predictionSeconds = .6f;
    [SerializeField, Min(1)] private float crossingExit = 2.7f;
    public DriveState State { get; private set; }
    public AlienBikeController Bike { get; private set; }
    public EnemyActor Actor { get; private set; }
    public bool HasActivated { get; private set; }
    public float PreferredSide => preferredSide;
    public float FireDelay => fireDelay;
    public Vector2 Intent { get; private set; }
    public DistanceBand Band { get; private set; }
    public VehicleAttack Maneuver { get; private set; }
    public bool CatchUpAdvantage { get; private set; }
    public float LongitudinalOffset => player == null ? 0 : Vector3.Dot(transform.position - player.transform.position, PlayerForward);
    public float DistanceToPlayer => player == null ? float.PositiveInfinity : Vector3.ProjectOnPlane(transform.position - player.transform.position, Vector3.up).magnitude;
    public bool InsideEnvelope => LongitudinalOffset >= -envelopeRear && LongitudinalOffset <= envelopeFront
        && DistanceToPlayer < 18 && Mathf.Abs(transform.position.y - player.transform.position.y) < 4;
    public bool OvershootRecovering { get; private set; }
    public Vector3 ContactTarget { get; private set; }
    public int AttackAttempts => attackNumber;
    public int CompletedAttacks => attackNumber - (attackEnds > 0 ? 1 : 0);
    public bool IsCommittingContact => attackEnds > Time.time;
    public bool PrioritizesContact => IsCommittingContact || State == DriveState.Pursuit && contactCorridor && DistanceToPlayer < 11;
    public bool IsLeadBlocker { get; private set; }
    public bool HoldingLead => IsLeadBlocker && leadHoldStarted > 0;
    public int LeadWindows { get; private set; }
    public bool ContactSinceLastLead => CompletedAttacks > attacksAtLastLead;
    public bool CanPrepareLead => player != null && player.IsDriving && isActiveAndEnabled && HasActivated && State == DriveState.Pursuit
        && Actor != null && Actor.IsAlive && !Actor.Motor.MovementLocked && Bike.IsGrounded
        && contactCorridor && !IsLeadBlocker && !IsCommittingContact
        && (vehicleLaser == null || vehicleLaser.Progress <= 0)
        && !CatchUpAdvantage && Time.time >= nextLead && Time.time >= nextAttack
        && LongitudinalOffset >= -15 && LongitudinalOffset <= 25 && DistanceToPlayer < 30;
    public bool CanStartLead => CanPrepareLead && LongitudinalOffset >= 2;
    private bool PreparingLead => chase != null && chase.ShouldStageLead(this);
    public bool CanStartContact => isActiveAndEnabled && HasActivated && State == DriveState.Pursuit
        && Actor != null && Actor.IsAlive && !Actor.Motor.MovementLocked && Bike.IsGrounded
        && contactCorridor && !IsLeadBlocker && !PreparingLead && (vehicleLaser == null || vehicleLaser.Progress < .15f) && Time.time >= nextAttack && DistanceToPlayer < attackDistance
        && LongitudinalOffset >= -15 && LongitudinalOffset <= envelopeFront && !OvershootRecovering && !CatchUpAdvantage
        && (player.Bike == null || Vector3.Dot(Bike.Body.linearVelocity, PlayerForward) >= player.Bike.Speed - 4);
    private Vector3 PlayerForward => Vector3.ProjectOnPlane(player.Bike != null ? player.Bike.Body.rotation * Vector3.forward : player.transform.forward, Vector3.up).normalized;
    private BikeRouteChaseDirector chase;
    private int stagingIndex;
    private float attackSide, recoverySide;
    private bool contactCorridor;
    private EnemyRangedAttack attack;
    private AlienBikeLaserWeapon vehicleLaser;
    private EnemyEquipment equipment;
    private BikeRouteGuide.Sample road;
    private float nextDecision, blockedSeconds, recoveryUntil, passSide;
    private bool started, turbo, jump;
    private float attackEnds, nextAttack;
    private int attackNumber;
    private float nextLead, leadStarted, leadHoldStarted, leadVisibleSeconds;
    private int attacksAtLastLead;
    private bool tuningCached;
    private float normalSpeed, normalAcceleration, turboSpeed, turboAcceleration;
    private Vector3 previousPosition;
    private Vector3 steeringTarget;
    private bool followSteeringTarget, followSpeedTarget;
    private float speedTarget;
    private readonly RaycastHit[] obstacleHits = new RaycastHit[12];
    public string RunStateKey => "enemy-bike-driver-v1";
    [Serializable] private sealed class Saved { public DriveState state; public bool activated; }

    private void Cache()
    {
        if (Bike == null) Bike = GetComponent<AlienBikeController>();
        if (Actor == null) Actor = GetComponent<EnemyActor>();
        if (attack == null) attack = GetComponent<EnemyRangedAttack>();
        if (vehicleLaser == null) vehicleLaser = GetComponent<AlienBikeLaserWeapon>();
        if (equipment == null) equipment = GetComponent<EnemyEquipment>();
        if (!tuningCached && Bike != null)
        {
            normalSpeed = Bike.maxSpeed; normalAcceleration = Bike.acceleration;
            turboSpeed = Bike.turboMaxSpeed; turboAcceleration = Bike.turboAcceleration;
            tuningCached = true;
        }
    }
    private void Awake() { Cache(); previousPosition = transform.position; }
    private void OnEnable()
    {
        Cache(); Actor.Health.OnDied += OnDeath; equipment.Changed += HideHandheld;
        HideHandheld(); started = false;
    }
    private void HideHandheld() => equipment.SetRangedPresentation(false);
    private void OnDisable()
    {
        EndLead();
        contactCorridor = false;
        attackEnds = 0;
        if (Band == DistanceBand.Attack) Band = DistanceBand.Recover;
        chase?.ReleaseContact(this);
        if (Actor != null) Actor.Health.OnDied -= OnDeath;
        if (equipment != null) equipment.Changed -= HideHandheld;
        attack?.Cancel();
        SetCatchUpAdvantage(false);
        Bike?.Detach(); started = false; followSteeringTarget = followSpeedTarget = false;
    }
    private void OnDeath()
    {
        EndLead();
        attackEnds = 0; Band = DistanceBand.Recover;
        chase?.ReleaseContact(this); attack.Cancel(); SetCatchUpAdvantage(false); Bike.Detach();
    }
    public void BindChase(BikeRouteChaseDirector owner, int index) { chase = owner; stagingIndex = index; }
    private void EndContact()
    {
        if (attackEnds <= 0) return;
        attackEnds = 0; nextAttack = Time.time + attackRecoverySeconds + fireDelay * .5f;
        Band = DistanceBand.Recover;
        recoverySide = Mathf.Sign(Vector3.Dot(transform.position - player.transform.position, Vector3.Cross(Vector3.up, PlayerForward)));
        if (recoverySide == 0) recoverySide = -attackSide;
        chase?.ReleaseContact(this);
    }
    private void EndLead()
    {
        if (IsLeadBlocker) { nextLead = Time.time + leadRecoverySeconds; attacksAtLastLead = CompletedAttacks; }
        IsLeadBlocker = false; leadHoldStarted = leadVisibleSeconds = 0;
        chase?.ReleaseLead(this);
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (attackEnds <= Time.time || player == null || player.Bike == null
            || collision.collider.GetComponentInParent<AlienBikeController>() != player.Bike
            || collision.impulse.sqrMagnitude <= 1) return;
        // Release first: compound-collider reentries cannot bill the same commitment twice.
        EndContact();
        if (committedRamDamage <= 0 || collision.impulse.sqrMagnitude < 60 * 60
            || collision.relativeVelocity.sqrMagnitude < 3 * 3 || collision.contactCount == 0) return;
        var contact = collision.GetContact(0);
        var hit = new HitInfo(committedRamDamage, contact.point, contact.normal,
            (player.Bike.Body.position - Bike.Body.position).normalized, gameObject);
        CombatHitResolver.Resolve(collision.collider, hit)?.ReceiveHit(hit);
    }
    public void Activate(Vector3 position, Quaternion rotation, bool frontPass)
    {
        Cache();
        if (HasActivated) return;
        // Initial placement only. Once activated, all travel/recovery is physical.
        transform.SetPositionAndRotation(position, rotation);
        HasActivated = true; State = frontPass ? DriveState.FrontPass : DriveState.Pursuit;
        passSide = Mathf.Sign(preferredSide);
        gameObject.SetActive(true);
        Bike.RememberSafePose(position);
        ActiveRunController.Instance?.MarkDirty();
    }
    public void Disengage()
    {
        EndLead();
        chase?.ReleaseContact(this);
        if (State == DriveState.Disengaged) return;
        State = DriveState.Disengaged;
        OvershootRecovering = false;
        Band = DistanceBand.Flee; attackEnds = 0; nextDecision = 0;
        followSteeringTarget = followSpeedTarget = false;
        SetCatchUpAdvantage(false);
        attack?.Cancel(); Actor?.ClearCurrentTarget(); Actor?.Perception?.ForgetTarget();
        Intent = Vector2.zero; turbo = jump = false;
        Bike?.SetDriveInput(this, Intent);
        Bike?.CancelCharge();
        ActiveRunController.Instance?.MarkDirty();
    }
    private void Update()
    {
        if (!HasActivated || Actor == null || !Actor.IsAlive || Time.timeScale <= 0
            || (ActiveRunController.Instance != null && !ActiveRunController.Instance.IsReady)) return;
        if (!started)
        {
            if (!Bike.ClaimDriver(this)) return;
            Bike.StartDriving(); started = true;
            road = guide.Project(transform.position);
            nextDecision = Time.time + reactionInterval;
            nextAttack = Time.time + 1 + fireDelay * 2;
            nextLead = Time.time + 4 + fireDelay;
            previousPosition = transform.position;
        }
        if (Actor.Motor.MovementLocked)
        {
            EndContact(); EndLead();
            Intent = Vector2.zero; turbo = jump = false; attack.Cancel();
        }
        else if (Time.time >= nextDecision)
        {
            nextDecision = Time.time + reactionInterval;
            // Chase-only rider presentation. Inventory/loot and all on-foot enemies
            // retain their equipment; the shared vehicle laser owns ranged attacks.
            attack.Cancel();
            ChooseDriveIntent();
        }
        if (followSteeringTarget && !Actor.Motor.MovementLocked)
        {
            // Tactical choices retain each rider's reaction cadence, but steering
            // must track the moving chassis between choices at 50+ m/s.
            Intent = new Vector2(SteerToward(steeringTarget),
                followSpeedTarget ? ThrottleForSpeed(speedTarget) : Intent.y);
        }
        Bike.SetDriveInput(this, Intent);
        Bike.TickControls(Time.deltaTime, jump, turbo);
    }
    private void ChooseDriveIntent()
    {
        followSteeringTarget = followSpeedTarget = false;
        if (player == null) { Disengage(); return; }
        road = guide.Project(transform.position, road.path);
        var targetRoad = guide.Project(player.transform.position);
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        bool fleeing = State == DriveState.Disengaged;
        bool passing = State == DriveState.FrontPass;
        if (passing && road.progress < targetRoad.progress - 9)
        { State = DriveState.TurnBack; ActiveRunController.Instance?.MarkDirty(); passing = false; }
        if (State == DriveState.TurnBack)
        {
            float angle = Vector3.SignedAngle(forward, Vector3.ProjectOnPlane(road.forward, Vector3.up), Vector3.up);
            Intent = new Vector2(Bike.Speed < 4 ? Mathf.Clamp(angle / 28, -1, 1) : 0, 0);
            turbo = jump = false;
            if (Mathf.Abs(angle) < 18 && Bike.Speed < 5) State = DriveState.Pursuit;
            return;
        }
        // Only the authored opening pass travels against route direction. An
        // overshooting pursuer slows and lets Amy close; it never U-turns into her.
        float direction = passing ? -1 : 1;
        float lookDistance = Mathf.Clamp(7 + Bike.Speed * .45f, 7, 24);
        bool preparingLead = PreparingLead;
        if (IsLeadBlocker || preparingLead) lookDistance = Mathf.Clamp(14 + Bike.Speed * .5f, 14, 32);
        int followPath = fleeing ? road.path : targetRoad.path;
        var ahead = guide.Ahead(road, lookDistance * direction, followPath);
        var steeringRoad = ahead;
        var farther = guide.Ahead(road, (lookDistance + 12) * direction, followPath);
        float lane = (passing ? passSide : Mathf.Clamp(preferredSide, -1, 1))
            * Mathf.Max(.5f, Mathf.Min(passing ? 2.8f : 1.7f, road.halfWidth - 1.05f));
        Vector3 destination = ahead.position + ahead.Right * lane;
        Vector3 delta = player.transform.position - transform.position;
        float playerDistance = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
        float playerSpeed = player.Bike != null ? player.Bike.Speed : 0;
        bool sharedCorridor = road.path == targetRoad.path || Mathf.Abs(road.progress - targetRoad.progress) < 12
            && Vector3.Distance(road.position, targetRoad.position) < 12;
        // Contact is local; a 17-m lead must also survive a normal section join.
        // Do not confuse adjacent joined sections with parallel shortcut branches.
        var ownPath = guide.paths[road.path];
        var playerPath = guide.paths[targetRoad.path];
        bool leadCorridor = sharedCorridor || playerDistance < 65
            && (Mathf.Abs(ownPath.startProgress - playerPath.endProgress) < 1
                || Mathf.Abs(ownPath.endProgress - playerPath.startProgress) < 1);
        contactCorridor = sharedCorridor && Mathf.Abs(transform.position.y - player.transform.position.y) < 4;
        float behind = targetRoad.progress - road.progress;
        float longitudinal = playerDistance < 45 && sharedCorridor ? LongitudinalOffset : -behind;
        if (IsLeadBlocker && (!player.IsDriving || !leadCorridor || playerDistance > 65 || Time.time - leadStarted > 22
            || leadVisibleSeconds >= leadHoldSeconds
            || leadHoldStarted > 0 && longitudinal < -4
            || Time.time - leadStarted > 4 && longitudinal < -12)) EndLead();
        if (!passing && !fleeing && !IsLeadBlocker)
        {
            if (longitudinal > overshootDistance) OvershootRecovering = true;
            else if (longitudinal < envelopeFront - 2) OvershootRecovering = false;
        }
        bool catching = !passing && !fleeing && !OvershootRecovering
            && -longitudinal > (CatchUpAdvantage ? Mathf.Max(2, envelopeRear - 2) : catchUpDistance);
        SetCatchUpAdvantage(catching, !fleeing && !passing);
        if (catching)
        {
            // Prepare a side before reaching Amy, rather than carrying the
            // catch-up momentum straight into her rear without a reservation.
            lane = Mathf.Sign(preferredSide) * Mathf.Lerp(.5f, 1.9f, Mathf.InverseLerp(30, 12, -longitudinal));
            destination = ahead.position + ahead.Right * lane;
            EndContact();
        }
        float desiredSpeed = catching ? Bike.turboMaxSpeed : Bike.maxSpeed;
        bool pressure = false;
        Band = fleeing ? DistanceBand.Flee : catching ? DistanceBand.CatchUp : DistanceBand.Pursuit;
        if (fleeing)
        {
            // Finish removes Amy from guidance as well as combat. Keep moving down
            // the authored corridor, spreading to individual sides of its run-out.
            lane = Mathf.Sign(preferredSide) * Mathf.Min(2.3f, ahead.halfWidth - 1.1f);
            destination = ahead.position + ahead.Right * lane;
            if (ahead.progress >= guide.FinishProgress - .1f)
                destination += ahead.forward * 12;
            desiredSpeed = normalSpeed * .85f;
        }
        else if (!passing && !catching)
        {
            if (!IsLeadBlocker && chase != null && chase.TryReserveLead(this))
            {
                IsLeadBlocker = true; leadStarted = Time.time; leadHoldStarted = leadVisibleSeconds = 0;
                OvershootRecovering = false;
            }
            if (attackEnds > 0 && (Time.time >= attackEnds || OvershootRecovering
                || !sharedCorridor || playerDistance > 35 || chase == null || chase.PrimaryAttacker != this)) EndContact();
            if (!OvershootRecovering && sharedCorridor && CanStartContact && chase != null && chase.TryReserveContact(this))
            {
                if (attackEnds <= 0)
                {
                    Maneuver = (VehicleAttack)((attackNumber++ + Mathf.RoundToInt(fireDelay * 4)) % 4);
                    attackEnds = Time.time + attackSeconds;
                    attackSide = Mathf.Sign(Vector3.Dot(transform.position - player.transform.position, targetRoad.Right));
                    if (attackSide == 0) attackSide = Mathf.Sign(preferredSide);
                    attack.Cancel();
                }
            }
            pressure = attackEnds > Time.time;
            bool recovering = OvershootRecovering || Time.time < nextAttack;
            Band = pressure ? DistanceBand.Attack : recovering ? DistanceBand.Recover : DistanceBand.Pursuit;
            // Each authored wave has two side/rear positions and one forward setup.
            // Speed is a damped position correction around Amy, never a race target.
            // A supporting rider stages toward an overtake. Reserve the lead only
            // after it is actually ahead, rather than monopolizing that role from behind.
            float wantedLong = preparingLead ? leadDistance : stagingIndex == 2 ? envelopeFront - 2 : -Mathf.Max(13, preferredDistance);
            float side = Mathf.Sign(preferredSide) * (preparingLead ? 2.1f : 1.9f);
            if (recovering) { wantedLong = -envelopeRear; side = (recoverySide == 0 ? Mathf.Sign(preferredSide) : recoverySide) * 2.5f; }
            float relativeSpeed = Vector3.Dot(Bike.Body.linearVelocity, PlayerForward) - playerSpeed;
            desiredSpeed = Mathf.Clamp(playerSpeed + (wantedLong - longitudinal) * 1.5f - relativeSpeed * .3f, 0, Bike.turboMaxSpeed);
            if (OvershootRecovering)
            {
                EndContact();
                desiredSpeed = Mathf.Max(0, playerSpeed - Mathf.Max(5, (longitudinal - envelopeFront) * 2));
            }
            destination = ahead.position + ahead.Right * side;
            if (sharedCorridor && playerDistance < 38)
            {
                float playerLane = Vector3.Dot(player.transform.position - targetRoad.position, targetRoad.Right);
                destination += ahead.Right * playerLane;
            }
            if (pressure)
            {
                // A real crossing target: predict Amy's physical travel, then aim
                // through that line to the opposite side. The road only bounds it.
                Vector3 velocity = player.Bike != null ? Vector3.ProjectOnPlane(player.Bike.Body.linearVelocity, Vector3.up) : Vector3.zero;
                // Follow Amy's chosen branch through a fork. A global nearest-
                // road projection of a future point can select the other branch
                // and send a crossing attacker hundreds of metres away.
                var predictionRoad = guide.Ahead(targetRoad, Mathf.Max(0, Vector3.Dot(velocity, targetRoad.forward)) * predictionSeconds, targetRoad.path);
                float predictedLane = Vector3.Dot(player.transform.position - targetRoad.position, targetRoad.Right)
                    + Vector3.Dot(velocity, targetRoad.Right) * predictionSeconds;
                float exit = -attackSide * crossingExit;
                float lead = 0;
                if (Maneuver == VehicleAttack.RearQuarter) { exit = attackSide * .3f; lead = -1.5f; }
                if (Maneuver == VehicleAttack.Block) { exit = attackSide * .35f; lead = 2; }
                steeringRoad = guide.Ahead(predictionRoad, lead, targetRoad.path);
                destination = steeringRoad.position + steeringRoad.Right * (predictedLane + exit);
                ContactTarget = destination;
                desiredSpeed = Mathf.Min(Bike.turboMaxSpeed, playerSpeed + 10);
                if (Maneuver == VehicleAttack.Block && longitudinal > 2) desiredSpeed = Mathf.Max(0, playerSpeed - 6);
            }
            if (IsLeadBlocker)
            {
                // Pass on an outside lane, then control a short forward window.
                // Amy can counter-lock this bike; it cannot fire backwards at her.
                if (leadHoldStarted <= 0 && longitudinal >= 10 && longitudinal <= 25)
                { leadHoldStarted = Time.time; LeadWindows++; }
                // The quiet post-shot cooldown is not a counter-lock opportunity.
                // Hold the blocking line through it, then allow a complete new lock.
                var playerLaser = player.Bike != null ? player.Bike.GetComponent<AlienBikeLaserWeapon>() : null;
                if (longitudinal >= 10 && longitudinal <= 25 && playerLaser != null && !playerLaser.CoolingDown && playerLaser.IsValidTarget(Bike))
                    leadVisibleSeconds += reactionInterval;
                else leadVisibleSeconds = 0;
                float leadLane = Mathf.Lerp(Mathf.Sign(preferredSide) * 2.1f,
                    Mathf.Sin((Time.time - leadStarted) * .8f) * .45f,
                    Mathf.SmoothStep(0, 1, Mathf.InverseLerp(8, 16, longitudinal)));
                float playerLane = Vector3.Dot(player.transform.position - targetRoad.position, targetRoad.Right);
                destination = ahead.position + ahead.Right * (playerLane + leadLane);
                desiredSpeed = Mathf.Clamp(playerSpeed + (leadDistance - longitudinal) * 1.1f - relativeSpeed * .35f, 0, Bike.turboMaxSpeed);
            }
        }
        if (passing && playerDistance < 70 && playerDistance > 35)
        {
            // Commit to the outer side, never Amy's exact predicted position on this first pass.
            float playerLane = Vector3.Dot(player.transform.position - targetRoad.position, targetRoad.Right);
            passSide = Mathf.Abs(playerLane) > .6f ? -Mathf.Sign(playerLane) : Mathf.Sign(preferredSide);
            lane = Mathf.Abs(lane) * passSide;
            destination = ahead.position + ahead.Right * lane;
        }
        foreach (var peer in peers)
        {
            if (peer == null || peer == this || !peer.isActiveAndEnabled || !peer.HasActivated) continue;
            Vector3 separation = transform.position - peer.transform.position;
            float distance = separation.magnitude;
            if (distance > .05f && distance < 5)
            {
                // Support riders yield space to the committed crossing instead of
                // steering its attacker away from Amy. Stable phases break ties.
                float sideBias = Vector3.Dot(separation, road.Right);
                if (Mathf.Abs(sideBias) < .2f) sideBias = fireDelay < peer.FireDelay ? -1 : 1;
                if (!pressure) destination += road.Right * Mathf.Sign(sideBias) * (5 - distance) * .8f;
                if (!pressure && Mathf.Abs(Vector3.Dot(separation, road.Right)) < 1.7f && Vector3.Dot(separation, forward) < 0)
                    desiredSpeed = Mathf.Min(desiredSpeed, Mathf.Max(0, peer.Bike.Speed - 3));
            }
        }
        // Keep offsets inside the authored lane; they never select an unauthored shortcut.
        // An intercept may be farther around a bend than the ordinary look-ahead.
        // Clamp against its own road cross-section, or the clamp steers the attack
        // away from Amy's lane before the bikes can make contact.
        float lateral = Vector3.Dot(destination - steeringRoad.position, steeringRoad.Right);
        // Targets must also fit inside the road-margin braking guard below;
        // otherwise a support lane repeatedly asks the bike to brake itself.
        float laneLimit = Mathf.Max(.3f, steeringRoad.halfWidth - (passing || IsLeadBlocker || preparingLead ? 1.05f : 1.65f));
        destination -= steeringRoad.Right * (lateral - Mathf.Clamp(lateral, -laneLimit, laneLimit));
        Vector3 desired = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up).normalized;
        float steeringAngle = Vector3.SignedAngle(forward, desired, Vector3.up);
        // Pitch changes at crests are handled by shared support, not steering.
        // Treating a hill crest as a sharp corner unnecessarily dumps chase speed.
        float bend = Vector3.Angle(Vector3.ProjectOnPlane(ahead.forward, Vector3.up),
            Vector3.ProjectOnPlane(farther.forward, Vector3.up));
        bool closing = !fleeing && !passing && !OvershootRecovering && desiredSpeed > Bike.maxSpeed;
        float cruisingSpeed = pressure || catching || closing ? Bike.turboMaxSpeed : Bike.maxSpeed;
        // The two guide headings are twelve metres apart. Use their curvature and
        // the chassis' available yaw rate to retain momentum through broad bends,
        // reserving steering headroom for lateral correction and physical contact.
        float curveSpeed = Bike.steeringStrength * Bike.steeringAtMaxSpeed * 12 * .8f / Mathf.Max(1, bend);
        desiredSpeed = Mathf.Min(desiredSpeed, Mathf.Min(cruisingSpeed, Mathf.Max(9, curveSpeed)));
        if (road.halfWidth < 3.2f) desiredSpeed = Mathf.Min(desiredSpeed,
            catching ? Bike.maxSpeed * .95f : passing ? Bike.maxSpeed * .72f : Mathf.Max(Bike.maxSpeed, playerSpeed + 5));
        if (Mathf.Abs(steeringAngle) > 15)
            desiredSpeed = Mathf.Min(desiredSpeed, Mathf.Lerp(IsLeadBlocker || preparingLead ? Mathf.Max(Bike.maxSpeed, playerSpeed) : Bike.maxSpeed, 5,
                Mathf.InverseLerp(15, 45, Mathf.Abs(steeringAngle))));
        bool boostLine = road.halfWidth >= (catching || closing ? 2.75f : 3.2f)
            && bend < (IsLeadBlocker || preparingLead ? 18 : catching || closing ? 12 : 5)
            && Mathf.Abs(steeringAngle) < (IsLeadBlocker || preparingLead ? 22 : 10);
        // Preserve V2's distant catch-up/opening pass. Close pursuit must keep its
        // envelope speed target even while requesting turbo to reach that target.
        if (boostLine && catching) desiredSpeed = Mathf.Min(Bike.turboMaxSpeed, Mathf.Max(9, curveSpeed));
        else if (boostLine && passing) desiredSpeed = Bike.turboMaxSpeed;
        if (catching && sharedCorridor && -longitudinal < 30)
            desiredSpeed = Mathf.Min(desiredSpeed, playerSpeed + Mathf.Max(0, -longitudinal - Mathf.Max(1, envelopeRear - 3)) * 1.5f);
        // Spend the existing charge on acceleration, not on partial-throttle
        // cruising. Keep the speed target while coasting between boost pulses.
        turbo = !fleeing && !OvershootRecovering && (passing || pressure || closing || catching) && boostLine
            && Bike.Turbo01 > .1f && Bike.Speed < desiredSpeed - (turbo ? .5f : 3);
        float roadLateral = Mathf.Abs(Vector3.Dot(transform.position - road.position, road.Right));
        if (roadLateral > road.halfWidth - (IsLeadBlocker || preparingLead ? .9f : 1.5f))
        {
            // Recover road margin before accelerating again. Looking ahead alone can
            // underestimate a bend while momentum is still carrying the bike outward.
            turbo = false; desiredSpeed = Mathf.Min(desiredSpeed, Bike.maxSpeed * .6f);
            if (!passing && !fleeing)
            {
                // Slowing while still aiming along the outer lane can strand a
                // rider at the guard indefinitely. Recover inward, then rejoin.
                destination = ahead.position;
                if (pressure) { EndContact(); pressure = false; Band = DistanceBand.Recover; }
            }
        }
        if (passing && playerDistance < 35 && road.halfWidth < 3.1f)
        { turbo = false; desiredSpeed = Mathf.Min(desiredSpeed, 6); }
        speedTarget = desiredSpeed; followSpeedTarget = true;
        float throttle = ThrottleForSpeed(desiredSpeed);
        if (Time.time < recoveryUntil) { throttle = -.65f; turbo = false; followSpeedTarget = false; }
        else
        {
            if (throttle > .1f && Vector3.Distance(previousPosition, transform.position) < .15f) blockedSeconds += reactionInterval;
            else blockedSeconds = 0;
            if (blockedSeconds > 1.2f) { recoveryUntil = Time.time + 1; blockedSeconds = 0; }
        }
        previousPosition = transform.position;
        // A short physical feeler reduces throttle for real scenery; enemy bikes remain contact obstacles.
        int count = Physics.SphereCastNonAlloc(transform.position + Vector3.up * .65f, .55f, forward,
            obstacleHits, Mathf.Max(2.5f, Bike.Speed * (passing ? .8f : .25f)), ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = obstacleHits[i];
            if (hit.transform.IsChildOf(transform)) continue;
            var otherBike = hit.collider.GetComponentInParent<AlienBikeController>();
            if (otherBike != null)
            {
                // Yield to Amy if she occupies the first-pass line. Pursuit contacts
                // still use ordinary physical pressure after this non-attacking pass.
                if (passing && otherBike.Rider != null) { throttle = 0; turbo = false; followSpeedTarget = false; }
                continue;
            }
            if (hit.normal.y > .65f) continue;
            if (Bike.Speed > 8) { throttle = 0; followSpeedTarget = false; }
        }
        Intent = new Vector2(Mathf.Clamp(steeringAngle / 30, -1, 1), throttle);
        steeringTarget = destination; followSteeringTarget = true;
        jump = !passing && !OvershootRecovering && guide.ShouldChargeJump(road, Bike.Speed, jumpTimingOffset);
    }
    private float ThrottleForSpeed(float desiredSpeed)
    {
        // Zero input means strong braking in the shared arcade controller. Coasting
        // near a target (especially between turbo bursts) must not repeatedly brake
        // a rider from turbo speed to normal speed at its tactical reaction interval.
        float tolerance = Band == DistanceBand.CatchUp || State == DriveState.FrontPass ? 2 : .6f;
        if (desiredSpeed < .5f || Bike.Speed > desiredSpeed + tolerance) return 0;
        if (turbo && Bike.Speed < desiredSpeed) return 1;
        return Mathf.Clamp((desiredSpeed - Bike.Speed) * .25f * throttleAggression, .06f, 1);
    }
    private float SteerToward(Vector3 destination)
    {
        Vector3 heading = Vector3.ProjectOnPlane(Bike.Body.rotation * Vector3.forward, Vector3.up);
        Vector3 delta = Vector3.ProjectOnPlane(destination - Bike.Body.position, Vector3.up);
        float angle = Vector3.SignedAngle(heading, delta, Vector3.up);
        if (Bike.Speed < 6 || Mathf.Abs(angle) > 70) return Mathf.Clamp(angle / 30, -1, 1);
        // Convert the look-ahead arc into the yaw rate the existing controller can
        // deliver. A fixed angle/30 joystick mapping understeers more as speed rises.
        float yawRate = 2 * Bike.Speed * Mathf.Sin(angle * Mathf.Deg2Rad) / Mathf.Max(4, delta.magnitude);
        float availableYaw = Bike.steeringStrength * Mathf.Deg2Rad
            * Mathf.Lerp(1, Bike.steeringAtMaxSpeed, Mathf.Clamp01(Bike.Speed / Bike.maxSpeed));
        return Mathf.Clamp(yawRate / Mathf.Max(.1f, availableYaw), -1, 1);
    }
    private void SetCatchUpAdvantage(bool active, bool pursuing = false)
    {
        if (!tuningCached || Bike == null) return;
        CatchUpAdvantage = active;
        float scale = active ? catchUpMultiplier : pursuing ? pursuitMultiplier : 1;
        // Temporary values on this enemy instance only. Shared physics and Amy's
        // serialized tuning remain untouched; remove the advantage on every exit.
        Bike.maxSpeed = normalSpeed * scale; Bike.acceleration = normalAcceleration * scale;
        Bike.turboMaxSpeed = turboSpeed * scale; Bike.turboAcceleration = turboAcceleration * scale;
    }
    public string CaptureRunState() => JsonUtility.ToJson(new Saved { state = State, activated = HasActivated });
    public void RestoreRunState(string json)
    {
        EndLead(); nextLead = leadStarted = leadHoldStarted = 0; LeadWindows = attacksAtLastLead = 0;
        chase?.ReleaseContact(this); OvershootRecovering = contactCorridor = false;
        var saved = JsonUtility.FromJson<Saved>(json);
        State = saved.state; HasActivated = saved.activated; started = false;
        SetCatchUpAdvantage(false); attackEnds = nextAttack = 0; attackNumber = 0;
        Band = State == DriveState.Disengaged ? DistanceBand.Flee : DistanceBand.Pursuit;
        passSide = Mathf.Sign(preferredSide);
        Intent = Vector2.zero; jump = turbo = false; blockedSeconds = recoveryUntil = 0;
        followSteeringTarget = followSpeedTarget = false;
        // Bike and health participants restore their own absolute values, in either order.
        attack?.Cancel();
    }
}
