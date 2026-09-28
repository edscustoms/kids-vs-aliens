using UnityEngine;
using UnityEngine.Events;

public enum ExcavatorMotionState { Idle, Moving, Completed }

/// <summary>One authored slew and blocker fall. Deliberately independent of mission and run state.</summary>
[DisallowMultipleComponent]
public sealed class ExcavatorMotionController : MonoBehaviour
{
    [System.Serializable]
    private sealed class BlockerPiece
    {
        public Transform piece;
        [Tooltip("Quadratic flight control point: pulls the initial kick away from impact, then drops toward the clear pose.")]
        public Transform flightControl;
        public Transform clearedPose;
        [Min(0)] public float delay;
        [Min(.01f)] public float fallDuration = .65f;
        [HideInInspector] public Vector3 startPosition;
        [HideInInspector] public Quaternion startRotation = Quaternion.identity;
    }

    [Header("Upper assembly (scene instance only)")]
    [SerializeField] private Transform upperPivot;
    [Tooltip("The imported model's bearing normal, expressed in the pivot's authored local frame.")]
    [SerializeField] private Vector3 slewAxis = Vector3.forward;
    [SerializeField] private float startAngle;
    [SerializeField] private float targetAngle = -166f;
    [SerializeField, Min(.01f)] private float duration = 6f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField, HideInInspector] private Quaternion referenceRotation = Quaternion.identity;

    [Header("Authored impact and exit")]
    [SerializeField] private Transform blocker;
    [Tooltip("Normalized swing time. The blocker stays in its authored start pose until this moment.")]
    [SerializeField, Range(0, 1)] private float impactProgress = .86f;
    [Tooltip("Separate scene-authored kick paths and rubble poses. Delays begin at impact.")]
    [SerializeField] private BlockerPiece[] blockerPieces = new BlockerPiece[0];
    [Tooltip("Striped-piece collision is disabled during the fall and restored at the settled rubble poses.")]
    [SerializeField] private Collider[] blockingColliders = new Collider[0];
    [Tooltip("One stationary box holds the exit shut during the fall. Disabled only after all pieces settle.")]
    [SerializeField] private BoxCollider temporaryExitBlocker;
    [Header("Completion (optional)")]
    [SerializeField] private UnityEvent onSwingCompleted = new UnityEvent();

    private float elapsed;
    public ExcavatorMotionState State { get; private set; }
    public bool HasImpacted { get; private set; }
    public bool IsExitClear { get; private set; }
    public Transform UpperPivot => upperPivot;
    public Transform Blocker => blocker;
    public float Duration => duration;
    public float ImpactTime => duration * impactProgress;
    public float BlockerClearTime
    {
        get
        {
            float end = ImpactTime;
            foreach (var piece in blockerPieces)
                end = Mathf.Max(end, ImpactTime + piece.delay + piece.fallDuration);
            return end;
        }
    }
    public float CompletionTime => Mathf.Max(duration, BlockerClearTime);
    public UnityEvent OnSwingCompleted => onSwingCompleted;

    public void PlaySwing()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || State != ExcavatorMotionState.Idle) return;
        if (upperPivot == null || blocker == null || temporaryExitBlocker == null || blockerPieces.Length == 0 || slewAxis.sqrMagnitude < .0001f)
        {
            Debug.LogError("Excavator swing requires its upper pivot, slew axis, temporary exit box and authored blocker pieces.", this);
            return;
        }
        foreach (var piece in blockerPieces)
            if (piece.piece == null || piece.flightControl == null || piece.clearedPose == null)
            {
                Debug.LogError("Each blocker piece requires its piece, flight control and cleared pose.", this);
                return;
            }
        elapsed = 0;
        State = ExcavatorMotionState.Moving;
    }

    private void Update()
    {
        if (State != ExcavatorMotionState.Moving) return;
        elapsed += Time.deltaTime;
        ApplyPose(elapsed);
        if (elapsed < CompletionTime) return;
        State = ExcavatorMotionState.Completed;
        onSwingCompleted.Invoke();
    }

    private static float Evaluate(AnimationCurve curve, float progress)
    {
        if (progress <= 0) return 0;
        if (progress >= 1) return 1;
        return curve == null || curve.length == 0 ? Mathf.SmoothStep(0, 1, progress) : Mathf.Clamp01(curve.Evaluate(progress));
    }

    private void ApplyPose(float time)
    {
        float swing = Evaluate(ease, time / Mathf.Max(.01f, duration));
        upperPivot.localRotation = referenceRotation * Quaternion.AngleAxis(Mathf.Lerp(startAngle, targetAngle, swing), slewAxis.normalized);
        if (time < ImpactTime) return;
        if (!HasImpacted)
        {
            if (temporaryExitBlocker != null) temporaryExitBlocker.enabled = true;
            SetBlockerCollision(false);
            HasImpacted = true;
        }
        // Use the same endpoint as completion and Inspector previews, including float rounding.
        bool atEnd = time >= BlockerClearTime;
        bool settled = true;
        foreach (var piece in blockerPieces)
        {
            if (piece.piece == null || piece.flightControl == null || piece.clearedPose == null) { settled = false; continue; }
            float progress = atEnd ? 1f
                : Mathf.Clamp01((time - ImpactTime - piece.delay) / Mathf.Max(.01f, piece.fallDuration));
            if (progress <= 0) { settled = false; continue; }
            var parent = piece.piece.parent;
            Vector3 start = parent != null ? parent.TransformPoint(piece.startPosition) : piece.startPosition;
            Quaternion rotation = parent != null ? parent.rotation * piece.startRotation : piece.startRotation;
            // A nonzero launch velocity gives a sharp kick. The quadratic flight accelerates
            // downward; the final tenth of the short fall settles the last four centimetres.
            float flight = Mathf.Clamp01(progress / .9f);
            float rest = 1 - flight;
            Vector3 landing = piece.clearedPose.position + Vector3.up * .04f;
            Vector3 position = rest * rest * start + 2 * rest * flight * piece.flightControl.position
                + flight * flight * landing;
            if (progress >= .9f)
            {
                float settle = Mathf.Clamp01((progress - .9f) / .1f);
                position = piece.clearedPose.position + Vector3.up * (.04f * (1 - settle) * (1 - settle));
            }
            piece.piece.SetPositionAndRotation(position,
                Quaternion.Slerp(rotation, piece.clearedPose.rotation, 1 - rest * rest));
            settled &= progress >= 1;
        }
        if (!settled || IsExitClear) return;
        SetBlockerCollision(true);
        IsExitClear = true;
        if (temporaryExitBlocker != null) temporaryExitBlocker.enabled = false;
    }

    private void SetBlockerCollision(bool enabled)
    {
        foreach (var collider in blockingColliders) if (collider != null) collider.enabled = enabled;
    }

    /// <summary>Safe run-restore endpoint. Assigns the existing authored final poses without replay or completion callbacks.</summary>
    public void RestoreCompletedPose()
    {
        ResetToStart();
        elapsed = CompletionTime;
        ApplyPose(elapsed);
        State = ExcavatorMotionState.Completed;
    }

    /// <summary>Explicit authoring/debug reset; never called implicitly at scene load or enable.</summary>
    public void ResetToStart()
    {
        elapsed = 0;
        State = ExcavatorMotionState.Idle;
        HasImpacted = IsExitClear = false;
        if (upperPivot != null) upperPivot.localRotation = referenceRotation * Quaternion.AngleAxis(startAngle, slewAxis.normalized);
        foreach (var piece in blockerPieces)
            if (piece.piece != null) piece.piece.SetLocalPositionAndRotation(piece.startPosition, piece.startRotation);
        SetBlockerCollision(true);
        if (temporaryExitBlocker != null) temporaryExitBlocker.enabled = true;
    }

#if UNITY_EDITOR
    public void CaptureCurrentAsStart()
    {
        if (Application.isPlaying) return;
        if (upperPivot != null) referenceRotation = upperPivot.localRotation * Quaternion.Inverse(Quaternion.AngleAxis(startAngle, slewAxis.normalized));
        foreach (var piece in blockerPieces)
            if (piece.piece != null) { piece.startPosition = piece.piece.localPosition; piece.startRotation = piece.piece.localRotation; }
    }

    public void PreviewProgress(float time)
    {
        if (Application.isPlaying || upperPivot == null || blocker == null) return;
        ResetToStart();
        ApplyPose(Mathf.Max(0, time));
    }
#endif
}
