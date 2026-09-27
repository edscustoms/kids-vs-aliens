using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;

public enum HealingPodPhase { Idle, Aligning, Closing, Rising, Healing, Lowering, Opening, Exiting }

/// <summary>Owns the pod sequence; health, input and saves retain their existing owners.</summary>
[DisallowMultipleComponent]
public sealed class HealingPodController : MonoBehaviour, IRunStateParticipant
{
    [Header("Replaceable visual")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;
    [SerializeField] private Transform roof;
    [SerializeField] private Renderer[] poweredRenderers = Array.Empty<Renderer>();
    [SerializeField] private Vector3 leftDoorOpenOffset;
    [SerializeField] private Vector3 rightDoorOpenOffset;
    [SerializeField] private Vector3 roofHingeAxis = Vector3.right;
    [SerializeField] private float roofOpenAngle = -75f;
    [SerializeField, Min(.01f)] private float openCloseDuration = .55f;

    [Header("Gameplay volumes and player-root markers")]
    [SerializeField] private SphereCollider proximityTrigger;
    [SerializeField, Min(.1f)] private float proximityRadius = 3f;
    [SerializeField] private BoxCollider chamberTrigger;
    [SerializeField] private Transform playerAlignPoint;
    [SerializeField] private Transform playerFloatPoint;
    [SerializeField] private Transform playerExitPoint;
    [SerializeField] private LayerMask obstructionMask = ~0;

    [Header("Sequence seconds (preserve authored prefab/instance tuning)")]
    [SerializeField, Min(.01f)] private float alignDuration = .45f;
    [SerializeField, Min(.01f)] private float riseDuration = .65f;
    [SerializeField, Min(.01f)] private float healDuration = .85f;
    [SerializeField, Min(.01f)] private float lowerDuration = .55f;
    [SerializeField, Min(.01f)] private float exitDuration = .8f;

    [Header("Healing supply (1 = one full health bar)")]
    [SerializeField, Min(0f)] private float healingCapacity = 1f;
    [SerializeField, Range(.05f, 1f)] private float suspendedAnimationSpeed = .45f;

    [Header("Prefab-owned presentation")]
    [SerializeField] private ParticleSystem healingVfx;
    [SerializeField] private GameObject healingEnergy;
    [SerializeField] private AudioEmitter healingEmitter;
    [SerializeField] private SoundEvent openSound;
    [SerializeField] private SoundEvent healingSound;
    [SerializeField] private SoundEvent deniedSound;

    private readonly HashSet<Collider> occupants = new();
    private Predicate<Collider> unavailableOccupant;
    private readonly Collider[] overlaps = new Collider[32];
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private Vector3 leftClosedPosition, rightClosedPosition, motionStart;
    private Quaternion roofClosedRotation, motionRotation;
    private bool initialized, deniedThisVisit;
    private float openness, elapsed, remainingCapacity, exitSpeed, exitVerticalVelocity;
    private PlayerCharacter player;
    private PlayerHealth health;
    private CharacterController capsule;
    private ThirdPersonController locomotion;
    private PlayerAnimation presentation;
    private GameplaySuspensionController.Lease lease;
    private ActiveRunController run;

    public float Openness => openness;
    public bool IsPlayerNearby => occupants.Count > 0;
    public bool IsDepleted => RemainingCapacity <= 0f;
    public float RemainingCapacity => initialized ? remainingCapacity : healingCapacity;
    public bool IsBusy => Phase != HealingPodPhase.Idle;
    public HealingPodPhase Phase { get; private set; }
    public string RunStateKey => "healing-pod";

    [Serializable] private sealed class SavedState
    {
        public float remainingCapacity = -1f;
        public bool depleted; // Old V1 snapshots contain only this flag.
    }

    private void Awake() => Initialize();
    private void Initialize()
    {
        if (initialized) return;
        if (leftDoor == null || rightDoor == null || roof == null)
        {
            Debug.LogError($"{name}: Healing Pod requires both doors and a roof transform.", this);
            enabled = false;
            return;
        }
        leftClosedPosition = leftDoor.localPosition;
        rightClosedPosition = rightDoor.localPosition;
        roofClosedRotation = roof.localRotation;
        unavailableOccupant = IsOutsideProximity;
        remainingCapacity = healingCapacity;
        initialized = true;
        ApplyDoorPose();
        ApplyPowerState();
        StopHealingPresentation();
    }

    private void OnValidate()
    {
        proximityRadius = Mathf.Max(.1f, proximityRadius);
        openCloseDuration = Mathf.Max(.01f, openCloseDuration);
        if (proximityTrigger != null)
        {
            proximityTrigger.isTrigger = true;
            proximityTrigger.radius = proximityRadius;
        }
    }

    public void EnterProximity(Collider other)
    {
        if (!isActiveAndEnabled || other == null || other.isTrigger || occupants.Contains(other)) return;
        if (other.GetComponentInParent<PlayerCharacter>() == null) return;
        bool first = occupants.Count == 0;
        occupants.Add(other);
        if (IsBusy || !RunIsReady()) return;
        if (first) AudioService.Play(openSound, transform.position);
    }

    public void ExitProximity(Collider other) => occupants.Remove(other);

    public void EnterChamber(Collider other)
    {
        if (!isActiveAndEnabled || other == null || other.isTrigger || IsBusy || !RunIsReady()) return;
        var candidate = other.GetComponentInParent<PlayerCharacter>();
        if (candidate == null) return;
        TryBeginHealing(candidate);
    }

    private bool RunIsReady() => ActiveRunController.Instance == null || ActiveRunController.Instance.IsReady;

    private void DenyOnce(PlayerCharacter candidate, bool fullHealth = false)
    {
        if (deniedThisVisit) return;
        deniedThisVisit = true;
        if (fullHealth) candidate.GetComponent<PlayerFeedback>()?.Report(
            new GameplayFeedbackEvent(FeedbackCode.HealthAlreadyFull));
        AudioService.Play(deniedSound, transform.position);
    }

    public bool TryBeginHealing(PlayerCharacter candidate)
    {
        if (!isActiveAndEnabled || !initialized || IsBusy || openness < .99f || !RunIsReady()
            || candidate == null || chamberTrigger == null || !chamberTrigger.enabled
            || playerAlignPoint == null || playerFloatPoint == null || playerExitPoint == null) return false;
        // A hand/weapon/toe crossing is insufficient: the player's root must enter the chamber.
        Vector3 local = chamberTrigger.transform.InverseTransformPoint(candidate.transform.position) - chamberTrigger.center;
        Vector3 half = chamberTrigger.size * .5f;
        if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.z) > half.z || Mathf.Abs(local.y) > half.y) return false;
        var suspension = candidate.GetComponent<GameplaySuspensionController>();
        var candidateHealth = candidate.GetComponent<PlayerHealth>();
        var candidateCapsule = candidate.GetComponent<CharacterController>();
        if (suspension == null || !suspension.isActiveAndEnabled || suspension.IsSuspended
            || candidateHealth == null || candidateHealth.IsDead || candidateCapsule == null || !candidateCapsule.enabled) return false;
        if (IsDepleted) { DenyOnce(candidate); return false; }
        if (candidateHealth.HealthNormalized >= 1f) { DenyOnce(candidate, true); return false; }

