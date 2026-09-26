using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[Category("CombatEconomy")]
public sealed class CombatEconomyTests
{
    internal const string PlasmaPath = "Assets/Game/Items/Resources/PlasmaCapsule.asset";
    internal const string ArmorPath = "Assets/Game/Items/Resources/ArmorCapsule.asset";
    internal const string PlasmaPrefabPath =
        "Assets/Game/Prefabs/Items/Resources/PlasmaCapsule.prefab";
    internal const string ArmorPrefabPath =
        "Assets/Game/Prefabs/Items/Resources/ArmorCapsule.prefab";

    private readonly List<Object> owned = new();
    private PlayerInventory inventory;
    private PlayerShooter shooter;
    private PlayerEquipment equipment;
    private WeaponItemData pistol,
        rifle;

    [SetUp]
    public void Setup()
    {
        var root = new GameObject("Economy inventory");
        owned.Add(root);
        equipment = root.AddComponent<PlayerEquipment>();
        inventory = root.AddComponent<PlayerInventory>();
        shooter = root.AddComponent<PlayerShooter>();
        Set(inventory, "playerEquipment", equipment);
        pistol = Weapon("Pistol");
        rifle = Weapon("Rifle");
        inventory.EnsureOwnedWeapon(pistol);
        inventory.EnsureOwnedWeapon(rifle);
    }

    internal static WeaponItemData Weapon(string name) =>
        AssetDatabase.LoadAssetAtPath<WeaponItemData>(
            "Assets/Game/Items/Weapons/Plasma" + name + "Item.asset"
        );

