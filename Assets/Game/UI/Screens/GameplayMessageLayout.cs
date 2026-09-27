using UnityEngine;

// Reserved regions only. Each channel retains its own state, duration and priority policy.
public sealed class GameplayMessageLayout : MonoBehaviour
{
    public static Rect ObjectiveRegion => Rect.MinMaxRect(.36f,.86f,.68f,.956f);
    public static Rect DialogueRegion => Rect.MinMaxRect(.25f,.185f,.75f,.35f);
    public void Arrange(RectTransform objective, RectTransform runMessage, RectTransform systemFeedback, RectTransform dialogue)
    {
        Region(objective, ObjectiveRegion.min, ObjectiveRegion.max);
        Region(runMessage, new(.30f,.79f), new(.71f,.845f));
        Region(systemFeedback, new(.28f,.715f), new(.73f,.775f));
        Region(dialogue, DialogueRegion.min, DialogueRegion.max);
    }
    private static void Region(RectTransform rect, Vector2 min, Vector2 max)
    {
        if (rect == null) return;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(.5f,.5f);
    }
}
