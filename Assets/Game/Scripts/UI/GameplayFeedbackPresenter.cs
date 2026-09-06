using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameplayFeedbackPresenter : MonoBehaviour
{
    [SerializeField]
    private PlayerFeedback source;

    [SerializeField]
    private PlayerSkillState skills;

    [SerializeField]
    private FeedbackPresentationCatalog catalog;

    [SerializeField]
    private CanvasGroup view;

    [SerializeField]
    private TMP_Text message;

    [SerializeField]
    private Image accent;
    private readonly FeedbackScheduler scheduler = new();
    private int revision = -1;
    private bool suppressed;

    private void OnEnable()
    {
        if (source != null)
            source.Reported += HandleFeedback;
        if (skills != null)
            skills.SkillUnlocked += HandleSkillUnlocked;
        if (view != null)
        {
            view.alpha = 0f;
            view.blocksRaycasts = false;
            view.interactable = false;
        }
    }

    private void OnDisable()
    {
        if (source != null)
            source.Reported -= HandleFeedback;
        if (skills != null)
            skills.SkillUnlocked -= HandleSkillUnlocked;
        scheduler.Clear();
        if (view != null)
            view.alpha = 0f;
    }

    public void SetSuppressed(bool value)
    {
        suppressed = value;
        if (value)
        {
            scheduler.Clear();
            if (view != null)
                view.alpha = 0f;
        }
    }

    private void HandleFeedback(GameplayFeedbackEvent feedback)
    {
        if (!suppressed && catalog != null && catalog.TryGet(feedback.Code, out var policy))
            scheduler.Submit(feedback, policy, Time.unscaledTimeAsDouble);
    }

    private void HandleSkillUnlocked(SkillData skill) =>
        scheduler.InvalidateSkill(skill, Time.unscaledTimeAsDouble);

    private void Update()
    {
        scheduler.Tick(Time.unscaledTimeAsDouble);
        if (revision != scheduler.Revision)
        {
            revision = scheduler.Revision;
            if (scheduler.HasActive)
            {
                if (message != null)
                    message.text = FeedbackPresentationCatalog.Format(
                        scheduler.Active,
                        scheduler.ActivePolicy
                    );
                if (accent != null)
                    accent.color =
                        scheduler.ActivePolicy.category == FeedbackCategory.Warning
                            ? new Color(1f, 0.77f, 0.3f)
                            : new Color(0.2f, 0.9f, 1f);
            }
        }
        if (view != null)
            view.alpha = Mathf.MoveTowards(
                view.alpha,
                scheduler.HasActive && !suppressed ? 1f : 0f,
                Time.unscaledDeltaTime * 8f
            );
    }
}
