using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[Category("CombatEconomy")]
public sealed class CombatEconomyStateTests
{
    private const string SaveKey = "CombatEconomyStateTests.Saves";
    private PlayerInventory inventory;
    private PlayerEquipment equipment;
    private PlayerShooter shooter;
    private WeaponItemData pistol,
        rifle;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        SessionState.SetString(
            SaveKey,
            Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? ""
        );
        Environment.SetEnvironmentVariable(
            "KIDS_TEST_SAVE_DIRECTORY",
            System.IO.Path.GetFullPath("Logs/CombatEconomyState-" + Guid.NewGuid().ToString("N"))
        );

        string scene =
            TestContext.CurrentContext.Test.Name
            == nameof(ConstructionSitePlasmaAcquisitionReloadsOnlyTheEquippedGun)
                ? "ConstructionSite"
                : "GamePoc";

        EditorSceneManager.OpenScene("Assets/Game/Scenes/" + scene + ".unity");
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return WaitReady();
        BindPlayer();

        pistol = CombatEconomyTests.Weapon("Pistol");
        rifle = CombatEconomyTests.Weapon("Rifle");
        inventory.EnsureOwnedWeapon(pistol);
        inventory.EnsureOwnedWeapon(rifle);

        var skills = inventory.GetComponent<PlayerSkillState>();
        skills.UnlockSkill(pistol.requiredSkill);
        skills.UnlockSkill(rifle.requiredSkill);

        // Learning is real gameplay and queues modal tutorials. Acknowledge them
        // before testing drop input or automatic reload.
        var tutorial = Object.FindAnyObjectByType<KnowledgeAcquiredPresenter>();
        while (tutorial != null && (tutorial.PendingCount > 0 || tutorial.CurrentSkill != null))
        {
            if (tutorial.CurrentSkill != null)
                tutorial.Close();
            yield return EditorTestFrame.Next();
        }

        inventory.RestoreCapsules(0, 0);
        ActiveRunController.Instance.SendMessage("OnApplicationFocus", true);
        Object.FindAnyObjectByType<BeamTransportController>()?.CancelTransport();
        Object.FindAnyObjectByType<InGameMenuController>().ResumeGame();

        var controller = inventory.GetComponent<CharacterController>();
        controller.enabled = false;
        inventory.transform.position = Vector3.one * 6000;
        controller.enabled = true;

        yield return EditorTestFrame.Next();
        yield return EditorTestFrame.Next();

