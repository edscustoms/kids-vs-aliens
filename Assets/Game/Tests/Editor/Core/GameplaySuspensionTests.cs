using System.Collections;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture, Category("Core")]
public sealed class GameplaySuspensionTests
{
    private GameObject player;
    private GameplaySuspensionController suspension;
    private StarterAssetsInputs input;
    private float timeScale;

    [SetUp]
    public void SetUp()
    {
        timeScale = Time.timeScale;
        player = new GameObject("Suspension test player");
        input = player.AddComponent<StarterAssetsInputs>();
        suspension = player.AddComponent<GameplaySuspensionController>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(player);
        Time.timeScale = timeScale;
    }

    [Test]
    public void IndependentOwners_OnlyFinalReleaseRestoresPreviousTimeScale()
    {
        Time.timeScale = 0.7f;
        var manual = suspension.Acquire(SuspensionReason.ManualPause);
        var modal = suspension.Acquire(SuspensionReason.KnowledgePresentation);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(suspension.OwnerCount, Is.EqualTo(2));
        manual.Dispose();
        manual.Dispose();
        Assert.That(Time.timeScale, Is.Zero);
        modal.Dispose();
        Assert.That(Time.timeScale, Is.EqualTo(0.7f));
        Assert.That(suspension.OwnerCount, Is.Zero);
    }

    [Test]
    public void CleanupInvalidatesOutstandingLeases_AndRepeatedOwnershipWorks()
    {
        var old = suspension.Acquire(SuspensionReason.ManualPause);
        suspension.ReleaseAll();
        for (int i = 0; i < 5; i++)
        {
            var current = suspension.Acquire(SuspensionReason.ManualPause);
            old.Dispose();
            Assert.That(Time.timeScale, Is.Zero);
            current.Dispose();
            Assert.That(suspension.IsSuspended, Is.False);
        }
        suspension.Acquire(SuspensionReason.Modal);
        // EditMode does not run ordinary MonoBehaviour disable callbacks.
        // Invoke the actual lifecycle hook explicitly; device/PlayMode checks
        // cover Unity's scene teardown delivery.
        typeof(GameplaySuspensionController)
            .GetMethod(
                "OnDisable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )
            .Invoke(suspension, null);
        Assert.That(Time.timeScale, Is.EqualTo(timeScale));
        Assert.That(input.GameplayInputBlocked, Is.False);
    }

    [UnityTest]
    public IEnumerator SuspensionCancelsInsteadOfReleasing_AndResumeRequiresNeutral()
    {
        int releases = 0,
            cancels = 0,
            presses = 0;
        input.ShootStateChanged += down =>
        {
            if (down)
                presses++;
            else
                releases++;
        };
        input.ShootCanceled += () => cancels++;
        input.ShootInput(true);
        input.MoveInput(Vector2.up);
        var lease = suspension.Acquire(SuspensionReason.ManualPause);
        Assert.That(cancels, Is.EqualTo(1));
        Assert.That(releases, Is.Zero);
        Assert.That(input.shoot, Is.False);
        Assert.That(input.move, Is.EqualTo(Vector2.zero));
        lease.Dispose();
        input.ShootInput(true); // Same-frame UI dismissal cannot become FIRE.
        yield return EditorTestFrame.Next();
        input.ShootInput(true); // Still-held pointer must remain suppressed.
        Assert.That(presses, Is.EqualTo(1));
        input.ShootInput(false);
        Assert.That(releases, Is.Zero);
        input.ShootInput(true);
        Assert.That(presses, Is.EqualTo(2));
        input.MoveInput(Vector2.up);
        Assert.That(input.move, Is.EqualTo(Vector2.zero));
        input.MoveInput(Vector2.zero);
        input.MoveInput(Vector2.up);
        Assert.That(input.move, Is.EqualTo(Vector2.up));
    }

    [Test]
    public void DesktopPauseIntentWorksWhileBlocked_ButCannotToggleThroughModal()
    {
        var view = new GameObject("Pause test view");
        var manual = view.AddComponent<ManualPauseButton>();
        var flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(ManualPauseButton).GetField("suspension", flags).SetValue(manual, suspension);
        typeof(ManualPauseButton).GetField("input", flags).SetValue(manual, input);
        typeof(ManualPauseButton).GetMethod("OnEnable", flags).Invoke(manual, null);
        try
        {
            input.PauseInput();
            Assert.That(manual.OwnsManualPause, Is.True);
            var modal = suspension.Acquire(SuspensionReason.KnowledgePresentation);
            input.PauseInput();
            Assert.That(manual.OwnsManualPause, Is.True);
            Assert.That(suspension.OwnerCount, Is.EqualTo(2));
            modal.Dispose();
            input.PauseInput();
            Assert.That(manual.OwnsManualPause, Is.False);
            Assert.That(suspension.IsSuspended, Is.False);
            input.PauseInput();
            suspension.ReleaseAll();
            Assert.That(
                manual.OwnsManualPause,
                Is.False,
                "Teardown invalidates manual ownership too."
            );
        }
        finally
        {
            typeof(ManualPauseButton).GetMethod("OnDisable", flags).Invoke(manual, null);
            Object.DestroyImmediate(view);
        }
    }

    [Test]
    public void LegacyEnemyCannotDealImmediateMeleeDamageWhileSuspended()
    {
        var enemy = new GameObject("Legacy enemy pause fixture");
        var health = player.AddComponent<PlayerHealth>();
        var movement = enemy.AddComponent<EnemyMovement>();
        var flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(PlayerHealth).GetMethod("Awake", flags).Invoke(health, null);
        typeof(EnemyMovement).GetField("player", flags).SetValue(movement, player.transform);
        typeof(EnemyMovement).GetField("playerHealth", flags).SetValue(movement, health);
        var update = typeof(EnemyMovement).GetMethod("Update", flags);
        int hits = 0;
        health.OnHealthChanged += () => hits++;
        try
        {
            var lease = suspension.Acquire(SuspensionReason.ManualPause);
            update.Invoke(movement, null);
            Assert.That(hits, Is.Zero);
            lease.Dispose();
            update.Invoke(movement, null);
            Assert.That(
                hits,
                Is.EqualTo(1),
                "Normal legacy melee remains functional after resume."
            );
        }
        finally
        {
            Object.DestroyImmediate(enemy);
        }
    }
}
