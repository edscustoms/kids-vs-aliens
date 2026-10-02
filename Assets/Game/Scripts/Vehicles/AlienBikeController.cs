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
    public float SteerInput => Rider != null && Rider.IsDriving ? Mathf.Clamp(Rider.DrivingInput.x, -1, 1) : 0;
    public Vector3 SafeExit => safeExit;
    public Quaternion SafeRotation => safeRotation;
    public string RunStateKey => "alien-bike-v1";
    private readonly RaycastHit[] hits = new RaycastHit[24];
    private float chargeTime, turboCharge, jumpLockUntil, nextSafeCheck;
    private bool turboExhausted, initialized;
    private Vector3 safePosition, safeExit;
    private Quaternion safeRotation;
    private bool hasSafePose;

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
    public bool CanMount => isActiveAndEnabled && Rider == null && RunReady && Speed <= safeDismountSpeed
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
        Body.isKinematic = true;
        CancelCharge();
    }
    public void StartDriving()
    {
        Body.isKinematic = false;
        Body.WakeUp();
        audioPresentation?.SetEngine(true);
    }
    public void Detach()
    {
        CancelCharge();
        IsTurbo = false;
        Rider = null;
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
    private bool Probe(Vector3 origin, float distance, out RaycastHit support)
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
                || hit.distance >= nearest || Vector3.Dot(hit.normal, Vector3.up) < .82f)
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
    public bool CheckGrounded()
    {
        Initialize();
        bool front = Probe(transform.position + transform.forward * .9f + Vector3.up * .4f, hoverHeight + .7f, out var a);
        bool back = Probe(transform.position - transform.forward * .9f + Vector3.up * .4f, hoverHeight + .7f, out var b);
        IsGrounded = front && back && Mathf.Abs(Body.linearVelocity.y) < 2
            && Mathf.Abs(a.distance - .4f - hoverHeight) < .25f && Mathf.Abs(b.distance - .4f - hoverHeight) < .25f
            && Time.time >= jumpLockUntil;
        return IsGrounded;
    }
    private void FixedUpdate()
    {
        if (!RunReady || Rider == null || !Rider.IsDriving || Body.isKinematic)
            return;
        bool supported = Probe(transform.position + Vector3.up * .4f, hoverHeight + 1f, out var ground);
        CheckGrounded();
        if (supported && Time.time >= jumpLockUntil)
        {
            float height = ground.distance - .4f;
            float lift = Mathf.Clamp((hoverHeight - height) * hoverSpring - Body.linearVelocity.y * hoverDamping, -25, 45);
            Body.AddForce(Vector3.up * (lift - Physics.gravity.y), ForceMode.Acceleration);
        }
        Vector2 move = Rider.DrivingInput;
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
        Body.MoveRotation(Quaternion.Euler(0, Body.rotation.eulerAngles.y + move.x * steeringStrength * steering * reverse * Time.fixedDeltaTime, 0));
        if (Time.time >= nextSafeCheck && IsGrounded)
        {
            nextSafeCheck = Time.time + .4f;
            if (Rider.TryFindDismount(out var exit))
                RememberSafePose(exit);
        }
    }
    public void RememberSafePose(Vector3 exit)
    {
        safePosition = transform.position;
        safeRotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
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
                v.y = Mathf.Lerp(minJump, maxJump, JumpCharge01);
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
        RestoreSafePose();
    }
}
