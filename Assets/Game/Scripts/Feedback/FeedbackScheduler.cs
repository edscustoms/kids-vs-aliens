using System.Collections.Generic;

// No Unity clock or view dependencies: policy can be tested deterministically.
public sealed class FeedbackScheduler
{
    private readonly struct Pending
    {
        public Pending(GameplayFeedbackEvent feedback, FeedbackPresentation policy, double expiry)
        {
            Feedback = feedback;
            Policy = policy;
            Expiry = expiry;
        }

        public readonly GameplayFeedbackEvent Feedback;
        public readonly FeedbackPresentation Policy;
        public readonly double Expiry;
    }

    private const int MaxPending = 3;
    private const int MaxCooldowns = 64;
    private readonly List<Pending> pending = new(MaxPending);
    private readonly Dictionary<FeedbackKey, double> cooldowns = new(MaxCooldowns);
    private double visibleUntil;

    public bool HasActive { get; private set; }
    public GameplayFeedbackEvent Active { get; private set; }
    public FeedbackPresentation ActivePolicy { get; private set; }
    public int Revision { get; private set; }
    public int PendingCount => pending.Count;

    public bool Submit(GameplayFeedbackEvent feedback, FeedbackPresentation policy, double now)
    {
        if (policy == null)
            return false;
        Tick(now);
        FeedbackKey key = feedback.Key;
        if (
            (HasActive && Active.Key.Equals(key))
            || (cooldowns.TryGetValue(key, out double until) && now < until)
        )
            return false;
        for (int i = 0; i < pending.Count; i++)
            if (pending[i].Feedback.Key.Equals(key))
                return false;

        if (
            !HasActive
            || policy.priority > ActivePolicy.priority
            || (policy.priority == ActivePolicy.priority && policy.replaceEqualPriority)
        )
        {
            Dismiss(now);
            Show(feedback, policy, now);
            return true;
        }
        if (!policy.queueWhenBlocked || pending.Count >= MaxPending)
            return false;
        pending.Add(new Pending(feedback, policy, now + policy.queueLifetime));
        return true;
    }

    public void Tick(double now)
    {
        for (int i = pending.Count - 1; i >= 0; i--)
            if (pending[i].Expiry <= now)
                pending.RemoveAt(i);
        if (HasActive && now >= visibleUntil)
            Dismiss(now);
        if (HasActive || pending.Count == 0)
            return;
        int best = 0;
        for (int i = 1; i < pending.Count; i++)
            if (pending[i].Policy.priority > pending[best].Policy.priority)
                best = i;
        Pending next = pending[best];
        pending.RemoveAt(best);
        Show(next.Feedback, next.Policy, now);
    }

    public void InvalidateSkill(SkillData skill, double now)
    {
        if (HasActive && Active.Code == FeedbackCode.MissingSkill && Active.Skill == skill)
            Dismiss(now);
        for (int i = pending.Count - 1; i >= 0; i--)
            if (
                pending[i].Feedback.Code == FeedbackCode.MissingSkill
                && pending[i].Feedback.Skill == skill
            )
                pending.RemoveAt(i);
    }

    public void Clear()
    {
        HasActive = false;
        Active = default;
        ActivePolicy = null;
        pending.Clear();
        cooldowns.Clear();
        Revision++;
    }

    private void Show(GameplayFeedbackEvent feedback, FeedbackPresentation policy, double now)
    {
        Active = feedback;
        ActivePolicy = policy;
        HasActive = true;
        visibleUntil = now + policy.duration;
        Revision++;
    }

    private void Dismiss(double now)
    {
        if (!HasActive)
            return;
        if (cooldowns.Count >= MaxCooldowns && !cooldowns.ContainsKey(Active.Key))
        {
            FeedbackKey oldest = default;
            double earliest = double.MaxValue;
            foreach (KeyValuePair<FeedbackKey, double> pair in cooldowns)
                if (pair.Value < earliest)
                {
                    earliest = pair.Value;
                    oldest = pair.Key;
                }
            cooldowns.Remove(oldest);
        }
        cooldowns[Active.Key] = now + ActivePolicy.cooldown;
        HasActive = false;
        ActivePolicy = null;
        Revision++;
    }
}
