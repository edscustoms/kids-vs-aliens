using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[Category("AuditRemediation")]
public sealed class AuditContinueTests
{
    private const string Folder = "Assets/AuditContinueFixture";
    private const string Path = Folder + "/AuditContinue.unity";
    private const string Key = "AuditContinue.Build";
    [Serializable] private class BuildBackup { public string[] paths; public bool[] enabled; }

    [UnityTest] public IEnumerator ContinueRestoresAllMagazinesAndCrossObjectAbsoluteStateWithoutRewards()
    {
        Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False);
        AssetDatabase.CreateFolder("Assets", "AuditContinueFixture");
        Assert.That(AssetDatabase.CopyAsset("Assets/Game/Scenes/GamePoc.unity", Path), Is.True);
        SessionState.SetString(Key + ".Saves", Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", System.IO.Path.GetFullPath("Logs/AuditContinue-" + Guid.NewGuid().ToString("N")));
        var scene = EditorSceneManager.OpenScene(Path);
        foreach (string id in new[] { "probe-a", "probe-b" })
        {
            var root = new GameObject(id);
            root.AddComponent<RunWorldObject>().ConfigureIdentity(id);
            root.AddComponent<RestorePeerProbe>();
            if (id == "probe-b")
            {
                var loot = new SerializedObject(root.AddComponent<EnemyPlasmaLoot>());
                loot.FindProperty("plasmaPickup").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(CombatEconomyTests.PlasmaPrefabPath).GetComponent<PickupItem>();
                loot.FindProperty("plasmaDropChance").floatValue = 1;
                loot.FindProperty("plasmaDropMin").intValue = 4;
                loot.FindProperty("plasmaDropMax").intValue = 4;
                loot.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        EditorSceneManager.SaveScene(scene);
        SessionState.SetString(Key, JsonUtility.ToJson(new BuildBackup { paths = EditorBuildSettings.scenes.Select(s => s.path).ToArray(), enabled = EditorBuildSettings.scenes.Select(s => s.enabled).ToArray() }));
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(Path, true) }).ToArray();
        yield return new EnterPlayMode();
        yield return VerifyContinue();
    }
    private static IEnumerator VerifyContinue()
    {
        Application.runInBackground = true;
        yield return WaitReady();
        var run = ActiveRunController.Instance;
        var inventory = run.GetComponent<PlayerInventory>();
        var equipment = run.GetComponent<PlayerEquipment>();
        var shooter = run.GetComponent<PlayerShooter>();
        var pistol = AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/PlasmaPistolItem.asset");
        var rifle = AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/PlasmaRifleItem.asset");
        inventory.EnsureOwnedWeapon(pistol); inventory.EnsureOwnedWeapon(rifle);
        equipment.EquipWeapon(pistol);
        inventory.GetWeaponState(pistol).Restore(5, Time.time, 30, 10);
        int selections = 0, presentations = 0;
        equipment.EquippedWeaponChanged += _ => selections++;
        equipment.WeaponPresentationChanged += _ => presentations++;
        var mounted = equipment.EquippedWeaponInstance;
        equipment.EquipWeapon(pistol);
        Assert.That(equipment.EquippedWeaponInstance, Is.SameAs(mounted));
        Assert.That(shooter.CurrentAmmo, Is.EqualTo(5)); Assert.That(shooter.IsReloading, Is.True);
        equipment.SetEquippedWeaponPresentationVisible(false);
        Assert.That(equipment.EquippedWeapon, Is.SameAs(pistol)); Assert.That(equipment.PresentedWeapon, Is.Null);
        equipment.SetEquippedWeaponPresentationVisible(true);
        Assert.That(selections, Is.Zero); Assert.That(presentations, Is.GreaterThanOrEqualTo(2));
        equipment.EquipWeapon(rifle); inventory.GetWeaponState(rifle).Restore(7, Time.time);
        equipment.EquipWeapon(pistol);
        Assert.That(shooter.CurrentAmmo, Is.EqualTo(5)); Assert.That(shooter.IsReloading, Is.True);
        Assert.That(inventory.GetWeaponState(pistol).NextFireTime, Is.GreaterThan(Time.time));
        // Save partial magazines without a pending reload for the scene round trip.
        inventory.GetWeaponState(pistol).Restore(5, Time.time);
        inventory.RestoreCapsules(9,3);
        var duplicate = Object.Instantiate(pistol.worldPrefab, Vector3.one * 4000, Quaternion.identity);
        string duplicateId = RunWorldObject.TrackSpawn(duplicate, pistol.worldPrefab).Id;
        Assert.That(duplicate.GetComponent<PickupItem>().TryCollect(inventory), Is.True);
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(15));
        var plasmaPickup = run.FindWorldObject("probe-b").GetComponent<EnemyPlasmaLoot>().ResolveDeath(rifle);
        Assert.That(plasmaPickup, Is.Not.Null);
        plasmaPickup.transform.position = Vector3.one * 4100;
        plasmaPickup.GetComponent<WorldItemFloat>().ResetAnchor();
        string plasmaId = plasmaPickup.GetComponent<RunWorldObject>().Id;
        var ammoDisplay = run.GetComponent<CombatAmmoDisplay>();
        Assert.That(ammoDisplay, Is.Not.Null); ammoDisplay.Refresh();
        AssertHud("Magazine", "05 / 12"); AssertHud("PlasmaCount", "15"); AssertHud("ArmorCapsuleCount", "3");
        ProceduralUIReview.Capture("combat-economy-hud-1080",1920,1080);
        ProceduralUIReview.Capture("combat-economy-hud-720",1280,720);
        equipment.EquipWeapon(rifle); ammoDisplay.Refresh(); AssertHud("Magazine", "07 / 28");
        equipment.EquipWeapon(pistol);
        var a = run.FindWorldObject("probe-a").GetComponent<RestorePeerProbe>();
        var b = run.FindWorldObject("probe-b").GetComponent<RestorePeerProbe>();
        a.PeerId = "probe-b"; a.Value = 11; a.Complete();
        b.PeerId = "probe-a"; b.Value = 23; b.Complete();
        Assert.That(run.Save(), Is.True);
        var saved = RunSaveService.ActiveStore.Read<ActiveRunSave>();
        Assert.That(saved.player.weapons.Count, Is.EqualTo(2));
        // Deliberately restore A before B to expose peer-dependent decisions.
        saved.world = saved.world.OrderBy(s => s.id == "probe-a" ? 0 : s.id == "probe-b" ? 1 : 2).ToList();
        RunSaveService.ActiveStore.Write(saved);
        run.PrepareToLeave();
        Assert.That(RunSaveService.Continue(), Is.True, RunSaveService.LastError);
        yield return EditorTestFrame.Next(); yield return WaitReady(); yield return EditorTestFrame.Next();
        run = ActiveRunController.Instance; inventory = run.GetComponent<PlayerInventory>();
        Assert.That(inventory.GetWeaponState(pistol).Rounds, Is.EqualTo(5));
        Assert.That(inventory.GetWeaponState(rifle).Rounds, Is.EqualTo(7));
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(15)); Assert.That(inventory.ArmorCapsules, Is.EqualTo(3));
        Assert.That(run.FindWorldObject(duplicateId) == null || !run.FindWorldObject(duplicateId).gameObject.activeSelf, Is.True);
        var restoredPickup = run.FindWorldObject(plasmaId).GetComponent<PickupItem>();
        Assert.That(run.FindWorldObject("probe-b").GetComponent<EnemyPlasmaLoot>().ResolveDeath(rifle), Is.Null,
            "The two restore passes must not reset a resolved loot roll.");
        Assert.That(restoredPickup.Quantity, Is.EqualTo(4));
        Assert.That(restoredPickup.TryCollect(inventory), Is.True); Assert.That(restoredPickup.TryCollect(inventory), Is.False);
        Assert.That(inventory.PlasmaCapsules, Is.EqualTo(19));
        a = run.FindWorldObject("probe-a").GetComponent<RestorePeerProbe>();
        b = run.FindWorldObject("probe-b").GetComponent<RestorePeerProbe>();
        Assert.That(a.FirstPeerValue, Is.Zero, "First pass may see a peer before its saved state");
        Assert.That(a.PeerValueAfterReady, Is.EqualTo(23)); Assert.That(b.PeerValueAfterReady, Is.EqualTo(11));
        foreach (var probe in new[] { a, b })
        {
            Assert.That(probe.RestoreCount, Is.EqualTo(2)); Assert.That(probe.Completed, Is.True);
            Assert.That(probe.RewardCount, Is.Zero); Assert.That(probe.CompletionEvents, Is.Zero);
        }
        // Continue again while a paid reload is pending; restoring must not charge again.
        equipment = run.GetComponent<PlayerEquipment>(); equipment.EquipWeapon(rifle);
        inventory.GetWeaponState(rifle).Restore(0,Time.time);
        Assert.That(inventory.TryBeginReload(rifle,Time.time),Is.True);
        Assert.That(inventory.PlasmaCapsules,Is.EqualTo(13));
        ammoDisplay = run.GetComponent<CombatAmmoDisplay>(); ammoDisplay.Refresh();
        AssertHud("Magazine","00 / 28"); AssertHud("AmmoStatus","RELOADING");
        equipment.EquipWeapon(pistol);
        Assert.That(run.Save(),Is.True); run.PrepareToLeave();
        Assert.That(RunSaveService.Continue(),Is.True,RunSaveService.LastError);
        yield return EditorTestFrame.Next(); yield return WaitReady(); yield return EditorTestFrame.Next();
        run=ActiveRunController.Instance; inventory=run.GetComponent<PlayerInventory>();
        Assert.That(inventory.PlasmaCapsules,Is.EqualTo(13)); Assert.That(inventory.ArmorCapsules,Is.EqualTo(3));
        Assert.That(inventory.GetWeaponState(pistol).Rounds,Is.EqualTo(5));
        Assert.That(inventory.GetWeaponState(rifle).IsReloading,Is.True);
        Assert.That(inventory.GetWeaponState(rifle).ReloadRemaining(Time.time),Is.GreaterThan(0));
        Assert.That(run.GetComponent<PlayerEquipment>().EquippedWeapon,Is.SameAs(pistol));
        Assert.That(run.FindWorldObject(plasmaId)==null || !run.FindWorldObject(plasmaId).gameObject.activeSelf,Is.True);
        inventory.RestoreCapsules(1,3); inventory.GetWeaponState(pistol).Restore(0,Time.time);
        run.GetComponent<CombatAmmoDisplay>().Refresh(); AssertHud("AmmoStatus","NEED PLASMA");
        var panel=Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include).Single(t=>t.name=="Magazine").transform.parent;
        Assert.That(panel.parent.name,Is.EqualTo("SafeArea"));
        Assert.That(panel.GetComponentsInChildren<UnityEngine.UI.Graphic>().All(g=>!g.raycastTarget),Is.True);
    }
    private static void AssertHud(string name,string value) => Assert.That(
        Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include).Single(t=>t.name==name).text,Is.EqualTo(value));
    private static IEnumerator WaitReady()
    {
        double timeout = EditorApplication.timeSinceStartup + 20;
        while ((ActiveRunController.Instance == null || !ActiveRunController.Instance.IsReady) && EditorApplication.timeSinceStartup < timeout)
            yield return EditorTestFrame.Next();
        Assert.That(ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, Is.True, ActiveRunController.Instance?.RestoreError);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (Application.isPlaying) { ActiveRunController.Instance?.PrepareToLeave(); yield return new ExitPlayMode(); }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AssetDatabase.DeleteAsset(Folder);
        var build = JsonUtility.FromJson<BuildBackup>(SessionState.GetString(Key, "{}"));
        if (build.paths != null) EditorBuildSettings.scenes = build.paths.Select((path, i) => new EditorBuildSettingsScene(path, build.enabled[i])).ToArray();
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", SessionState.GetString(Key + ".Saves", ""));
        SessionState.EraseString(Key + ".Saves");
        SessionState.EraseString(Key); Time.timeScale = 1;
    }
}
