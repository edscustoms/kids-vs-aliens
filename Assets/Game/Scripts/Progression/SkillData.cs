using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SkillUnlockDefinition
{
    [Min(1)]
    public int requiredLevel = 1;

    public string id;
    public string displayName;
}

[CreateAssetMenu(
    fileName = "NewSkill",
    menuName = "Kids VS Aliens/Progression/Skill"
)]
public sealed class SkillData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string id;

    [SerializeField]
    private string displayName;

    [TextArea]
    [SerializeField]
    private string description;

    [Header("POC Progression")]
    [SerializeField, Min(1)]
    private int startingLevel = 1;

    [Tooltip(
        "Temporary POC rule: every level costs the same amount of XP. " +
        "We can replace the curve later without changing acquired skill data."
    )]
    [SerializeField, Min(1)]
    private int xpPerLevel = 100;

    [Header("Future Unlocks")]
    [SerializeField]
    private List<SkillUnlockDefinition> unlocks = new();

    public string Id => id;
    public string DisplayName =>
        string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName;

    public string Description => description;
    public int StartingLevel => startingLevel;
    public int XpPerLevel => xpPerLevel;
    public IReadOnlyList<SkillUnlockDefinition> Unlocks => unlocks;

    public int GetLevelForTotalXp(int totalXp)
    {
        totalXp = Mathf.Max(0, totalXp);

        return startingLevel +
               totalXp / xpPerLevel;
    }

    public int GetXpIntoCurrentLevel(int totalXp)
    {
        totalXp = Mathf.Max(0, totalXp);

        return totalXp % xpPerLevel;
    }

    private void OnValidate()
    {
        startingLevel = Mathf.Max(1, startingLevel);
        xpPerLevel = Mathf.Max(1, xpPerLevel);

        if (string.IsNullOrWhiteSpace(id))
        {
            id = name
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", "_");
        }
    }
}
