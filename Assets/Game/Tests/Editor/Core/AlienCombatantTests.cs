using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class AlienCombatantTests
{
    static readonly Vector3 Home = new Vector3(1000, .05f, 1000);
    GameObject target;
    NavMeshDataInstance nav;
    Camera camera;
    readonly List<GameObject> actors = new();
    readonly List<string> results = new();
    [UnitySetUp] public IEnumerator Setup()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        Application.runInBackground = true; Time.timeScale = 1;
        Directory.CreateDirectory("Logs/AlienCombat");
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = Home + Vector3.down * .55f;
        floor.transform.localScale = new Vector3(40, 1, 40);
        var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = floor.transform.localToWorldMatrix, size = Vector3.one } };
        nav = NavMesh.AddNavMeshData(NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources, new Bounds(Vector3.zero, new Vector3(40, 5, 40)), Home, Quaternion.identity));
        target = new GameObject("Amy target", typeof(CapsuleCollider), typeof(PlayerHealth)); target.tag = "Player";
        var capsule = target.GetComponent<CapsuleCollider>(); capsule.radius = .28f; capsule.height = 1.8f; capsule.center = Vector3.up * .9f;
        target.transform.position = Home + Vector3.forward * 5;
        var amy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Player/Characters/Amy.prefab"), target.transform);
        amy.GetComponentInChildren<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
        camera = new GameObject("Review camera", typeof(Camera)).GetComponent<Camera>(); camera.enabled = false;
        camera.transform.position = Home + new Vector3(3, 2.2f, -3); camera.transform.LookAt(Home + Vector3.up * .9f);
        camera.fieldOfView = 42; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f,.09f,.13f);
        var light = new GameObject("Review light", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(45,-35,0);
        yield return EditorTestFrame.Next();
    }
    [UnityTearDown] public IEnumerator Teardown()
    {
        File.WriteAllLines("Logs/AlienCombat/results-" + TestContext.CurrentContext.Test.Name + ".txt", results);
        if (nav.valid) nav.Remove();
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
    GameObject Spawn(string weapon = null, Vector3? offset = null, bool pickup = false)
    {
        var staging = new GameObject("Inactive fixture staging"); staging.SetActive(false);
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyCombatantSetup.PrefabPath), staging.transform);
        actor.transform.SetPositionAndRotation(Home + (offset ?? Vector3.zero), Quaternion.identity);
        Set(actor.GetComponent<EnemyEquipment>(), "startingWeapon", weapon == null ? null : EnemyCombatantSetup.Weapon(weapon));
        Set(actor.GetComponent<EnemyWeaponAwareness>(), "allowWeaponPickup", pickup);
        var config = Object.Instantiate(actor.GetComponent<EnemyEquipment>().Profile); config.spreadDegrees = 0;
        Set(actor.GetComponent<EnemyEquipment>(), "profile", config);
        actor.GetComponentInChildren<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
        actor.transform.SetParent(null); Object.Destroy(staging); actors.Add(actor); return actor;
    }
    [UnityTest]
    public IEnumerator LiveAimBodyCacheStaysAboveFeet()
    {
        var actor = Spawn();
        for (int i = 0; i < 10; i++) yield return EditorTestFrame.Next();
        var aimTarget = actor.GetComponent<AimTarget>();
        TestContext.WriteLine($"Live cached body={aimTarget.BodyCenter - actor.transform.position}, radius={aimTarget.BodyRadius}");
        Assert.That(aimTarget.BodyCenter.y - actor.transform.position.y, Is.GreaterThan(.5f));
    }

    [UnityTest, Timeout(180000)] public IEnumerator SpawnWeaponsAndTransitions()
    {
        foreach (string weapon in new[] { "PlasmaPistolItem", "PlasmaRifleItem" })
        {
            target.GetComponent<PlayerHealth>().RestoreRunHealth(100,50); target.transform.position = Home + Vector3.forward * 5;
            var actor = Spawn(weapon); var equipment = actor.GetComponent<EnemyEquipment>(); var ranged = actor.GetComponent<EnemyRangedAttack>();
            int shots = 0; ranged.ShotFired += (h,c) => { shots++; results.Add(weapon + " shot " + Time.time + " -> " + (c == null ? "miss" : c.name)); };
            yield return Seconds(2.5f);
            var animator = actor.GetComponentInChildren<Animator>();
            results.Add(weapon + " shots=" + shots + " state=" + actor.GetComponent<EnemyBrain>().State + " ready=" + actor.GetComponent<EnemyCombatPresentation>().ReadyToFire
                + " muzzle=" + equipment.Muzzle.position + " forward=" + equipment.Muzzle.forward + " angle=" + Vector3.Angle(equipment.Muzzle.forward, target.transform.position + Vector3.up*.9f - equipment.Muzzle.position));
            Capture(weapon + "-aim");
            Assert.That(equipment.HasWeapon, Is.True); Assert.That(shots, Is.GreaterThan(0), weapon + " must fire physical shots");
            Assert.That(target.GetComponent<PlayerHealth>().CurrentArmor, Is.LessThan(50));
            target.transform.position = actor.transform.position + actor.transform.forward * .7f;
            yield return Seconds(.8f); Assert.That(ranged.InCloseMode, Is.True); Capture(weapon + "-melee");
            target.transform.position = actor.transform.position + actor.transform.forward * 1.3f;
            yield return Seconds(.1f); Assert.That(ranged.InCloseMode, Is.True, "Hysteresis holds melee between thresholds");
            target.transform.position = actor.transform.position + actor.transform.forward * 5;
            yield return Seconds(2f); Assert.That(ranged.InCloseMode, Is.False); Assert.That(equipment.HasWeapon, Is.True);
            string saved = equipment.CaptureRunState(); int ammo = equipment.Ammo; equipment.Unequip();
            equipment.RestoreRunState(saved); Assert.That(equipment.Ammo, Is.EqualTo(ammo)); Assert.That(equipment.Weapon, Is.EqualTo(EnemyCombatantSetup.Weapon(weapon)));
            equipment.Unequip(); yield return Seconds(.1f); Assert.That(equipment.HasWeapon, Is.False); Assert.That(actor.GetComponent<EnemyBrain>().State, Is.EqualTo(EnemyBrainState.Chase));
            Object.Destroy(actor); yield return EditorTestFrame.Next();
        }
    }
    [UnityTest, Timeout(180000)] public IEnumerator InterruptionAndCover()
    {
        var actor = Spawn("PlasmaPistolItem"); var ranged = actor.GetComponent<EnemyRangedAttack>(); int shots = 0;
        ranged.ShotFired += (h,c) => shots++;
        yield return Seconds(.35f);
        var motor = actor.GetComponent<EnemyMotor>(); motor.SetMovementLock(EnemyMovementLockReason.Stun, true);
        int before = shots; yield return Seconds(1.5f); Assert.That(shots, Is.EqualTo(before), "Stun cancels pending aim/fire");
        motor.SetMovementLock(EnemyMovementLockReason.Stun, false); yield return Seconds(1.3f); Assert.That(shots, Is.GreaterThan(before));
        actor.GetComponent<EnemyHitReaction>().ReceiveHit(new HitInfo(1,actor.transform.position,Vector3.back,Vector3.forward,target));
        before = shots; yield return Seconds(.35f); Assert.That(shots, Is.EqualTo(before), "Hit reaction cannot fire");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = Home + new Vector3(0,1,2);
        wall.transform.localScale = new Vector3(12,3,.4f); Physics.SyncTransforms();
        before = shots; float armor = target.GetComponent<PlayerHealth>().CurrentArmor;
        yield return Seconds(1); Assert.That(shots, Is.EqualTo(before), "Concrete blocks muzzle/perception");
        Assert.That(target.GetComponent<PlayerHealth>().CurrentArmor, Is.EqualTo(armor)); Capture("cover");
        Object.Destroy(wall); yield return Seconds(1.5f);
        ranged.enabled = false; before = shots; yield return Seconds(1); Assert.That(shots, Is.EqualTo(before), "Disabled action cannot fire through Brain.Tick");
    }
    [UnityTest, Timeout(180000)] public IEnumerator PickupCompetitionPressureAndPersistence()
    {
        target.transform.position = Home + Vector3.forward * 18;
        var a = Spawn(null,new Vector3(-1,0,0),true); var b = Spawn(null,new Vector3(1,0,0),true);
        var gun = Object.Instantiate(EnemyCombatantSetup.Weapon("PlasmaPistolItem").worldPrefab,Home+Vector3.forward*2,Quaternion.identity);
        yield return Seconds(4);
        int owners = (a.GetComponent<EnemyEquipment>().HasWeapon?1:0)+(b.GetComponent<EnemyEquipment>().HasWeapon?1:0);
        Assert.That(owners, Is.EqualTo(1)); Assert.That(gun != null && !gun.activeSelf, Is.True);
        Assert.That(a.GetComponent<EnemyWeaponAwareness>().ReservedWeapon == null && b.GetComponent<EnemyWeaponAwareness>().ReservedWeapon == null, Is.True);
        Capture("pickup-competition");
        var equipped = a.GetComponent<EnemyEquipment>().HasWeapon ? a : b; var state = equipped.GetComponent<EnemyEquipment>().CaptureRunState();
        equipped.GetComponent<EnemyEquipment>().Unequip(); equipped.GetComponent<EnemyEquipment>().RestoreRunState(state); Assert.That(equipped.GetComponent<EnemyEquipment>().HasWeapon, Is.True);
        Object.Destroy(gun);
        Object.Destroy(a); Object.Destroy(b); yield return EditorTestFrame.Next();
        var c = Spawn(null,Vector3.zero,true);
        var distant = Object.Instantiate(EnemyCombatantSetup.Weapon("PlasmaRifleItem").worldPrefab,Home+Vector3.right*4,Quaternion.identity);
        target.transform.position = Home + Vector3.forward * .7f;
        yield return Seconds(.4f); Assert.That(c.GetComponent<EnemyWeaponAwareness>().ReservedWeapon, Is.Null); Assert.That(c.GetComponent<EnemyEquipment>().HasWeapon, Is.False);
        target.transform.position=Home+Vector3.forward*18;
        yield return Seconds(.9f);
        Assert.That(c.GetComponent<EnemyWeaponAwareness>().ReservedWeapon,Is.Not.Null);
        c.GetComponent<EnemyHitReaction>().ReceiveHit(new HitInfo(1,c.transform.position,Vector3.back,Vector3.forward,target));
        Assert.That(c.GetComponent<EnemyWeaponAwareness>().ReservedWeapon,Is.Null,"Unseen incoming hit releases the gun before Brain's investigation wait");
        Assert.That(distant.GetComponent<PickupItem>().CanReserve(c.GetComponent<EnemyEquipment>()),Is.True);
        results.Add("Single ownership, local pickup and pressure cancellation passed.");
    }
    [UnityTest, Timeout(180000)] public IEnumerator AcquiredWeaponReturnsOriginalPickupOnceOnDeath()
    {
        target.transform.position = Home + Vector3.forward * 18;
        foreach (bool authoredDrop in new[] { false, true })
        {
            var actor = Spawn(); var equipment = actor.GetComponent<EnemyEquipment>();
            Set(equipment, "dropWeaponOnDeath", authoredDrop);
            yield return EditorTestFrame.Next();
            var weapon = EnemyCombatantSetup.Weapon("PlasmaPistolItem");
            var gun = Object.Instantiate(weapon.worldPrefab, Home + Vector3.right * 3, Quaternion.identity);
            var pickup = gun.GetComponent<PickupItem>();
            yield return EditorTestFrame.Next(); // Cache the original floating anchor.
            Assert.That(pickup.TryReserve(equipment), Is.True);
            Assert.That(equipment.TryAcquire(pickup, equipment), Is.True);
            Assert.That(equipment.HasWeapon, Is.True); Assert.That(equipment.Muzzle, Is.Not.Null);
            Assert.That(equipment.SpendRound(), Is.True);
            var world = gun.GetComponent<RunWorldObject>(); string id = world.Id;
            Assert.That(world.Capture().removed, Is.False); Assert.That(gun.activeSelf, Is.False);
            Assert.That(equipment.TryAcquire(pickup, equipment), Is.False);
            actor.transform.position = Home + Vector3.right * 5;
            var loot = actor.GetComponent<EnemyPlasmaLoot>();
            Assert.That(loot, Is.Not.Null);
            Set(loot, "plasmaDropChance", 1f); Set(loot, "plasmaDropMin", 2); Set(loot, "plasmaDropMax", 2);
            int capsulesBefore = PickupItem.Available.Count(p => p.Item is CapsuleItemData);
            actor.GetComponent<EnemyHealth>().TakeDamage(1000);
            Assert.That(PickupItem.Available.Count(p => p.Item is CapsuleItemData), Is.EqualTo(capsulesBefore + 1));
            actor.GetComponent<EnemyHealth>().TakeDamage(1000);
            Assert.That(PickupItem.Available.Count(p => p.Item is CapsuleItemData), Is.EqualTo(capsulesBefore + 1));
            Assert.That(equipment.HasWeapon, Is.False);
            Assert.That(gun != null && gun.activeSelf, Is.True);
            Assert.That(world.Id, Is.EqualTo(id)); Assert.That(world.Capture().removed, Is.False);
            Assert.That(equipment.Drop(), Is.Null, "Death already returned the weapon");
            yield return Seconds(.15f);
            Assert.That(gun.transform.position.x, Is.EqualTo(actor.transform.position.x).Within(.01f), "Bobbing must use the dropped position");
            int copies = 0; foreach (var item in PickupItem.Available) if (item.Item == weapon) copies++;
            Assert.That(copies, Is.EqualTo(1));
            Object.Destroy(actor); yield return EditorTestFrame.Next();
            Assert.That(gun != null && gun.activeSelf, Is.True, "Corpse cleanup cannot destroy the pickup");
            var player = new GameObject("Recover weapon", typeof(BoxCollider), typeof(PlayerInventory));
            gun.SendMessage("OnTriggerEnter", player.GetComponent<Collider>());
            Assert.That(player.GetComponent<PlayerInventory>().Items, Is.EqualTo(new[] { weapon }));
            yield return EditorTestFrame.Next(); Assert.That(gun == null, Is.True);
            Object.Destroy(player);
        }
    }
    [UnityTest, Timeout(180000)] public IEnumerator AcquiredOwnershipAndWorldIdentitySurviveResumeThenDeath()
    {
        target.transform.position = Home + Vector3.forward * 18;
        var runOwner = new GameObject("Isolated world registry");
        var run = runOwner.AddComponent<ActiveRunController>(); run.enabled = false;
        var weapon = EnemyCombatantSetup.Weapon("PlasmaPistolItem");
        var actor = Spawn(); var equipment = actor.GetComponent<EnemyEquipment>();
        yield return EditorTestFrame.Next();
        var gun = Object.Instantiate(weapon.worldPrefab, Home + Vector3.right * 3, Quaternion.identity);
        var world = RunWorldObject.TrackSpawn(gun, weapon.worldPrefab);
        var pickup = gun.GetComponent<PickupItem>(); pickup.TryReserve(equipment);
        Assert.That(equipment.TryAcquire(pickup, equipment), Is.True);
        equipment.SpendRound(); int ammo = equipment.Ammo;
        string savedEquipment = equipment.CaptureRunState();
        var snapshot = JsonUtility.FromJson<SavedWorldObject>(JsonUtility.ToJson(world.Capture()));
        Object.Destroy(actor); Object.Destroy(gun); yield return EditorTestFrame.Next();
        // Recreate saved world entities exactly as Continue does, retaining identity.
        var restoredGun = Object.Instantiate(RunContentCatalog.Instance.Resolve<GameObject>(snapshot.prefab));
        var restoredWorld = restoredGun.AddComponent<RunWorldObject>(); restoredWorld.ConfigureIdentity(snapshot.id); run.Register(restoredWorld);
        var restoredActor = Spawn(); var restoredEquipment = restoredActor.GetComponent<EnemyEquipment>();
        yield return EditorTestFrame.Next();
        restoredEquipment.RestoreRunState(savedEquipment); restoredWorld.Restore(snapshot);
        restoredEquipment.RestoreRunState(savedEquipment); // Continue's second pass must be idempotent.
        Assert.That(restoredEquipment.Ammo, Is.EqualTo(ammo));
        Assert.That(restoredGun.activeSelf, Is.False); Assert.That(restoredWorld.Capture().removed, Is.False);
        restoredActor.GetComponent<EnemyHealth>().TakeDamage(1000);
        Assert.That(restoredGun.activeSelf, Is.True); Assert.That(restoredWorld.Id, Is.EqualTo(snapshot.id));
        var droppedSave = JsonUtility.FromJson<SavedWorldObject>(JsonUtility.ToJson(restoredWorld.Capture()));
        Assert.That(droppedSave.active && !droppedSave.removed, Is.True);
        restoredGun.SetActive(false); restoredWorld.Restore(droppedSave);
        int copies = 0; foreach (var item in PickupItem.Available) if (item.Item == weapon) copies++;
        Assert.That(copies, Is.EqualTo(1));
        Object.Destroy(runOwner);
    }
    [UnityTest, Timeout(180000)] public IEnumerator StartingWeaponsKeepAuthoredDeathDropPolicy()
    {
        target.transform.position = Home + Vector3.forward * 18;
        foreach (bool authoredDrop in new[] { false, true })
        {
            var actor = Spawn("PlasmaPistolItem"); Set(actor.GetComponent<EnemyEquipment>(), "dropWeaponOnDeath", authoredDrop);
            yield return EditorTestFrame.Next();
            actor.GetComponent<EnemyHealth>().TakeDamage(1000);
            int copies = 0; GameObject dropped = null;
            foreach (var item in PickupItem.Available) if (item.Item == EnemyCombatantSetup.Weapon("PlasmaPistolItem")) { copies++; dropped = item.gameObject; }
            Assert.That(copies, Is.EqualTo(authoredDrop ? 1 : 0));
            Object.Destroy(actor); if (dropped != null) Object.Destroy(dropped);
            yield return EditorTestFrame.Next();
        }
    }
    [UnityTest, Timeout(180000)] public IEnumerator MixedEncounterAndAlternateVisual()
    {
        Set(target.GetComponent<PlayerHealth>(), "maxHealth", 5000f); target.GetComponent<PlayerHealth>().RestoreRunHealth(5000,50);
        foreach(int encounterSeed in new[]{31,117,409})
        {
        UnityEngine.Random.InitState(encounterSeed);target.GetComponent<PlayerHealth>().RestoreRunHealth(5000,50);
        var a=Spawn("PlasmaPistolItem",new Vector3(-1.5f,0,0));
        var b=Spawn("PlasmaRifleItem",new Vector3(1.5f,0,0));
        var c=Spawn(null,new Vector3(0,0,3)); int shots=0, contacts=0;
        a.GetComponent<EnemyRangedAttack>().ShotFired+=(h,col)=>{shots++;Assert.That(a.GetComponent<EnemyMotor>().MovementLocked,Is.False);};
        b.GetComponent<EnemyRangedAttack>().ShotFired+=(h,col)=>{shots++;Assert.That(b.GetComponent<EnemyMotor>().MovementLocked,Is.False);};
        c.GetComponent<EnemyMeleeAttack>().ContactResolved+=h=>contacts++;
        for(int sample=0;sample<10;sample++)
        {
            yield return Seconds(.75f);results.Add("Mixed "+sample+" shots="+shots+" pistol "+Diagnostic(a)+" rifle "+Diagnostic(b));
        }
        Capture("mixed-three"); Assert.That(shots,Is.GreaterThan(3)); Assert.That(contacts,Is.GreaterThan(0));
        results.Add("Mixed encounter shots="+shots+", melee contacts="+contacts);
        foreach(var actor in new[]{a,b,c})Object.Destroy(actor);
        yield return EditorTestFrame.Next();
        }
        var staging=new GameObject("Alternate visual fixture");staging.SetActive(false);
        var alternate=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyCombatantSetup.PrefabPath),staging.transform);
        var presentation=alternate.GetComponent<EnemyCombatPresentation>();
        Object.DestroyImmediate(presentation.Visual.gameObject);
        var granny=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Player/Characters/SportyGranny.prefab"),alternate.transform);
        Set(presentation,"visual",granny.GetComponent<CharacterVisual>());
        Set(alternate.GetComponent<EnemyEquipment>(),"startingWeapon",EnemyCombatantSetup.Weapon("PlasmaPistolItem"));
        granny.GetComponentInChildren<Animator>().cullingMode=AnimatorCullingMode.AlwaysAnimate;
        // A new visual's authored default socket is independent of the removed reference rig's optional mounts.
        alternate.transform.position=Home;alternate.transform.SetParent(null);Object.Destroy(staging);
        target.transform.position=Home+Vector3.forward*5;int alternateShots=0;
        alternate.GetComponent<EnemyRangedAttack>().ShotFired+=(h,col)=>alternateShots++;
        yield return Seconds(2.5f);Capture("alternate-granny");
        Assert.That(alternate.GetComponent<EnemyEquipment>().HasWeapon,Is.True);Assert.That(alternateShots,Is.GreaterThan(0));
        alternate.GetComponent<EnemyEquipment>().Drop();Assert.That(alternate.GetComponent<EnemyEquipment>().HasWeapon,Is.False);
        results.Add("Alternate Humanoid fired "+alternateShots+" shots and dropped its weapon.");
    }
    [UnityTest, Timeout(180000)] public IEnumerator PickupOcclusionAndInactiveSave()
    {
        target.transform.position=Home+Vector3.forward*18;
        var a=Spawn(null,Vector3.zero,true);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=Home+new Vector3(0,1,1.5f);wall.transform.localScale=new Vector3(8,3,.4f);
        var gun=Object.Instantiate(EnemyCombatantSetup.Weapon("PlasmaPistolItem").worldPrefab,Home+Vector3.forward*3,Quaternion.identity);
        yield return Seconds(1);Assert.That(a.GetComponent<EnemyWeaponAwareness>().ReservedWeapon,Is.Null);Assert.That(a.GetComponent<EnemyEquipment>().HasWeapon,Is.False);
        Object.Destroy(wall);Object.Destroy(a);Object.Destroy(gun);yield return EditorTestFrame.Next();
        var staging=new GameObject("Inactive save fixture");staging.SetActive(false);
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyCombatantSetup.PrefabPath),staging.transform);
        root.transform.position=Home;var equipment=root.GetComponent<EnemyEquipment>();Set(equipment,"startingWeapon",EnemyCombatantSetup.Weapon("PlasmaRifleItem"));
        var json=equipment.CaptureRunState();equipment.RestoreRunState(json);
        root.transform.SetParent(null);Object.Destroy(staging);yield return Seconds(.2f);
        Assert.That(equipment.Weapon,Is.EqualTo(EnemyCombatantSetup.Weapon("PlasmaRifleItem")));Assert.That(equipment.Ammo,Is.EqualTo(equipment.Weapon.magazineSize));
        results.Add("Occluded pickup rejected; never-activated encounter equipment round-tripped.");
    }
    [UnityTest, Timeout(180000)] public IEnumerator LowCoverReloadAndWorldState()
    {
        Set(target.GetComponent<PlayerHealth>(), "maxHealth", 5000f);target.GetComponent<PlayerHealth>().RestoreRunHealth(5000,50);
        var actor=Spawn("PlasmaRifleItem");var ranged=actor.GetComponent<EnemyRangedAttack>();var equipment=actor.GetComponent<EnemyEquipment>();int shots=0;
        ranged.ShotFired+=(h,c)=>shots++;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=Home+new Vector3(0,.62f,2);
        wall.transform.localScale=new Vector3(1.1f,1.24f,.35f);
        var obstacle=wall.AddComponent<NavMeshObstacle>();obstacle.carving=true;obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=Vector3.one;
        yield return Seconds(3);
        results.Add("Low cover displacement="+(actor.transform.position-Home)+", shots="+shots);
        for(int sample=0;sample<6;sample++){yield return Seconds(.5f);results.Add("Cover "+sample+" shots="+shots+" "+Diagnostic(actor));}
        Assert.That(Mathf.Abs(actor.transform.position.x-Home.x),Is.GreaterThan(.2f),"Muzzle-level cover should cause a local sidestep");
        Assert.That(shots,Is.GreaterThan(0),"Resume fire from a clear local position");Capture("low-cover-reposition");
        Object.Destroy(wall);yield return EditorTestFrame.Next();
        while(equipment.Ammo>0)equipment.SpendRound();int before=shots;
        yield return Seconds(.5f);Assert.That(shots,Is.EqualTo(before),"Empty magazine cannot fire immediately");
        yield return Seconds(2.5f);Assert.That(shots,Is.GreaterThan(before),"Magazine reload resumes fire");
        actor.GetComponent<EnemyBrain>().enabled=false;
        var entity=actor.AddComponent<RunWorldObject>();entity.ConfigureIdentity("isolated-test-enemy");
        var snapshot=JsonUtility.FromJson<SavedWorldObject>(JsonUtility.ToJson(entity.Capture()));int ammo=equipment.Ammo;
        equipment.Unequip();entity.Restore(snapshot);Assert.That(equipment.Weapon,Is.EqualTo(EnemyCombatantSetup.Weapon("PlasmaRifleItem")));Assert.That(equipment.Ammo,Is.EqualTo(ammo));
        var gun=Object.Instantiate(EnemyCombatantSetup.Weapon("PlasmaPistolItem").worldPrefab,Home+Vector3.right*3,Quaternion.identity);
        var world=gun.AddComponent<RunWorldObject>();world.ConfigureIdentity("isolated-test-pickup");var pickup=gun.GetComponent<PickupItem>();
        Assert.That(pickup.TryReserve(equipment),Is.True);Assert.That(pickup.ConsumeReserved(equipment),Is.True);Assert.That(world.Capture().removed,Is.True);
        Assert.That(pickup.ConsumeReserved(equipment),Is.False,"Consumption is one-shot");
        Assert.That(RunContentCatalog.Instance.Id(EnemyCombatantSetup.Weapon("PlasmaPistolItem").worldPrefab),Is.Not.Empty);
        results.Add("Reload, full SavedWorldObject equipment JSON round-trip and pickup removal passed.");
    }
    [UnityTest, Timeout(300000)] public IEnumerator PositionCommitmentsAndCadence()
    {
        Set(target.GetComponent<PlayerHealth>(), "maxHealth", 5000f);
        foreach(int seed in new[]{19,73,211})
        foreach(string weapon in new[]{"PlasmaPistolItem","PlasmaRifleItem"})
        {
            UnityEngine.Random.InitState(seed);target.GetComponent<PlayerHealth>().RestoreRunHealth(5000,50);
            target.transform.position=Home+Vector3.forward*6.3f;
            var actor=Spawn(weapon);var ranged=actor.GetComponent<EnemyRangedAttack>();
            var equipment=actor.GetComponent<EnemyEquipment>();var times=new List<float>();
            ranged.ShotFired+=(h,c)=>times.Add(Time.time);
            yield return Seconds(.8f);
            Vector3 planted=actor.transform.position;int before=times.Count;
            // These previously crossed the exact preferred-radius boundary and restarted acquisition.
            target.transform.position+=new Vector3(.15f,0,.2f);
            yield return Seconds(.3f);
            Assert.That(Vector3.Distance(actor.transform.position,planted),Is.LessThan(.08f),"Tiny back/side step must preserve initial position commitment");
            target.transform.position+=new Vector3(-.22f,0,.05f);
            yield return Seconds(.3f);
            Assert.That(Vector3.Distance(actor.transform.position,planted),Is.LessThan(.08f));
            yield return Seconds(3.6f);
            Assert.That(times.Count,Is.GreaterThan(before+2),"Minor target movement must not perpetually restart aim");
            var lengths=new List<int>();int length=1;
            for(int i=1;i<times.Count;i++)
            {
                if(times[i]-times[i-1]<.3f)length++;
                else {lengths.Add(length);length=1;}
            }
            if(weapon=="PlasmaRifleItem")
            {
                Assert.That(lengths.Exists(x=>x>=4&&x<=6),Is.True,"Rifle executes an authored 4–6 shot burst");
                Assert.That(lengths.TrueForAll(x=>x<=6),Is.True);
            }
            else Assert.That(lengths.TrueForAll(x=>x==1),Is.True,"Pistol remains measured individual shots");
            results.Add(seed+" "+weapon+": shots="+times.Count+" bursts="+string.Join(",",lengths)+" displacement="+Vector3.Distance(planted,actor.transform.position));
            Capture("commitment-"+seed+"-"+weapon);
            // A substantial withdrawal outside the comfort band must eventually prompt approach.
            target.transform.position=actor.transform.position+Vector3.forward*12;
            Vector3 old=actor.transform.position;yield return Seconds(5);
            Assert.That(Vector3.Distance(old,actor.transform.position),Is.GreaterThan(.5f));
            Object.Destroy(actor);yield return EditorTestFrame.Next();
        }
    }
    [UnityTest, Timeout(180000)] public IEnumerator ArmedDeathDropsExactlyOneNormalPickup()
    {
        target.transform.position=Home+Vector3.forward*18;
        foreach(string weapon in new[]{"PlasmaPistolItem","PlasmaRifleItem"})
        {
            var actor=Spawn(weapon);Set(actor.GetComponent<EnemyEquipment>(),"dropWeaponOnDeath",true);
            Set(actor.GetComponent<EnemyPlasmaLoot>(),"plasmaDropChance",1f);
            yield return Seconds(.2f);
            var health=actor.GetComponent<EnemyHealth>();
            var damage=new HitInfo(10000,actor.transform.position,Vector3.up,Vector3.forward,target);
            health.ReceiveDamage(damage);health.ReceiveDamage(damage);
            yield return Seconds(.2f);
            var pickups=Object.FindObjectsByType<PickupItem>();
            var guns=pickups.Where(p=>p.Item is WeaponItemData).ToArray();
            Assert.That(guns.Length,Is.EqualTo(1),"Death must drop one gun, including repeated lethal hits");
            Assert.That(guns[0].Item,Is.EqualTo(EnemyCombatantSetup.Weapon(weapon)));
            Assert.That(pickups.Count(p=>p.Item is CapsuleItemData capsule && capsule.kind==CapsuleKind.Plasma),Is.EqualTo(1),
                "The guaranteed loot roll is separate from weapon custody and resolves once");
            Assert.That(actor.GetComponent<EnemyEquipment>().HasWeapon,Is.False);
            Assert.That(actor.GetComponent<EnemyEquipment>().Drop(),Is.Null,"Already-dropped weapon cannot duplicate");
            results.Add(weapon+" dropped one normal "+guns[0].name);
            foreach(var pickup in pickups) Object.Destroy(pickup.gameObject);
            Object.Destroy(actor);yield return EditorTestFrame.Next();
        }
    }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target,value);
    string Diagnostic(GameObject actor)
    {
        var ranged=actor.GetComponent<EnemyRangedAttack>();var muzzle=actor.GetComponent<EnemyEquipment>().Muzzle;
        var aim=target.GetComponent<Collider>().bounds.center;
        bool clear=(bool)typeof(EnemyRangedAttack).GetMethod("HasClearMuzzle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ranged,new object[]{target.transform,aim});
        return ranged.Decision+" pos="+(actor.transform.position-Home)+" clear="+clear+" barrel="+Vector3.Angle(muzzle.forward,aim-muzzle.position)+" body="+Vector3.Angle(actor.transform.forward,Vector3.ProjectOnPlane(aim-actor.transform.position,Vector3.up))+" ready="+actor.GetComponent<EnemyCombatPresentation>().ReadyToFire;
    }
    static IEnumerator Seconds(float seconds) { float until=Time.time+seconds; while(Time.time<until) yield return EditorTestFrame.Next(); }
    void Capture(string label)
    {
        var rt=new RenderTexture(640,480,24);rt.Create();var old=RenderTexture.active;
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
        var tex=new Texture2D(640,480,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,640,480),0,0);tex.Apply();
        File.WriteAllBytes("Logs/AlienCombat/"+label+".png",tex.EncodeToPNG());RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);
    }
}
