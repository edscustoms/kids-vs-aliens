using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[Category("AuditRemediation")]
public sealed class OwnedWeaponStateTests
{
    private GameObject owner;
    private PlayerInventory inventory;
    private PlayerShooter shooter;
    private WeaponItemData pistol, rifle;
    [SetUp] public void Setup()
    {
        owner = new GameObject("Owned magazines");
        inventory = owner.AddComponent<PlayerInventory>();
        shooter = owner.AddComponent<PlayerShooter>();
        pistol = AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/PlasmaPistolItem.asset");
        rifle = AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/PlasmaRifleItem.asset");
        inventory.EnsureOwnedWeapon(pistol); inventory.EnsureOwnedWeapon(rifle);
        inventory.GetWeaponState(pistol).Restore(5, Time.time, 10, 2);
        inventory.GetWeaponState(rifle).Restore(7, Time.time);
    }
    [TearDown] public void Cleanup() => Object.DestroyImmediate(owner);
    [Test] public void ReselectionAndSwapKeepEachOwnedMagazineAndTimers()
    {
        var state = inventory.GetWeaponState(pistol);
        shooter.EquipWeapon(pistol, owner.transform);
        shooter.EquipWeapon(pistol, owner.transform);
        Assert.That(shooter.ActiveWeaponState, Is.SameAs(state));
        Assert.That(shooter.CurrentAmmo, Is.EqualTo(5));
        Assert.That(shooter.IsReloading, Is.True);
        float deadline = state.NextFireTime;
        shooter.EquipWeapon(rifle, owner.transform);
        Assert.That(shooter.CurrentAmmo, Is.EqualTo(7));
        shooter.EquipWeapon(pistol, owner.transform);
        Assert.That(shooter.CurrentAmmo, Is.EqualTo(5));
        Assert.That(shooter.IsReloading, Is.True);
        Assert.That(state.NextFireTime, Is.EqualTo(deadline));
    }
    [Test] public void AllMagazinesRoundTripWithReloadAndCooldown()
    {
        var saved = JsonUtility.FromJson<SavedPlayer>(JsonUtility.ToJson(new SavedPlayer { weapons = inventory.CaptureWeaponStates() }));
        inventory.RestoreSavedItems(new ItemData[] { pistol, rifle }, new[] { 0, 1, -1, -1, -1 });
        inventory.RestoreWeaponStates(saved.weapons, rifle, 50);
        Assert.That(inventory.GetWeaponState(pistol).Rounds, Is.EqualTo(5));
        Assert.That(inventory.GetWeaponState(rifle).Rounds, Is.EqualTo(7));
        Assert.That(inventory.GetWeaponState(pistol).IsReloading, Is.True);
        Assert.That(inventory.GetWeaponState(pistol).NextFireTime, Is.GreaterThan(Time.time));
    }
    [Test] public void MissingWeaponRecordsUseSelectedLegacyMagazineAndOtherFullMagazine()
    {
        var old = JsonUtility.FromJson<SavedPlayer>("{\"ammo\":3}");
        inventory.RestoreWeaponStates(old.weapons, pistol, old.ammo);
        Assert.That(inventory.GetWeaponState(pistol).Rounds, Is.EqualTo(3));
        Assert.That(inventory.GetWeaponState(rifle).Rounds, Is.EqualTo(rifle.magazineSize));
        Assert.That(inventory.GetWeaponState(pistol).IsReloading, Is.False);
    }
    [Test] public void ReloadCompletesOnceAndCooldownRejectsPrematureFire()
    {
        var state = inventory.GetWeaponState(pistol);
        state.Restore(5, 100, 2, 4);
        Assert.That(state.TrySpendRound(101), Is.False);
        Assert.That(state.FinishReload(102), Is.True);
        Assert.That(state.FinishReload(103), Is.False);
        Assert.That(state.TrySpendRound(103), Is.False);
        Assert.That(state.TrySpendRound(104), Is.True);
        Assert.That(state.Rounds, Is.EqualTo(pistol.magazineSize - 1));
    }
}
