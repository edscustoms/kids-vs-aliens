using UnityEngine;

public enum SkillDemoType
{
    Stance = 0,
    WeaponFire = 1,
    GrenadeThrow = 2,
}

[CreateAssetMenu(menuName = "Kids VS Aliens/Presentation/Skill Tutorial")]
public sealed class SkillTutorialData : ScriptableObject
{
    public string titleOverride;

    [TextArea]
    public string shortDescription;

    [TextArea]
    public string instructions;

    [Tooltip("Optional base localization key; no localization dependency required in V1.")]
    public string localizationKey;

    [Header("Demonstration")]
    [Tooltip("What kind of presentation-only demonstration should run for this tutorial.")]
    public SkillDemoType demoType = SkillDemoType.Stance;

    [Tooltip(
        "Optional real shared Animator action to request during the demo. "
            + "If the current controller has no matching trigger yet, the preview keeps the valid equipped stance and continues cosmetic presentation."
    )]
    public CharacterActionId action = CharacterActionId.EquippedStance;
    public bool combatStance;

    public ItemData equipment;
    public bool loop = true;

    [Min(0.25f)]
    [Tooltip("Loop interval for ordinary animation-action tutorials. WeaponFire uses the timing below instead.")]
    public float loopDuration = 4f;

    [Header("Weapon fire demo")]
    [Min(0f)]
    public float weaponInitialDelay = 0.35f;

    [Min(1)]
    public int weaponShotsPerBurst = 4;

    [Min(0.05f)]
    public float weaponShotInterval = 0.7f;

    [Min(0f)]
    public float weaponBurstPause = 2f;

    [Min(0.1f)]
    public float weaponBoltDistance = 2.5f;

    [Min(0f)]
    public float weaponRecoilDistance = 0.035f;

    [Min(0.01f)]
    public float weaponRecoilDuration = 0.12f;

    [Header("Preview framing")]
    [Min(0.1f)]
    public float distanceMultiplier = 1.15f;
    public Vector3 targetOffset;

    public string TitleFor(SkillData skill) =>
        !string.IsNullOrWhiteSpace(titleOverride) ? titleOverride
        : skill != null ? skill.DisplayName
        : string.Empty;
}
