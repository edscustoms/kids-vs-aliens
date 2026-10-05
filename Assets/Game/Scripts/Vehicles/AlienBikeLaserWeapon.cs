using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared vehicle lock/fire owner. Rider weapons and their ammunition are independent.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(AlienBikeController))]
public sealed class AlienBikeLaserWeapon : MonoBehaviour
{
    [Header("Explicit vehicle combat wiring")]
    public Transform muzzle;
    public GameplayCameraController combatCamera;
    public PlayerBikeRider player;
    public AlienBikeController[] targets = System.Array.Empty<AlienBikeController>();
    public AlienBikeLaserBolt boltPrefab;
    [Header("Heavy laser")]
    [Min(.1f)] public float lockSeconds = 3.5f;
    [Min(.1f)] public float cooldownSeconds = 8;
    [Min(0)] public float dischargeSeconds = .3f;
    [Tooltip("Enemy-only prediction refresh at the start of the discharge beat; the remaining beat and bolt flight stay committed.")]
    [Range(0, .1f)] public float enemyPredictionRefreshSeconds;
    [Range(1, 89)] public float firingHalfAngle = 60;
    [Min(0)] public float initialDelay;
    [Min(1)] public float range = 110;
    [Min(1)] public float boltSpeed = 72;
    [Min(0)] public float damage = 30;
    [Header("Semantic audio")]
    public SoundEvent lockBeep, lockComplete, fireSound;
    private static readonly List<AlienBikeLaserWeapon> active = new();
    public static IReadOnlyList<AlienBikeLaserWeapon> Active => active;
    public AlienBikeController Bike { get; private set; }
    public AlienBikeController Target { get; private set; }
    public AlienBikeController LastTarget { get; private set; }
    public float Progress { get; private set; }
    public bool IsWindingUp { get; private set; }
    public float LockedAt { get; private set; }
    public int LocksCompleted { get; private set; }
    public float LastFireTime { get; private set; } = float.NegativeInfinity;
    public int ShotsFired { get; private set; }
    public bool CoolingDown => Time.time < readyAt;
    public float BeepInterval => Mathf.Lerp(.75f, .12f, Mathf.SmoothStep(0, 1, Progress));
    private EnemyBikeDriver driver;
    private PlayerHealth playerHealth;
    private float readyAt, beepElapsed;
    private bool wasOperational;
    private Vector3 dischargePoint;
    private readonly RaycastHit[] hits = new RaycastHit[32];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => active.Clear();
    private void Awake()
    {
        Bike = GetComponent<AlienBikeController>();
        driver = GetComponent<EnemyBikeDriver>();
        if (player != null) playerHealth = player.GetComponent<PlayerHealth>();
    }
    private void OnEnable() { active.Add(this); wasOperational = false; }
    private void OnDisable() { active.Remove(this); CancelLock(); wasOperational = false; }
    public void CancelLock() { Target = null; Progress = beepElapsed = 0; IsWindingUp = false; }
    public static Vector3 AimPoint(AlienBikeController target)
    {
        var hull = target.GetComponent<BoxCollider>();
        return hull != null ? hull.bounds.center : target.transform.position + Vector3.up * .7f;
    }
    private bool Operational => Bike != null && Bike.IsDriven && muzzle != null && combatCamera != null
        && Time.timeScale > 0 && (ActiveRunController.Instance == null || ActiveRunController.Instance.IsReady)
        && (player != null ? player.IsDriving && player.Bike == Bike && playerHealth != null && !playerHealth.IsDead
            && player.GetComponent<StarterAssets.StarterAssetsInputs>().CanProcessBikeControls
            && (player.DrivingInput.y > .05f || Vector3.Dot(Bike.Body.linearVelocity, Bike.transform.forward) > 1)
            : driver != null && driver.Actor.IsAlive && driver.State == EnemyBikeDriver.DriveState.Pursuit
                && !driver.PrioritizesContact);

    public bool InsideFiringCone(Vector3 point)
    {
        Vector3 forward = Vector3.ProjectOnPlane(Bike.Body.rotation * Vector3.forward, Vector3.up).normalized;
        Vector3 direction = Vector3.ProjectOnPlane(point - Bike.Body.position, Vector3.up).normalized;
        return Vector3.Dot(forward, direction) >= Mathf.Cos(firingHalfAngle * Mathf.Deg2Rad);
    }

