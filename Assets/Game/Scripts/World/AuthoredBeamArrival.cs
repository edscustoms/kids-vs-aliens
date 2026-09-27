using System;
using StarterAssets;
using UnityEngine;

public enum AuthoredArrivalState { Available, Targeting, Arriving, Arrived }

// Owns only arrival. The dormant payload keeps its own gameplay and RunWorldObject state.
[DisallowMultipleComponent, RequireComponent(typeof(RunWorldObject))]
public sealed class AuthoredBeamArrival : MonoBehaviour, IRunStateParticipant
{
    [Serializable] public sealed class LandingCandidate
    {
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
    }

    [Header("Authored payload (existing prefab instance)")]
    [SerializeField] private GameObject payloadGate;
    [SerializeField] private GameObject payload;
    [SerializeField] private Behaviour[] interactionBehaviours = Array.Empty<Behaviour>();
    [SerializeField] private BeamTransportVFX beam;
    [Header("Landing authoring")]
    [SerializeField, Min(.5f)] private float candidateSpacing = 1.5f;
    [SerializeField] private float landingYaw = -174.51f;
    [SerializeField, Range(0,.25f)] private float viewportMargin = .1f;
    [HideInInspector, SerializeField] private LandingCandidate[] landingCandidates = Array.Empty<LandingCandidate>();
    [HideInInspector, SerializeField] private string validationSignature;
    [SerializeField, Min(0)] private float minimumPlayerDistance = 7.8f;
    [SerializeField, Min(.2f)] private float beamArrivalDuration = 1f;
    [SerializeField, Min(0)] private float safetyMargin = .75f;
    [SerializeField, Min(.1f)] private float footprintRadius = 2f;
    [SerializeField, Min(.1f)] private float clearanceHeight = 3.5f;
    [SerializeField] private LayerMask obstructionMask = ~0;
    [HideInInspector, SerializeField] private float validatedRadius, validatedHeight;

    [Header("Pending landing selection")]
    [SerializeField, Min(.05f)] private float candidateEvaluationInterval = .15f;
    [SerializeField, Min(0)] private float candidateStabilityDuration = .4f;
    [SerializeField, Min(.1f)] private float previewMoveSpeed = 12f;

    private Collider[] colliders;
    private bool[] colliderEnabled, behaviourEnabled;
    private readonly Collider[] overlaps = new Collider[32];
    private Vector3 authoredLocalPosition, authoredScale;
    private Quaternion authoredLocalRotation;
    private PlayerCharacter player;
    private CharacterController playerCapsule;
    private float elapsed, evaluationClock, stableTime;
    private bool initialized, arrived, restorePending, previewShown;
    private int selectedIndex = -1;
    private GameplayTrigger region;
    public LandingCandidate[] Candidates => landingCandidates;
    public Vector3 CandidatePosition(int index) => transform.TransformPoint(landingCandidates[index].localPosition);
    public Quaternion CandidateRotation(int index) => transform.rotation * landingCandidates[index].localRotation;
    public AuthoredArrivalState State { get; private set; }
    public bool HasArrived => arrived;
    public int SelectedIndex => selectedIndex;
    public string RunStateKey => "beam-arrival";

