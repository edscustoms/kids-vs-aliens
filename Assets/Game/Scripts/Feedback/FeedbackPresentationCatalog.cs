using System;
using UnityEngine;

public enum FeedbackCategory
{
    Information,
    Warning,
    ActionDenied,
}

[Serializable]
public sealed class FeedbackPresentation
{
    public FeedbackCode code;

    [TextArea]
    public string template;
    public string localizationKey;
    public FeedbackCategory category;
    public int priority = 50;

    [Min(0.1f)]
    public float duration = 2f;

    [Min(0f)]
    public float cooldown = 2f;
    public bool replaceEqualPriority = true;
    public bool queueWhenBlocked;

    [Min(0.1f)]
    public float queueLifetime = 8f;
}

[CreateAssetMenu(menuName = "Kids VS Aliens/Presentation/Feedback Catalog")]
public sealed class FeedbackPresentationCatalog : ScriptableObject
{
    [SerializeField]
    private FeedbackPresentation[] entries = Array.Empty<FeedbackPresentation>();

    public bool TryGet(FeedbackCode code, out FeedbackPresentation entry)
    {
        foreach (FeedbackPresentation candidate in entries)
        {
            if (candidate != null && candidate.code == code)
            {
                entry = candidate;
                return true;
            }
        }
        entry = null;
        return false;
    }

    public static string Format(GameplayFeedbackEvent feedback, FeedbackPresentation entry)
    {
        return (entry.template ?? string.Empty)
            .Replace(
                "{skillName}",
                feedback.Skill != null ? feedback.Skill.DisplayName : string.Empty
            )
            .Replace("{itemName}", feedback.Item != null ? feedback.Item.itemName : string.Empty);
    }
}