        var suspension = inventory.GetComponent<GameplaySuspensionController>();
        Assert.That(
            inventory.GetComponent<StarterAssets.StarterAssetsInputs>().CanProcessGameplayInput,
            Is.True,
            $"Input is blocked: suspension={suspension.IsSuspended}, owners={suspension.OwnerCount}, timeScale={Time.timeScale}, shooter={shooter.enabled}"
        );
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying)
        {
            ActiveRunController.Instance?.PrepareToLeave();
            yield return new ExitPlayMode();
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Environment.SetEnvironmentVariable(
            "KIDS_TEST_SAVE_DIRECTORY",
            SessionState.GetString(SaveKey, "")
        );
        SessionState.EraseString(SaveKey);
        Time.timeScale = 1;
    }

    [UnityTest]
    public IEnumerator DroppedWeaponsTransferTheirMagazineReloadAndCooldownBackToInventory()
    {
        foreach (var weapon in new[] { pistol, rifle })
        {
            equipment.EquipWeapon(weapon);
            var state = inventory.GetWeaponState(weapon);
            state.Restore(5, Time.time, 30, 10);

            float cooldown = state.NextFireTime;
            float reload = state.ReloadRemaining(Time.time);

            var pickup = Drop(weapon);

            Assert.That(equipment.EquippedWeapon, Is.Null);
            Assert.That(shooter.ActiveWeaponState, Is.Null);
            Assert.That(inventory.GetWeaponState(weapon), Is.Null);

            Assert.That(pickup.TryCollect(inventory), Is.True);
            Assert.That(pickup.TryCollect(inventory), Is.False);

            var restored = inventory.GetWeaponState(weapon);
            Assert.That(restored.Rounds, Is.EqualTo(5));
            Assert.That(
                restored,
                Is.SameAs(state),
                "Custody transfers the existing record, not a second magazine"
            );
            Assert.That(restored.ReloadRemaining(Time.time), Is.EqualTo(reload));
            Assert.That(restored.NextFireTime, Is.EqualTo(cooldown));
            Assert.That(inventory.PlasmaCapsules, Is.Zero);
        }

        yield return EditorTestFrame.Next();
    }

    [UnityTest]
    public IEnumerator FreshWorldWeaponStartsFullWhileTheSpecificOldDropRetainsItsMagazine()
    {
        equipment.EquipWeapon(pistol);
        inventory.GetWeaponState(pistol).Restore(4, Time.time);

        var oldDrop = Drop(pistol);

        var fresh = Object
            .Instantiate(pistol.worldPrefab, Vector3.one * 6100, Quaternion.identity)
            .GetComponent<PickupItem>();
        RunWorldObject.TrackSpawn(fresh.gameObject, pistol.worldPrefab);

        fresh.RestoreRunState("{\"quantity\":1}"); // Legacy pickup records have no carried weapon state.

        string freshSnapshot = fresh.CaptureRunState();
        fresh.RestoreRunState(freshSnapshot);
        fresh.RestoreRunState(freshSnapshot);

        Assert.That(fresh.TryCollect(inventory), Is.True);
        Assert.That(
            inventory.GetWeaponState(pistol).Rounds,
            Is.EqualTo(pistol.magazineSize),
            freshSnapshot
        );

        // Move the first drop so the second can be identified at the normal drop position.
        oldDrop.transform.position = Vector3.one * 6200;
        oldDrop.GetComponent<WorldItemFloat>()?.ResetAnchor();

        Drop(pistol);

        Assert.That(oldDrop.TryCollect(inventory), Is.True);
        Assert.That(inventory.GetWeaponState(pistol).Rounds, Is.EqualTo(4));

        yield return EditorTestFrame.Next();
    }

    [UnityTest]
    public IEnumerator ContinuePreservesTheStateOfAPlayerWeaponStillOnTheGround()
    {
        equipment.EquipWeapon(pistol);
        inventory.GetWeaponState(pistol).Restore(5, Time.time, 30, 10);

        var pickup = Drop(pistol);
        string id = pickup.GetComponent<RunWorldObject>().Id;

        equipment.EquipWeapon(rifle);

        Assert.That(ActiveRunController.Instance.Save(), Is.True);
        ActiveRunController.Instance.PrepareToLeave();

        Assert.That(RunSaveService.Continue(), Is.True, RunSaveService.LastError);

        yield return EditorTestFrame.Next();
        yield return WaitReady();
        BindPlayer();

        Assert.That(inventory.GetWeaponState(pistol), Is.Null);

        pickup = ActiveRunController.Instance.FindWorldObject(id).GetComponent<PickupItem>();

        Assert.That(pickup.TryCollect(inventory), Is.True);

        var state = inventory.GetWeaponState(pistol);
        Assert.That(state.Rounds, Is.EqualTo(5));
        Assert.That(state.IsReloading, Is.True);
        Assert.That(state.ReloadRemaining(Time.time), Is.GreaterThan(25));
        Assert.That(state.NextFireTime - Time.time, Is.GreaterThan(5));
        Assert.That(inventory.PlasmaCapsules, Is.Zero);
    }

    [UnityTest]
    public IEnumerator PlasmaAcquisitionReloadsOnlyTheEquippedGunThenSwitchingCanReloadTheOther() =>
        VerifyPlasmaAcquisition();

    [UnityTest]
    public IEnumerator ConstructionSitePlasmaAcquisitionReloadsOnlyTheEquippedGun() =>
        VerifyPlasmaAcquisition();

    private IEnumerator VerifyPlasmaAcquisition()
    {
        foreach (bool duplicateGun in new[] { true, false })
        {
            inventory.RestoreCapsules(0, 0);

            var p = inventory.GetWeaponState(pistol);
            var r = inventory.GetWeaponState(rifle);

            p.Restore(0, Time.time);
            r.Restore(0, Time.time);

            equipment.EquipWeapon(rifle);

            var prefab = duplicateGun
                ? rifle.worldPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(CombatEconomyTests.PlasmaPrefabPath);

            var pickup = Object
                .Instantiate(prefab, Vector3.one * 6100, Quaternion.identity)
                .GetComponent<PickupItem>();
            RunWorldObject.TrackSpawn(pickup.gameObject, prefab);

            pickup.SetQuantity(rifle.duplicatePlasmaReward);

            Assert.That(pickup.TryCollect(inventory), Is.True);
            Assert.That(inventory.PlasmaCapsules, Is.EqualTo(12));
            Assert.That(p.Rounds, Is.Zero);
            Assert.That(r.Rounds, Is.Zero);

            yield return EditorTestFrame.Next();
            yield return EditorTestFrame.Next();

            Assert.That(r.IsReloading, Is.True);
            Assert.That(p.IsReloading, Is.False);
            Assert.That(inventory.PlasmaCapsules, Is.EqualTo(6));

            yield return Seconds(rifle.reloadTime + .1f);

            Assert.That(r.Rounds, Is.EqualTo(28));
            Assert.That(p.Rounds, Is.Zero);
            Assert.That(inventory.PlasmaCapsules, Is.EqualTo(6));

            equipment.EquipWeapon(pistol);

            yield return EditorTestFrame.Next();
            yield return EditorTestFrame.Next();

            Assert.That(p.IsReloading, Is.True);
            Assert.That(inventory.PlasmaCapsules, Is.EqualTo(3));

            equipment.EquipWeapon(pistol);

            yield return Seconds(pistol.reloadTime + .1f);

            Assert.That(p.Rounds, Is.EqualTo(12));
            Assert.That(r.Rounds, Is.EqualTo(28));
            Assert.That(inventory.PlasmaCapsules, Is.EqualTo(3));
        }
    }

    [UnityTest]
    public IEnumerator ReloadTransactionRejectsInactiveWeaponsEvenWhenPlasmaIsAvailable()
    {
        inventory.GetWeaponState(pistol).Restore(0, Time.time);
        inventory.GetWeaponState(rifle).Restore(0, Time.time);

        inventory.RestoreCapsules(12, 0);

        equipment.EquipWeapon(rifle);

        Assert.That(inventory.TryBeginReload(pistol, Time.time), Is.False);
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(12));
        Assert.That(inventory.GetWeaponState(pistol).IsReloading, Is.False);

        Assert.That(inventory.TryBeginReload(rifle, Time.time), Is.True);
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(6));

        var paidState = inventory.GetWeaponState(rifle);

        Assert.That(Drop(rifle).TryCollect(inventory), Is.True);

        equipment.EquipWeapon(rifle);

        Assert.That(inventory.GetWeaponState(rifle), Is.SameAs(paidState));
        Assert.That(paidState.IsReloading, Is.True);
        Assert.That(inventory.TryBeginReload(rifle, Time.time), Is.False);

        yield return Seconds(rifle.reloadTime + .1f);

        Assert.That(paidState.Rounds, Is.EqualTo(rifle.magazineSize));
        Assert.That(inventory.GetWeaponState(pistol).Rounds, Is.Zero);
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(6));
    }

    private PickupItem Drop(WeaponItemData weapon)
    {
        Vector3 position =
            inventory.transform.position + inventory.transform.forward * 2 + Vector3.up * .6f;
        inventory.DropItem(inventory.Items.ToList().IndexOf(weapon));
        return Object
            .FindObjectsByType<PickupItem>()
            .Single(p =>
                p.Item == weapon && Vector3.Distance(p.transform.position, position) < .01f
            );
    }

    private void BindPlayer()
    {
        inventory = ActiveRunController.Instance.GetComponent<PlayerInventory>();
        equipment = inventory.GetComponent<PlayerEquipment>();
        shooter = inventory.GetComponent<PlayerShooter>();
    }

    private static IEnumerator Seconds(float duration)
    {
        float end = Time.time + duration;
        double timeout = EditorApplication.timeSinceStartup + 15;
        while (Time.time < end && EditorApplication.timeSinceStartup < timeout)
            yield return EditorTestFrame.Next();
        Assert.That(
            Time.time,
            Is.GreaterThanOrEqualTo(end),
            "Gameplay unexpectedly remained suspended"
        );
    }

    private static IEnumerator WaitReady()
    {
        double timeout = EditorApplication.timeSinceStartup + 20;
        while (
            (ActiveRunController.Instance == null || !ActiveRunController.Instance.IsReady)
            && EditorApplication.timeSinceStartup < timeout
        )
            yield return EditorTestFrame.Next();
        Assert.That(
            ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady,
            Is.True
        );
    }
}
