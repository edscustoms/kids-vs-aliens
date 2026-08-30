using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerSkillState : MonoBehaviour
{
    public readonly struct SkillProgress
    {
        public SkillProgress(
            SkillData skill,
            int level,
            int totalXp)
        {
            Skill = skill;
            Level = level;
            TotalXp = totalXp;
        }

        public SkillData Skill { get; }
        public int Level { get; }
        public int TotalXp { get; }
    }

    private sealed class MutableSkillProgress
    {
        public SkillData skill;
        public int totalXp;
    }

    // POC persistence:
    // survives scene/player recreation for the current app session.
    // A real save/profile system can replace this later.
    private static readonly Dictionary<string, MutableSkillProgress>
        RuntimeSkills = new();

    public event Action<SkillData> SkillUnlocked;
    public event Action<SkillData, int, int> SkillXpChanged;
    public event Action<SkillData, int> SkillLevelChanged;

    public bool HasSkill(SkillData skill)
    {
        if (!TryGetSkillId(skill, out string id))
            return false;

        return RuntimeSkills.ContainsKey(id);
    }

    public bool UnlockSkill(SkillData skill)
    {
        if (!TryGetSkillId(skill, out string id))
            return false;

        if (RuntimeSkills.ContainsKey(id))
            return false;

        RuntimeSkills.Add(
            id,
            new MutableSkillProgress
            {
                skill = skill,
                totalXp = 0
            });

        SkillUnlocked?.Invoke(skill);

        return true;
    }

    public bool AddXp(
        SkillData skill,
        int amount)
    {
        if (skill == null ||
            amount <= 0 ||
            !TryGetSkillId(skill, out string id) ||
            !RuntimeSkills.TryGetValue(
                id,
                out MutableSkillProgress progress))
        {
            return false;
        }

        int oldLevel =
            skill.GetLevelForTotalXp(
                progress.totalXp);

        progress.totalXp += amount;

        int newLevel =
            skill.GetLevelForTotalXp(
                progress.totalXp);

        SkillXpChanged?.Invoke(
            skill,
            amount,
            progress.totalXp);

        if (newLevel > oldLevel)
        {
            SkillLevelChanged?.Invoke(
                skill,
                newLevel);
        }

        return true;
    }

    public bool TryGetProgress(
        SkillData skill,
        out SkillProgress progress)
    {
        progress = default;

        if (!TryGetSkillId(skill, out string id) ||
            !RuntimeSkills.TryGetValue(
                id,
                out MutableSkillProgress runtime))
        {
            return false;
        }

        progress =
            new SkillProgress(
                runtime.skill,
                runtime.skill.GetLevelForTotalXp(
                    runtime.totalXp),
                runtime.totalXp);

        return true;
    }

    public int GetLevel(SkillData skill)
    {
        return TryGetProgress(
            skill,
            out SkillProgress progress)
            ? progress.Level
            : 0;
    }

    public int GetTotalXp(SkillData skill)
    {
        return TryGetProgress(
            skill,
            out SkillProgress progress)
            ? progress.TotalXp
            : 0;
    }

    public static void ResetRuntimeSkills()
    {
        RuntimeSkills.Clear();
    }

    private static bool TryGetSkillId(
        SkillData skill,
        out string id)
    {
        id = null;

        if (skill == null ||
            string.IsNullOrWhiteSpace(skill.Id))
        {
            return false;
        }

        id = skill.Id.Trim();

        return true;
    }
}
