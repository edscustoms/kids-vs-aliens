using UnityEngine;

/// <summary>
/// Temporary POC/debug helper for proving the skill XP architecture.
/// Remove this component later; PlayerSkillState remains the real system.
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillXpDebugTester : MonoBehaviour
{
    [SerializeField]
    private PlayerSkillState playerSkillState;

    [SerializeField]
    private SkillData skill;

    [SerializeField, Min(1)]
    private int xpPerClick = 25;

    public SkillData Skill => skill;
    public int XpPerClick => xpPerClick;

    private void Reset()
    {
        playerSkillState =
            GetComponent<PlayerSkillState>();
    }

    public void AddXp()
    {
        EnsureSkillState();

        if (playerSkillState == null)
        {
            Debug.LogWarning(
                "Skill XP Debug: PlayerSkillState is missing.",
                this);

            return;
        }

        if (skill == null)
        {
            Debug.LogWarning(
                "Skill XP Debug: no SkillData assigned.",
                this);

            return;
        }

        if (!playerSkillState.HasSkill(skill))
        {
            Debug.Log(
                $"Skill XP Debug: {skill.DisplayName} is not unlocked yet. Read/acquire the knowledge first.",
                this);

            return;
        }

        if (!playerSkillState.AddXp(
                skill,
                xpPerClick))
        {
            return;
        }

        PrintProgress();
    }

    public void PrintProgress()
    {
        EnsureSkillState();

        if (playerSkillState == null ||
            skill == null)
        {
            return;
        }

        if (!playerSkillState.TryGetProgress(
                skill,
                out PlayerSkillState.SkillProgress progress))
        {
            Debug.Log(
                $"Skill XP Debug: {skill.DisplayName} is not unlocked.",
                this);

            return;
        }

        int xpIntoLevel =
            skill.GetXpIntoCurrentLevel(
                progress.TotalXp);

        Debug.Log(
            $"{skill.DisplayName} | Lv{progress.Level} | " +
            $"{xpIntoLevel}/{skill.XpPerLevel} XP " +
            $"({progress.TotalXp} total)",
            this);
    }

    private void EnsureSkillState()
    {
        if (playerSkillState == null)
        {
            playerSkillState =
                GetComponent<PlayerSkillState>();
        }
    }
}