    internal static void Set(object target, string field, object value) =>
        target
            .GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);

    internal static void Invoke(object target, string method) =>
        target
            .GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, null);

    private void Select(WeaponItemData weapon)
    {
        // This isolated inventory fixture has no character/mounted visual. Real
        // selection and automatic reload are exercised by CombatEconomyStateTests.
        Set(equipment, "equippedWeapon", weapon);
        shooter.EquipWeapon(weapon, inventory.transform);
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (var obj in owned)
            if (obj != null)
                Object.DestroyImmediate(obj);
        owned.Clear();
    }

    [TestCase("Pistol", 12, 3, 6)]
    [TestCase("Rifle", 28, 6, 12)]
    public void AuthoredWeaponsCarryEconomyAndSemanticAudio(
        string name,
        int capacity,
        int cost,
        int reward
    )
    {
        var weapon = Weapon(name);
        Assert.That(weapon.magazineSize, Is.EqualTo(capacity));
        Assert.That(weapon.plasmaReloadCost, Is.EqualTo(cost));
        Assert.That(weapon.duplicatePlasmaReward, Is.EqualTo(reward));
        Assert.That(weapon.usesPlasmaCapsules, Is.True);
        Assert.That(weapon.dryFireSound, Is.Not.Null);
        Assert.That(weapon.dryFireSound.ClipCount, Is.GreaterThan(0));
        Assert.That(
            AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioAssets.LibraryPath).events,
            Does.Contain(weapon.dryFireSound)
        );
        Assert.That(pistol.damage, Is.GreaterThan(rifle.damage));
        Assert.That(rifle.fireRate, Is.GreaterThan(pistol.fireRate * 3));
        Assert.That(
            pistol.damage * pistol.magazineSize / pistol.plasmaReloadCost,
            Is.GreaterThan(rifle.damage * rifle.magazineSize / rifle.plasmaReloadCost)
        );
    }

    [TestCase("Pistol")]
    [TestCase("Rifle")]
    public void EmptyReloadChargesOnceAndRefillsOnlyItsOwnedMagazine(string name)
    {
        var weapon = Weapon(name);
        var state = inventory.GetWeaponState(weapon);
        Select(weapon);
        state.Restore(0, 100);
        inventory.RestoreCapsules(20, 4);
        Assert.That(inventory.TryBeginReload(weapon, 100), Is.True);
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(20 - weapon.plasmaReloadCost));
        Assert.That(inventory.ArmorCapsules, Is.EqualTo(4));
        Assert.That(inventory.TryBeginReload(weapon, 100), Is.False);
        Assert.That(state.FinishReload(100 + weapon.reloadTime - .01f), Is.False);
        Assert.That(state.FinishReload(100 + weapon.reloadTime), Is.True);
        Assert.That(state.Rounds, Is.EqualTo(weapon.magazineSize));
        Assert.That(state.TrySpendRound(101 + weapon.reloadTime), Is.True);
        Assert.That(state.Rounds, Is.EqualTo(weapon.magazineSize - 1));
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(20 - weapon.plasmaReloadCost));
    }

    [TestCase("Pistol")]
    [TestCase("Rifle")]
    public void InsufficientOrPartialMagazineNeverStartsOrSpends(string name)
    {
        var weapon = Weapon(name);
        var state = inventory.GetWeaponState(weapon);
        Select(weapon);
        inventory.RestoreCapsules(weapon.plasmaReloadCost - 1, 99);
        state.Restore(0, 100);
        Assert.That(inventory.TryBeginReload(weapon, 100), Is.False);
        Assert.That(state.IsReloading, Is.False);
        inventory.RestoreCapsules(50, 99);
        state.Restore(4, 100);
        Assert.That(inventory.TryBeginReload(weapon, 100), Is.False);
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(50));
    }

    [Test]
    public void SwapReselectAndPaidReloadKeepIndependentState()
    {
        inventory.RestoreCapsules(20, 0);
        var p = inventory.GetWeaponState(pistol);
        var r = inventory.GetWeaponState(rifle);
        p.Restore(4, Time.time);
        r.Restore(13, Time.time);
        Select(pistol);
        Select(pistol);
        Select(rifle);
        Select(pistol);
        Assert.That(p.Rounds, Is.EqualTo(4));
        Assert.That(r.Rounds, Is.EqualTo(13));
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(20));
        p.Restore(0, Time.time);
        Assert.That(inventory.TryBeginReload(pistol, Time.time), Is.True);
        Select(rifle);
        Select(pistol);
        Assert.That(p.IsReloading, Is.True);
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(17));
    }

    [Test]
    public void AbsoluteResourceRestoreAndLegacyMissingFieldsNeverReward()
    {
        inventory.RestoreCapsules(17, 3);
        inventory.RestoreCapsules(17, 3);
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(17));
        Assert.That(inventory.ArmorCapsules, Is.EqualTo(3));
        var legacy = JsonUtility.FromJson<SavedPlayer>("{\"ammo\":4}");
        inventory.RestoreCapsules(legacy.plasmaCapsules, legacy.armorCapsules);
        inventory.RestoreWeaponStates(legacy.weapons, pistol, legacy.ammo);
        Assert.That(inventory.PlasmaCapsules, Is.Zero);
        Assert.That(inventory.ArmorCapsules, Is.Zero);
        Assert.That(inventory.GetWeaponState(pistol).Rounds, Is.EqualTo(4));
        Assert.That(inventory.GetWeaponState(rifle).Rounds, Is.EqualTo(28));
    }

    [Test]
    public void ResourceOwnershipDoesNotUseBackpackSlots()
    {
        Set(inventory, "maxSlots", 2);
        var plasma = AssetDatabase.LoadAssetAtPath<CapsuleItemData>(PlasmaPath);
        var armor = AssetDatabase.LoadAssetAtPath<CapsuleItemData>(ArmorPath);
        Assert.That(inventory.TryAddItem(plasma), Is.True);
        Assert.That(inventory.TryAddItem(armor), Is.True);
        Assert.That(inventory.Items.Count, Is.EqualTo(2));
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(1));
        Assert.That(inventory.ArmorCapsules, Is.EqualTo(1));
        var policy = new FeedbackPresentation { template = "+{amount} PLASMA" };
        Assert.That(
            FeedbackPresentationCatalog.Format(
                new GameplayFeedbackEvent(FeedbackCode.PlasmaCollected, amount: 6),
                policy
            ),
            Is.EqualTo("+6 PLASMA")
        );
    }

    [Test]
    public void RepeatedCapsuleToastsKeepEveryRewardInTheExistingScheduler()
    {
        var scheduler = new FeedbackScheduler();
        var policy = new FeedbackPresentation
        {
            template = "+{amount} PLASMA",
            cooldown = 0,
            duration = 2,
        };
        Assert.That(
            scheduler.Submit(
                new GameplayFeedbackEvent(
                    FeedbackCode.PlasmaCollected,
                    item: pistol,
                    action: FeedbackAction.Pickup,
                    amount: 6
                ),
                policy,
                0
            ),
            Is.True
        );
        Assert.That(
            scheduler.Submit(
                new GameplayFeedbackEvent(
                    FeedbackCode.PlasmaCollected,
                    item: rifle,
                    action: FeedbackAction.Pickup,
                    amount: 12
                ),
                policy,
                .2
            ),
            Is.True
        );
        Assert.That(
            FeedbackPresentationCatalog.Format(scheduler.Active, policy),
            Is.EqualTo("+18 PLASMA")
        );
    }
}

