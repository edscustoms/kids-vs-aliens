using UnityEngine;

// Reserved regions only. Each channel retains its own state, duration and priority policy.
public sealed class GameplayMessageLayout : MonoBehaviour
{
    public void Arrange(RectTransform objective, RectTransform runMessage, RectTransform systemFeedback, RectTransform dialogue)
    {
        Region(objective, new(.27f,.86f), new(.74f,.955f));
        Region(runMessage, new(.30f,.79f), new(.71f,.845f));
        Region(systemFeedback, new(.28f,.715f), new(.73f,.775f));
        Region(dialogue, new(.25f,.205f), new(.75f,.35f));
    }
    private static void Region(RectTransform rect, Vector2 min, Vector2 max)
    {
        if (rect == null) return;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(.5f,.5f);
    }
}
