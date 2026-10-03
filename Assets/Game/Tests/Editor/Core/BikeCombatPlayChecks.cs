using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// Uses BikeCameraTests' real ConstructionSite mount/Continue fixture and completion guard.
public static class BikeCombatPlayChecks
{
    private static PlayerBikeRider Rider => Object.FindAnyObjectByType<PlayerBikeRider>();
    private static AlienBikeController Bike => Object.FindAnyObjectByType<AlienBikeController>();
    private static readonly Vector3 Home = new(1000, .05f, 1000);
    private static void Set(Object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static IEnumerator Seconds(float duration)
    {
        float end = Time.time + duration; double deadline = EditorApplication.timeSinceStartup + duration + 30;
        while (Time.time < end && EditorApplication.timeSinceStartup < deadline)
        {
            Foreground(); yield return EditorTestFrame.Next();
        }
        Assert.That(Time.time, Is.GreaterThanOrEqualTo(end));
    }
    private static void Foreground()
    {
        if (Time.timeScale > 0) return;
        ActiveRunController.Instance?.SendMessage("OnApplicationFocus", true);
        ActiveRunController.Instance?.SendMessage("OnApplicationPause", false);
        Object.FindAnyObjectByType<InGameMenuController>()?.ResumeGame();
    }
    private static IEnumerator Mount()
    {
        Assert.That(Rider.TryMount(Bike), Is.True);
        for (int i = 0; i < 60 && !Rider.IsDriving; i++) yield return Seconds(.1f);
        Assert.That(Rider.IsDriving, Is.True);
        Bike.Secure();
    }
    private static NavMeshDataInstance Arena()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Disposable bike combat floor";
        floor.transform.position = Home + Vector3.down * .55f;
        floor.transform.localScale = new(40, 1, 40);
        var sources = new List<NavMeshBuildSource> { new() { shape = NavMeshBuildSourceShape.Box,
            transform = floor.transform.localToWorldMatrix, size = Vector3.one } };
        var nav = NavMesh.AddNavMeshData(NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
            new Bounds(Vector3.zero, new Vector3(40, 5, 40)), Home, Quaternion.identity));
        Bike.transform.SetPositionAndRotation(Home + Vector3.up * Bike.hoverHeight, Quaternion.identity);
        Bike.Body.position = Bike.transform.position; Bike.Body.rotation = Quaternion.identity;
        Physics.SyncTransforms();
        return nav;
    }
    private static EnemyActor Spawn(Vector3 point, string weapon = null)
    {
        var staging = new GameObject("Inactive combat staging"); staging.SetActive(false);
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyCombatantSetup.PrefabPath), staging.transform);
        go.transform.SetPositionAndRotation(point, Quaternion.LookRotation(Vector3.ProjectOnPlane(Bike.transform.position - point, Vector3.up)));
        Set(go.GetComponent<EnemyEquipment>(), "startingWeapon", weapon != null ? EnemyCombatantSetup.Weapon(weapon) : null);
        Set(go.GetComponent<EnemyWeaponAwareness>(), "allowWeaponPickup", false);
        go.GetComponentInChildren<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
        go.transform.SetParent(null); Object.Destroy(staging);
        return go.GetComponent<EnemyActor>();
    }
    public static IEnumerator MountedCombat()
    {
        yield return Mount(); var nav = Arena();
        try
        {
            yield return Seconds(.2f);
            var health = Rider.GetComponent<PlayerHealth>(); health.RestoreRunHealth(100, 0);
            var ranged = Spawn(Home + Vector3.back * 6, "PlasmaPistolItem");
            int shots = 0, occupiedHits = 0;
            ranged.GetComponent<EnemyRangedAttack>().ShotFired += (hit, collider) =>
            { shots++; if (PlayerBikeRider.IsOccupiedTargetCollider(Rider.transform, collider)) occupiedHits++; };
            yield return Seconds(3);
            Assert.That(ranged.Perception.Target, Is.SameAs(Rider.transform));
            Assert.That(ranged.Perception.CanSeeTarget, Is.True);
            Assert.That(shots, Is.GreaterThan(0), "Normal authored enemy firing cadence");
            Assert.That(occupiedHits, Is.GreaterThan(0), "Real physical shots hit the occupied hull");
            Assert.That(health.CurrentHealth, Is.LessThan(100));
            Assert.That(Rider.GetComponent<CharacterController>().enabled, Is.False);
            ranged.Motor.SetMovementLock(EnemyMovementLockReason.Stun, true);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = Vector3.Lerp(ranged.transform.position, Bike.transform.position, .5f) + Vector3.up;
            wall.transform.localScale = new(12, 5, .4f); Physics.SyncTransforms();
            yield return Seconds(.5f);
            Assert.That(ranged.Perception.CanSeeTarget, Is.False, "Real wall still blocks mounted LOS");
            var buffer = new RaycastHit[32];
            Assert.That(WeaponShotQuery.Clear(ranged.transform, Rider.transform, ranged.transform.position + Vector3.up * 1.3f,
                Rider.MountedAimPoint, buffer), Is.False);
            Object.Destroy(wall); yield return Seconds(.1f);
            var melee = Spawn(Home + Vector3.right * 5);
            yield return Seconds(.5f);
            Assert.That(melee.Perception.Target, Is.SameAs(Rider.transform));
            Assert.That(melee.GetComponent<EnemyBrain>().State, Is.EqualTo(EnemyBrainState.Chase));
            float before = health.CurrentHealth;
            for (int i = 0; i < 60 && health.CurrentHealth == before; i++) yield return Seconds(.1f);
            Assert.That(health.CurrentHealth, Is.LessThan(before), "Melee approaches the hull and resolves an authored physical contact; position="
                + melee.transform.position + " hull=" + Bike.GetComponent<Collider>().bounds + " state=" + melee.GetComponent<EnemyBrain>().State
                + " locks=" + melee.Motor.MovementLocks + " attacking=" + melee.GetComponent<EnemyMeleeAttack>().IsAttacking);
            melee.Motor.SetMovementLock(EnemyMovementLockReason.Stun, true);
            var hull = Bike.GetComponent<Collider>();
            var hit = new HitInfo(7, hull.bounds.center, Vector3.back, Vector3.forward, ranged.gameObject);
            before = health.CurrentHealth; CombatHitResolver.Resolve(hull, hit);
            Assert.That(health.CurrentHealth, Is.EqualTo(before - 7).Within(.001));
            // Existing Amy collider still routes directly to PlayerHealth; no alternate HP pool.
            before = health.CurrentHealth; CombatHitResolver.Resolve(Rider.GetComponent<CharacterController>(), hit);
            Assert.That(health.CurrentHealth, Is.EqualTo(before - 7).Within(.001));
            Rider.AbortRide(); before = health.CurrentHealth; CombatHitResolver.Resolve(hull, hit);
            Assert.That(health.CurrentHealth, Is.EqualTo(before), "Empty parked bike never redirects damage");
        }
        finally { if (nav.valid) nav.Remove(); }
    }
    public static IEnumerator Ram()
    {
        yield return Mount(); var nav = Arena();
        try
        {
            yield return Seconds(1.2f); // Let the actual gameplay camera settle after fixture relocation.
            var enemy = Spawn(Home + Vector3.forward * 4);
            enemy.GetComponent<EnemyBrain>().enabled = false;
            yield return Seconds(.1f);
            var health = enemy.GetComponent<EnemyHealth>(); var body = enemy.GetComponent<Collider>();
            var impact = Bike.GetComponent<AlienBikeImpact>();
            Assert.That(impact, Is.Not.Null);
            float before = health.CurrentHealth;
            Assert.That(impact.TryImpact(body, Vector3.forward * 2, body.bounds.center), Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(before)); Assert.That(enemy.Motor.IsExternallyDisplaced, Is.False);
            var extra = new GameObject("Second collider"); extra.transform.SetParent(enemy.transform, false); var second = extra.AddComponent<BoxCollider>();
            Assert.That(impact.TryImpact(body, Vector3.forward * 10, body.bounds.center), Is.True);
            float expected = Mathf.Lerp(20, 140, (10 - 4.5f) / (20 - 4.5f));
            Assert.That(health.CurrentHealth, Is.EqualTo(before - expected).Within(.01));
            Assert.That(impact.TryImpact(second, Vector3.forward * 10, body.bounds.center), Is.False, "Enemy-root deduplication");
            Vector3 start = enemy.transform.position;
            yield return Seconds(.25f);
            Assert.That(enemy.transform.position.y, Is.GreaterThan(start.y + .2f));
            Assert.That(enemy.transform.position.z, Is.GreaterThan(start.z + 1));
            ProceduralUIReview.Capture("bike-ram-surviving-launch", 1280, 720);
            var snapshot = enemy.GetComponent<RunWorldObject>()?.Capture();
            if (snapshot != null) Assert.That(snapshot.position.y, Is.LessThan(start.y + .1f));
            yield return Seconds(1.6f);
            Assert.That(enemy.Motor.IsExternallyDisplaced, Is.False); Assert.That(enemy.Motor.IsReady, Is.True);
            Assert.That(enemy.Motor.MovementLocked, Is.False);
            Assert.That(enemy.Motor.SetDestination(Home + Vector3.forward * 10), Is.True);
            yield return Seconds(.35f); Assert.That(enemy.Motor.Velocity.magnitude, Is.GreaterThan(.1f), "Navigation resumes");
            var lethal = Spawn(Home + Vector3.left * 5); lethal.GetComponent<EnemyBrain>().enabled = false;
            yield return Seconds(.1f);
            Vector3 lethalStart = lethal.transform.position;
            Assert.That(impact.TryImpact(lethal.GetComponent<Collider>(), Vector3.forward * 30, lethalStart), Is.True);
            Assert.That(lethal.GetComponent<EnemyHealth>().IsDead, Is.True);
            yield return Seconds(.25f);
            Assert.That(lethal.transform.position.y, Is.GreaterThan(lethalStart.y + .35f));
            Assert.That(lethal.transform.position.z, Is.GreaterThan(lethalStart.z + 1.5f));
            ProceduralUIReview.Capture("bike-ram-lethal-launch", 1280, 720);
            yield return Seconds(1.5f);
            Assert.That(lethal.GetComponent<EnemyHealth>().IsDead, Is.True);
            Assert.That(lethal.Motor.Agent.enabled, Is.False, "Death retains navigation ownership");
            var physical = Spawn(Home + Vector3.forward * 4);
            physical.GetComponent<EnemyBrain>().enabled = false;
            yield return Seconds(.1f);
            Bike.StartDriving();
            var input = Rider.GetComponent<StarterAssets.StarterAssetsInputs>();
            input.MoveInput(Vector2.up);
            for (int i = 0; i < 25 && physical.Health.CurrentHealth == physical.Health.MaxHealth; i++)
                yield return Seconds(.1f);
            input.MoveInput(Vector2.zero); Bike.Secure();
            Assert.That(physical.Health.CurrentHealth, Is.LessThan(physical.Health.MaxHealth), "Actual Rigidbody collision dispatches the ram");
        }
        finally { if (nav.valid) nav.Remove(); }
    }
    public static IEnumerator RamObstructions()
    {
        yield return Mount(); var nav = Arena();
        try
        {
            var enemy = Spawn(Home + Vector3.forward * 4); enemy.GetComponent<EnemyBrain>().enabled = false;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = Home + new Vector3(0, 2, 6); wall.transform.localScale = new(6, 4, .3f);
            yield return Seconds(.1f);
            Assert.That(Bike.GetComponent<AlienBikeImpact>().TryImpact(enemy.GetComponent<Collider>(), Vector3.forward * 10, enemy.transform.position), Is.True);
            yield return Seconds(1.6f);
            Assert.That(enemy.Motor.IsReady, Is.True); Assert.That(enemy.Motor.IsExternallyDisplaced, Is.False);
            Assert.That(enemy.transform.position.z, Is.LessThan(wall.GetComponent<Collider>().bounds.min.z - enemy.Motor.Agent.radius + .05f));
            var edge = Spawn(Home + Vector3.right * 17); edge.GetComponent<EnemyBrain>().enabled = false;
            yield return Seconds(.1f);
            Assert.That(Bike.GetComponent<AlienBikeImpact>().TryImpact(edge.GetComponent<Collider>(), Vector3.right * 10, edge.transform.position), Is.True);
            yield return Seconds(1.6f);
            Assert.That(edge.Motor.IsReady, Is.True); Assert.That(edge.Motor.IsExternallyDisplaced, Is.False);
            Assert.That(edge.transform.position.x, Is.LessThan(Home.x + 19.8f), "Missing safe ground stops the horizontal launch");
            Assert.That(edge.transform.position.y, Is.LessThan(Home.y + .2f));
        }
        finally { if (nav.valid) nav.Remove(); }
    }
    public static IEnumerator SaveContinue()
    {
        yield return Mount();
        // Use authored identities and the scene's own NavMesh so a real reload can restore them.
        var enemies = Object.FindObjectsByType<EnemyActor>(FindObjectsInactive.Include)
            .Where(e => e.GetComponent<RunWorldObject>() != null && !e.Health.IsDead && e.Health.MaxHealth >= 140)
            .OrderBy(e => e.GetComponent<RunWorldObject>().Id).Take(2).ToArray();
        Assert.That(enemies.Length, Is.EqualTo(2));
        foreach (var enemy in enemies)
        {
            enemy.gameObject.SetActive(true);
            for (var parent = enemy.transform.parent; parent != null; parent = parent.parent) parent.gameObject.SetActive(true);
            enemy.GetComponent<EnemyBrain>().enabled = false;
        }
        yield return Seconds(.2f);
        // Do not let unspecified scene-search order choose a legacy low-HP actor or a
        // launch straight into authored cover. Restore still uses the real scene/identities.
        var placedPositions = new List<Vector3>();
        foreach (var enemy in enemies)
        {
            bool placed = false;
            for (int x = -12; x <= 12 && !placed; x += 3)
                for (int z = -12; z <= 12 && !placed; z += 3)
                {
                    Vector3 candidate = Bike.transform.position + new Vector3(x, -Bike.hoverHeight, z);
                    if (!NavMesh.SamplePosition(candidate, out var ground, 1.5f, NavMesh.AllAreas)
                        || placedPositions.Any(p => Vector3.Distance(p, ground.position) < 6)
                        || Physics.CheckBox(ground.position + Vector3.up * 1.5f, new Vector3(2, 1.1f, 2), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                    Assert.That(enemy.Motor.Agent.Warp(ground.position), Is.True, "Place authored enemy on clear scene ground");
                    enemy.transform.position = ground.position;
                    placedPositions.Add(ground.position);
                    Physics.SyncTransforms(); placed = true;
                }
            Assert.That(placed, Is.True, "Find open native ground for airborne snapshot");
            Assert.That(enemy.Motor.IsReady, Is.True, enemy.name + " navigation before ram");
        }
        var ids = enemies.Select(e => e.GetComponent<RunWorldObject>().Id).ToArray();
        var impact = Bike.GetComponent<AlienBikeImpact>();
        Assert.That(impact.TryImpact(enemies[0].GetComponent<Collider>(), Vector3.forward * 10, enemies[0].transform.position), Is.True);
        Assert.That(impact.TryImpact(enemies[1].GetComponent<Collider>(), Vector3.forward * 30, enemies[1].transform.position), Is.True);
        Assert.That(enemies[1].Health.IsDead, Is.True, "Turbo impact is lethal through normal health");
        Assert.That(enemies[0].Motor.IsExternallyDisplaced, Is.True, "Impact immediately starts native enemy launch");
        Vector3 initial = enemies[0].transform.position;
        yield return Seconds(.2f);
        Assert.That(enemies[0].Motor.IsExternallyDisplaced, Is.True, "Living victim is mid-launch at save: " + enemies[0].name
            + " from=" + initial + " to=" + enemies[0].transform.position + " HP=" + enemies[0].Health.CurrentHealth);
        Assert.That(enemies[0].transform.position.y, Is.GreaterThan(initial.y + .1f), "Snapshot is actually airborne");
        float hp = enemies[0].GetComponent<EnemyHealth>().CurrentHealth;
        Vector3 savedGround = enemies[0].Motor.RunPosition;
        var run = ActiveRunController.Instance; Assert.That(run.Save(), Is.True); run.PrepareToLeave();
        Assert.That(RunSaveService.Continue(), Is.True, RunSaveService.LastError);
        double end = EditorApplication.timeSinceStartup + 30;
        do { yield return EditorTestFrame.Next(); }
        while ((ActiveRunController.Instance == null || !ActiveRunController.Instance.IsReady) && EditorApplication.timeSinceStartup < end);
        Assert.That(ActiveRunController.Instance.IsReady, Is.True); Assert.That(Time.timeScale, Is.Zero);
        var objects = Object.FindObjectsByType<RunWorldObject>(FindObjectsInactive.Include);
        var living = objects.Single(o => o.Id == ids[0]); var dead = objects.SingleOrDefault(o => o.Id == ids[1]);
        Assert.That(living.GetComponent<EnemyHealth>().CurrentHealth, Is.EqualTo(hp).Within(.01));
        Assert.That(Vector3.Distance(living.transform.position, savedGround), Is.LessThan(.15f));
        Assert.That(living.GetComponent<EnemyMotor>().IsExternallyDisplaced, Is.False);
        Assert.That(dead == null || !dead.gameObject.activeInHierarchy, Is.True);
        Assert.That(Rider.IsBusy, Is.False);
    }
}
