using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BeamHoistAbility : MonoBehaviour
{
    [Header("Knowledge")]
    [SerializeField]
    private SkillData requiredSkill;

    [Header("Ability Limits")]
    [Tooltip("Minimum height difference required between Amy and the landing.")]
    [SerializeField, Min(0.1f)]
    private float minimumVerticalGain = 0.6f;

    [Tooltip("Maximum height the LANDING may be above Amy's starting position.")]
    [SerializeField, Min(0.1f)]
    private float maximumHoistHeight = 6f;

    [Tooltip("Maximum horizontal distance from Amy to the landing.")]
    [SerializeField, Min(0.1f)]
    private float maximumLateralDistance = 4f;

    [Header("Movement")]
    [Tooltip("Total duration of the complete START -> LANDING hoist curve.")]
    [SerializeField, Min(0.1f)]
    private float hoistDuration = 2.4f;

    [Tooltip(
        "How high the hoist curve should arch above the LANDING. "
            + "Safety clearance may force the path slightly higher."
    )]
    [SerializeField, Min(0f)]
    private float arcHeightAboveLanding = 1f;

    private PlayerSkillState skills;
    private StarterAssetsInputs input;
    private BeamTransportController transport;

    public SkillData RequiredSkill => requiredSkill;
    public float MaximumLateralDistance => maximumLateralDistance;

    public bool IsUnlocked =>
        skills != null && requiredSkill != null && skills.HasSkill(requiredSkill);

    public bool HasNearbyTarget
    {
        get
        {
            if (
                !IsUnlocked
                || transport == null
                || transport.IsTransporting
                || input == null
                || !input.CanProcessGameplayInput
            )
            {
                return false;
            }

            foreach (var target in BeamHoistTarget.RegisteredTargets)
            {
                if (
                    target != null
                    && target.gameObject.scene == gameObject.scene
                    && target.IsInRange(transform.position)
                )
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void Resolve()
    {
        skills = GetComponent<PlayerSkillState>();
        input = GetComponent<StarterAssetsInputs>();
        transport = GetComponent<BeamTransportController>();
    }

    private void Awake()
    {
        Resolve();
    }

    private void OnEnable()
    {
        Resolve();

        if (input != null)
        {
            input.HoistRequested += RequestHoist;
            input.ContextualJumpRequested += TryActivate;
        }
    }

    private void OnDisable()
    {
        if (input == null)
            return;

        input.HoistRequested -= RequestHoist;
        input.ContextualJumpRequested -= TryActivate;
    }

    private void RequestHoist()
    {
        TryActivate();
    }

    public bool TryActivate()
    {
        if (
            !isActiveAndEnabled
            || !IsUnlocked
            || input == null
            || !input.CanProcessGameplayInput
            || transport == null
            || transport.IsTransporting
        )
        {
            return false;
        }

        // Don't acquire another hoist while Amy is in mid-air.
        if (!transport.IsLandingSafe(transform.position))
            return false;

        bool found = false;

        BeamHoistPath best = default;

        float distance = float.PositiveInfinity;

        /*
         * SMART HOIST SURFACES
         *
         * These use our global ability tuning:
         *
         * Hoist Duration
         * Arc Height Above Landing
         */
        foreach (var surface in BeamHoistSurface.Active)
        {
            if (surface == null || surface.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            for (int i = 0; i < surface.CandidateCount; i++)
            {
                if (!TryBuildSurfaceHoist(surface, i, transform.position, out var path)) continue;

                Select(path, ref best, ref distance, ref found);
            }
        }

        /*
         * LEGACY / AUTHORED TARGETS
         *
         * Keep these working for irregular/manual hoist points.
         *
         * They still use their own authored duration values.
         */
        foreach (var target in BeamHoistTarget.ActiveTargets)
        {
            if (
                target == null
                || target.gameObject.scene != gameObject.scene
                || !target.IsInRange(transform.position)
            )
            {
                continue;
            }

            if (transport.TryBuildHoist(target, out var path))
            {
                Select(path, ref best, ref distance, ref found);
            }
        }

        return found && transport.TryHoist(best);
    }

    // Presentation observes these queries; none acquires control, moves Amy or starts VFX.
    public bool CanPreviewHoist => isActiveAndEnabled && IsUnlocked && input != null
        && input.CanProcessGameplayInput && transport != null && transport.CanBeginTransport;

    public bool TryBuildSurfaceHoist(BeamHoistSurface surface, int index, Vector3 start, out BeamHoistPath path)
    {
        path = default;
        if (surface == null || surface.gameObject.scene != gameObject.scene || transport == null
            || !surface.TryGetCandidate(start, index, transport.FeetOffset, out var end, out float releaseY)) return false;
        path = CreateSurfaceHoist(start, end, releaseY);
        return true;
    }

    // Shared by the editor baker and activation: the same authored trajectory and limits.
    public BeamHoistPath CreateSurfaceHoist(Vector3 start, Vector3 landing, float releaseHeight) =>
        BeamHoistPath.Create(start, landing, releaseHeight, arcHeightAboveLanding, hoistDuration, 0f);

#if UNITY_EDITOR
    public static event System.Action<BeamHoistAbility> BakeSettingsChanged;
    private void OnValidate() => BakeSettingsChanged?.Invoke(this);
#endif

    public bool CanPreviewSurfaceHoist(BeamHoistSurface surface, int index, Vector3 start)
    {
        return CanPreviewHoist && TryBuildSurfaceHoist(surface, index, start, out var path)
            && IsWithinLimits(path) && transport.IsLandingSafe(start) && transport.IsHoistRouteClear(path);
    }

    public bool IsWithinLimits(BeamHoistPath path)
    {
        /*
         * Landing-height limitation.
         *
         * IMPORTANT:
         *
         * Maximum Hoist Height now controls the actual destination,
         * NOT the decorative arc height.
         */
        float gain = path.landing.y - path.start.y;

        Vector3 delta = path.landing - path.start;

        delta.y = 0f;

        return gain >= minimumVerticalGain
            && gain <= maximumHoistHeight
            && delta.sqrMagnitude <= maximumLateralDistance * maximumLateralDistance;
    }

    private void Select(
        BeamHoistPath candidate,
        ref BeamHoistPath best,
        ref float distance,
        ref bool found
    )
    {
        float next = (candidate.landing - transform.position).sqrMagnitude;

        if (next >= distance || !IsWithinLimits(candidate) || !transport.CanHoist(candidate))
        {
            return;
        }

        best = candidate;
        distance = next;
        found = true;
    }
}
