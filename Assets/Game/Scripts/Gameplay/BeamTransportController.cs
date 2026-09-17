using System;
using StarterAssets;
using UnityEngine;

// Single owner of motion; scene triggers and abilities only request paths.
[DefaultExecutionOrder(-180), DisallowMultipleComponent,
 RequireComponent(typeof(CharacterController), typeof(GameplaySuspensionController))]
public sealed class BeamTransportController : MonoBehaviour
{
    [SerializeField] private BeamTransportVFX vfxPrefab;
    [SerializeField] private LayerMask obstructionMask = ~0;
    [SerializeField, Min(0.01f)] private float landingProbeDistance = 0.2f;
    private readonly Vector3[] points = new Vector3[4];
    private readonly float[] durations = new float[3];
    private readonly Collider[] overlaps = new Collider[32];
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private CharacterController capsule;
    private GameplaySuspensionController suspension;
    private GameplaySuspensionController.Lease lease;
    private BeamTransportVFX reusableVfx, activeVfx;
    private int segment, segmentCount;
    private float elapsed, delay, hold;
    private bool controllerWasEnabled, exitTransport, awaitingExit;
    private Action completed;
    public bool IsTransporting => lease != null;

    // Initialize after suspension (-200), before the arrival adapter (-150).
    private void Awake() => Resolve();

    private void Resolve()
    {
        if (capsule == null) capsule = GetComponent<CharacterController>();
        if (suspension == null) suspension = GetComponent<GameplaySuspensionController>();
    }

    public bool TryArrival(Transform destination, BeamTransportVFX effect, float height, float duration,
        float initialDelay = 0f, float landingHold = 0f)
    {
        if (destination == null || height <= 0f) return false;
        Resolve();
        Vector3 end = destination.position, start = end + Vector3.up * height;
        if (!CanBegin() || !IsLandingSafe(end) || !IsSegmentClear(start, end)) return false;
        points[0] = start; points[1] = end; durations[0] = duration;
        if (!Begin(effect, end, BeamTransportDirection.Down, 1, initialDelay, landingHold)) return false;
        // Synchronous startup handoff: no yield, Start coroutine or later pose reset.
        transform.SetPositionAndRotation(start, destination.rotation);
        return true;
    }

    public bool TryHoist(BeamHoistTarget target)
    {
        Resolve();
        if (!CanBegin() || target == null || !target.TryGetPath(transform.position, out var lift, out var across, out var end)
            || !IsLandingSafe(end) || !IsSegmentClear(transform.position, lift)
            || !IsSegmentClear(lift, across) || !IsSegmentClear(across, end)) return false;
        points[0] = transform.position; points[1] = lift; points[2] = across; points[3] = end;
        durations[0] = target.LiftDuration; durations[1] = target.TransferDuration; durations[2] = target.LandingDuration;
        return Begin(null, points[0], BeamTransportDirection.Up, 3, 0f, 0f);
    }

    public bool CanHoist(BeamHoistTarget target)
    {
        Resolve();
        return CanBegin() && target != null
            && target.TryGetPath(transform.position, out var lift, out var across, out var end)
            && IsLandingSafe(end) && IsSegmentClear(transform.position, lift)
            && IsSegmentClear(lift, across) && IsSegmentClear(across, end);
    }

    public bool TryDeparture(float height, float duration, Action onCompleted)
    {
        Resolve();
        Vector3 start = transform.position, end = start + Vector3.up * height;
        if (!CanBegin() || height <= 0f || !IsSegmentClear(start, end)) return false;
        points[0] = start; points[1] = end; durations[0] = duration;
        if (!Begin(null, start, BeamTransportDirection.Up, 1, 0f, 0f)) return false;
        exitTransport = true;
        completed = onCompleted;
        return true;
    }

    private bool CanBegin() => isActiveAndEnabled && capsule != null && capsule.enabled
        && suspension != null && suspension.isActiveAndEnabled && !suspension.IsSuspended && !IsTransporting;

