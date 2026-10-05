using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns hover physics, jump/turbo resources and the last safe grounded save pose.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(RunWorldObject))]
public sealed class AlienBikeController : MonoBehaviour, IRunStateParticipant
{
    public static readonly HashSet<AlienBikeController> Available = new();
    public const int MaxMountApproaches = 8;
    [Serializable]
    public struct MountApproach
    {
        public Transform approachPoint;
        public Transform mountPoint;
        public MountApproach(Transform approach, Transform mount)
        {
            approachPoint = approach;
            mountPoint = mount;
        }
    }
    [Header("Player-root authoring markers")]
    [Tooltip("Up to eight approaches, ordered around the hull. Adjacent points form safe walking alternatives; each leads to its paired mount point.")]
    public MountApproach[] mountApproaches = Array.Empty<MountApproach>();
    public Transform seatPoint, dismountLeft, dismountRight;
    [Tooltip("Horizontal interaction radius measured from the bike root.")]
    [Min(.1f)] public float approachRange = 3;
    [Min(0)] public float safeDismountSpeed = 1.2f;
    [Header("Arcade handling (metres / seconds)")]
    [Min(.1f)] public float acceleration = 12, maxSpeed = 12, reverseSpeed = 4;
    [Min(0)] public float steeringStrength = 100, lateralGrip = 8;
    [Range(0, 1)] public float steeringAtMaxSpeed = .45f;
    [Min(.1f)] public float hoverHeight = .85f, hoverSpring = 70, hoverDamping = 14;
    [SerializeField] private LayerMask environmentMask = ~0;
    [Header("Slope support")]
    [SerializeField, Min(.1f)] private float supportNormalResponse = 8;
    [SerializeField, Range(20, 50)] private float maximumSupportSlope = 40;
    [Header("Charged jump (vertical speed)")]
    [Min(.01f)] public float maxChargeTime = 1.4f;
    [Min(0)] public float minJump = 4, maxJump = 9;
    [Header("Turbo (seconds of charge)")]
    [Min(.01f)] public float maxTurboCharge = 5;
    [Min(0)] public float rechargeRate = .65f, drainRate = 1, turboAcceleration = 21, turboMaxSpeed = 20;
    [Header("Presentation")]
    [SerializeField] private AlienBikeVisual visual;
    [SerializeField] private AlienBikeAudio audioPresentation;
    public PlayerBikeRider Rider
    {
        get; private set;
    }
    public Rigidbody Body
    {
        get; private set;
    }
    public bool IsGrounded
    {
        get; private set;
    }
    public bool IsCharging
    {
        get; private set;
    }
    public bool IsTurbo
    {
        get; private set;
    }
    public float JumpCharge01 => Mathf.Clamp01(chargeTime / maxChargeTime);
    public float Turbo01 => Mathf.Clamp01(turboCharge / maxTurboCharge);
    public float Speed => Body == null ? 0 : Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up).magnitude;
    public float ForwardSpeed => Body == null ? 0 : Vector3.Dot(Body.linearVelocity, transform.forward);
    public float SteerInput => Rider != null && Rider.IsDriving ? Rider.DrivingInput.x : IsDriven ? drivingInput.x : 0;
    public bool IsDriven => driver != null && Body != null && !Body.isKinematic;
    public Vector3 SupportNormal => supportNormal;
    public Vector3 SafeExit => safeExit;
    public Quaternion SafeRotation => safeRotation;
    public Quaternion SafePlayerRotation => Quaternion.Euler(0, safeRotation.eulerAngles.y, 0);
    public string RunStateKey => "alien-bike-v1";
    private readonly RaycastHit[] hits = new RaycastHit[24];
    private float chargeTime, turboCharge, jumpLockUntil, nextSafeCheck;
    private bool turboExhausted, initialized;
    private Vector3 safePosition, safeExit;
    private Quaternion safeRotation;
    private bool hasSafePose;
    private MonoBehaviour driver;
    private Vector2 drivingInput;
    private Vector3 supportNormal = Vector3.up;
    private readonly RaycastHit[] supportHits = new RaycastHit[5];
    private readonly bool[] supportValid = new bool[5];
    private readonly float[] supportHeights = new float[5];
    private readonly float[] sortedHeights = new float[5];

    [Serializable]
    private sealed class Saved
    {
        public Vector3 position, exit;
        public Quaternion rotation;
        public float turbo;
        public bool hasSafePose;
    }
    private void Awake() => Initialize();
    private void Initialize()
    {
        if (initialized)
            return;
        Body = GetComponent<Rigidbody>();
        turboCharge = maxTurboCharge;
        safePosition = transform.position;
        safeRotation = transform.rotation;
        safeExit = dismountLeft != null ? dismountLeft.position : transform.position;
        Body.isKinematic = true;
        initialized = true;
    }
    private void OnEnable() => Available.Add(this);
    private void OnDisable()
    {
        Available.Remove(this);
        Rider?.AbortRide();
        CancelCharge();
        audioPresentation?.Stop();
    }
    private bool RunReady => ActiveRunController.Instance == null || ActiveRunController.Instance.IsReady;
    public bool CanMount => isActiveAndEnabled && driver == null && RunReady && Speed <= safeDismountSpeed
        && mountApproaches != null && mountApproaches.Length > 0 && mountApproaches.Length <= MaxMountApproaches && seatPoint != null;
    public bool IsWithinInteractionRange(Vector3 playerPosition)
    {
        Vector3 offset = playerPosition - transform.position;
        return new Vector2(offset.x, offset.z).sqrMagnitude <= approachRange * approachRange;
    }
    public void Attach(PlayerBikeRider rider)
    {
        Initialize();
        Rider = rider;
        driver = rider;
        Body.isKinematic = true;
        CancelCharge();
    }
    public void StartDriving()
    {
        Body.isKinematic = false;
        Body.WakeUp();
        audioPresentation?.SetEngine(true);
    }
    // One physical owner; either the player or an AI supplies intentions through this seam.
    public bool ClaimDriver(MonoBehaviour owner)
    {
        Initialize();
        if (owner == null || (driver != null && driver != owner)) return false;
        driver = owner;
        return true;
    }
    public void SetDriveInput(MonoBehaviour owner, Vector2 intent)
    {
        if (driver == owner) drivingInput = new Vector2(Mathf.Clamp(intent.x, -1, 1), Mathf.Clamp(intent.y, -1, 1));
    }
    public void Detach()
    {
        CancelCharge();
        IsTurbo = false;
        Rider = null;
        driver = null;
        drivingInput = Vector2.zero;
        Secure();
        audioPresentation?.Stop();
        visual?.SetPower(0);
        ActiveRunController.Instance?.MarkDirty();
    }
    public void Secure()
    {
        if (Body == null)
            return;
        if (!Body.isKinematic)
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }
        Body.isKinematic = true;
    }
    private bool Probe(Vector3 origin, float distance, out RaycastHit support, float minimumUp = .82f)
    {
        support = default;
        float nearest = float.PositiveInfinity;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, distance, environmentMask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length)
            return false;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider.transform.IsChildOf(transform) || (Rider != null && hit.collider.transform.IsChildOf(Rider.transform))
                || hit.distance >= nearest || Vector3.Dot(hit.normal, Vector3.up) < minimumUp
                || hit.collider.GetComponentInParent<AlienBikeController>() != null)
                continue;
            nearest = hit.distance;
            support = hit;
        }
        return !float.IsPositiveInfinity(nearest);
    }
    public bool TryGroundPoint(Vector3 point, out Vector3 grounded)
    {
        if (Probe(point + Vector3.up * .65f, 1.75f, out var hit))
        {
            grounded = hit.point + Vector3.up * .035f;
            return true;
        }
        grounded = default;
        return false;
    }
    private bool SampleSupport(out float surfaceHeight, out Vector3 normal)
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        int count = 0;
        float minimumUp = Mathf.Cos(maximumSupportSlope * Mathf.Deg2Rad);
        for (int i = 0; i < 5; i++)
        {
            Vector3 offset = i == 1 ? forward * 1.05f : i == 2 ? -forward * 1.05f
                : i == 3 ? right * .55f : i == 4 ? -right * .55f : Vector3.zero;
            supportValid[i] = Probe(transform.position + offset + Vector3.up * 1.2f,
                hoverHeight + 2.2f, out supportHits[i], minimumUp);
            if (!supportValid[i]) continue;
            var hit = supportHits[i];
            // Project each contact plane to the chassis center, so a real incline is
            // consistent across the footprint while a tiny seam/spike is an outlier.
            supportHeights[i] = hit.point.y - Vector3.Dot(new Vector3(hit.normal.x, 0, hit.normal.z),
                transform.position - hit.point) / hit.normal.y;
            // A wall/railing top above the chassis is not a floor. Sampling farther
            // ahead for slopes must not turn hover lift into a stair-climbing force.
            if (supportHeights[i] >= transform.position.y - .05f)
            { supportValid[i] = false; continue; }
            sortedHeights[count++] = supportHeights[i];
        }
        surfaceHeight = 0; normal = Vector3.up;
        if (count < 3) return false;
        Array.Sort(sortedHeights, 0, count);
        float median = sortedHeights[count / 2];
        int accepted = 0; Vector3 normals = Vector3.zero;
        for (int i = 0; i < 5; i++)
        {
            supportValid[i] &= Mathf.Abs(supportHeights[i] - median) <= .24f;
            if (!supportValid[i]) continue;
            surfaceHeight += supportHeights[i]; normals += supportHits[i].normal; accepted++;
        }
        if (accepted < 3) return false;
        surfaceHeight /= accepted;
        normal = normals.normalized;
        if (supportValid[1] && supportValid[2] && supportValid[3] && supportValid[4])
        {
            Vector3 plane = Vector3.Cross(supportHits[1].point - supportHits[2].point,
                supportHits[3].point - supportHits[4].point).normalized;
            if (plane.y >= minimumUp) normal = plane;
        }
        return true;
    }
    private float SlopeVerticalSpeed(Vector3 normal) =>
        -Vector3.Dot(new Vector3(normal.x, 0, normal.z), Body.linearVelocity) / Mathf.Max(.5f, normal.y);
    private void SetGrounded(bool supported, float height, Vector3 normal)
    {
        IsGrounded = supported && Time.time >= jumpLockUntil
            && Mathf.Abs(transform.position.y - height - hoverHeight) < .32f
            && Mathf.Abs(Body.linearVelocity.y - SlopeVerticalSpeed(normal)) < 2.5f;
    }
    public bool CheckGrounded()
    {
        Initialize();
        bool supported = SampleSupport(out float height, out var normal);
        SetGrounded(supported, height, normal);
        return IsGrounded;
    }
    private void FixedUpdate()
    {
        if (!RunReady || !IsDriven) return;
        bool supported = SampleSupport(out float height, out var normal);
        SetGrounded(supported, height, normal);
        supportNormal = Vector3.Slerp(supportNormal, supported ? normal : Vector3.up,
            1 - Mathf.Exp(-supportNormalResponse * Time.fixedDeltaTime));
        if (supported && Time.time >= jumpLockUntil)
        {
            float relativeVertical = Body.linearVelocity.y - SlopeVerticalSpeed(normal);
            float lift = Mathf.Clamp((hoverHeight - (transform.position.y - height)) * hoverSpring
                - relativeVertical * hoverDamping, -25, 45);
            Body.AddForce(Vector3.up * (lift - Physics.gravity.y), ForceMode.Acceleration);
        }
        Vector2 move = drivingInput;
        float throttle = move.y;
        float speedLimit = throttle < 0 ? reverseSpeed : IsTurbo ? turboMaxSpeed : maxSpeed;
        float forwardSpeed = Vector3.Dot(Body.linearVelocity, transform.forward);
        if (Mathf.Abs(forwardSpeed) < speedLimit || Mathf.Sign(throttle) != Mathf.Sign(forwardSpeed))
            Body.AddForce(transform.forward * (throttle * (IsTurbo ? turboAcceleration : acceleration)), ForceMode.Acceleration);
        Body.AddForce(-transform.right * Vector3.Dot(Body.linearVelocity, transform.right) * lateralGrip, ForceMode.Acceleration);
        if (Mathf.Abs(throttle) < .05f)
            Body.AddForce(-Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up) * 2.2f, ForceMode.Acceleration);
        float steering = Mathf.Lerp(1, steeringAtMaxSpeed, Mathf.Clamp01(Speed / maxSpeed));
        float reverse = forwardSpeed < -.5f ? -1 : 1;
        Vector3 heading = Quaternion.AngleAxis(move.x * steeringStrength * steering * reverse * Time.fixedDeltaTime, Vector3.up)
            * Vector3.ProjectOnPlane(Body.rotation * Vector3.forward, Vector3.up).normalized;
        // Preserve the physics yaw, including with Rigidbody render interpolation enabled.
        // Solve the tangent's vertical component instead of changing its horizontal heading.
        heading.y = -(supportNormal.x * heading.x + supportNormal.z * heading.z) / Mathf.Max(.5f, supportNormal.y);
        Body.MoveRotation(Quaternion.LookRotation(heading, supportNormal));
        if (Time.time >= nextSafeCheck && IsGrounded)
        {
            nextSafeCheck = Time.time + .4f;
            if (Rider == null) RememberSafePose(transform.position);
            else if (Rider.TryFindDismount(out var exit)) RememberSafePose(exit);
        }
    }
    public void RememberSafePose(Vector3 exit)
    {
        safePosition = transform.position;
        safeRotation = transform.rotation;
        safeExit = exit;
        hasSafePose = true;
    }
    public void RestoreSafePose()
    {
        Secure();
        transform.SetPositionAndRotation(safePosition, safeRotation);
        Body.position = safePosition;
        Body.rotation = safeRotation;
        Physics.SyncTransforms();
    }
    public void TickControls(float dt, bool jumpHeld, bool turboHeld)
    {
        if (dt <= 0)
            return;
        if (jumpHeld && !IsCharging && IsGrounded)
        {
            IsCharging = true;
            chargeTime = 0;
        }
        if (IsCharging)
        {
            if (!IsGrounded)
                CancelCharge();
            else if (jumpHeld)
                chargeTime = Mathf.Min(maxChargeTime, chargeTime + dt);
            else
            {
                Vector3 v = Body.linearVelocity;
                // The authored launch speed is relative to the supported road. On
                // an incline, replacing climbing velocity with that speed can drive
                // the bike back into the road instead of producing a jump.
                float roadRise = SampleSupport(out _, out var launchNormal) ? SlopeVerticalSpeed(launchNormal) : 0;
                v.y = roadRise + Mathf.Lerp(minJump, maxJump, JumpCharge01);
                Body.linearVelocity = v;
                jumpLockUntil = Time.time + .3f;
                IsGrounded = false;
                audioPresentation?.Jump();
                CancelCharge();
            }
        }
        if (!turboHeld)
            turboExhausted = false;
        IsTurbo = turboHeld && !turboExhausted && turboCharge > 0;
        if (IsTurbo)
        {
            turboCharge = Mathf.Max(0, turboCharge - drainRate * dt);
            if (turboCharge <= 0)
            {
                IsTurbo = false;
                turboExhausted = true;
            }
        }
        else if (IsGrounded)
            turboCharge = Mathf.Min(maxTurboCharge, turboCharge + rechargeRate * dt);
        audioPresentation?.SetCharge(IsCharging, JumpCharge01);
        audioPresentation?.SetTurbo(IsTurbo);
        audioPresentation?.SetEngine(true, Mathf.Clamp01(Speed / turboMaxSpeed));
        visual?.SetPower(IsTurbo ? 1 : Mathf.Max(JumpCharge01 * .7f, Speed / turboMaxSpeed * .35f));
    }
    public void CancelCharge()
    {
        IsCharging = false;
        chargeTime = 0;
        IsTurbo = false;
        audioPresentation?.SetCharge(false, 0);
        audioPresentation?.SetTurbo(false);
    }
    public string CaptureRunState()
    {
        Initialize();
        // Active Run captures world participants before the player. Publish the matching
        // exit synchronously, so a FixedUpdate safe-pose change cannot split that pair.
        Rider?.RefreshSavePoint();
        return JsonUtility.ToJson(new Saved
        {
            position = hasSafePose ? safePosition : transform.position,
            rotation = hasSafePose ? safeRotation : transform.rotation,
            exit = safeExit,
            turbo = turboCharge,
            hasSafePose = hasSafePose
        });
    }
    public void RestoreRunState(string json)
    {
        Initialize();
        // Both Active Run passes assign only this bike. No player/peer lookup or mount replay.
        var saved = JsonUtility.FromJson<Saved>(json);
        CancelCharge();
        turboExhausted = false;
        jumpLockUntil = 0;
        nextSafeCheck = 0;
        IsGrounded = false;
        safePosition = saved.position;
        safeRotation = saved.rotation;
        safeExit = saved.exit;
        hasSafePose = saved.hasSafePose;
        turboCharge = Mathf.Clamp(saved.turbo, 0, maxTurboCharge);
        supportNormal = safeRotation * Vector3.up;
        drivingInput = Vector2.zero;
        RestoreSafePose();
    }
}