    public bool IsValidTarget(AlienBikeController candidate)
    {
        if (candidate == null || candidate == Bike || !candidate.isActiveAndEnabled || !candidate.IsDriven) return false;
        var enemy = candidate.GetComponent<EnemyActor>();
        if (enemy != null)
        {
            var aim = candidate.GetComponent<AimTarget>();
            if (!enemy.IsAlive || aim == null || !aim.IsTargetable) return false;
        }
        else if (candidate.Rider == null || !candidate.Rider.IsDriving || candidate.Rider.GetComponent<PlayerHealth>().IsDead) return false;
        Vector3 point = AimPoint(candidate);
        if (!InsideFiringCone(point)) return false;
        if ((point - muzzle.position).sqrMagnitude > range * range) return false;
        Vector3 viewport = combatCamera.ForwardBikeViewportPoint(point);
        if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return false;
        // Real scenery and intervening vehicles remain blockers, including faded scenery.
        return WeaponShotQuery.Clear(transform, candidate.transform, muzzle.position, point, hits);
    }
    public AlienBikeController SelectTarget()
    {
        if (IsValidTarget(Target)) return Target;
        AlienBikeController best = null;
        float bestCenter = float.PositiveInfinity, bestDistance = float.PositiveInfinity;
        foreach (var candidate in targets)
        {
            if (!IsValidTarget(candidate)) continue;
            Vector3 viewport = combatCamera.ForwardBikeViewportPoint(AimPoint(candidate));
            float center = new Vector2(viewport.x - .5f, viewport.y - .5f).sqrMagnitude;
            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (center < bestCenter - .0001f || Mathf.Abs(center - bestCenter) <= .0001f && distance < bestDistance)
            { best = candidate; bestCenter = center; bestDistance = distance; }
        }
        return best;
    }
    public static AlienBikeLaserWeapon IncomingFor(AlienBikeController target)
    {
        AlienBikeLaserWeapon best = null;
        foreach (var weapon in active)
            if (weapon.player == null && weapon.Target == target && weapon.Progress > 0
                && (best == null || weapon.Progress > best.Progress)) best = weapon;
        return best;
    }
    private bool Audible => player != null ? IncomingFor(Bike) == null : Target != null && IncomingFor(Target) == this;
    private void Update()
    {
        if (!Operational) { CancelLock(); wasOperational = false; return; }
        if (!wasOperational) { readyAt = Mathf.Max(readyAt, Time.time + initialDelay); wasOperational = true; }
        if (Time.time < readyAt) return;
        if (IsWindingUp)
        {
            // Chase enemies get one short settling interval; a late dodge still escapes
            // the committed solution. Player aim and all bolts remain unchanged.
            if (!IsValidTarget(Target)) { CancelLock(); return; }
            float elapsed = Time.time - LockedAt;
            if (driver != null && elapsed <= Mathf.Min(enemyPredictionRefreshSeconds, dischargeSeconds / 3))
                CommitPrediction(Mathf.Max(0, dischargeSeconds - elapsed));
            if (!InsideFiringCone(dischargePoint)) { CancelLock(); return; }
            if (elapsed >= dischargeSeconds) Fire();
            return;
        }
        var next = SelectTarget();
        if (next != Target) { CancelLock(); Target = next; }
        if (Target == null) return;
        Progress = Mathf.Min(1, Progress + Time.deltaTime / Mathf.Max(.1f, lockSeconds));
        beepElapsed += Time.deltaTime;
        if (Progress >= 1)
        {
            LockedAt = Time.time; IsWindingUp = true; LocksCompleted++;
            CommitPrediction(dischargeSeconds);
            if (Audible) AudioService.Play2D(lockComplete);
            return;
        }
        if (beepElapsed >= BeepInterval)
        {
            beepElapsed = 0;
            if (Audible) AudioService.Play2D(lockBeep);
        }
    }
    private void CommitPrediction(float untilFire)
    {
        Vector3 velocity = Target.Body.linearVelocity;
        dischargePoint = Intercept(muzzle.position + Bike.Body.linearVelocity * untilFire,
            AimPoint(Target) + velocity * untilFire, velocity, boltSpeed);
    }
    private void Fire()
    {
        // The final firing solution is committed before discharge. The bolt never reads its target again.
        Vector3 direction = (dischargePoint - muzzle.position).normalized;
        var bolt = VfxPool.Spawn(boltPrefab, muzzle.position, Quaternion.LookRotation(direction));
        if (bolt == null) { CancelLock(); readyAt = Time.time + cooldownSeconds; return; }
        AudioService.Play(fireSound, muzzle.position);
        bolt.Launch(transform, player != null ? player.transform : null,
            player != null ? player.gameObject : gameObject, direction, boltSpeed, damage);
        LastTarget = Target; LastFireTime = Time.time; ShotsFired++;
        readyAt = Time.time + cooldownSeconds;
        CancelLock();
    }
    public static Vector3 Intercept(Vector3 start, Vector3 target, Vector3 velocity, float speed)
    {
        Vector3 delta = target - start;
        float a = velocity.sqrMagnitude - speed * speed, b = 2 * Vector3.Dot(delta, velocity), c = delta.sqrMagnitude;
        float t = delta.magnitude / Mathf.Max(1, speed);
        float discriminant = b * b - 4 * a * c;
        if (Mathf.Abs(a) < .001f) { if (b < -.001f) t = -c / b; }
        else if (discriminant >= 0)
        {
            float root = Mathf.Sqrt(discriminant), first = (-b - root) / (2 * a), second = (-b + root) / (2 * a);
            if (first > 0 && second > 0) t = Mathf.Min(first, second);
            else if (first > 0 || second > 0) t = Mathf.Max(first, second);
        }
        return target + velocity * Mathf.Clamp(t, 0, 3);
    }
}
