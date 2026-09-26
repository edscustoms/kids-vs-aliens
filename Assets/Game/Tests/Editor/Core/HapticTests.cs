using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[TestFixture, Category("Core")]
public sealed class HapticTests
{
    private sealed class Recorder : IHapticBackend
    {
        public readonly List<HapticProfile> pulses = new();
        public void Play(HapticProfile profile) => pulses.Add(profile);
    }

    private readonly List<Object> owned = new();
    private Recorder recorder;
    private readonly List<CameraFeedbackProfile> cameraRequests = new();
    private bool hadCameraPreference;
    private int savedCameraPreference;
    private void RecordCamera(CameraFeedbackProfile profile, float strength, Vector3 direction) => cameraRequests.Add(profile);
    private bool hadPreference;
    private int savedPreference;
    private GameInputMode inputMode;
    private static readonly FieldInfo Backend = typeof(HapticService).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic);
    private static HapticProfile Profile(string name) => AssetDatabase.LoadAssetAtPath<HapticProfile>("Assets/Game/Resources/Haptics/" + name + ".asset");
    private static WeaponItemData Weapon(string name) => AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/Plasma" + name + "Item.asset");

    [SetUp] public void SetUp()
    {
        hadCameraPreference = PlayerPrefs.HasKey(CameraFeedbackSettings.PreferenceKey);
        savedCameraPreference = PlayerPrefs.GetInt(CameraFeedbackSettings.PreferenceKey);
        CameraFeedbackSettings.Enabled = true; cameraRequests.Clear();
        CameraFeedbackService.Requested += RecordCamera;
        hadPreference = PlayerPrefs.HasKey(HapticSettings.PreferenceKey);
        savedPreference = PlayerPrefs.GetInt(HapticSettings.PreferenceKey);
        HapticSettings.Enabled = true;
        recorder = new Recorder(); Backend.SetValue(null, recorder);
        inputMode = InputModeController.CurrentMode;
        typeof(InputModeController).GetProperty("CurrentMode").SetValue(null, GameInputMode.Desktop);
    }

    [TearDown] public void TearDown()
    {
        CameraFeedbackService.Requested -= RecordCamera;
        if (hadCameraPreference) PlayerPrefs.SetInt(CameraFeedbackSettings.PreferenceKey, savedCameraPreference);
        else PlayerPrefs.DeleteKey(CameraFeedbackSettings.PreferenceKey);
        Backend.SetValue(null, null);
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        if (hadPreference) PlayerPrefs.SetInt(HapticSettings.PreferenceKey, savedPreference);
        else PlayerPrefs.DeleteKey(HapticSettings.PreferenceKey);
        PlayerPrefs.Save();
        typeof(InputModeController).GetProperty("CurrentMode").SetValue(null, inputMode);
    }

    private PlayerShooter Shooter(string weaponName, out StarterAssetsInputs input, out PlayerAim aim)
    {
        var root = new GameObject("Local haptic test player"); owned.Add(root);
        root.transform.position = new Vector3(5000, 0, 5000);
        input = root.AddComponent<StarterAssetsInputs>();
        aim = root.AddComponent<PlayerAim>();
        var shooter = root.AddComponent<PlayerShooter>();
        Invoke(aim, "Awake"); Invoke(shooter, "Awake");
        var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(root.transform, false);
        muzzle.localPosition = new Vector3(0, 1, 1);
        typeof(PlayerAim).GetProperty("AimPoint").SetValue(aim, muzzle.position + Vector3.forward * 10);
        typeof(PlayerAim).GetProperty("HasAimPoint").SetValue(aim, true);
        var weapon = Object.Instantiate(Weapon(weaponName)); owned.Add(weapon);
        weapon.requiredSkill = null; // Other cases explicitly exercise the Knowledge gate.
        root.AddComponent<PlayerInventory>().EnsureOwnedWeapon(weapon);
        shooter.EquipWeapon(weapon, muzzle);
        return shooter;
    }

    [Test] public void AcceptedPistolShot_OneConfiguredPulse_OneRound_AndDeferredDamage()
    {
        var shooter = Shooter("Pistol", out var input, out _);
        var target = new GameObject("Target", typeof(BoxCollider), typeof(DamageableProbe));
        owned.Add(target); target.transform.position = shooter.transform.position + new Vector3(0, 1, 5);
        Physics.SyncTransforms();
        int before = shooter.CurrentAmmo;
        input.shoot = true; Invoke(shooter, "Update");
        CollectionAssert.AreEqual(new[] { Profile("PistolFire") }, recorder.pulses);
        CollectionAssert.AreEqual(new[] { Weapon("Pistol").fireCameraFeedback }, cameraRequests);
        Assert.That(shooter.CurrentAmmo, Is.EqualTo(before - 1));
        var damage = target.GetComponent<DamageableProbe>();
        Assert.That(damage.ReceiveCount, Is.Zero, "Missing cosmetic prefab still preserves travel delay.");
        Invoke(shooter, "Update");
        Assert.That(recorder.pulses.Count, Is.EqualTo(1), "Held semi-auto input is not another bullet.");
    }

    [TestCase("cooldown")]
    [TestCase("empty")]
    [TestCase("reloading")]
    [TestCase("blocked")]
    [TestCase("no aim")]
    [TestCase("no muzzle")]
    [TestCase("missing knowledge")]
    [TestCase("no input")]
    public void RejectedPistolShot_NoPulse_NoAmmoSpent(string reason)
    {
        var shooter = Shooter("Pistol", out var input, out var aim);
        input.shoot = true;
        switch (reason)
        {
            case "cooldown": shooter.ActiveWeaponState.Restore(shooter.CurrentAmmo, Time.time, cooldownRemaining: 1000); break;
            case "empty": shooter.RestoreRunAmmo(0); break;
            case "reloading": shooter.ActiveWeaponState.Restore(shooter.CurrentAmmo, Time.time, reloadRemaining: 1000); break;
            case "blocked": shooter.SetFireBlocked(true); break;
            case "no aim": typeof(PlayerAim).GetProperty("HasAimPoint").SetValue(aim, false); break;
            case "no muzzle": Set(shooter, "muzzle", null); break;
            case "missing knowledge": Get<WeaponItemData>(shooter, "equippedWeapon").requiredSkill = Weapon("Pistol").requiredSkill; break;
            case "no input": input.shoot = false; break;
        }
        int before = shooter.CurrentAmmo;
        Invoke(shooter, "Update");
        Assert.That(recorder.pulses, Is.Empty);
        Assert.That(shooter.CurrentAmmo, Is.EqualTo(before));
        Assert.That(cameraRequests, Is.Empty);
    }

    [Test] public void MuzzleObstruction_NoPulse_PreservesExistingAmmoAndImpact()
    {
        var shooter = Shooter("Pistol", out var input, out _);
        var wall = new GameObject("Muzzle blocker", typeof(BoxCollider), typeof(DamageableProbe));
        owned.Add(wall); wall.transform.position = shooter.transform.position + new Vector3(0, 1, .5f);
        wall.transform.localScale = new Vector3(1, 1, .1f); Physics.SyncTransforms();
        int before = shooter.CurrentAmmo;
        input.shoot = true; Invoke(shooter, "Update");
        Assert.That(recorder.pulses, Is.Empty);
        Assert.That(shooter.CurrentAmmo, Is.EqualTo(before - 1));
        Assert.That(wall.GetComponent<DamageableProbe>().ReceiveCount, Is.Zero, "Obstructed impacts also wait for travel.");
        Assert.That(cameraRequests, Is.Empty);
    }

    [Test] public void RifleHeldBurst_OneLightPulsePerEmittedBullet()
    {
        var shooter = Shooter("Rifle", out var input, out _);
        int before = shooter.CurrentAmmo;
        input.shoot = true;
        for (int i = 0; i < 5; i++)
        {
            shooter.ActiveWeaponState.Restore(shooter.CurrentAmmo, Time.time);
            Invoke(shooter, "Update");
            Assert.That(recorder.pulses.Count, Is.EqualTo(i + 1));
            Assert.That(recorder.pulses[i], Is.SameAs(Profile("RifleFire")));
            Assert.That(cameraRequests.Count, Is.EqualTo(i + 1));
            Assert.That(cameraRequests[i], Is.SameAs(Weapon("Rifle").fireCameraFeedback));
            Invoke(shooter, "Update"); // Same-time cooldown must not pulse.
            Assert.That(recorder.pulses.Count, Is.EqualTo(i + 1));
        }
        Assert.That(shooter.CurrentAmmo, Is.EqualTo(before - 5));
        Assert.That(cameraRequests.Count, Is.EqualTo(5));
        Assert.That(Weapon("Rifle").fireCameraFeedback.rotation.magnitude, Is.LessThan(Weapon("Pistol").fireCameraFeedback.rotation.magnitude));
        Assert.That(Profile("RifleFire").durationMilliseconds, Is.LessThan(Profile("PistolFire").durationMilliseconds));
        Assert.That(Profile("RifleFire").amplitude, Is.LessThan(Profile("PistolFire").amplitude));
        Assert.That(Profile("RifleFire").intensity, Is.LessThan(Profile("PistolFire").intensity));
    }

    [Test] public void ActualArmorHealthAndLethalDamage_PulseOnceEach_InvalidAndRestoreDoNot()
    {
        var root = new GameObject("Damaged player", typeof(PlayerHealth)); owned.Add(root);
        var health = root.GetComponent<PlayerHealth>(); Invoke(health, "Awake");
        int changes = 0, deaths = 0; health.OnHealthChanged += () => changes++; health.OnDied += () => deaths++;
        health.TakeDamage(0); health.TakeDamage(-10);
        Assert.That(recorder.pulses, Is.Empty); Assert.That(changes, Is.Zero);
        Assert.That(cameraRequests, Is.Empty);
        health.ReceiveDamage(new HitInfo(10, Vector3.zero, Vector3.up, Vector3.forward, root));
        Assert.That(health.CurrentHealth, Is.EqualTo(100)); Assert.That(health.CurrentArmor, Is.EqualTo(40));
        health.TakeDamage(45);
        Assert.That(health.CurrentHealth, Is.EqualTo(95)); Assert.That(health.CurrentArmor, Is.Zero);
        health.TakeDamage(1000); health.TakeDamage(10);
        CollectionAssert.AreEqual(new[] { Profile("PlayerDamage"), Profile("PlayerDamage"), Profile("PlayerDamage") }, recorder.pulses);
        Assert.That(health.CurrentHealth, Is.Zero); Assert.That(deaths, Is.EqualTo(1)); Assert.That(changes, Is.EqualTo(3));
        health.RestoreRunHealth(100, 50);
        Assert.That(recorder.pulses.Count, Is.EqualTo(3), "Loading a run must not vibrate.");
        CollectionAssert.AreEqual(new[] { CameraFeedbackService.Config.playerDamage, CameraFeedbackService.Config.playerDamage, CameraFeedbackService.Config.playerDamage }, cameraRequests);
        Assert.That(Profile("PlayerDamage").amplitude, Is.GreaterThan(Profile("PistolFire").amplitude));
    }

    [Test] public void DisabledSetting_SuppressesWeaponsAndDamage_WithoutChangingGameplay()
    {
        HapticSettings.Enabled = false;
        foreach (string name in new[] { "Pistol", "Rifle" })
        {
            var shooter = Shooter(name, out var input, out _);
            int before = shooter.CurrentAmmo; input.shoot = true; Invoke(shooter, "Update");
            Assert.That(shooter.CurrentAmmo, Is.EqualTo(before - 1));
        }
        var root = new GameObject("Player", typeof(PlayerHealth)); owned.Add(root);
        var health = root.GetComponent<PlayerHealth>(); Invoke(health, "Awake"); health.TakeDamage(10);
        Assert.That(health.CurrentArmor, Is.EqualTo(40)); Assert.That(recorder.pulses, Is.Empty);
    }

    [Test] public void CameraShakeOff_SuppressesAllFeedback_WhileShotsDamageAndHapticsContinue()
    {
        CameraFeedbackSettings.Enabled = false;
        foreach (string name in new[] { "Pistol", "Rifle" })
        {
            var shooter = Shooter(name, out var input, out _);
            int before = shooter.CurrentAmmo; input.shoot = true; Invoke(shooter, "Update");
            Assert.That(shooter.CurrentAmmo, Is.EqualTo(before - 1));
        }
        var root = new GameObject("Camera damage test", typeof(PlayerHealth)); owned.Add(root);
        var health = root.GetComponent<PlayerHealth>(); Invoke(health, "Awake"); health.TakeDamage(10);
        CameraFeedbackService.Landed(20);
        Assert.That(health.CurrentArmor, Is.EqualTo(40));
        Assert.That(cameraRequests, Is.Empty);
        Assert.That(recorder.pulses.Count, Is.EqualTo(3), "Camera preference is independent of haptics.");
    }

    [Test] public void Preference_DefaultsOn_PersistsOff_AndUiIsIdempotentAndSilent()
    {
        PlayerPrefs.DeleteKey(HapticSettings.PreferenceKey); Assert.That(HapticSettings.Enabled, Is.True);
        var root = new GameObject("Options", typeof(RectTransform)); owned.Add(root);
        HapticsOptionView.Ensure(root.transform, true); HapticsOptionView.Ensure(root.transform, true);
        Assert.That(root.GetComponentsInChildren<HapticsOptionView>().Length, Is.EqualTo(1));
        var view = root.GetComponentInChildren<HapticsOptionView>(); Invoke(view, "OnDisable"); Invoke(view, "OnEnable");
        view.GetComponent<Button>().onClick.Invoke();
        Assert.That(HapticSettings.Enabled, Is.False);
        Assert.That(PlayerPrefs.GetInt(HapticSettings.PreferenceKey), Is.Zero);
        Assert.That(view.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("HAPTICS: OFF"));
        Assert.That(recorder.pulses, Is.Empty);
    }

    [Test] public void EditorBackend_IsSilent_AndMissingProfilesAreSafe()
    {
        Backend.SetValue(null, null);
        Assert.DoesNotThrow(() => { HapticService.Play(null); HapticService.Play(Profile("PistolFire")); HapticService.PlayerDamaged(); });
        Assert.That(Backend.GetValue(null).GetType().Name, Is.EqualTo("SilentHapticBackend"));
    }

