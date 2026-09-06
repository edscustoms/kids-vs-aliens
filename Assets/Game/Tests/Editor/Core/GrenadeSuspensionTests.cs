using System.Collections;
using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture, Category("Core")]
public sealed class GrenadeSuspensionTests
{
    private GameObject player,
        heldPrefab,
        thrownPrefab;
    private GrenadeItemData item;
    private StarterAssetsInputs input;
    private PlayerInventory inventory;
    private PlayerGrenadeController grenades;
    private PlayerPrimaryActionRouter router;
    private GameplaySuspensionController suspension;
    private float previousTimeScale;

    [SetUp]
    public void SetUp()
    {
        previousTimeScale = Time.timeScale;
        player = new GameObject("Grenade suspension player");
        input = player.AddComponent<StarterAssetsInputs>();
        inventory = player.AddComponent<PlayerInventory>();
        var character = player.AddComponent<PlayerCharacter>();
        var actor = new GameObject("Visual", typeof(Animator), typeof(CharacterVisual));
        actor.transform.SetParent(player.transform);
        var visual = actor.GetComponent<CharacterVisual>();
        Set(visual, "weaponSocket", actor.transform);
        typeof(PlayerCharacter)
            .GetProperty("ActiveVisual")
            .GetSetMethod(true)
            .Invoke(character, new object[] { visual });
        heldPrefab = new GameObject("Held fixture", typeof(HeldItemGrip));
        var grip = new GameObject("Grip");
        grip.transform.SetParent(heldPrefab.transform);
        Set(heldPrefab.GetComponent<HeldItemGrip>(), "gripPoint", grip.transform);
        thrownPrefab = new GameObject(
            "Thrown fixture",
            typeof(SphereCollider),
            typeof(GrenadeInstance)
        );
        item = ScriptableObject.CreateInstance<GrenadeItemData>();
        item.heldPrefab = heldPrefab;
        item.thrownPrefab = thrownPrefab.GetComponent<GrenadeInstance>();
        inventory.TryAddItem(item);
        grenades = player.AddComponent<PlayerGrenadeController>();
        router = player.AddComponent<PlayerPrimaryActionRouter>();
        suspension = player.AddComponent<GameplaySuspensionController>();
        // Ordinary MonoBehaviour lifecycle is not delivered by EditMode fixtures.
        Call(grenades, "Awake");
        Call(grenades, "OnEnable");
        Call(router, "Awake");
        Call(router, "OnEnable");
    }

    [TearDown]
    public void TearDown()
    {
        suspension.ReleaseAll();
        // Destroy scene fixtures immediately; normal runtime cleanup uses Destroy.
        Set(grenades, "heldVisual", null);
        Call(router, "OnDisable");
        Call(grenades, "OnDisable");
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(heldPrefab);
        Object.DestroyImmediate(thrownPrefab);
        Object.DestroyImmediate(item);
        Time.timeScale = previousTimeScale;
    }

    [UnityTest]
    public IEnumerator HeldGrenadeSurvivesSuspension() => VerifySuspension(false);

    [UnityTest]
    public IEnumerator ChargingGrenadeReturnsToHeldWithoutThrowOrConsumption() =>
        VerifySuspension(true);

    private IEnumerator VerifySuspension(bool charging)
    {
        Assert.That(grenades.SelectGrenade(item), Is.True);
        var held = player.GetComponentInChildren<HeldItemGrip>();
        int instances = Object.FindObjectsByType<GrenadeInstance>().Length;
        if (charging)
        {
            input.ShootInput(true);
            Assert.That(grenades.IsCharging, Is.True);
            Set(grenades, "chargeTime", 0.6f);
        }
        var manual = suspension.Acquire(SuspensionReason.ManualPause);
        AssertHeld(held, instances);
        Assert.That(grenades.BeginCharge(), Is.False);
        Assert.That(grenades.ReleaseThrow(), Is.False);
        input.ShootInput(true);
        manual.Dispose();
        AssertHeld(held, instances);
        yield return EditorTestFrame.Next();
        input.ShootInput(true); // A held FIRE must not restart the cancelled charge.
        AssertHeld(held, instances);
        input.ShootInput(false);
        AssertHeld(held, instances);
        input.ShootInput(true);
        Assert.That(grenades.IsCharging, Is.True, "Fresh input starts a new charge after resume.");
        Assert.That(inventory.GrenadeCount, Is.EqualTo(1));
    }

    private void AssertHeld(HeldItemGrip held, int instances)
    {
        Assert.That(grenades.IsGrenadeSelected, Is.True);
        Assert.That(grenades.SelectedGrenade, Is.SameAs(item));
        Assert.That(grenades.IsCharging, Is.False);
        Assert.That(grenades.Charge01, Is.Zero);
        Assert.That(player.GetComponentInChildren<HeldItemGrip>(), Is.SameAs(held));
        Assert.That(held.gameObject.activeSelf, Is.True);
        Assert.That(inventory.GrenadeCount, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<GrenadeInstance>().Length, Is.EqualTo(instances));
        Assert.That(grenades.enabled && router.enabled, Is.True);
    }

    private static void Set(object target, string name, object value) =>
        target
            .GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);

    private static void Call(object target, string name) =>
        target
            .GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);
}
