using UnityEngine;

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
    public CharacterActionId action;
    public ItemData equipment;
    public bool loop = true;

    [Min(0.25f)]
    public float loopDuration = 4f;

    [Header("Preview framing")]
    [Min(0.1f)]
    public float distanceMultiplier = 1.15f;
    public Vector3 targetOffset;

    public string TitleFor(SkillData skill) =>
        !string.IsNullOrWhiteSpace(titleOverride) ? titleOverride
        : skill != null ? skill.DisplayName
        : string.Empty;
}