[Category("CombatEconomy")]
public sealed class CombatEconomyPlayTests
{
    [UnitySetUp]
    public IEnumerator Setup()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying)
            yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DuplicateAndResourcePickupsConsumeExactlyOnceAndLootRestoresAbsolutely()
    {
        var root = new GameObject("Collector", typeof(PlayerInventory), typeof(PlayerFeedback));
        var inventory = root.GetComponent<PlayerInventory>();
        int notifications = 0;
        root.GetComponent<PlayerFeedback>().Reported += _ => notifications++;
        foreach (string name in new[] { "Pistol", "Rifle" })
        {
            var weapon = CombatEconomyTests.Weapon(name);
            inventory.EnsureOwnedWeapon(weapon);
            int before = inventory.PlasmaCapsules;
            var pickup = Object.Instantiate(weapon.worldPrefab).GetComponent<PickupItem>();
            var world =
                pickup.GetComponent<RunWorldObject>()
                ?? pickup.gameObject.AddComponent<RunWorldObject>();
            world.ConfigureIdentity(name);
            Assert.That(pickup.TryCollect(inventory), Is.True);
            Assert.That(pickup.TryCollect(inventory), Is.False);
            Assert.That(world.Capture().removed, Is.True);
            Assert.That(
                inventory.PlasmaCapsules,
                Is.EqualTo(before + weapon.duplicatePlasmaReward)
            );
            Assert.That(inventory.Items.Count(item => item == weapon), Is.EqualTo(1));
        }

        var capsule = Object
            .Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(CombatEconomyTests.ArmorPrefabPath)
            )
            .GetComponent<PickupItem>();
        capsule.SetQuantity(3);
        string quantity = capsule.CaptureRunState();
        capsule.SetQuantity(1);
        capsule.RestoreRunState(quantity);
        capsule.RestoreRunState(quantity);
        Assert.That(capsule.TryCollect(inventory), Is.True);
        Assert.That(capsule.TryCollect(inventory), Is.False);
        Assert.That(inventory.ArmorCapsules, Is.EqualTo(3));
        Assert.That(notifications, Is.EqualTo(3));

        var enemy = new GameObject("Loot owner", typeof(EnemyPlasmaLoot));
        var loot = enemy.GetComponent<EnemyPlasmaLoot>();
        CombatEconomyTests.Set(
            loot,
            "plasmaPickup",
            AssetDatabase
                .LoadAssetAtPath<GameObject>(CombatEconomyTests.PlasmaPrefabPath)
                .GetComponent<PickupItem>()
        );
        CombatEconomyTests.Set(loot, "plasmaDropChance", 1f);
        CombatEconomyTests.Set(loot, "plasmaDropMin", 2);
        CombatEconomyTests.Set(loot, "plasmaDropMax", 2);
        Assert.That(loot.ResolveDeath(null), Is.Null, "Unarmed never rolls plasma");
        loot.RestoreRunState("{\"resolved\":false}");
        var drop = loot.ResolveDeath(CombatEconomyTests.Weapon("Rifle"));
        Assert.That(drop, Is.Not.Null);
        Assert.That(drop.GetComponent<PickupItem>().Quantity, Is.EqualTo(2));
        string saved = loot.CaptureRunState();
        loot.RestoreRunState(saved);
        loot.RestoreRunState(saved);
        Assert.That(loot.ResolveDeath(CombatEconomyTests.Weapon("Rifle")), Is.Null);
        int plasma = inventory.PlasmaCapsules;
        Assert.That(drop.GetComponent<PickupItem>().TryCollect(inventory), Is.True);
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(plasma + 2));
        yield return EditorTestFrame.Next();
    }

    [UnityTest]
    public IEnumerator EmptyAutomaticTriggerClicksOnceThenCapsulesStartAutoReloadWithoutAnotherPress()
    {
        var root = new GameObject("Dry trigger");
        root.transform.position = Vector3.one * 5000;
        var input = root.AddComponent<StarterAssets.StarterAssetsInputs>();
        var equipment = root.AddComponent<PlayerEquipment>();
        equipment.enabled = false;
        var inventory = root.AddComponent<PlayerInventory>();
        var shooter = root.AddComponent<PlayerShooter>();
        shooter.enabled = false;
        var weapon = Object.Instantiate(CombatEconomyTests.Weapon("Rifle"));
        weapon.requiredSkill = null;

        // A long silent fixture clip leaves the semantic dry-fire voice observable.
        var sound = ScriptableObject.CreateInstance<SoundEvent>();
        var clip = AudioClip.Create("Dry fixture", 441000, 1, 44100, false);
        sound.variants = new[] { clip };
        sound.maxVoices = 32;
        sound.cooldown = 0;
        weapon.dryFireSound = sound;

        var library = ScriptableObject.CreateInstance<AudioLibrary>();
        library.events.Add(sound);
        var serviceRoot = new GameObject("Fixture audio");
        serviceRoot.SetActive(false);
        var service = serviceRoot.AddComponent<AudioService>();
        CombatEconomyTests.Set(service, "library", library);
        serviceRoot.SetActive(true);

        inventory.EnsureOwnedWeapon(weapon);
        CombatEconomyTests.Set(equipment, "equippedWeapon", weapon);
        shooter.EquipWeapon(weapon, root.transform);
        shooter.ActiveWeaponState.Restore(0, Time.time);

        input.shoot = true;
        for (int i = 0; i < 60; i++)
            CombatEconomyTests.Invoke(shooter, "Update");

        Assert.That(
            serviceRoot.GetComponentsInChildren<AudioSource>().Count(s => s.clip == clip),
            Is.EqualTo(1)
        );
        Assert.That(shooter.CurrentAmmo, Is.Zero);
        Assert.That(shooter.IsReloading, Is.False);

        input.shoot = false;
        CombatEconomyTests.Invoke(shooter, "Update");
        input.shoot = true;
        CombatEconomyTests.Invoke(shooter, "Update");

        Assert.That(
            serviceRoot.GetComponentsInChildren<AudioSource>().Count(s => s.clip == clip),
            Is.EqualTo(2)
        );

        inventory.RestoreCapsules(weapon.plasmaReloadCost, 0);
        CombatEconomyTests.Invoke(shooter, "Update");

        Assert.That(shooter.IsReloading, Is.True);
        Assert.That(inventory.PlasmaCapsules, Is.Zero);

        shooter.ActiveWeaponState.FinishReload(Time.time + weapon.reloadTime);

        Assert.That(shooter.CurrentAmmo, Is.EqualTo(weapon.magazineSize));

        Object.Destroy(root);
        Object.Destroy(serviceRoot);
        Object.Destroy(library);
        Object.Destroy(weapon);
        Object.Destroy(sound);
        Object.Destroy(clip);

        yield return EditorTestFrame.Next();
    }
}