    private bool Begin(BeamTransportVFX effect, Vector3 beamPosition, BeamTransportDirection direction,
        int count, float initialDelay, float landingHold)
    {
        if (effect == null)
        {
            if (reusableVfx == null && vfxPrefab != null)
            {
                reusableVfx = Instantiate(vfxPrefab);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(reusableVfx.gameObject, gameObject.scene);
            }
            effect = reusableVfx;
        }
        if (effect == null || effect.transform.IsChildOf(transform)) return false;
        lease = suspension.Acquire(SuspensionReason.BeamTransport);
        controllerWasEnabled = capsule.enabled;
        capsule.enabled = false;
        GetComponent<ThirdPersonController>()?.ResetMotion();
        activeVfx = effect;
        segment = 0; segmentCount = count; elapsed = 0f;
        delay = Mathf.Max(0f, initialDelay); hold = Mathf.Max(0f, landingHold);
        exitTransport = awaitingExit = false;
        activeVfx.Show(beamPosition, direction);
        return true;
    }

    private void Update() => Advance(Time.deltaTime);

    // Deterministic stepping allows tests without wall-clock delays.
    public void Advance(float deltaTime)
    {
        if (!IsTransporting) return;
        if (!lease.IsActive) { CancelTransport(); return; }
        if (suspension.IsWorldPaused || awaitingExit || deltaTime <= 0f) return;
        if (delay > 0f) { delay -= deltaTime; return; }
        if (segment < segmentCount)
        {
            elapsed += deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, durations[segment]));
            Vector3 next = Vector3.Lerp(points[segment], points[segment + 1], Mathf.SmoothStep(0f, 1f, t));
            if (!IsSegmentClear(transform.position, next)) { CancelTransport(); return; }
            transform.position = next;
            if (t >= 1f) { segment++; elapsed = 0f; }
            return;
        }
        if (hold > 0f) { hold -= deltaTime; return; }
        if (exitTransport)
        {
            awaitingExit = true;
            activeVfx.Hide();
            var callback = completed; completed = null;
            callback?.Invoke(); // Remain locked until scene unload or explicit CancelTransport.
        }
        else CancelTransport();
    }

    public void CancelTransport()
    {
        if (!IsTransporting) return;
        if (activeVfx != null) activeVfx.Hide();
        activeVfx = null;
        if (capsule != null) capsule.enabled = controllerWasEnabled;
        GetComponent<ThirdPersonController>()?.ResetMotion();
        var previous = lease; lease = null; completed = null;
        previous.Dispose();
    }

    // Skin contraction allows contact with the support surface without penetration.
    private void CapsuleAt(Vector3 position, out Vector3 bottom, out Vector3 top, out float radius)
    {
        Vector3 scale = transform.lossyScale;
        float fullRadius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        radius = Mathf.Max(0.01f, fullRadius - Mathf.Min(capsule.skinWidth, fullRadius * 0.25f));
        float half = Mathf.Max(fullRadius, capsule.height * Mathf.Abs(scale.y) * 0.5f);
        Vector3 center = position + transform.TransformVector(capsule.center);
        bottom = center - Vector3.up * (half - fullRadius);
        top = center + Vector3.up * (half - fullRadius);
    }

    private bool IsOwn(Collider collider) => collider == null || collider.transform.IsChildOf(transform);

    public bool IsSegmentClear(Vector3 from, Vector3 to)
    {
        Resolve();
        Physics.SyncTransforms();
        CapsuleAt(to, out var bottom, out var top, out float radius);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps, obstructionMask, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return false;
        for (int i = 0; i < count; i++) if (!IsOwn(overlaps[i])) return false;
        CapsuleAt(from, out bottom, out top, out radius);
        Vector3 delta = to - from;
        if (delta.sqrMagnitude < 0.000001f) return true;
        count = Physics.CapsuleCastNonAlloc(bottom, top, radius, delta.normalized, hits, delta.magnitude,
            obstructionMask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false;
        for (int i = 0; i < count; i++) if (!IsOwn(hits[i].collider)) return false;
        return true;
    }

    public bool IsLandingSafe(Vector3 position)
    {
        Resolve();
        if (!IsSegmentClear(position, position)) return false;
        CapsuleAt(position, out var bottom, out _, out float radius);
        int count = Physics.RaycastNonAlloc(bottom, Vector3.down, hits, radius + landingProbeDistance,
            obstructionMask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false;
        for (int i = 0; i < count; i++)
            if (!IsOwn(hits[i].collider) && Vector3.Angle(hits[i].normal, Vector3.up) <= capsule.slopeLimit) return true;
        return false;
    }

    private void OnDisable() => CancelTransport();
    private void OnDestroy()
    {
        CancelTransport();
        if (reusableVfx != null) Destroy(reusableVfx.gameObject);
    }
}
