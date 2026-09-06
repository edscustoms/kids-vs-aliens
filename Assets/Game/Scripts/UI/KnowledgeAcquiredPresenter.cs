using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class KnowledgeAcquiredPresenter : MonoBehaviour
{
    [SerializeField]
    private PlayerSkillState skills;

    [SerializeField]
    private GameplaySuspensionController suspension;

    [SerializeField]
    private GameObject view;

    [SerializeField]
    private TMP_Text title;

    [SerializeField]
    private TMP_Text description;

    [SerializeField]
    private TMP_Text instructions;

    [SerializeField]
    private Button acknowledgeButton;
    private readonly KnowledgePresentationQueue queue = new();
    private GameplaySuspensionController.Lease lease;
    private int closedFrame = -1;

    public SkillData CurrentSkill { get; private set; }
    public int PendingCount => queue.Count;
    public event Action<SkillData> PresentationStarted;
    public event Action PresentationClosed;

    private void OnEnable()
    {
        if (skills != null)
            skills.SkillUnlocked += Enqueue;
        if (acknowledgeButton != null)
            acknowledgeButton.onClick.AddListener(Close);
        if (view != null)
            view.SetActive(false);
    }

    private void OnDisable()
    {
        if (skills != null)
            skills.SkillUnlocked -= Enqueue;
        if (acknowledgeButton != null)
            acknowledgeButton.onClick.RemoveListener(Close);
        Close();
        queue.Clear();
    }

    private void Enqueue(SkillData skill) => queue.Enqueue(skill, Time.frameCount);

    private void Update()
    {
        if (CurrentSkill != null || Time.frameCount <= closedFrame)
            return;
        if (queue.TryDequeue(Time.frameCount, out SkillData skill) && skill != null)
            Open(skill);
    }

    private void Open(SkillData skill)
    {
        if (view == null || suspension == null || !suspension.isActiveAndEnabled)
        {
            Debug.LogWarning(
                "Knowledge presentation is not configured; acquired skill is preserved.",
                this
            );
            return;
        }
        try
        {
            lease = suspension.Acquire(SuspensionReason.KnowledgePresentation);
            CurrentSkill = skill;
            SkillTutorialData tutorial = skill.TutorialData;
            if (title != null)
                title.text = tutorial != null ? tutorial.TitleFor(skill) : skill.DisplayName;
            if (description != null)
                description.text =
                    tutorial != null && !string.IsNullOrWhiteSpace(tutorial.shortDescription)
                        ? tutorial.shortDescription
                    : !string.IsNullOrWhiteSpace(skill.Description) ? skill.Description
                    : "New knowledge acquired.";
            if (instructions != null)
                instructions.text = tutorial != null ? tutorial.instructions : string.Empty;
            view.SetActive(true);
            PresentationStarted?.Invoke(skill);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            // Presentation failure has no progression side effects.
            Close();
        }
    }

    public void Close()
    {
        bool wasOpen = CurrentSkill != null || lease != null;
        try
        {
            if (wasOpen)
                PresentationClosed?.Invoke();
        }
        finally
        {
            CurrentSkill = null;
            if (view != null)
                view.SetActive(false);
            lease?.Dispose();
            lease = null;
            closedFrame = Time.frameCount;
        }
    }
}
