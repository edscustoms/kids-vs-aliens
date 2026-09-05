using System.Collections.Generic;

public sealed class KnowledgePresentationQueue
{
    private readonly struct Request
    {
        public Request(SkillData skill, int frame)
        {
            Skill = skill;
            Frame = frame;
        }

        public readonly SkillData Skill;
        public readonly int Frame;
    }

    private readonly Queue<Request> pending = new();
    private readonly HashSet<string> seen = new();
    public int Count => pending.Count;

    public bool Enqueue(SkillData skill, int frame)
    {
        if (skill == null || string.IsNullOrWhiteSpace(skill.Id) || !seen.Add(skill.Id.Trim()))
            return false;
        pending.Enqueue(new Request(skill, frame));
        return true;
    }

    public bool TryDequeue(int currentFrame, out SkillData skill)
    {
        skill = null;
        if (pending.Count == 0 || pending.Peek().Frame >= currentFrame)
            return false;
        skill = pending.Dequeue().Skill;
        return true;
    }

    public void Clear()
    {
        pending.Clear();
        seen.Clear();
    }
}
