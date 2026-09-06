using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[TestFixture, Category("Core")]
public sealed class GrenadeAnimationTests
{
    private GameObject player, heldPrefab, thrownPrefab;
    private CharacterVisual actor;
    private PlayerAnimation animation;
    private PlayerGrenadeController grenades;
    private PlayerInventory inventory;
    private PlayerEquipment equipment;
    private PlayerShooter shooter;
    private WeaponInstance weapon;
    private WeaponItemData weaponData;
    private GrenadeItemData grenade;
    private SkillData missingSkill;
    private Animator animator;
    private float previousScale;

    [SetUp]
    public void SetUp()
    {
        previousScale = Time.timeScale; Time.timeScale = 1f;
        player = new GameObject("Grenade animation fixture");
        player.AddComponent<StarterAssetsInputs>();
        inventory = player.AddComponent<PlayerInventory>();
        var character = player.AddComponent<PlayerCharacter>();
        actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterVisual>(
            "Assets/Game/Prefabs/Player/Characters/Amy.prefab"), player.transform);
        typeof(PlayerCharacter).GetProperty("ActiveVisual").GetSetMethod(true)
            .Invoke(character, new object[] { actor });
        animator = actor.Animator;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind(); animator.Update(0f);
        equipment = player.AddComponent<PlayerEquipment>();
        shooter = player.AddComponent<PlayerShooter>();
        weaponData = ScriptableObject.CreateInstance<WeaponItemData>();
        weaponData.animationStyle = WeaponAnimationStyle.Rifle;
        var weaponObject = new GameObject("Existing equipped weapon", typeof(WeaponInstance));
        weaponObject.transform.SetParent(actor.WeaponSocket);
        weapon = weaponObject.GetComponent<WeaponInstance>();
        Set(equipment, "equippedWeapon", weaponData);
        Set(equipment, "equippedWeaponInstance", weapon);
        Set(shooter, "currentAmmo", 7); Set(shooter, "isReloading", true);
        animation = player.AddComponent<PlayerAnimation>();
        Call(animation, "Awake"); Call(animation, "OnEnable");
        heldPrefab = new GameObject("Held fixture", typeof(HeldItemGrip));
        var grip = new GameObject("Grip"); grip.transform.SetParent(heldPrefab.transform);
        Set(heldPrefab.GetComponent<HeldItemGrip>(), "gripPoint", grip.transform);
        thrownPrefab = new GameObject("Thrown fixture", typeof(SphereCollider), typeof(GrenadeInstance));
        grenade = ScriptableObject.CreateInstance<GrenadeItemData>();
        missingSkill = ScriptableObject.CreateInstance<SkillData>();
        grenade.requiredSkill = missingSkill; // Real inert path, no effect execution.
        grenade.heldPrefab = heldPrefab; grenade.worldPrefab = heldPrefab;
        grenade.thrownPrefab = thrownPrefab.GetComponent<GrenadeInstance>();
        grenade.minThrowSpeed = 2f; grenade.maxThrowSpeed = 12f; grenade.maxChargeTime = 2f;
        inventory.TryAddItem(grenade); inventory.TryAddItem(grenade);
        grenades = player.AddComponent<PlayerGrenadeController>();
        Call(grenades, "Awake"); Call(grenades, "OnEnable");
        Assert.That(grenades.SelectGrenade(grenade), Is.True);
        Assert.That(grenades.BeginCharge(), Is.True);
        Set(grenades, "chargeTime", 1.5f);
    }

    [TearDown]
    public void TearDown()
    {
        if (grenades != null) Call(grenades, "OnDisable");
        if (animation != null) Call(animation, "OnDisable");
        foreach (GrenadeInstance instance in Launched()) Object.DestroyImmediate(instance.gameObject);
        Object.DestroyImmediate(player); Object.DestroyImmediate(heldPrefab); Object.DestroyImmediate(thrownPrefab);
        Object.DestroyImmediate(grenade); Object.DestroyImmediate(missingSkill); Object.DestroyImmediate(weaponData);
        Time.timeScale = previousScale;
    }

    [Test]
    public void ReleaseStartsAnimation_MarkerCommitsOnce_WithSnapshotAndEquipmentPreserved()
    {
        int feedback = 0;
        player.AddComponent<PlayerFeedback>().Reported += message =>
        {
            Assert.That(grenades.IsGrenadeSelected, Is.False, "Cleanup precedes inert feedback.");
            Assert.That(message.Code, Is.EqualTo(FeedbackCode.GrenadeThrownInert));
            feedback++;
        };
        Assert.That(grenades.ReleaseThrow(), Is.True);
        Assert.That(grenades.IsThrowing, Is.True);
        Assert.That(grenades.IsCharging, Is.False);
        Assert.That(inventory.GrenadeCount, Is.EqualTo(2));
        Assert.That(Launched(), Is.Empty);
        Assert.That(actor.GetComponentInChildren<HeldItemGrip>(), Is.Not.Null);
        Assert.That(weapon.gameObject.activeSelf, Is.False);
        Assert.That(Get<bool>(shooter, "fireBlocked"), Is.True);
        Assert.That(grenades.BeginCharge(), Is.False);
        Assert.That(grenades.ReleaseThrow(), Is.False);
        Assert.That(grenades.SelectGrenade(grenade), Is.False);
        Set(grenades, "chargeTime", 0f); // The committed snapshot must remain 75%.
        AdvanceThroughMarker();
        Assert.That(grenades.IsGrenadeSelected, Is.False);
        Assert.That(inventory.GrenadeCount, Is.EqualTo(1));
        Assert.That(Launched().Length, Is.EqualTo(1));
        Assert.That(Launched()[0].GetComponent<Rigidbody>().linearVelocity.magnitude, Is.EqualTo(9.5f).Within(0.001f));
        Assert.That(Launched()[0].IsArmed, Is.False);
        Assert.That(weapon.gameObject.activeSelf, Is.True);
        Assert.That(equipment.EquippedWeaponInstance, Is.SameAs(weapon));
        Assert.That(equipment.EquippedWeapon, Is.SameAs(weaponData));
        Assert.That(Get<int>(shooter, "currentAmmo"), Is.EqualTo(7));
        Assert.That(Get<bool>(shooter, "isReloading"), Is.True);
        Assert.That(Get<bool>(shooter, "fireBlocked"), Is.False);
        Call(grenades, "HandleAnimationEvent", CharacterAnimationEventId.GrenadeRelease);
        AdvanceThroughMarker();
        Assert.That(inventory.GrenadeCount, Is.EqualTo(1));
        Assert.That(Launched().Length, Is.EqualTo(1));
        Assert.That(feedback, Is.EqualTo(1));
    }

    [TestCase(30)]
    [TestCase(60)]
    public void NativeMarkerLaunchesFromTheExtendedHand(int framesPerSecond)
    {
        animator.SetInteger("WeaponStyle", 0);
        animator.Update(0);
        Assert.That(grenades.ReleaseThrow(), Is.True);
        for (int frame = 0; frame < framesPerSecond * 2 && grenades.IsThrowing; frame++)
            animator.Update(1f / framesPerSecond);
        var launched = Launched();
        Assert.That(launched.Length, Is.EqualTo(1));
        Vector3 hand = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
        Vector3 shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
        Assert.That(Vector3.Dot(launched[0].transform.position - shoulder, actor.transform.forward),
            Is.GreaterThan(.30f), "Native event must launch from a hand extended in front of the shoulder.");
        Assert.That(Vector3.Distance(launched[0].transform.position, hand), Is.LessThan(.08f),
            "The gameplay release position must follow the animated hand at the marker.");
    }

    [TestCase("mapping")]
    [TestCase("facade")]
    [TestCase("relay")]
    public void MissingAnimationContractFallsBackToSamePhysicalThrow(string unavailable)
    {
        if (unavailable == "mapping")
        {
            Set(actor, "animationActions", null);
            Call(animation, "OnCharacterChanged", actor);
        }
        if (unavailable == "facade") Set(grenades, "playerAnimation", null);
        if (unavailable == "relay") actor.Animator.GetComponent<CharacterAnimationEventRelay>().enabled = false;
        Assert.That(grenades.ReleaseThrow(), Is.True);
        Assert.That(grenades.IsGrenadeSelected, Is.False);
        Assert.That(inventory.GrenadeCount, Is.EqualTo(1));
        Assert.That(Launched().Length, Is.EqualTo(1));
    }

    [TestCase("cancel")]
    [TestCase("disable")]
    [TestCase("character")]
    [TestCase("equipment")]
    public void CancellationBeforeMarkerCannotLaunchFromOldAnimation(string reason)
    {
        Assert.That(grenades.ReleaseThrow(), Is.True);
        animator.Update(0.1f); // Enter wind-up, before the authored marker.
        if (reason == "cancel") grenades.CancelThrow();
        if (reason == "disable") Call(grenades, "OnDisable");
        if (reason == "character")
        {
            Call(animation, "OnCharacterChanged", (object)null);
            Call(grenades, "HandleCharacterChanged", (object)null);
        }
        if (reason == "equipment") Call(grenades, "HandleEquippedWeaponChanged", weaponData);
        AdvanceThroughMarker();
        Assert.That(grenades.IsGrenadeSelected, Is.False);
        Assert.That(inventory.GrenadeCount, Is.EqualTo(2));
        Assert.That(Launched(), Is.Empty);
    }

    [Test]
    public void InterruptedAnimationReturnsToHeld_NoConsumptionOrStuckThrow()
    {
        Assert.That(grenades.ReleaseThrow(), Is.True);
        animator.Update(0.1f); Call(animation, "LateUpdate");
        int layer = animator.GetLayerIndex("GrenadeThrow");
        animator.Play("GrenadeThrow.Idle", layer); animator.Update(0f);
        Call(animation, "LateUpdate");
        Assert.That(grenades.IsGrenadeSelected, Is.True);
        Assert.That(grenades.IsThrowing, Is.False);
        Assert.That(grenades.BeginCharge(), Is.True);
        Assert.That(inventory.GrenadeCount, Is.EqualTo(2));
        AdvanceThroughMarker();
        Assert.That(Launched(), Is.Empty);
    }

    [Test]
    public void CancelThenReselectCannotReuseTheCancelledPerformancesMarker()
    {
        Assert.That(grenades.ReleaseThrow(), Is.True);
        animator.Update(0.1f);
        grenades.CancelThrow();
        Assert.That(grenades.SelectGrenade(grenade), Is.True);
        Assert.That(grenades.BeginCharge(), Is.True);
        // The previous animation is still in wind-up. A new request safely
        // uses immediate fallback instead of inheriting its pending marker.
        Assert.That(grenades.ReleaseThrow(), Is.True);
        Assert.That(Launched().Length, Is.EqualTo(1));
        AdvanceThroughMarker();
        Assert.That(inventory.GrenadeCount, Is.EqualTo(1));
        Assert.That(Launched().Length, Is.EqualTo(1));
    }

    [Test]
    public void PauseDuringThrowingDefersMarkerUntilResume_WithoutLosingCharge()
    {
        Assert.That(grenades.ReleaseThrow(), Is.True);
        var suspension = player.AddComponent<GameplaySuspensionController>();
        var lease = suspension.Acquire(SuspensionReason.ManualPause);
        grenades.CancelCharge(); // Existing router cancellation leaves committed wind-up intact.
        AdvanceThroughMarker(); // Even an externally sampled paused Animator cannot launch.
        Assert.That(grenades.IsThrowing, Is.True);
        Assert.That(inventory.GrenadeCount, Is.EqualTo(2));
        Assert.That(Launched(), Is.Empty);
        lease.Dispose(); Call(grenades, "Update");
        Assert.That(Launched().Length, Is.EqualTo(1));
        Assert.That(Launched()[0].GetComponent<Rigidbody>().linearVelocity.magnitude, Is.EqualTo(9.5f).Within(0.001f));
    }

    private void AdvanceThroughMarker()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CharacterAnimationSetup.ThrowClipPath);
        var marker = clip.events.Single(e => e.functionName == nameof(CharacterAnimationEventRelay.OnCharacterAnimationEvent));
        // Read the authored marker, not an animation frame/pose expectation.
        animator.Update(0.01f);
        animator.Update(marker.time + 0.1f);
    }

    private GrenadeInstance[] Launched() => Object.FindObjectsByType<GrenadeInstance>()
        .Where(instance => instance != thrownPrefab.GetComponent<GrenadeInstance>() && instance.IsLaunched
            && Get<GameObject>(instance, "owner") == player).ToArray();
    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static T Get<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}
