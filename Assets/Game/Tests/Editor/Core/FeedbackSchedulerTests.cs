using NUnit.Framework;
using UnityEngine;

[TestFixture, Category("Core")]
public sealed class FeedbackSchedulerTests
{
    private FeedbackScheduler scheduler;
    private SkillData pistol;
    private SkillData rifle;
    private FeedbackPresentation normal;

    [SetUp]
    public void SetUp()
    {
        scheduler = new FeedbackScheduler();
        pistol = ScriptableObject.CreateInstance<SkillData>();
        rifle = ScriptableObject.CreateInstance<SkillData>();
        normal = new FeedbackPresentation
        {
            duration = 2,
            cooldown = 2,
            priority = 50,
        };
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(pistol);
        Object.DestroyImmediate(rifle);
    }

    [Test]
    public void ActiveDuplicates_DoNotExtendDuration_AndCooldownExpires()
    {
        var feedback = new GameplayFeedbackEvent(FeedbackCode.MissingSkill, pistol);
        Assert.That(scheduler.Submit(feedback, normal, 0), Is.True);
        int revision = scheduler.Revision;
        Assert.That(scheduler.Submit(feedback, normal, 1.9), Is.False);
        Assert.That(scheduler.Revision, Is.EqualTo(revision));
        scheduler.Tick(2);
        Assert.That(scheduler.HasActive, Is.False);
        Assert.That(scheduler.Submit(feedback, normal, 3.9), Is.False);
        Assert.That(scheduler.Submit(feedback, normal, 4), Is.True);
    }

    [Test]
    public void DifferentSkills_AreDistinct_AndEqualPriorityCanReplace()
    {
        scheduler.Submit(new GameplayFeedbackEvent(FeedbackCode.MissingSkill, pistol), normal, 0);
        Assert.That(
            scheduler.Submit(
                new GameplayFeedbackEvent(FeedbackCode.MissingSkill, rifle),
                normal,
                0.1
            ),
            Is.True
        );
        Assert.That(scheduler.Active.Skill, Is.SameAs(rifle));
    }

    [Test]
    public void HigherPriorityReplaces_LowerPriorityDenialIsNotQueued()
    {
        var urgent = new FeedbackPresentation { priority = 80 };
        scheduler.Submit(new GameplayFeedbackEvent(FeedbackCode.MissingSkill, pistol), normal, 0);
        Assert.That(
            scheduler.Submit(
                new GameplayFeedbackEvent(FeedbackCode.GrenadeThrownInert),
                urgent,
                0.1
            ),
            Is.True
        );
        Assert.That(
            scheduler.Submit(
                new GameplayFeedbackEvent(FeedbackCode.MissingSkill, rifle),
                normal,
                0.2
            ),
            Is.False
        );
        Assert.That(scheduler.PendingCount, Is.Zero);
    }

    [Test]
    public void QueueIsBounded_Deduplicated_AndExpires()
    {
        scheduler.Submit(
            new GameplayFeedbackEvent(FeedbackCode.InventoryFull),
            new FeedbackPresentation { priority = 100, duration = 10 },
            0
        );
        var hint = new FeedbackPresentation
        {
            priority = 10,
            queueWhenBlocked = true,
            queueLifetime = 2,
        };
        var a = new GameplayFeedbackEvent(FeedbackCode.MissingSkill, pistol);
        Assert.That(scheduler.Submit(a, hint, 0), Is.True);
        Assert.That(scheduler.Submit(a, hint, 0), Is.False);
        Assert.That(
            scheduler.Submit(new GameplayFeedbackEvent(FeedbackCode.MissingSkill, rifle), hint, 0),
            Is.True
        );
        Assert.That(
            scheduler.Submit(
                new GameplayFeedbackEvent(FeedbackCode.KnowledgeAlreadyKnown, pistol),
                hint,
                0
            ),
            Is.True
        );
        Assert.That(
            scheduler.Submit(
                new GameplayFeedbackEvent(FeedbackCode.KnowledgeAlreadyKnown, rifle),
                hint,
                0
            ),
            Is.False
        );
        Assert.That(scheduler.PendingCount, Is.EqualTo(3));
        scheduler.Tick(2);
        Assert.That(scheduler.PendingCount, Is.Zero);
    }

    [Test]
    public void InventoryFull_CollapsesItemContext_AndUnlockInvalidatesRequirement()
    {
        var item = ScriptableObject.CreateInstance<WeaponItemData>();
        try
        {
            Assert.That(
                new GameplayFeedbackEvent(FeedbackCode.InventoryFull, item: item).Key,
                Is.EqualTo(new GameplayFeedbackEvent(FeedbackCode.InventoryFull).Key)
            );
            scheduler.Submit(
                new GameplayFeedbackEvent(FeedbackCode.MissingSkill, pistol),
                normal,
                0
            );
            scheduler.InvalidateSkill(pistol, 0.2);
            Assert.That(scheduler.HasActive, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(item);
        }
    }
}