#if UNITY_ANDROID
    [Test] public void AndroidPermission_IsAddedOnce_PreservingManifestActivity()
    {
        string path = "Temp/HapticTestManifest.xml";
        try
        {
            File.WriteAllText(path, "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application><activity android:name=\"ExistingActivity\" /></application></manifest>");
            HapticAndroidBuild.EnsurePermission(path); string once = File.ReadAllText(path);
            HapticAndroidBuild.EnsurePermission(path);
            Assert.That(File.ReadAllText(path), Is.EqualTo(once));
            Assert.That(once, Does.Contain("android.permission.VIBRATE")); Assert.That(once, Does.Contain("ExistingActivity"));
        }
        finally { File.Delete(path); }
    }
#endif

    [UnityTest] public IEnumerator ActualEnemyFire_WithBothWeaponProfiles_DoesNotRequestDeviceHaptics()
    {
        SessionState.SetFloat("HapticTests.TimeScale", Time.timeScale);
        Time.timeScale = 1;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        recorder = new Recorder(); Backend.SetValue(null, recorder); HapticSettings.Enabled = true;
        CameraFeedbackSettings.Enabled = true; cameraRequests.Clear();
        CameraFeedbackService.Requested -= RecordCamera; CameraFeedbackService.Requested += RecordCamera;
        var target = new GameObject("Non-damaging target", typeof(BoxCollider)); owned.Add(target);
        target.transform.position = new Vector3(0, 1, 6);
        foreach (string name in new[] { "Pistol", "Rifle" })
        {
            var staging = new GameObject("Staging"); owned.Add(staging); staging.SetActive(false);
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyCombatantSetup.PrefabPath), staging.transform);
            owned.Add(actor);
            actor.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            actor.GetComponent<EnemyBrain>().enabled = false;
            Set(actor.GetComponent<EnemyEquipment>(), "startingWeapon", Weapon(name));
            actor.transform.SetParent(null);
            yield return EditorTestFrame.Next();
            var equipment = actor.GetComponent<EnemyEquipment>();
            var presentation = actor.GetComponent<EnemyCombatPresentation>();
            var ranged = actor.GetComponent<EnemyRangedAttack>();
            var profile = Get<EnemyAnimationProfile>(presentation, "animationProfile");
            presentation.Animator.Play(name == "Pistol" ? profile.pistolReadyState : profile.rifleReadyState, 0, 0);
            presentation.Animator.Update(0);
            Assert.That(presentation.ReadyToFire, Is.True);
            equipment.Muzzle.LookAt(target.transform.position);
            int shots = 0; ranged.ShotFired += (_, __) => shots++;
            Set(ranged, "target", target.transform); Set(ranged, "requested", true);
            int ammo = equipment.Ammo;
            Invoke(ranged, "LateUpdate");
            Assert.That(shots, Is.EqualTo(1), "Exercise a real accepted enemy bullet, not an early rejection.");
            Assert.That(equipment.Ammo, Is.EqualTo(ammo - 1));
            Assert.That(recorder.pulses, Is.Empty);
            Assert.That(cameraRequests, Is.Empty, "Enemy bullets must not recoil the local camera.");
            Object.DestroyImmediate(actor);
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown] public IEnumerator ExitAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        if (SessionState.GetFloat("HapticTests.TimeScale", -1) >= 0)
        {
            Time.timeScale = SessionState.GetFloat("HapticTests.TimeScale", 1);
            SessionState.EraseFloat("HapticTests.TimeScale");
        }
    }

    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