    private void Awake() => Initialize();
    private void Initialize()
    {
        if (initialized) return;
        if (payloadGate == null || payload == null || beam == null) return;
        colliders = payload.GetComponentsInChildren<Collider>(true);
        colliderEnabled = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++) colliderEnabled[i] = colliders[i].enabled;
        behaviourEnabled = new bool[interactionBehaviours.Length];
        for (int i = 0; i < interactionBehaviours.Length; i++)
            behaviourEnabled[i] = interactionBehaviours[i] != null && interactionBehaviours[i].enabled;
        authoredLocalPosition = payload.transform.localPosition;
        authoredLocalRotation = payload.transform.localRotation;
        authoredScale = payload.transform.localScale;
        initialized = true;
        SetInteractive(false);
        payloadGate.SetActive(false);
        beam.Hide();
    }

    public float RequiredPlayerDistance(PlayerCharacter candidate)
    {
        var movement = candidate.GetComponent<ThirdPersonController>();
        var capsule = candidate.GetComponent<CharacterController>();
        float speed = movement != null ? Mathf.Max(movement.MoveSpeed, movement.SprintSpeed) : 0;
        float radius = capsule != null ? capsule.radius * Mathf.Max(candidate.transform.lossyScale.x, candidate.transform.lossyScale.z) : 0;
        return Mathf.Max(minimumPlayerDistance, speed * beamArrivalDuration + footprintRadius + radius + safetyMargin);
    }

    public int SelectCandidate(PlayerCharacter candidate, Camera gameplayCamera = null)
    {
        if (candidate == null || validatedRadius != footprintRadius || validatedHeight != clearanceHeight) return -1;
        float minimum = RequiredPlayerDistance(candidate);
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        float nearest = float.PositiveInfinity;
        bool selectedVisible = false;
        if (region == null) region = GetComponent<GameplayTrigger>();
        Physics.SyncTransforms();
        int selected = -1;
        for (int i = 0; i < landingCandidates.Length; i++)
        {
            var option = landingCandidates[i];
            if (option == null) continue;
            Vector3 position = CandidatePosition(i);
            if (region == null || !region.ContainsPoint(position + Vector3.up * .1f)) continue;
            float distance = Vector3.ProjectOnPlane(position - candidate.transform.position, Vector3.up).sqrMagnitude;
            if (distance < minimum * minimum) continue;
            if (!FootprintClear(position, CandidateRotation(i))) continue;
            bool visible = IsCandidateVisible(position, gameplayCamera);
            if (selected >= 0 && ((!visible && selectedVisible)
                || (visible == selectedVisible && distance >= nearest - .0001f))) continue;
            // Visibility wins, then the closest safe distance; equal scores keep authored order.
            selectedVisible = visible; nearest = distance; selected = i;
        }
        return selected;
    }

    public bool IsCandidateVisible(Vector3 position, Camera gameplayCamera)
    {
        if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled) return false;
        // Keep the footprint and top comfortably inside the actual rendered camera.
        // These are projections of authored points, not placement/ground searches.
        for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                if (!InFrame(position + new Vector3(x * footprintRadius, .15f, z * footprintRadius), gameplayCamera)) return false;
        if (!InFrame(position + Vector3.up * clearanceHeight, gameplayCamera)) return false;
        // A few bounded sight lines reject a pod hidden entirely behind a prop.
        for (int sample = 1; sample <= 3; sample++)
        {
            Vector3 target = position + Vector3.up * (clearanceHeight * sample * .25f);
            if (!Physics.Linecast(gameplayCamera.transform.position, target, out var hit, obstructionMask, QueryTriggerInteraction.Ignore)
                || (payload != null && hit.transform.IsChildOf(payload.transform))) return true;
        }
        return false;
    }

    private bool InFrame(Vector3 point, Camera camera)
    {
        Vector3 viewport = camera.WorldToViewportPoint(point);
        return viewport.z > camera.nearClipPlane && viewport.z < camera.farClipPlane
            && viewport.x >= viewportMargin && viewport.x <= 1-viewportMargin
            && viewport.y >= viewportMargin && viewport.y <= 1-viewportMargin;
    }

    public bool TryBegin(PlayerCharacter candidate)
    {
        Initialize();
        var run = ActiveRunController.Instance;
        if (!initialized || !isActiveAndEnabled || State != AuthoredArrivalState.Available || restorePending
            || run == null || !run.IsReady || candidate == null || run.gameObject != candidate.gameObject) return false;
        player = candidate; playerCapsule = candidate.GetComponent<CharacterController>();
        selectedIndex = -1; stableTime = evaluationClock = 0; previewShown = false;
        SetInteractive(false); payloadGate.SetActive(false);
        State = AuthoredArrivalState.Targeting;
        EvaluatePendingTarget();
        run.MarkDirty();
        return true;
    }

    private void EvaluatePendingTarget()
    {
        int next = SelectCandidate(player);
        if (next != selectedIndex) stableTime = 0;
        selectedIndex = next;
        if (next < 0) { stableTime = 0; return; }
        if (!previewShown)
        {
            beam.Show(CandidatePosition(next),BeamTransportDirection.Down);
            beam.BeginHoistFadeIn(); previewShown = true;
        }
    }

    private void UpdateTargeting(float delta)
    {
        evaluationClock -= delta;
        if (evaluationClock <= 0)
        {
            evaluationClock = Mathf.Max(.05f,candidateEvaluationInterval);
            EvaluatePendingTarget();
        }
        if (previewShown) beam.AdvanceHoistFadeIn(delta);
        if (selectedIndex < 0) return; // Pending until authored ground has a safe candidate.
        Vector3 target = CandidatePosition(selectedIndex);
        beam.transform.position = Vector3.MoveTowards(beam.transform.position,target,previewMoveSpeed*delta);
        if ((beam.transform.position-target).sqrMagnitude > .0025f) { stableTime = 0; return; }
        stableTime += delta;
        if (stableTime < candidateStabilityDuration) return;
        // Recheck immediately before locking the landing pose, even between scheduled evaluations.
        int next = SelectCandidate(player);
        if (next != selectedIndex) { selectedIndex = next; stableTime = 0; return; }
        payloadGate.transform.SetPositionAndRotation(target,CandidateRotation(selectedIndex));
        payload.transform.SetLocalPositionAndRotation(authoredLocalPosition,authoredLocalRotation);
        payload.transform.localScale = authoredScale*.001f;
        payloadGate.SetActive(true);
        elapsed = 0; evaluationClock = 0;
        State = AuthoredArrivalState.Arriving;
    }

    private void Update()
    {
        if (restorePending)
        {
            if (ActiveRunController.Instance == null || !ActiveRunController.Instance.IsReady) return;
            restorePending = false;
            // The payload's own world/health participant has now completed BOTH restore passes.
            payload.transform.localScale = authoredScale;
            SetInteractive(arrived);
            payloadGate.SetActive(arrived);
            State = arrived ? AuthoredArrivalState.Arrived : AuthoredArrivalState.Available;
        }
        if (State == AuthoredArrivalState.Available || State == AuthoredArrivalState.Arrived || Time.deltaTime <= 0) return;
        if (player == null || player.GetComponent<PlayerHealth>().IsDead) { Abort(); return; }
        if (State == AuthoredArrivalState.Targeting) { UpdateTargeting(Time.deltaTime); return; }

        // The pod is now materializing: never chase another candidate or move Amy.
        // If she enters its footprint, hold this pose with all gameplay/collision disabled.
        if (!PlayerOutsideFootprint()) return;
        elapsed += Time.deltaTime;
        float fraction = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.8f,elapsed/beamArrivalDuration));
        payload.transform.localScale = authoredScale*Mathf.Max(.001f,fraction);
        payload.transform.localPosition = authoredLocalPosition+Vector3.up*(.6f*(1-fraction));
        if (elapsed < beamArrivalDuration) return;
        evaluationClock -= Time.deltaTime;
        if (evaluationClock > 0) return;
        evaluationClock = Mathf.Max(.05f,candidateEvaluationInterval);
        Physics.SyncTransforms();
        if (!FootprintClear(payloadGate.transform.position,payloadGate.transform.rotation)) return;
        arrived = true; State = AuthoredArrivalState.Arrived;
        payload.transform.localScale = authoredScale;
        payload.transform.localPosition = authoredLocalPosition;
        beam.FadeOutAfterLanding(); SetInteractive(true);
        player = null; playerCapsule = null;
        ActiveRunController.Instance?.Save();
    }

    private bool PlayerOutsideFootprint()
    {
        float radius = footprintRadius + (playerCapsule != null ? playerCapsule.radius : .3f) + .15f;
        return Vector3.ProjectOnPlane(player.transform.position - payloadGate.transform.position, Vector3.up).sqrMagnitude > radius * radius;
    }
    private bool FootprintClear(Vector3 position, Quaternion rotation)
    {
        Vector3 center = position + Vector3.up * (clearanceHeight * .5f + .15f);
        int count = Physics.OverlapBoxNonAlloc(center, new Vector3(footprintRadius, clearanceHeight * .5f, footprintRadius), overlaps,
            rotation, obstructionMask, QueryTriggerInteraction.Ignore);
        return count == 0;
    }
    private void SetInteractive(bool enabled)
    {
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = enabled && colliderEnabled[i];
        for (int i = 0; i < interactionBehaviours.Length; i++)
            if (interactionBehaviours[i] != null) interactionBehaviours[i].enabled = enabled && behaviourEnabled[i];
    }
    private void Abort()
    {
        if (!initialized || arrived) return;
        beam.Hide(); SetInteractive(false); payloadGate.SetActive(false);
        payload.transform.localScale = authoredScale;
        payload.transform.localPosition = authoredLocalPosition;
        State = AuthoredArrivalState.Available; player = null; playerCapsule = null;
        ActiveRunController.Instance?.MarkDirty();
    }
    private void OnDisable() { if (!arrived) Abort(); }
    [Serializable] private struct SavedState { public bool arrived; public int selectedIndex; }
    public string CaptureRunState() => JsonUtility.ToJson(new SavedState { arrived = arrived, selectedIndex = arrived ? selectedIndex : -1 });
    public void RestoreRunState(string json)
    {
        Initialize();
        var saved = JsonUtility.FromJson<SavedState>(json);
        arrived = saved.arrived; selectedIndex = saved.selectedIndex;
        beam.Hide(); SetInteractive(false); payloadGate.SetActive(false);
        player = null; playerCapsule = null; restorePending = true;
        State = arrived ? AuthoredArrivalState.Arrived : AuthoredArrivalState.Available;
    }
}
