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

[Category("AuditRemediation")]
public sealed class AuditLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
    private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);

    [TestCase(false, false, false, false)]
    [TestCase(true, false, false, false)]
    [TestCase(false, true, false, false)]
    [TestCase(true, true, false, false)]
    [TestCase(false, false, true, false)]
    [TestCase(false, false, false, true)]
    public void SightReductionUsesDistanceNotQueryOrder(bool reverse, bool behind, bool lowCover, bool alien)
    {
        var observer = new GameObject("Observer");
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var perception = observer.AddComponent<EnemyPerception>();
            target.transform.position = new Vector3(1000, 2, 8);
            blocker.transform.position = new Vector3(1000, lowCover ? 0 : 2, behind ? 10 : 4);
            if (alien) blocker.AddComponent<EnemyActor>();
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(new Vector3(1000, 2, 0), Vector3.forward, 15).OrderBy(h => h.distance).ToArray();
            Assert.That(hits.Length, Is.EqualTo(lowCover ? 1 : 2));
            if (reverse) Array.Reverse(hits);
            bool visible = (bool)Call(perception, "AreSightHitsClear", target.transform, hits, hits.Length);
            Assert.That(visible, Is.EqualTo(behind || lowCover || alien));
        }
        finally { Object.DestroyImmediate(observer); Object.DestroyImmediate(target); Object.DestroyImmediate(blocker); }
    }

    [UnityTest] public IEnumerator OcclusionOwnershipSurvivesReenableAndDuplicateDestruction()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifyOcclusion();
    }
    private static IEnumerator VerifyOcclusion()
    {
        Time.timeScale = 1;
        var root = new GameObject("First occlusion"); root.SetActive(false);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform);
        wall.transform.position = new Vector3(0, 2, -5); wall.transform.localScale = new Vector3(10, 5, 1);
        var player = new GameObject("Player sample");
        var camera = new GameObject("Occlusion camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 2, -10); camera.transform.LookAt(Vector3.up);
        var first = root.AddComponent<CameraOcclusionController>();
        Set(first, "player", player.transform); Set(first, "gameplayCamera", camera);
        root.SetActive(true); Physics.SyncTransforms();
        var silhouette = new System.Collections.Generic.List<CameraOcclusionController.SilhouetteRenderer>();
        for (int cycle = 0; cycle < 2; cycle++)
        {
            float until = Time.time + .4f;
            while (Time.time < until) yield return EditorTestFrame.Next();
            Assert.That(CameraOcclusionController.Active, Is.SameAs(first));
            Assert.That(first.IsOccluded(wall.GetComponent<Renderer>()), Is.True);
            first.CollectSilhouetteRenderers(camera, silhouette); Assert.That(silhouette.Count, Is.EqualTo(1));
            first.enabled = false; Assert.That(CameraOcclusionController.Active, Is.Null);
            Assert.That(first.IsOccluded(wall.GetComponent<Renderer>()), Is.False);
            first.enabled = true; Assert.That(CameraOcclusionController.Active, Is.SameAs(first));
        }
        var second = new GameObject("Second occlusion").AddComponent<CameraOcclusionController>();
        Assert.That(CameraOcclusionController.Active, Is.SameAs(second));
        Object.Destroy(second.gameObject); yield return EditorTestFrame.Next();
        Assert.That(CameraOcclusionController.Active, Is.SameAs(first));
        Object.Destroy(first.gameObject); yield return EditorTestFrame.Next();
        Assert.That(CameraOcclusionController.Active, Is.Null);
        Object.Destroy(camera.gameObject); Object.Destroy(player);
    }

    [UnityTest] public IEnumerator PlasmaArrivalSurvivesEveryVisualInterruptionAndMissingPrefab()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifyPlasma();
    }
    private static IEnumerator VerifyPlasma()
    {
        Time.timeScale = 1;
        Application.runInBackground = true;
        var root = new GameObject("Shooter");
        var inventory = root.AddComponent<PlayerInventory>();
        var aim = root.AddComponent<PlayerAim>(); aim.enabled = false;
        var shooter = root.AddComponent<PlayerShooter>();
        var suspension = root.AddComponent<GameplaySuspensionController>();
        Set(suspension, "gameplayBehaviours", new Behaviour[] { shooter });
        var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(root.transform); muzzle.position = Vector3.up;
        var weapon = ScriptableObject.CreateInstance<WeaponItemData>();
        weapon.magazineSize = 16; weapon.fireRate = 5; weapon.range = 100; weapon.damage = 10;
        inventory.EnsureOwnedWeapon(weapon); shooter.EquipWeapon(weapon, muzzle);
        var template = new GameObject("Bolt template").AddComponent<PlasmaBoltVFX>();
        template.gameObject.SetActive(false); Set(template, "speed", 10f);
        for (int mode = 0; mode < 7; mode++)
        {
            var target = new GameObject("Resolved target", typeof(BoxCollider), typeof(EnemyHealth), typeof(HitReactionProbe));
            var health = target.GetComponent<EnemyHealth>();
            Set(shooter, "plasmaBoltPrefab", mode == 4 ? null : template);
            float speed = mode == 4 ? PlasmaBoltVFX.DefaultSpeed : template.TravelSpeed;
            // Near collider face is exactly .3 seconds away at this bolt speed.
            target.transform.position = muzzle.position + Vector3.forward * (speed * .3f + .5f);
            typeof(PlayerAim).GetProperty("AimPoint").SetValue(aim, target.transform.position);
            typeof(PlayerAim).GetProperty("HasAimPoint").SetValue(aim, true);
            inventory.GetWeaponState(weapon).Restore(5, Time.time);
            Physics.SyncTransforms();
            int damage = 0; float committed = -1, fired = Time.time;
            health.OnDamaged += () => { damage++; committed = Time.time; };
            Call(shooter, "Shoot");
            var bolt = Object.FindObjectsByType<PlasmaBoltVFX>(FindObjectsInactive.Exclude).SingleOrDefault();
            Assert.That(shooter.CurrentAmmo, Is.EqualTo(4));
            Assert.That(health.CurrentHealth, Is.EqualTo(30), "No instant damage, including missing presentation");
            if (mode == 1) bolt.gameObject.SetActive(false);
            if (mode == 2) VfxPool.Release(bolt);
            if (mode == 3) Object.Destroy(bolt.gameObject);
            GameplaySuspensionController.Lease lease = null;
            if (mode == 5)
            {
                lease = suspension.Acquire(SuspensionReason.BeamTransport);
                Assert.That(shooter.enabled, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1));
            }
            if (mode == 6)
            {
                using (suspension.Acquire(SuspensionReason.ManualPause))
                {
                    double pausedUntil = EditorApplication.timeSinceStartup + .4;
                    while (EditorApplication.timeSinceStartup < pausedUntil) yield return EditorTestFrame.Next();
                    Assert.That(damage, Is.Zero, "A world pause also pauses accepted shots");
                    Assert.That(bolt.gameObject.activeSelf, Is.True);
                }
            }
            double timeout = EditorApplication.timeSinceStartup + 5;
            while (damage == 0 && EditorApplication.timeSinceStartup < timeout)
                yield return EditorTestFrame.Next();
            Assert.That(damage, Is.EqualTo(1), "Interruption mode " + mode);
            Assert.That(health.CurrentHealth, Is.EqualTo(20));
            Assert.That(target.GetComponent<HitReactionProbe>().ReceiveCount, Is.EqualTo(1));
            Assert.That(committed, Is.GreaterThanOrEqualTo(fired + .3f - .0001f));
            Assert.That(committed, Is.LessThanOrEqualTo(fired + .3f + Time.deltaTime + .01f));
            if (mode == 0 || mode >= 5) Assert.That(bolt.gameObject.activeSelf, Is.False, "Visual and damage arrive in the same frame");
            lease?.Dispose();
            yield return EditorTestFrame.Next(); Assert.That(damage, Is.EqualTo(1));
            Object.Destroy(target); yield return EditorTestFrame.Next();
        }
        int immediate = 0;
        Call(shooter, "SpawnShotVFX", muzzle.position, muzzle.position, null, (Action)(() => immediate++));
        Assert.That(immediate, Is.EqualTo(1), "Zero-length travel preserves immediate arrival");
        Object.Destroy(root); Object.Destroy(template.gameObject); Object.Destroy(weapon);
    }

    [UnityTest] public IEnumerator RepeatedDeathsReleaseOnlyTheirPrivateMaterials()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return VerifyDeathMaterials();
    }
    private static IEnumerator VerifyDeathMaterials()
    {
        var shared = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        for (int i = 0; i < 12; i++)
        {
            var corpse = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var renderer = corpse.GetComponent<Renderer>(); renderer.sharedMaterial = shared;
            var sequence = corpse.AddComponent<EnemyDeathSequence>();
            Set(sequence, "corpseHoldDuration", 0f); Set(sequence, "fadeDuration", .05f);
            corpse.GetComponent<EnemyHealth>().TakeDamage(30);
            var owned = renderer.sharedMaterial;
            Assert.That(owned, Is.Not.SameAs(shared));
            Call(sequence, "ApplyFade", .25f);
            Assert.That(owned.GetColor("_BaseColor").a, Is.EqualTo(.25f).Within(.001f));
            if (i % 2 == 0) { corpse.SetActive(false); Object.Destroy(corpse); }
            float until = Time.time + .12f;
            while (Time.time < until) yield return EditorTestFrame.Next();
            yield return EditorTestFrame.Next();
            Assert.That(owned == null, Is.True, "Owned native material must be destroyed without UnloadUnusedAssets");
            Assert.That(shared != null, Is.True);
            Assert.That(shared.GetColor("_BaseColor").a, Is.EqualTo(1));
        }
        Object.Destroy(shared);
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        Time.timeScale = 1;
    }
}
