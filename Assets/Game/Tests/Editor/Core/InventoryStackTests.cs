using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[Category("Core")]
public sealed class InventoryStackTests
{
    GameObject owner;
    PlayerInventory inventory;
    GrenadeItemData grenade;
    WeaponItemData pistol, rifle;

    [SetUp] public void Setup()
    {
        owner = new GameObject("Stack inventory");
        inventory = owner.AddComponent<PlayerInventory>();
        grenade = ScriptableObject.CreateInstance<GrenadeItemData>();
        pistol = ScriptableObject.CreateInstance<WeaponItemData>();
        rifle = ScriptableObject.CreateInstance<WeaponItemData>();
        pistol.itemName = rifle.itemName = "Same display name";
    }
    [TearDown] public void Cleanup()
    {
        foreach (var item in new Object[] { owner, grenade, pistol, rifle }) Object.DestroyImmediate(item);
    }
    [Test] public void WeaponsAreUniqueByDataIdentityAndDifferentWeaponsCoexist()
    {
        Assert.That(inventory.TryAddItem(pistol), Is.True);
        int changes = 0; inventory.OnInventoryChanged += () => changes++;
        Assert.That(inventory.TryAddItem(pistol, out var failure), Is.False);
        Assert.That(failure, Is.EqualTo(InventoryAddFailure.AlreadyOwned));
        Assert.That(changes, Is.Zero);
        Assert.That(inventory.TryAddItem(rifle), Is.True);
        Assert.That(inventory.Items, Is.EqualTo(new[] { pistol, rifle }));
        Assert.That(pistol.IsStackable, Is.False);
    }
    [Test] public void FullBackpackAcceptsExistingStackButRejectsNewItem()
    {
        Set(inventory, "maxSlots", 2);
        inventory.TryAddItem(pistol); inventory.TryAddItem(grenade);
        Assert.That(inventory.TryAddItem(grenade), Is.True);
        Assert.That(inventory.Items.Count, Is.EqualTo(2));
        Assert.That(inventory.CountAt(1), Is.EqualTo(2));
        Assert.That(inventory.TryAddItem(rifle, out var failure), Is.False);
        Assert.That(failure, Is.EqualTo(InventoryAddFailure.Full));
        Assert.That(inventory.TryAddItem(pistol, out failure), Is.False);
        Assert.That(failure, Is.EqualTo(InventoryAddFailure.AlreadyOwned));
    }
    [Test] public void GenericStackCapabilityWorksWithoutGrenadeTypeChecks()
    {
        var item = ScriptableObject.CreateInstance<KnowledgeBookItemData>();
        try
        {
            // A generic test item; no use behavior is involved.
            var data = new SerializedObject(item); data.FindProperty("stackable").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
            inventory.TryAddItem(item); inventory.TryAddItem(item);
            Assert.That(inventory.Items.Count, Is.EqualTo(1));
            Assert.That(inventory.CountAt(0), Is.EqualTo(2));
            Assert.That(inventory.TryConsumeItem(item), Is.True);
            Assert.That(inventory.CountAt(0), Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(item); }
    }
    [Test] public void ConsumptionAndReorderingKeepQuickSlotsUntilFinalUnit()
    {
        inventory.TryAddItem(grenade); inventory.TryAddItem(grenade); inventory.TryAddItem(pistol);
        inventory.AssignQuickSlot(4, 0); inventory.SwapItems(0, 1);
        Assert.That(inventory.CountAt(1), Is.EqualTo(2));
        Assert.That(inventory.QuickSlotItem(4), Is.SameAs(grenade));
        inventory.TryConsumeGrenade(grenade);
        Assert.That(inventory.CountAt(1), Is.EqualTo(1));
        Assert.That(inventory.QuickSlotIndex(4), Is.EqualTo(1));
        inventory.TryConsumeGrenade(grenade);
        Assert.That(inventory.Items, Is.EqualTo(new[] { pistol }));
        Assert.That(inventory.QuickSlotItem(4), Is.Null);
        Assert.That(inventory.QuickSlotItem(1), Is.SameAs(pistol));
    }
    [Test] public void LegacyDuplicatesMergeAndRemapWithoutLosingQuantities()
    {
        inventory.RestoreSavedItems(new ItemData[] { pistol, grenade, rifle, grenade, pistol }, new[] { 4, 3, 2, -1, -1 });
        Assert.That(inventory.Items, Is.EqualTo(new ItemData[] { pistol, grenade, rifle }));
        Assert.That(inventory.CaptureCounts(), Is.EqualTo(new[] { 1, 2, 1 }));
        Assert.That(inventory.CaptureQuickSlots(), Is.EqualTo(new[] { 0, 1, 2, -1, -1 }));
    }
    [Test] public void SavedStackRoundTripPreservesCountsAndAssignments()
    {
        inventory.TryAddItem(grenade); inventory.TryAddItem(grenade); inventory.TryAddItem(pistol);
        inventory.AssignQuickSlot(4, 0);
        var saved = new SavedPlayer { items = new() { "grenade", "pistol" }, itemCounts = inventory.CaptureCounts(), quickSlots = inventory.CaptureQuickSlots() };
        var restored = JsonUtility.FromJson<SavedPlayer>(JsonUtility.ToJson(saved));
        inventory.TryConsumeGrenade(grenade);
        inventory.RestoreSavedItems(restored.items.Select(id => id == "grenade" ? (ItemData)grenade : pistol).ToArray(), restored.quickSlots, restored.itemCounts);
        Assert.That(inventory.CountAt(0), Is.EqualTo(2));
        Assert.That(inventory.QuickSlotItem(4), Is.SameAs(grenade));
        Assert.That(inventory.Items.Count, Is.EqualTo(2));
    }
    [Test] public void InvalidQuantitiesDoNotPartiallyReplaceLiveInventory()
    {
        inventory.TryAddItem(pistol);
        Assert.Throws<ArgumentException>(() => inventory.RestoreSavedItems(new ItemData[] { grenade }, new[] { 0, -1, -1, -1, -1 }, new[] { 0 }));
        Assert.That(inventory.Items, Is.EqualTo(new[] { pistol }));
        Assert.That(inventory.CountAt(0), Is.EqualTo(1));
    }
    internal static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}

[Category("Core")]
public sealed class InventoryPickupStackTests
{
    [UnitySetUp] public IEnumerator Setup()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
    }
    [UnityTest] public IEnumerator WorldPickupRejectionStackingAndSingleUnitDrop()
    {
        var player = new GameObject("Pickup player", typeof(BoxCollider), typeof(PlayerInventory));
        var inventory = player.GetComponent<PlayerInventory>();
        Assert.That(inventory, Is.Not.Null);
        var pistol = AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/PlasmaPistolItem.asset");
        var rifle = AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/PlasmaRifleItem.asset");
        var grenade = AssetDatabase.LoadAssetAtPath<GrenadeItemData>("Assets/Game/Data/Items/Grenades/ElectricGrenade.asset");
        Assert.That(pistol, Is.Not.Null); Assert.That(rifle, Is.Not.Null); Assert.That(grenade, Is.Not.Null);
        var first = Spawn(pistol);
        var duplicate = Spawn(pistol);
        var different = Spawn(rifle);
        Collect(first, player); Collect(duplicate, player); Collect(different, player);
        yield return EditorTestFrame.Next();
        Assert.That(first == null && different == null, Is.True);
        Assert.That(duplicate == null, Is.True, "Duplicate plasma pickup converts and is consumed.");
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(pistol.duplicatePlasmaReward));
        Assert.That(inventory.Items, Is.EqualTo(new[] { pistol, rifle }));
        var a = Spawn(grenade); var b = Spawn(grenade); Collect(a, player); Collect(b, player);
        Assert.That(inventory.Items.Count, Is.EqualTo(3)); Assert.That(inventory.CountAt(2), Is.EqualTo(2));
        inventory.AssignQuickSlot(4, 2); inventory.DropQuickSlot(4);
        Assert.That(inventory.CountAt(2), Is.EqualTo(1)); Assert.That(inventory.QuickSlotItem(4), Is.SameAs(grenade));
        yield return EditorTestFrame.Next();
        var dropped = PickupItem.Available.Single(p => p.Item == grenade);
        Collect(dropped.gameObject, player);
        Assert.That(inventory.CountAt(2), Is.EqualTo(2));
        inventory.TryConsumeGrenade(grenade); inventory.TryConsumeGrenade(grenade);
        Assert.That(inventory.QuickSlotItem(4), Is.Null);
        yield return new ExitPlayMode();
    }
    static GameObject Spawn(ItemData item)
    {
        Assert.That(item.worldPrefab, Is.Not.Null);
        var obj = Object.Instantiate(item.worldPrefab, Vector3.one * 100, Quaternion.identity);
        var world = obj.GetComponent<RunWorldObject>() ?? obj.AddComponent<RunWorldObject>();
        Assert.That(world, Is.Not.Null);
        world.ConfigureIdentity(Guid.NewGuid().ToString("N"));
        return obj;
    }
    static void Collect(GameObject pickup, GameObject player) => pickup.SendMessage("OnTriggerEnter", player.GetComponent<Collider>());
    [UnityTearDown] public IEnumerator Cleanup() { if (EditorApplication.isPlaying) yield return new ExitPlayMode(); }
}
