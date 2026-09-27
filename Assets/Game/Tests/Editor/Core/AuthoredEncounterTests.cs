using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class AuthoredEncounterTests
{
    [Test] public void SceneOwnsThreeDormantMeleeEnemiesWithClearConnectedPlacement()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        try
        {
            var encounter = Object.FindAnyObjectByType<AuthoredEncounter>();
            Assert.That(encounter, Is.Not.Null);
            Assert.That(encounter.Enemies.Count, Is.EqualTo(3));
            Assert.That(encounter.Enemies.Select(e => e.Id).Distinct().Count(), Is.EqualTo(3));
            var trigger = encounter.GetComponent<BoxCollider>();
            Assert.That(trigger.isTrigger, Is.True);
            Assert.That(trigger.bounds.min.x, Is.GreaterThan(-12.1f + 1.5f), "Commitment beyond the entry partition");
            Assert.That(trigger.bounds.min.y, Is.GreaterThan(8), "Ground-floor travel cannot trigger this encounter");
            Physics.SyncTransforms();
            foreach (var enemy in encounter.Enemies)
            {
                Assert.That(enemy.gameObject.activeSelf, Is.False);
                Assert.That(enemy.Id, Is.Not.Empty);
                Assert.That(enemy.GetComponent<EnemyHealth>().MaxHealth, Is.EqualTo(140));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(enemy.gameObject), Is.EqualTo(
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Enemies/PF_Enemy_Melee_POC_V1.prefab")));
                var equipment = enemy.GetComponent<EnemyEquipment>();
                Assert.That(equipment.Profile.meleeEnabled, Is.True);
                Assert.That(equipment.Profile.rangedEnabled, Is.False);
                Assert.That(equipment.Profile.weaponPickupEnabled, Is.False);
                Assert.That(new SerializedObject(equipment).FindProperty("startingWeapon").objectReferenceValue, Is.Null);
                Assert.That(NavMesh.SamplePosition(enemy.transform.position, out var hit,.15f,NavMesh.AllAreas), Is.True);
                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(hit.position,encounter.transform.position,NavMesh.AllAreas,path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(Physics.CheckCapsule(enemy.transform.position+Vector3.up*.35f,enemy.transform.position+Vector3.up*1.5f,.27f,~0,QueryTriggerInteraction.Ignore), Is.False);
            }
            Assert.That(Object.FindObjectsByType<RunWorldObject>(FindObjectsInactive.Include).GroupBy(e=>e.Id).Any(g=>g.Count()>1),Is.False);
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
    }
}

public sealed class AuthoredEncounterPlayTests
{
    const string Key = "AuthoredEncounterTests.Saves";
    static AuthoredEncounter Encounter => Object.FindAnyObjectByType<AuthoredEncounter>();
    static ActiveRunController Run => ActiveRunController.Instance;

    [UnitySetUp] public IEnumerator Setup()
    {
        SessionState.SetString(Key,Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY")??"");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",System.IO.Path.GetFullPath("Logs/Encounter01Test-"+Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        yield return new EnterPlayMode();
        Application.runInBackground=true;
        yield return WaitReady();
        Run.GetComponent<BeamTransportController>().CancelTransport();
        Resume(); yield return EditorTestFrame.Next();
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(Application.isPlaying) { Run?.PrepareToLeave(); yield return new ExitPlayMode(); }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",SessionState.GetString(Key,""));
        SessionState.EraseString(Key); Time.timeScale=1;
    }

    [UnityTest] public IEnumerator DormantContinueThenPhysicalEntryAlertsAllThreeOnlyOnce()
    {
        Assert.That(Encounter.HasTriggered,Is.False);
        Assert.That(Encounter.Enemies.All(e=>!e.gameObject.activeInHierarchy),Is.True);
        yield return Continue();
        Assert.That(Encounter.HasTriggered,Is.False);
        Assert.That(Encounter.Enemies.All(e=>!e.gameObject.activeSelf),Is.True);
        Resume(); yield return EditorTestFrame.Next();
        // Stand immediately outside; capsule overlap must not activate before the root enters.
        Place(new Vector3(-10.35f,8.23f,15.1f));
        yield return Seconds(.15f);
        Assert.That(Encounter.HasTriggered,Is.False);
        var lease=Run.GetComponent<GameplaySuspensionController>().Acquire(SuspensionReason.BeamTransport);
        Place(Encounter.transform.position+Vector3.up*.03f);
        Assert.That(Encounter.TryActivate(Run.GetComponent<PlayerCharacter>()),Is.False);
        lease.Dispose();
        yield return Seconds(.2f);
        Assert.That(Encounter.HasTriggered,Is.True,"Real CharacterController/trigger contact activates the encounter");
        Assert.That(Encounter.Enemies.All(e=>e.gameObject.activeSelf),Is.True);
        Assert.That(Encounter.TryActivate(Run.GetComponent<PlayerCharacter>()),Is.False);
        foreach(var enemy in Encounter.Enemies)
        {
            Assert.That(enemy.GetComponent<EnemyBrain>().State,Is.EqualTo(EnemyBrainState.Investigate).Or.EqualTo(EnemyBrainState.Chase).Or.EqualTo(EnemyBrainState.Attack));
            Assert.That(enemy.GetComponent<EnemyEquipment>().HasWeapon,Is.False);
        }
        float deadline=Time.time+18;
        while(Encounter.Enemies.Any(e=>e.GetComponent<EnemyActor>().CurrentTarget!=Run.transform) && Time.time<deadline)
        {
            Run.GetComponent<PlayerHealth>().RestoreRunHealth(100,50);
            yield return EditorTestFrame.Next();
        }
        Assert.That(Encounter.Enemies.All(e=>e.GetComponent<EnemyActor>().CurrentTarget==Run.transform),Is.True,"All three navigate from the stair positions and engage Amy");
    }

    [UnityTest] public IEnumerator PartialAndCompletedDeathsPersistWithoutRespawnAndHardRestartResets()
    {
        Place(Encounter.transform.position+Vector3.up*.03f);
        Assert.That(Encounter.TryActivate(Run.GetComponent<PlayerCharacter>()),Is.True);
        yield return EditorTestFrame.Next();
        var ids=Encounter.Enemies.Select(e=>e.Id).ToArray();
        int pickups=Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include).Length;
        Encounter.Enemies[0].GetComponent<EnemyHealth>().TakeDamage(1000);
        Assert.That(Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include).Length,Is.EqualTo(pickups),"Unarmed death creates no pickup");
        Assert.That(Encounter.IsComplete,Is.False);
        yield return Continue();
        Assert.That(Encounter.HasTriggered,Is.True);
        Assert.That(Run.FindWorldObject(ids[0]).IsRemoved,Is.True);
        Assert.That(Run.FindWorldObject(ids[0]).gameObject.activeSelf,Is.False);
        Assert.That(Encounter.Enemies.Count(e=>e.gameObject.activeSelf),Is.EqualTo(2));
        Assert.That(Encounter.IsComplete,Is.False);
        Assert.That(Encounter.Enemies.Select(e=>e.Id),Is.EqualTo(ids));
        foreach(var enemy in Encounter.Enemies.Skip(1))
        {
            Assert.That(enemy.GetComponent<EnemyBrain>().State,Is.EqualTo(EnemyBrainState.Investigate));
            Assert.That(enemy.GetComponent<EnemyEquipment>().HasWeapon,Is.False);
            pickups=Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include).Length;
            enemy.GetComponent<EnemyHealth>().TakeDamage(1000);
            Assert.That(Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include).Length,Is.EqualTo(pickups));
        }
        Assert.That(Encounter.IsComplete,Is.True);
        yield return Continue();
        Assert.That(Encounter.IsComplete,Is.True);
        Assert.That(Encounter.Enemies.All(e=>e.IsRemoved&&!e.gameObject.activeSelf),Is.True);
        Resume(); yield return EditorTestFrame.Next();
        Place(new Vector3(-11,8.23f,15)); yield return Seconds(.1f);
        Place(Encounter.transform.position+Vector3.up*.03f); yield return Seconds(.1f);
        Assert.That(Encounter.Enemies.All(e=>!e.gameObject.activeSelf),Is.True);
        Assert.That(Encounter.TryActivate(Run.GetComponent<PlayerCharacter>()),Is.False);
        var save=RunSaveService.ActiveStore.Read<ActiveRunSave>();
        Assert.That(save.world.Where(w=>ids.Contains(w.id)).All(w=>w.removed),Is.True);
        Assert.That(Run.RestartFromBeginning(),Is.True);
        yield return EditorTestFrame.Next(); yield return WaitReady();
        Assert.That(Encounter.HasTriggered,Is.False);
        Assert.That(Encounter.IsComplete,Is.False);
        Assert.That(Encounter.Enemies.All(e=>!e.IsRemoved&&!e.gameObject.activeSelf&&e.GetComponent<EnemyHealth>().CurrentHealth==140),Is.True);
    }

    [UnityTest] public IEnumerator ExistingFightingAnimationDamagesAnEncounterAlien()
    {
        var melee=Run.GetComponent<PlayerMeleeController>();
        Run.GetComponent<PlayerSkillState>().UnlockSkill(melee.DefaultCombatItem.requiredSkill);
        yield return EditorTestFrame.Next();
        Object.FindAnyObjectByType<KnowledgeAcquiredPresenter>().Close(); Resume();
        yield return EditorTestFrame.Next();
        Assert.That(melee.SelectCombatItem(melee.DefaultCombatItem),Is.True);
        Place(Encounter.transform.position+Vector3.up*.03f);
        Assert.That(Encounter.TryActivate(Run.GetComponent<PlayerCharacter>()),Is.True);
        yield return EditorTestFrame.Next();
        foreach(var enemy in Encounter.Enemies)
        {
            enemy.GetComponent<EnemyBrain>().enabled=false;
            enemy.GetComponent<EnemyMeleeAttack>().enabled=false;
            enemy.GetComponent<NavMeshAgent>().enabled=false;
            enemy.GetComponentInChildren<Animator>().cullingMode=AnimatorCullingMode.AlwaysAnimate;
        }
        var victim=Encounter.Enemies[0];
        // Use the existing melee lab's close-contact distance. At .8 m this jab
        // can legitimately miss the narrow capsule; do not widen production contact.
        victim.transform.position=Run.transform.position+Vector3.forward*.7f;
        victim.transform.rotation=Quaternion.Euler(0,180,0);
        Run.GetComponent<PlayerAim>().enabled=false;
        Run.transform.rotation=Quaternion.identity;
        Run.GetComponent<PlayerCharacter>().ActiveVisual.Animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        Physics.SyncTransforms(); yield return Seconds(.5f);
        int impacts=0;
        Run.GetComponent<PlayerAnimation>().AnimationEventReceived+=marker=>
        {
            if(marker!=CharacterAnimationEventId.MeleeImpact) return;
            impacts++;
            var visual=Run.GetComponent<PlayerCharacter>().ActiveVisual;
            visual.AnimationActions.TryGetBinding(melee.DefaultCombatItem.attackChain[0],out var binding);
            binding.meleeContact.TryGetCenter(visual.Animator,out var center);
            TestContext.WriteLine($"Impact: player {Run.transform.position:F3}, enemy {victim.transform.position:F3}, center {center:F3}, gap {Vector3.Distance(center,victim.GetComponent<Collider>().ClosestPoint(center))}, targetable {victim.GetComponent<AimTarget>().IsTargetable}");
        };
        Assert.That(melee.TryAttack(),Is.True);
        yield return Seconds(1.5f);
        Assert.That(impacts,Is.EqualTo(1),"Real authored animation impact marker");
        Assert.That(victim.GetComponent<EnemyHealth>().CurrentHealth,Is.EqualTo(110),"Existing typed melee impact applies the authored 30 damage");
    }

    static void Place(Vector3 position)
    {
        var capsule=Run.GetComponent<CharacterController>(); capsule.enabled=false;
        Run.transform.position=position; capsule.enabled=true;
        Run.GetComponent<StarterAssets.ThirdPersonController>().RestoreRunVerticalVelocity(0);
        Physics.SyncTransforms();
    }
    static void Resume()
    {
        Run.SendMessage("OnApplicationPause",false); Run.SendMessage("OnApplicationFocus",true);
        Object.FindAnyObjectByType<InGameMenuController>().ResumeGame();
    }
    static IEnumerator Continue()
    {
        Assert.That(Run.Save(),Is.True);
        // Exercise encounter-before-peer restoration; neither pass may reactivate enemies.
        var save=RunSaveService.ActiveStore.Read<ActiveRunSave>();
        string id=Encounter.GetComponent<RunWorldObject>().Id;
        save.world=save.world.OrderBy(w=>w.id==id?0:1).ToList(); RunSaveService.ActiveStore.Write(save);
        Run.PrepareToLeave(); Assert.That(RunSaveService.Continue(),Is.True,RunSaveService.LastError);
        yield return EditorTestFrame.Next(); yield return WaitReady();
        Assert.That(Object.FindObjectsByType<AuthoredEncounter>(FindObjectsInactive.Include).Length,Is.EqualTo(1));
        Assert.That(Encounter.Enemies.Count,Is.EqualTo(3));
        Assert.That(Object.FindAnyObjectByType<InGameMenuController>().IsOpen,Is.True);
    }
    static IEnumerator WaitReady()
    {
        double deadline=EditorApplication.timeSinceStartup+30;
        while(Run==null||!Run.IsReady)
        {
            Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(deadline),Run?.RestoreError);
            yield return EditorTestFrame.Next();
        }
        yield return EditorTestFrame.Next();
    }
    static IEnumerator Seconds(float seconds)
    {
        float end=Time.time+seconds; double deadline=EditorApplication.timeSinceStartup+40;
        while(Time.time<end) { Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(deadline),"Unexpected pause"); yield return EditorTestFrame.Next(); }
    }
}
