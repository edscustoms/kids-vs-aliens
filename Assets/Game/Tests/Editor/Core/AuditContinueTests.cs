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
        a = run.FindWorldObject("probe-a").GetComponent<RestorePeerProbe>();
        b = run.FindWorldObject("probe-b").GetComponent<RestorePeerProbe>();
        Assert.That(a.FirstPeerValue, Is.Zero, "First pass may see a peer before its saved state");
        Assert.That(a.PeerValueAfterReady, Is.EqualTo(23)); Assert.That(b.PeerValueAfterReady, Is.EqualTo(11));
        foreach (var probe in new[] { a, b })
        {
            Assert.That(probe.RestoreCount, Is.EqualTo(2)); Assert.That(probe.Completed, Is.True);
            Assert.That(probe.RewardCount, Is.Zero); Assert.That(probe.CompletionEvents, Is.Zero);
        }
    }
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
