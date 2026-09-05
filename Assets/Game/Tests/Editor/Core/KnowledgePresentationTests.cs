using System.Collections;
using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture, Category("Core")]
public sealed class KnowledgePresentationTests
{
    private GameObject player;
    private PlayerSkillState state;
    private SkillData first,
        second;
    private KnowledgePresentationQueue queue;

    [SetUp]
    public void SetUp()
    {
        PlayerSkillState.ResetRuntimeSkills();
        player = new GameObject("Knowledge test player");
        state = player.AddComponent<PlayerSkillState>();
        first = CreateSkill("first");
        second = CreateSkill("second");
        queue = new KnowledgePresentationQueue();
        state.SkillUnlocked += skill => queue.Enqueue(skill, 10);
    }

    [TearDown]
    public void TearDown()
    {
        PlayerSkillState.ResetRuntimeSkills();
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(first);
        Object.DestroyImmediate(second);
    }

    private static SkillData CreateSkill(string id)
    {
        var skill = ScriptableObject.CreateInstance<SkillData>();
        var serialized = new SerializedObject(skill);
        serialized.FindProperty("id").stringValue = id;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return skill;
    }

    [Test]
    public void UnlockQueuesOnce_AndDefersUntilAfterAcquisitionFrame()
    {
        Assert.That(state.UnlockSkill(first), Is.True);
        Assert.That(state.UnlockSkill(first), Is.False);
        Assert.That(queue.Count, Is.EqualTo(1));
        Assert.That(queue.TryDequeue(10, out _), Is.False);
        Assert.That(queue.TryDequeue(11, out var skill), Is.True);
        Assert.That(skill, Is.SameAs(first));
        Assert.That(state.HasSkill(first), Is.True);
    }

    [Test]
    public void UniqueAcquisitionsPreserveOrder_WithoutTutorialData()
    {
        state.UnlockSkill(first);
        state.UnlockSkill(second);
        Assert.That(first.TutorialData, Is.Null);
        Assert.That(queue.TryDequeue(11, out var a), Is.True);
        Assert.That(queue.TryDequeue(11, out var b), Is.True);
        Assert.That(a, Is.SameAs(first));
        Assert.That(b, Is.SameAs(second));
        queue.Clear();
        Assert.That(state.HasSkill(first), Is.True);
        Assert.That(state.HasSkill(second), Is.True);
    }

    [UnityTest]
    public IEnumerator BookAcquisitionFinishesBeforeModal_AndClosingPreservesManualPauseAndKnowledge()
    {
        float previousTimeScale = Time.timeScale;
        player.AddComponent<StarterAssetsInputs>();
        var inventory = player.AddComponent<PlayerInventory>();
        Call(inventory, "Awake");
        var suspension = player.AddComponent<GameplaySuspensionController>();
        var presenter = player.AddComponent<KnowledgeAcquiredPresenter>();
        var view = new GameObject("Knowledge fallback view");
        var book = ScriptableObject.CreateInstance<KnowledgeBookItemData>();
        book.skill = first;
        book.itemType = ItemType.KnowledgeBook;
        var serialized = new SerializedObject(presenter);
        serialized.FindProperty("skills").objectReferenceValue = state;
        serialized.FindProperty("suspension").objectReferenceValue = suspension;
        serialized.FindProperty("view").objectReferenceValue = view;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Call(presenter, "OnEnable");
        try
        {
            Assert.That(inventory.TryAddItem(book), Is.True);
            inventory.UseItem(0);
            Assert.That(inventory.Items, Is.Empty);
            Assert.That(state.HasSkill(first), Is.True);
            Assert.That(presenter.PendingCount, Is.EqualTo(1));
            Call(presenter, "Update");
            Assert.That(view.activeSelf, Is.False, "No modal inside the inventory mutation frame.");
            var manual = suspension.Acquire(SuspensionReason.ManualPause);
            yield return null;
            Call(presenter, "Update");
            Assert.That(
                view.activeSelf,
                Is.True,
                "Missing tutorial data still permits acknowledgement."
            );
            Assert.That(presenter.CurrentSkill, Is.SameAs(first));
            Assert.That(suspension.OwnerCount, Is.EqualTo(2));
            presenter.Close();
            Assert.That(view.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(suspension.OwnerCount, Is.EqualTo(1));
            Assert.That(state.HasSkill(first), Is.True);
            manual.Dispose();
            Assert.That(Time.timeScale, Is.EqualTo(previousTimeScale));
            yield return null;
            Assert.That(inventory.TryAddItem(book), Is.True);
            inventory.UseItem(0);
            Assert.That(
                inventory.Items.Count,
                Is.EqualTo(1),
                "Already-known books are not consumed."
            );
            Assert.That(presenter.PendingCount, Is.Zero);
            Assert.That(new KnowledgePresentationQueue().Count, Is.Zero);
        }
        finally
        {
            Call(presenter, "OnDisable");
            suspension.ReleaseAll();
            Object.DestroyImmediate(view);
            Object.DestroyImmediate(book);
            Time.timeScale = previousTimeScale;
        }
    }

    private static void Call(object target, string name) =>
        target
            .GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
}
