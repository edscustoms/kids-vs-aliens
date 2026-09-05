using System;

public enum FeedbackCode
{
    MissingSkill,
    InventoryFull,
    KnowledgeAlreadyKnown,
    GrenadeThrownInert,
}

public enum FeedbackAction
{
    None,
    Fire,
    Pickup,
    Learn,
    Throw,
}

public readonly struct GameplayFeedbackEvent
{
    public GameplayFeedbackEvent(
        FeedbackCode code,
        SkillData skill = null,
        ItemData item = null,
        FeedbackAction action = FeedbackAction.None
    )
    {
        Code = code;
        Skill = skill;
        Item = item;
        Action = action;
    }

    public FeedbackCode Code { get; }
    public SkillData Skill { get; }
    public ItemData Item { get; }
    public FeedbackAction Action { get; }

    public FeedbackKey Key => new(this);
}

public readonly struct FeedbackKey : IEquatable<FeedbackKey>
{
    private readonly FeedbackCode code;
    private readonly SkillData skill;
    private readonly ItemData item;
    private readonly FeedbackAction action;

    public FeedbackKey(GameplayFeedbackEvent value)
    {
        code = value.Code;
        // Capacity is global to this player's inventory; requirements are
        // shared by weapons/items, regardless of which item requested them.
        skill = code == FeedbackCode.InventoryFull ? null : value.Skill;
        item = skill != null || code == FeedbackCode.InventoryFull ? null : value.Item;
        action = code == FeedbackCode.InventoryFull ? FeedbackAction.None : value.Action;
    }

    public bool Equals(FeedbackKey other) =>
        code == other.code && skill == other.skill && item == other.item && action == other.action;

    public override bool Equals(object obj) => obj is FeedbackKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(code, skill, item, action);
}
