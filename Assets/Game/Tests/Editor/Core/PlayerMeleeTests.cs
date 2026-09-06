using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[TestFixture, Category("Core")]
public sealed class PlayerMeleeTests
{
    private GameObject player;
    private CharacterVisual actor;
    private PlayerMeleeController melee;
    private PlayerAnimation animation;
    private PlayerSkillState skills;
    private PlayerEquipment equipment;
    private PlayerInventory inventory;
    private PlayerGrenadeController grenades;
    private PlayerPrimaryActionRouter router;
    private PlayerShooter shooter;
    private StarterAssetsInputs input;
    private GameplaySuspensionController suspension;
    private UnarmedCombatItemData item;
    private float oldTimeScale;
    private readonly List<GameObject> objects = new();
    private readonly List<CharacterActionId> requests = new();

    [SetUp]
    public void SetUp()
    {
        oldTimeScale = Time.timeScale; Time.timeScale = 1;
        PlayerSkillState.ResetRuntimeSkills();
        item = AssetDatabase.LoadAssetAtPath<UnarmedCombatItemData>(UnarmedCombatSetup.ItemPath);
        player = new GameObject("Melee fixture"); player.transform.position = new Vector3(1000, 0, 1000);
        input = player.AddComponent<StarterAssetsInputs>();
        skills = player.AddComponent<PlayerSkillState>();
        inventory = player.AddComponent<PlayerInventory>();
        var character = player.AddComponent<PlayerCharacter>();
        actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterVisual>("Assets/Game/Prefabs/Player/Characters/Amy.prefab"), player.transform);
        typeof(PlayerCharacter).GetProperty("ActiveVisual").GetSetMethod(true).Invoke(character, new object[] { actor });
        actor.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        actor.Animator.Rebind(); actor.Animator.Update(0);
        equipment = player.AddComponent<PlayerEquipment>();
        shooter = player.AddComponent<PlayerShooter>();
        Set(equipment, "playerShooter", shooter); Call(equipment, "Awake");
        animation = player.AddComponent<PlayerAnimation>(); Call(animation, "Awake"); Call(animation, "OnEnable");
        grenades = player.AddComponent<PlayerGrenadeController>(); Call(grenades, "Awake"); Call(grenades, "OnEnable");
        suspension = player.AddComponent<GameplaySuspensionController>(); Call(suspension, "Awake");
        melee = player.AddComponent<PlayerMeleeController>(); Set(melee, "defaultCombatItem", item);
        Call(melee, "Awake"); Call(melee, "OnEnable"); melee.AttackRequested += requests.Add;
        router = player.AddComponent<PlayerPrimaryActionRouter>(); Call(router, "Awake"); Call(router, "OnEnable");
        Call(inventory, "Awake");
    }

    [TearDown]
    public void TearDown()
    {
        Call(router, "OnDisable"); Call(melee, "OnDisable"); Call(grenades, "OnDisable"); Call(animation, "OnDisable");
        suspension.ReleaseAll();
        Object.DestroyImmediate(player);
        foreach (var go in objects) if (go != null)
        {
            var target = go.GetComponent<AimTarget>();
            if (target != null) Call(target, "OnDisable");
            Object.DestroyImmediate(go);
        }
        objects.Clear(); requests.Clear();
        PlayerSkillState.ResetRuntimeSkills(); Time.timeScale = oldTimeScale;
    }

    [Test]
    public void MissingSkillBlocksFireAndSelection_EvenBesideTarget()
    {
        Target(1); input.ShootInput(true); Call(melee, "Update");
        Assert.That(melee.IsEligible, Is.False); Assert.That(melee.IsCombatStance, Is.False);
        Assert.That(melee.SelectCombatItem(item), Is.False); Assert.That(requests, Is.Empty);
    }

    [Test]
    public void PlainUnarmedTapEntersStanceAndRequestsAnAirPunch_HoldingDoesNotRepeat()
    {
        Learn(); input.ShootInput(true); input.ShootInput(true);
        Assert.That(melee.IsCombatStance, Is.True); Assert.That(requests, Is.EqualTo(new[] { CharacterActionId.MeleeLight1 }));
        Assert.That(melee.HasBufferedAttack, Is.False);
        Impact(); Assert.That(melee.IsWaitingForImpact, Is.False);
        input.ShootInput(false); input.ShootInput(true);
        Assert.That(requests.Last(), Is.EqualTo(CharacterActionId.MeleeLight2));
    }

    [TestCase("PlasmaPistolItem")]
    [TestCase("PlasmaRifleItem")]
    public void EquippedWeaponOwnsFire_EvenWithNearbyEnemy(string weapon)
    {
        Learn(); Target(1);
        Set(equipment, "equippedWeapon", AssetDatabase.LoadAssetAtPath<WeaponItemData>($"Assets/Game/Items/Weapons/{weapon}.asset"));
        input.ShootInput(true); Call(melee, "Update");
        Assert.That(Get<bool>(shooter, "triggerHeld"), Is.True);
        Assert.That(melee.IsCombatStance, Is.False); Assert.That(requests, Is.Empty);
        input.ShootInput(false); Assert.That(Get<bool>(shooter, "triggerHeld"), Is.False);
    }

    [Test]
    public void GrenadeOwnsFire_PointerCancellationCancelsChargeButKeepsSelection()
    {
        Learn(); Target(1); SelectGrenade(); input.ShootInput(true);
        Assert.That(grenades.IsCharging, Is.True); Assert.That(requests, Is.Empty);
        input.CancelShootInput();
        Assert.That(grenades.IsGrenadeSelected, Is.True); Assert.That(grenades.IsCharging, Is.False);
        Assert.That(melee.IsCombatStance, Is.False);
    }

    [Test]
    public void SelectingFightingUsesInventoryAndUnequipsWeapon_SelectionOutlivesStance()
    {
        Learn(); Set(equipment, "equippedWeapon", AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/PlasmaPistolItem.asset"));
        inventory.TryAddItem(item); inventory.UseItem(0);
        Assert.That(equipment.EquippedWeapon, Is.Null); Assert.That(melee.SelectedItem, Is.SameAs(item));
        Assert.That(melee.IsCombatStance, Is.True);
        Set(melee, "lastInputTime", Time.time - 3.1f); Call(melee, "Update");
        Assert.That(melee.IsCombatStance, Is.False); Assert.That(melee.SelectedItem, Is.SameAs(item));
    }

    [Test]
    public void SelectingFightingCancelsSelectedGrenade()
    {
        Learn(); SelectGrenade(); inventory.TryAddItem(item); inventory.UseItem(1);
        Assert.That(grenades.IsGrenadeSelected, Is.False); Assert.That(melee.SelectedItem, Is.SameAs(item));
        Assert.That(melee.IsCombatStance, Is.True);
    }

    [Test]
    public void ProximityHasHysteresis_KeepsStancePastTimeout_AndExitsWhenBothConditionsExpire()
    {
        Learn(); var target = Target(2);
        Call(melee, "Update"); Assert.That(melee.IsCombatStance, Is.True);
        Set(melee, "lastInputTime", Time.time - 10); Scan(); Assert.That(melee.IsCombatStance, Is.True);
        target.transform.position = player.transform.position + new Vector3(0, 1, 2.55f); Physics.SyncTransforms();
        Scan(); Assert.That(melee.IsCombatStance, Is.True, "The collider is still inside the exit threshold.");
        target.transform.position = player.transform.position + new Vector3(0, 1, 3); Physics.SyncTransforms();
        Scan(); Assert.That(melee.IsCombatStance, Is.False);
        Assert.That(melee.TryAttack(), Is.True); Impact(); Set(melee, "lastInputTime", Time.time - 2.9f); Scan();
        Assert.That(melee.IsCombatStance, Is.True, "Recent FIRE holds stance without a nearby target.");
    }

    [Test]
    public void ImpactAppliesOneHitAndReaction_DuplicateAndLateMarkersAreIgnored()
    {
        Learn(); var target = Target(1); var probe = target.GetComponent<MeleeImpactProbe>();
        Assert.That(melee.TryAttack(), Is.True); Assert.That(probe.DamageCount, Is.Zero);
        Impact(); Assert.That(probe.DamageCount, Is.EqualTo(1)); Assert.That(probe.ReactionCount, Is.EqualTo(1));
        Assert.That(probe.LastHit.Damage, Is.EqualTo(10)); Assert.That(probe.LastHit.Instigator, Is.SameAs(player));
        Call(animation, "HandleMarker", CharacterAnimationEventId.MeleeImpact, State(CharacterActionId.MeleeLight1));
        Call(melee, "HandleImpact", CharacterAnimationEventId.MeleeImpact);
        Assert.That(probe.DamageCount, Is.EqualTo(1));
    }

    [TestCase("wall")]
    [TestCase("far")]
    [TestCase("behind")]
    [TestCase("beside")]
    public void PhysicsRangeAndForwardVolumePreventInvalidHits(string situation)
    {
        Learn(); var target = Target(situation == "far" ? 4 : situation == "behind" ? -1 : 1);
        if (situation == "beside") target.transform.position = player.transform.position + new Vector3(1, 1, 0);
        if (situation == "wall")
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(wall);
            wall.transform.position = player.transform.position + new Vector3(0, 1, .5f); wall.transform.localScale = new Vector3(2, 2, .1f);
        }
        Physics.SyncTransforms(); melee.TryAttack(); Impact();
        Assert.That(target.GetComponent<MeleeImpactProbe>().DamageCount, Is.Zero);
    }

    [Test]
    public void OnePunchHitsAtMostOneOfSeveralTargets()
    {
        Learn(); var first = Target(.9f); var second = Target(1.2f);
        melee.TryAttack(); Impact();
        Assert.That(first.GetComponent<MeleeImpactProbe>().DamageCount + second.GetComponent<MeleeImpactProbe>().DamageCount, Is.EqualTo(1));
    }

    [TestCase("cancel")]
    [TestCase("suspend")]
    [TestCase("disable")]
    [TestCase("character")]
    [TestCase("equipment")]
    [TestCase("grenade")]
    [TestCase("interrupted")]
    [TestCase("skill")]
    public void InvalidatedAttackNeverDealsDelayedDamage(string reason)
    {
        Learn(); var target = Target(1); melee.TryAttack(); actor.Animator.Update(.02f); Call(animation, "LateUpdate");
        GameplaySuspensionController.Lease lease = null;
        switch (reason)
        {
            case "cancel": melee.CancelCombat(); break;
            case "suspend": lease = suspension.Acquire(SuspensionReason.ManualPause); break;
            case "disable": melee.enabled = false; Call(melee, "OnDisable"); break;
            case "character": Call(animation, "OnCharacterChanged", (object)null); break;
            case "equipment":
                Set(equipment, "equippedWeapon", AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/PlasmaPistolItem.asset"));
                Call(melee, "HandleEquipment", equipment.EquippedWeapon); break;
            case "grenade": SelectGrenade(); break;
            case "interrupted":
                actor.Animator.Play("UnarmedCombatActions.Empty", actor.Animator.GetLayerIndex("UnarmedCombatActions"));
                actor.Animator.Update(0); Call(animation, "LateUpdate"); break;
            case "skill": PlayerSkillState.ResetRuntimeSkills(); Call(melee, "Update"); break;
        }
        for (int i = 0; i < 100; i++) actor.Animator.Update(.01f);
        Call(animation, "HandleMarker", CharacterAnimationEventId.MeleeImpact, State(CharacterActionId.MeleeLight1));
        Assert.That(target.GetComponent<MeleeImpactProbe>().DamageCount, Is.Zero);
        Assert.That(melee.IsWaitingForImpact, Is.False);
        lease?.Dispose();
    }

    [Test]
    public void OneBufferedTapChainsNextSemanticAction_ExtraTapsDoNotQueueMore()
    {
        Learn(); var target = Target(1); melee.TryAttack(); melee.TryAttack(); melee.TryAttack();
        Assert.That(melee.HasBufferedAttack, Is.True); Impact();
        Call(melee, "Update"); Assert.That(requests, Is.EqualTo(new[] { CharacterActionId.MeleeLight1, CharacterActionId.MeleeLight2 }));
        Call(animation, "HandleMarker", CharacterAnimationEventId.MeleeImpact, State(CharacterActionId.MeleeLight1));
        Assert.That(target.GetComponent<MeleeImpactProbe>().DamageCount, Is.EqualTo(1));
        Impact(); Call(melee, "Update"); Assert.That(requests.Count, Is.EqualTo(2));
        Assert.That(target.GetComponent<MeleeImpactProbe>().DamageCount, Is.EqualTo(2));
        Set(melee, "lastInputTime", Time.time - 4); target.SetTargetable(false); Scan();
        actor.Animator.Update(1); melee.TryAttack(); Assert.That(requests.Last(), Is.EqualTo(CharacterActionId.MeleeLight1));
    }

    [Test]
    public void MissingMappingFailsWithoutInvisibleDamageAndLeavesStanceUsable()
    {
        Learn(); var target = Target(1);
        Set(animation, "driver", new CharacterAnimatorDriver(actor.Animator));
        Assert.That(melee.TryAttack(), Is.False); Assert.That(melee.IsWaitingForImpact, Is.False);
        Assert.That(melee.IsCombatStance, Is.True); Assert.That(target.GetComponent<MeleeImpactProbe>().DamageCount, Is.Zero);
        Call(animation, "OnCharacterChanged", actor);
        Assert.That(melee.TryAttack(), Is.True);
    }

    [Test]
    public void BookUnlocksKnowledgeAndReplacesItsSlotWithFighting_WhenInventoryIsFull()
    {
        Set(inventory, "maxSlots", 1);
        var book = AssetDatabase.LoadAssetAtPath<KnowledgeBookItemData>(UnarmedCombatSetup.BookPath);
        Assert.That(inventory.TryAddItem(book), Is.True); inventory.UseItem(0);
        Assert.That(skills.HasSkill(item.requiredSkill), Is.True);
        Assert.That(inventory.Items.Count, Is.EqualTo(1)); Assert.That(inventory.Items[0], Is.SameAs(item));
        Assert.That(item.worldPrefab, Is.Null); inventory.UseItem(0); Assert.That(melee.SelectedItem, Is.SameAs(item));
    }

    private void Learn() => skills.UnlockSkill(item.requiredSkill);
    private void Scan() { Set(melee, "nextProximityCheck", 0f); Call(melee, "Update"); }
    private void Impact()
    {
        for (int i = 0; i < 240 && melee.IsWaitingForImpact; i++)
        { actor.Animator.Update(1f / 120); Call(animation, "LateUpdate"); }
        Assert.That(melee.IsWaitingForImpact, Is.False, "The authored marker must complete the attack.");
    }
    private int State(CharacterActionId action)
    {
        actor.AnimationActions.TryGetBinding(action, out var binding);
        return Animator.StringToHash(binding.statePath);
    }
    private AimTarget Target(float forward)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); objects.Add(go);
        go.transform.position = player.transform.position + new Vector3(0, 1, forward); go.transform.localScale = Vector3.one * .4f;
        go.AddComponent<MeleeImpactProbe>(); var target = go.AddComponent<AimTarget>(); target.CacheBodyData();
        Call(target, "OnEnable"); // EditMode fixtures explicitly run ordinary gameplay lifecycle hooks.
        Physics.SyncTransforms(); return target;
    }
    private void SelectGrenade()
    {
        var grenade = AssetDatabase.LoadAssetAtPath<GrenadeItemData>("Assets/Game/Data/Items/Grenades/ElectricGrenade.asset");
        inventory.TryAddItem(grenade); Assert.That(grenades.SelectGrenade(grenade), Is.True);
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
}

public sealed class MeleeImpactProbe : MonoBehaviour, IDamageable, IHitReaction
{
    public int DamageCount, ReactionCount;
    public HitInfo LastHit;
    public void ReceiveDamage(HitInfo hit) { DamageCount++; LastHit = hit; }
    public void ReceiveHit(HitInfo hit) => ReactionCount++;
}
