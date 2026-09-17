using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BeamHoistAbility : MonoBehaviour
{
    [SerializeField] private SkillData requiredSkill;
    [SerializeField, Min(0.1f)] private float minimumVerticalGain = 0.6f;
    [SerializeField, Min(0.1f)] private float maximumHoistHeight = 6f;
    [SerializeField, Min(0.1f)] private float maximumLateralDistance = 4f;
    [SerializeField, Min(0.1f)] private float liftDuration = 1.5f;
    [SerializeField, Min(0.1f)] private float transferDuration = 0.9f;
    private PlayerSkillState skills;
    private StarterAssetsInputs input;
    private BeamTransportController transport;
    public SkillData RequiredSkill => requiredSkill;
    public bool IsUnlocked => skills != null && requiredSkill != null && skills.HasSkill(requiredSkill);
    public bool HasNearbyTarget
    {
        get
        {
            if (!IsUnlocked || transport == null || transport.IsTransporting || input == null || !input.CanProcessGameplayInput) return false;
            foreach (var target in BeamHoistTarget.RegisteredTargets)
                if (target != null && target.gameObject.scene == gameObject.scene && target.IsInRange(transform.position)) return true;
            return false;
        }
    }
    private void Resolve()
    {
        skills = GetComponent<PlayerSkillState>();
        input = GetComponent<StarterAssetsInputs>();
        transport = GetComponent<BeamTransportController>();
    }
    private void Awake() => Resolve();
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
        if (input == null) return;
        input.HoistRequested -= RequestHoist;
        input.ContextualJumpRequested -= TryActivate;
    }
    private void RequestHoist() => TryActivate();
    public bool TryActivate()
    {
        if (!isActiveAndEnabled || !IsUnlocked || input == null || !input.CanProcessGameplayInput
            || transport == null || transport.IsTransporting) return false;
        if (!transport.IsLandingSafe(transform.position)) return false; // No midair reacquisition.
        bool found = false;
        BeamHoistPath best = default;
        float distance = float.PositiveInfinity;
        foreach (var surface in BeamHoistSurface.Active)
        {
            if (surface == null || surface.gameObject.scene != gameObject.scene) continue;
            for (int i = 0; i < surface.CandidateCount; i++)
            {
                if (!surface.TryGetCandidate(transform.position, i, transport.FeetOffset, out var end, out float releaseY)) continue;
                var path = BeamHoistPath.Create(transform.position, end, releaseY, liftDuration, transferDuration);
                Select(path, ref best, ref distance, ref found);
            }
        }
        foreach (var target in BeamHoistTarget.ActiveTargets)
        {
            if (target == null || target.gameObject.scene != gameObject.scene || !target.IsInRange(transform.position)) continue;
            if (transport.TryBuildHoist(target, out var path)) Select(path, ref best, ref distance, ref found);
        }
        return found && transport.TryHoist(best);
    }

    public bool IsWithinLimits(BeamHoistPath path)
    {
        float gain = path.landing.y - path.start.y;
        Vector3 delta = path.landing - path.start;
        delta.y = 0f;
        return gain >= minimumVerticalGain && gain <= maximumHoistHeight
            && path.release.y - path.start.y <= maximumHoistHeight
            && delta.sqrMagnitude <= maximumLateralDistance * maximumLateralDistance;
    }

    private void Select(BeamHoistPath candidate, ref BeamHoistPath best, ref float distance, ref bool found)
    {
        float next = (candidate.landing - transform.position).sqrMagnitude;
        if (next >= distance || !IsWithinLimits(candidate) || !transport.CanHoist(candidate)) return;
        best = candidate; distance = next; found = true;
    }
}