        player = candidate;
        capsule = candidateCapsule;
        Physics.SyncTransforms();
        if (!IsPoseClear(playerAlignPoint.position) || !IsPoseClear(playerFloatPoint.position)
            || !IsPoseClear(playerExitPoint.position)) { player = null; capsule = null; return false; }
        run = candidate.GetComponent<ActiveRunController>();
        if (run != null && !run.TryReservePlayerSavePoint(this, playerExitPoint))
        { player = null; capsule = null; run = null; return false; }
        presentation = candidate.GetComponent<PlayerAnimation>();
        if (presentation == null || !presentation.TryBeginAuthoredMotion(this))
        {
            run?.ClearPlayerSavePoint(this);
            player = null; capsule = null; run = null; presentation = null;
            return false;
        }
        health = candidateHealth;
        locomotion = candidate.GetComponent<ThirdPersonController>();
        lease = suspension.Acquire(SuspensionReason.HealingPod);
        // Clear the capsule's last walking velocity before ordinary animation observes the suspended player.
        capsule.Move(Vector3.zero);
        capsule.enabled = false;
        locomotion?.ResetMotion();
        health.OnDied += AbortSequence;
        SetPhase(HealingPodPhase.Aligning);
        run?.MarkDirty();
        return true;
    }

    private static bool IsUnavailable(Collider collider) =>
        collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy;

    private bool IsOutsideProximity(Collider collider)
    {
        if (IsUnavailable(collider) || proximityTrigger == null || !proximityTrigger.enabled) return true;
        // Disable/teleport/reenable in one frame can omit Exit. Prune only known occupants;
        // this conservative sphere/AABB check does not scan physics or rebuild collision.
        Vector3 center = proximityTrigger.transform.TransformPoint(proximityTrigger.center);
        Vector3 scale = proximityTrigger.transform.lossyScale;
        float radius = proximityTrigger.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return (collider.bounds.ClosestPoint(center) - center).sqrMagnitude > radius * radius;
    }

    private void Update()
    {
        if (!initialized) return;
        if (occupants.Count > 0) occupants.RemoveWhere(unavailableOccupant);
        if (IsBusy)
        {
            if (player == null || !player.gameObject.activeInHierarchy || capsule == null || health == null || health.IsDead
                || lease == null || !lease.IsActive || presentation == null || !presentation.OwnsAuthoredMotion(this))
            { AbortSequence(); return; }
            AdvanceSequence(Time.deltaTime);
            return;
        }
        if (!IsPlayerNearby) deniedThisVisit = false;
        if (!RunIsReady()) return;
        MoveDoors(IsPlayerNearby, Time.deltaTime);
    }

    private void MoveDoors(bool open, float deltaTime)
    {
        float next = Mathf.MoveTowards(openness, open ? 1 : 0, deltaTime / openCloseDuration);
        if (next == openness) return;
        openness = next;
        ApplyDoorPose();
    }

    private void ApplyDoorPose()
    {
        float eased = Mathf.SmoothStep(0, 1, openness);
        leftDoor.localPosition = leftClosedPosition + leftDoorOpenOffset * eased;
        rightDoor.localPosition = rightClosedPosition + rightDoorOpenOffset * eased;
        roof.localRotation = roofClosedRotation * Quaternion.AngleAxis(roofOpenAngle * eased, roofHingeAxis);
    }

    private void SetPhase(HealingPodPhase phase)
    {
        Phase = phase;
        elapsed = 0;
        if (player != null) { motionStart = player.transform.position; motionRotation = player.transform.rotation; }
        if (phase == HealingPodPhase.Rising)
        {
            presentation.SetAuthoredSuspension(this, true, suspendedAnimationSpeed);
            if (healingEnergy != null) healingEnergy.SetActive(true);
            if (healingVfx != null) healingVfx.Play(true);
            if (healingEmitter != null) healingEmitter.Play(healingSound);
        }
        if (phase == HealingPodPhase.Opening)
        {
            presentation.SetAuthoredSuspension(this, false);
            StopHealingPresentation();
        }
        if (phase == HealingPodPhase.Exiting)
        {
            capsule.enabled = true;
            Vector3 distance = playerExitPoint.position - player.transform.position;
            distance.y = 0;
            exitSpeed = distance.magnitude / Mathf.Max(.01f, exitDuration);
            exitVerticalVelocity = 0;
        }
    }

    private void AdvanceSequence(float deltaTime)
    {
        if (deltaTime <= 0) return;
        elapsed += deltaTime;
        switch (Phase)
        {
            case HealingPodPhase.Aligning:
                if (MovePlayer(playerAlignPoint, alignDuration)) SetPhase(HealingPodPhase.Closing);
                break;
            case HealingPodPhase.Closing:
                MoveDoors(false, deltaTime);
                if (openness == 0) SetPhase(HealingPodPhase.Rising);
                break;
            case HealingPodPhase.Rising:
                if (MovePlayer(playerFloatPoint, riseDuration)) SetPhase(HealingPodPhase.Healing);
                break;
            case HealingPodPhase.Healing:
                if (elapsed < healDuration) break;
                // Account for the exact clamped grant before health notifications can save.
                // No yield/callback separates this calculation from the authoritative health mutation.
                float grant = Mathf.Min(health.MaxHealth - health.CurrentHealth, remainingCapacity * health.MaxHealth);
                float restored = Mathf.Min(health.MaxHealth, health.CurrentHealth + grant) - health.CurrentHealth;
                remainingCapacity = Mathf.Max(0, remainingCapacity - restored / health.MaxHealth);
                // A float round trip can leave a few billionths of a bar after its final grant.
                if (remainingCapacity < .000001f) remainingCapacity = 0;
                health.Heal(grant);
                ApplyPowerState();
                run?.Save();
                SetPhase(HealingPodPhase.Lowering);
                break;
            case HealingPodPhase.Lowering:
                if (MovePlayer(playerAlignPoint, lowerDuration)) SetPhase(HealingPodPhase.Opening);
                break;
            case HealingPodPhase.Opening:
                MoveDoors(true, deltaTime);
                if (openness == 1) SetPhase(HealingPodPhase.Exiting);
                break;
            case HealingPodPhase.Exiting:
                WalkOutside(deltaTime);
                break;
        }
    }

    private void WalkOutside(float deltaTime)
    {
        // The same capsule and PlayerAnimation velocity-driven walk used by locomotion;
        // input stays leased. Gravity lets Amy step down from the platform naturally.
        Vector3 delta = playerExitPoint.position - player.transform.position;
        delta.y = 0;
        exitVerticalVelocity = capsule.isGrounded ? -2f
            : exitVerticalVelocity + (locomotion != null ? locomotion.Gravity : -15f) * deltaTime;
        capsule.Move(Vector3.ClampMagnitude(delta, exitSpeed * deltaTime) + Vector3.up * (exitVerticalVelocity * deltaTime));
        player.transform.rotation = Quaternion.Slerp(motionRotation, playerExitPoint.rotation,
            Mathf.Clamp01(elapsed / Mathf.Max(.01f, exitDuration)));
        Vector3 remaining = playerExitPoint.position - player.transform.position;
        remaining.y = 0;
        if (remaining.sqrMagnitude < .0004f && capsule.isGrounded) FinishSequence();
        else if (elapsed > exitDuration + 1f) AbortSequence();
    }

    private bool MovePlayer(Transform destination, float duration)
    {
        float t = Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
        float eased = Mathf.SmoothStep(0, 1, t);
        Vector3 position = Vector3.Lerp(motionStart, destination.position, eased);
        if (!IsMotionClear(player.transform.position, position)) { AbortSequence(); return false; }
        player.transform.SetPositionAndRotation(position, Quaternion.Slerp(motionRotation, destination.rotation, eased));
        return t == 1;
    }

    private void CapsuleAt(Vector3 position, out Vector3 bottom, out Vector3 top, out float radius)
    {
        Vector3 scale = player.transform.lossyScale;
        float fullRadius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        radius = Mathf.Max(.01f, fullRadius - Mathf.Min(capsule.skinWidth, fullRadius * .25f));
        float half = Mathf.Max(fullRadius, capsule.height * Mathf.Abs(scale.y) * .5f);
        Vector3 center = position + player.transform.TransformVector(capsule.center);
        bottom = center - Vector3.up * (half - fullRadius);
        top = center + Vector3.up * (half - fullRadius);
    }

    private bool IsPoseClear(Vector3 position)
    {
        CapsuleAt(position, out var bottom, out var top, out float radius);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps, obstructionMask, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return false;
        for (int i = 0; i < count; i++)
            if (!overlaps[i].transform.IsChildOf(player.transform)) return false;
        return true;
    }

    private bool IsMotionClear(Vector3 from, Vector3 to)
    {
        if (!IsPoseClear(to)) return false;
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < .000001f) return true;
        CapsuleAt(from, out var bottom, out var top, out float radius);
        int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, delta.normalized, hits, delta.magnitude,
            obstructionMask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false;
        for (int i = 0; i < count; i++)
            if (!hits[i].collider.transform.IsChildOf(player.transform)) return false;
        return true;
    }

    private void FinishSequence()
    {
        // Exit is outside the chamber. A fresh chamber attempt may report why it is denied,
        // even if the player has not walked beyond the larger proximity sphere yet.
        deniedThisVisit = false;
        ReleasePlayer();
        Phase = HealingPodPhase.Idle;
        StopHealingPresentation();
        ActiveRunController.Instance?.MarkDirty();
    }

    public void AbortSequence()
    {
        if (!IsBusy) return;
        // Interruption opens the physical exit before returning control; no trapped/suspended player.
        openness = 1;
        ApplyDoorPose();
        Physics.SyncTransforms();
        if (player != null && capsule != null && playerExitPoint != null && IsPoseClear(playerExitPoint.position))
            player.transform.SetPositionAndRotation(playerExitPoint.position, playerExitPoint.rotation);
        FinishSequence();
    }

    private void ReleasePlayer()
    {
        presentation?.EndAuthoredMotion(this);
        if (health != null) health.OnDied -= AbortSequence;
        run?.ClearPlayerSavePoint(this);
        if (capsule != null)
        {
            capsule.enabled = true;
            if (!IsOutsideProximity(capsule)) occupants.Add(capsule);
        }
        locomotion?.ResetMotion();
        var previousLease = lease;
        lease = null;
        player = null; capsule = null; health = null; locomotion = null; run = null; presentation = null;
        previousLease?.Dispose();
    }

    private void ApplyPowerState()
    {
        foreach (var renderer in poweredRenderers) if (renderer != null) renderer.enabled = !IsDepleted;
    }

    private void StopHealingPresentation()
    {
        if (healingVfx != null) healingVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (healingEnergy != null) healingEnergy.SetActive(false);
        if (healingEmitter != null) healingEmitter.Stop();
    }

    public string CaptureRunState() => JsonUtility.ToJson(new SavedState { remainingCapacity = RemainingCapacity });

    public void RestoreRunState(string json)
    {
        Initialize();
        if (IsBusy) ReleasePlayer();
        Phase = HealingPodPhase.Idle;
        elapsed = 0;
        occupants.Clear();
        deniedThisVisit = false;
        // Absolute assignment; neither pass resolves peers, moves the player, grants health, or plays sound.
        var saved = JsonUtility.FromJson<SavedState>(json);
        remainingCapacity = saved == null ? healingCapacity : saved.remainingCapacity < 0
            ? (saved.depleted ? 0 : healingCapacity) : Mathf.Clamp(saved.remainingCapacity, 0, healingCapacity);
        if (remainingCapacity < .000001f) remainingCapacity = 0;
        openness = 0;
        if (initialized) ApplyDoorPose();
        ApplyPowerState();
        StopHealingPresentation();
    }

    private void OnDisable()
    {
        AbortSequence();
        occupants.Clear();
        StopHealingPresentation();
        if (initialized) { openness = 1; ApplyDoorPose(); }
    }
}
