using StarterAssets;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BeamHoistAbility : MonoBehaviour
{
    [SerializeField] private SkillData requiredSkill;
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
        if (input != null) input.HoistRequested += RequestHoist;
    }
    private void OnDisable() { if (input != null) input.HoistRequested -= RequestHoist; }
    private void RequestHoist() => TryActivate();
    public bool TryActivate()
    {
        if (!isActiveAndEnabled || !IsUnlocked || input == null || !input.CanProcessGameplayInput
            || transport == null || transport.IsTransporting) return false;
        BeamHoistTarget best = null;
        float distance = float.PositiveInfinity;
        foreach (var target in BeamHoistTarget.ActiveTargets)
        {
            if (target == null || target.gameObject.scene != gameObject.scene || !target.IsInRange(transform.position)) continue;
            float next = (target.transform.position - transform.position).sqrMagnitude;
            if (next < distance && transport.CanHoist(target)) { best = target; distance = next; }
        }
        return best != null && transport.TryHoist(best);
    }
}
