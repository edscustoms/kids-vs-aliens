using System;
using System.Collections;
using System.IO;
using System.Linq;
using KidsVsAliens.Environment;
using KidsVsAliens.Interaction;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class ConstructionCombatTests
{
    internal const string ScenePath="Assets/Game/Scenes/ConstructionSite.unity";
    internal static WeaponItemData Weapon(string name)=>AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/Plasma"+name+"Item.asset");
    internal static AuthoredEncounter Encounter(string name)=>Object.FindObjectsByType<AuthoredEncounter>().Single(e=>e.name==name);
    internal static LootChest Chest=>Object.FindObjectsByType<LootChest>().Single(c=>c.name=="CS_Route5_RifleChest");
    internal static PickupItem Pickup(string name)=>Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include).Single(p=>p.name==name);

    [SetUp] public void Setup()=>EditorSceneManager.OpenScene(ScenePath);
    [TearDown] public void Cleanup()=>EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);

    [TestCase("CS_Repair_Route4",2,0,30,-22)]
    [TestCase("CS_Repair_Electrical",2,0,-24,-43)]
    [TestCase("CS_RifleChest_Ambush",2,1,18,-39)]
    [TestCase("CS_Excavator_Return",3,2,29,37)]
    public void AuthoredGroupsHaveDormantArmedMembersAndClearConnectedPositions(string name,int pistols,int rifles,float x,float z)
    {
        var encounter=Encounter(name); Physics.SyncTransforms();
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(encounter),Is.Not.Null);
        Assert.That(new SerializedObject(encounter).FindProperty("activateOnEntry").boolValue,Is.False);
        Assert.That(encounter.GetComponent<BoxCollider>().enabled,Is.False);
        Assert.That(encounter.Enemies.Count,Is.EqualTo(pistols+rifles));
        Assert.That(NavMesh.SamplePosition(new Vector3(x,0,z),out var target,1,NavMesh.AllAreas),Is.True);
        int armedPistols=0,armedRifles=0;
        foreach(var enemy in encounter.Enemies)
        {
            Assert.That(enemy.gameObject.activeSelf,Is.False); Assert.That(enemy.Id,Is.Not.Empty);
            Assert.That(enemy.GetComponent<EnemyHealth>().MaxHealth,Is.EqualTo(140));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(enemy.gameObject),Is.Not.Null);
            var equipment=enemy.GetComponent<EnemyEquipment>(); var weapon=(WeaponItemData)new SerializedObject(equipment).FindProperty("startingWeapon").objectReferenceValue;
            Assert.That(equipment.Profile.Allows(weapon),Is.True); Assert.That(equipment.Profile.rangedEnabled,Is.True);
            if(weapon==Weapon("Pistol"))armedPistols++; else if(weapon==Weapon("Rifle"))armedRifles++; else Assert.Fail("Unexpected loadout");
            Assert.That(enemy.GetComponent<EnemyRangedAttack>(),Is.Not.Null); Assert.That(enemy.GetComponent<EnemyPlasmaLoot>(),Is.Not.Null);
            Vector3 p=enemy.transform.position;
            Assert.That(Physics.CheckCapsule(p+Vector3.up*.4f,p+Vector3.up*1.5f,.3f,~0,QueryTriggerInteraction.Ignore),Is.False,enemy.name);
            Assert.That(NavMesh.SamplePosition(p,out var hit,.2f,NavMesh.AllAreas),Is.True,enemy.name);
            var path=new NavMeshPath(); Assert.That(NavMesh.CalculatePath(hit.position,target.position,NavMesh.AllAreas,path),Is.True);
            Assert.That(path.status,Is.EqualTo(NavMeshPathStatus.PathComplete),enemy.name);
            Assert.That(encounter.Enemies.Where(e=>e!=enemy).All(e=>Vector3.Distance(e.transform.position,p)>3),Is.True,"Spaced authored positions");
            Assert.That(Physics.Linecast(p+Vector3.up*1.2f,target.position+Vector3.up*1.2f,~0,QueryTriggerInteraction.Ignore),Is.False,"Authored firing lane obstructed: "+enemy.name);
            TestContext.WriteLine($"{enemy.name}: {p:F3}; path {string.Join(" -> ",path.corners.Select(c=>c.ToString("F1")))}");
        }
        Assert.That(armedPistols,Is.EqualTo(pistols)); Assert.That(armedRifles,Is.EqualTo(rifles));
        var identities=Object.FindObjectsByType<RunWorldObject>(FindObjectsInactive.Include).Select(e=>e.Id).ToArray();
        Assert.That(identities.All(i=>!string.IsNullOrEmpty(i)) && identities.Distinct().Count()==identities.Length,Is.True);
    }

    [Test] public void ChestAndMissionUseExplicitOwnersWithDeterministicLootAndFirstGunOrder()
    {
        var mission=Object.FindAnyObjectByType<ExcavatorRepairMission>(); var data=new SerializedObject(mission);
        Assert.That(data.FindProperty("requiredWeapon").objectReferenceValue,Is.SameAs(Weapon("Pistol")));
        foreach(var pair in new[]{("route4Encounter","CS_Repair_Route4"),("electricalEncounter","CS_Repair_Electrical"),("returnEncounter","CS_Excavator_Return")})
            Assert.That(data.FindProperty(pair.Item1).objectReferenceValue,Is.SameAs(Encounter(pair.Item2)));
        var chest=new SerializedObject(Chest);
        Assert.That(Object.FindAnyObjectByType<PlayerCharacter>().GetComponent<ProximityInteractor>(),Is.Not.Null);
        Assert.That(chest.FindProperty("minimumLootCount").intValue,Is.EqualTo(2));Assert.That(chest.FindProperty("maximumLootCount").intValue,Is.EqualTo(2));
        var loot=chest.FindProperty("possibleLootPrefabs"); Assert.That(loot.arraySize,Is.EqualTo(2));
        var contents=Enumerable.Range(0,loot.arraySize).Select(i=>((GameObject)loot.GetArrayElementAtIndex(i).objectReferenceValue).GetComponent<PickupItem>()).ToArray();
        Assert.That(contents.Count(p=>p.Item==Weapon("Rifle")&&p.Quantity==1),Is.EqualTo(1));
        Assert.That(contents.Count(p=>p.Item is CapsuleItemData c&&c.kind==CapsuleKind.Plasma&&p.Quantity==6),Is.EqualTo(1));
        foreach(var p in contents)Assert.That(RunContentCatalog.Instance.Id(p.gameObject),Is.Not.Empty);
        Assert.That(chest.FindProperty("onOpened.encounter").objectReferenceValue,Is.SameAs(Encounter("CS_RifleChest_Ambush")));
        Assert.That(chest.FindProperty("requiredKnowledge").objectReferenceValue,Is.SameAs(Weapon("Rifle").requiredSkill));
        Assert.That(chest.FindProperty("requiredWeapon").objectReferenceValue,Is.SameAs(Weapon("Pistol")));
        var pickups=Object.FindObjectsByType<PickupItem>();
        foreach(var e in Object.FindObjectsByType<AuthoredEncounter>().Where(e=>e.name.StartsWith("CS_")))
            Assert.That(e.GetComponentsInChildren<EnemyHealth>(true),Is.EquivalentTo(e.Enemies.Select(m=>m.GetComponent<EnemyHealth>())),"No unused template members");
        Assert.That(pickups.Count(p=>p.Item==Weapon("Pistol")),Is.EqualTo(1)); Assert.That(pickups.Any(p=>p.Item==Weapon("Rifle")),Is.False);
        Assert.That(Vector3.Distance(pickups.Single(p=>p.Item is KnowledgeBookItemData b&&b.skill==Weapon("Rifle").requiredSkill).transform.position,Chest.transform.position),Is.LessThan(4));
        Assert.That(pickups.Where(p=>p.Item is CapsuleItemData c&&c.kind==CapsuleKind.Plasma).Sum(p=>p.Quantity),Is.EqualTo(39));
    }

    [TestCase("GamePoc")]
    [TestCase("ConstructionSite")]
    public void InteractionRepairIsIdempotentAndPreservesExistingMarker(string sceneName)
    {
        var preview=EditorSceneManager.OpenPreviewScene("Assets/Game/Scenes/"+sceneName+".unity");
        try
        {
            var player=preview.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerCharacter>(true)).Single();
            var existing=player.GetComponent<ProximityInteractor>();
            GameplaySceneSetup.ConfigureInteractions(player);var marker=player.GetComponent<ProximityInteractor>();
            Assert.That(marker,Is.Not.Null);if(existing!=null)Assert.That(marker,Is.SameAs(existing));
            GameplaySceneSetup.ConfigureInteractions(player);Assert.That(player.GetComponents<ProximityInteractor>(),Is.EqualTo(new[]{marker}));
        }
        finally{EditorSceneManager.ClosePreviewScene(preview);}
    }
}

public sealed class ConstructionCombatPlayTests
{
    const string Key="ConstructionCombatTests.Saves";
    static ActiveRunController Run=>ActiveRunController.Instance;
    static PlayerInventory Inventory=>Run.GetComponent<PlayerInventory>();
    static ExcavatorRepairMission Mission=>Object.FindAnyObjectByType<ExcavatorRepairMission>();
    static PlayerCharacter Player=>Run.GetComponent<PlayerCharacter>();
    static AuthoredEncounter Encounter(string name)=>ConstructionCombatTests.Encounter(name);
    static LootChest Chest=>ConstructionCombatTests.Chest;
    static PickupItem Pickup(string name)=>ConstructionCombatTests.Pickup(name);
    static WeaponItemData Weapon(string name)=>ConstructionCombatTests.Weapon(name);
    static string[] Names={"CS_Repair_Route4","CS_Repair_Electrical","CS_RifleChest_Ambush","CS_Excavator_Return"};

    [UnitySetUp] public IEnumerator Setup()
    {
        SessionState.SetString(Key,Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY")??"");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",Path.GetFullPath("Logs/ConstructionCombatTest-"+Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene(ConstructionCombatTests.ScenePath); yield return new EnterPlayMode(); Application.runInBackground=true;
        yield return Ready(); Run.GetComponent<BeamTransportController>().CancelTransport(); Resume(); yield return EditorTestFrame.Next();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(Application.isPlaying){Run?.PrepareToLeave();yield return new ExitPlayMode();}
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",SessionState.GetString(Key,""));SessionState.EraseString(Key);Time.timeScale=1;
    }

    [UnityTest] public IEnumerator AuthoredSequencePaysForMissesWithoutDropsAndRestoresEveryActivationBoundary()
    {
        Assert.That(Names.Select(Encounter).All(e=>!e.HasTriggered&&e.Enemies.All(m=>!m.gameObject.activeSelf)),Is.True);
        Place(ExcavatorRepairTests.EntryPoint(Mission.GetComponentInChildren<GameplayTrigger>())); yield return Seconds(.15f);
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.NotStarted)); Assert.That(Chest.TryOpen(Player.transform),Is.False);
        yield return Continue(); Assert.That(Chest.IsOpen,Is.False); Resume();
        Place(new Vector3(10,.1f,-3));yield return Seconds(.1f);
        Collect("CS_Crash_FirstPistol"); Collect("CS_Crash_Plasma9");
        yield return Learn("Pistol");
        Assert.That(Inventory.GetWeaponState(Weapon("Pistol")).Rounds,Is.EqualTo(12));
        Place(ExcavatorRepairTests.EntryPoint(Mission.GetComponentInChildren<GameplayTrigger>()));yield return Seconds(.2f);
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.Collecting)); Object.FindAnyObjectByType<DialoguePlayer>().Stop();
        AssertGroup("CS_Repair_Route4",2);AssertGroup("CS_Repair_Electrical",2);
        Assert.That(Encounter("CS_RifleChest_Ambush").HasTriggered,Is.False); Assert.That(Encounter("CS_Excavator_Return").HasTriggered,Is.False);
        yield return Seconds(.1f);
        yield return Review("CS_Repair_Route4",new Vector3(28,.1f,-21));
        SpendAndDefeat("CS_Repair_Route4","Pistol",10,8);
        yield return Continue(); Resume(); yield return Seconds(.05f);
        Assert.That(Encounter("CS_Repair_Route4").IsComplete,Is.True);AssertGroup("CS_Repair_Electrical",2); Assert.That(Chest.IsOpen,Is.False);
        yield return Review("CS_Repair_Electrical",new Vector3(-24,.2f,-44));
        SpendAndDefeat("CS_Repair_Electrical","Pistol",10,8);
        Assert.That(Inventory.PlasmaCapsules,Is.Zero); Assert.That(Inventory.GetWeaponState(Weapon("Pistol")).Rounds,Is.EqualTo(8));
        Collect("CS_Electrical_Plasma12");Collect("CS_Route5_Plasma18");
        Place(Chest.transform.position-Chest.transform.forward*.9f+Vector3.up*.05f);
        yield return Seconds(3.5f);
        Assert.That(Chest.IsOpen,Is.False,"Rifle Knowledge must be learned before the ambush");
        var hold=Chest.GetComponentInChildren<ProximityHoldTrigger>();
        Assert.That(hold.IsCompleted,Is.False);Assert.That(hold.enabled,Is.True,"A rejected opening allows another approach");
        Place(Chest.transform.position-Chest.transform.forward*4);yield return Seconds(.1f);
        yield return Learn("Rifle");
        var probes=Encounter("CS_RifleChest_Ambush").Enemies.Select(e=>e.gameObject.AddComponent<ChestLootOnEnableProbe>()).ToArray();
        // Enter the normal hold volume: its actual completed opening must expose loot before activating members.
        Place(Chest.transform.position-Chest.transform.forward*.9f+Vector3.up*.05f);
        yield return Seconds(4.2f);
        Assert.That(Chest.IsOpen,Is.True);AssertGroup("CS_RifleChest_Ambush",3);
        Assert.That(probes.All(p=>p.LootWasAvailable),Is.True,"Both loot pickups exist before any ambush member activates");
        Assert.That(Chest.TryOpen(Player.transform),Is.False);
        var loot=Object.FindObjectsByType<PickupItem>().Where(p=>p.name.StartsWith("Plasma_Rifle_Dropped")||p.name.StartsWith("PlasmaCapsule_6")).ToArray();
        Assert.That(loot.Length,Is.EqualTo(2));
        foreach(var p in loot)Assert.That(p.TryCollect(Inventory),Is.True);
        Assert.That(Inventory.PlasmaCapsules,Is.EqualTo(36));Assert.That(Inventory.GetWeaponState(Weapon("Rifle")).Rounds,Is.EqualTo(28));
        Inventory.UseItem(Inventory.Items.ToList().IndexOf(Weapon("Rifle"))); Assert.That(Inventory.SelectedItem,Is.SameAs(Weapon("Rifle")));
        yield return Review("CS_RifleChest_Ambush",new Vector3(19,.2f,-39));
        SpendAndDefeat("CS_RifleChest_Ambush","Rifle",24,18,1);
        yield return Continue(); Resume(); yield return Seconds(.05f);
        Assert.That(Chest.IsOpen,Is.True);Assert.That(Encounter("CS_RifleChest_Ambush").Enemies.Count(e=>e.IsRemoved),Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<PickupItem>().Any(p=>p.name.StartsWith("PlasmaCapsule_6")||p.name.StartsWith("Plasma_Rifle_Dropped")),Is.False,"No replayed chest loot");
        SpendAndDefeat("CS_RifleChest_Ambush","Rifle",24,18);
        foreach(var p in Mission.GetComponentsInChildren<PickupItem>(true))Assert.That(p.TryCollect(Inventory),Is.True);
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.ReturnToExcavator));AssertGroup("CS_Excavator_Return",5);
        yield return Continue();Resume();AssertGroup("CS_Excavator_Return",5);
        yield return Review("CS_Excavator_Return",new Vector3(27,.1f,35));
        SpendAndDefeat("CS_Excavator_Return","Rifle",24,18);
        Assert.That(Inventory.PlasmaCapsules,Is.Zero,"45 guaranteed Plasma pays exactly 3 pistol + 6 rifle reloads");
        Assert.That(Inventory.GetWeaponState(Weapon("Rifle")).Rounds,Is.EqualTo(4));
        Assert.That(Names.Select(Encounter).All(e=>e.IsComplete),Is.True);
        yield return Continue(); Resume(); Assert.That(Names.Select(Encounter).All(e=>e.IsComplete&&e.Enemies.All(m=>m.IsRemoved&&!m.gameObject.activeSelf)),Is.True);
        Place(ExcavatorRepairTests.EntryPoint(Mission.GetComponentInChildren<GameplayTrigger>())); yield return Seconds(.2f);
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.RunningFinale));
        var motion=Object.FindAnyObjectByType<ExcavatorMotionController>();yield return Seconds(motion.CompletionTime+.2f);
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.Completed)); Assert.That(motion.IsExitClear,Is.True);
        yield return Continue();Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.Completed));Assert.That(Chest.IsOpen,Is.True);
        Assert.That(Run.RestartFromBeginning(),Is.True);yield return EditorTestFrame.Next();yield return Ready();
        Assert.That(Chest.IsOpen,Is.False);Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.NotStarted));
        Assert.That(Names.Select(Encounter).All(e=>!e.HasTriggered&&e.Enemies.All(m=>!m.IsRemoved&&!m.gameObject.activeSelf&&m.GetComponent<EnemyHealth>().CurrentHealth==140)),Is.True);
        Assert.That(Pickup("CS_Crash_FirstPistol").gameObject.activeSelf,Is.True);Assert.That(Inventory.PlasmaCapsules,Is.Zero);
    }

    static void AssertGroup(string name,int count)
    {
        var e=Encounter(name);Assert.That(e.HasTriggered,Is.True);Assert.That(e.Enemies.Count(m=>m.gameObject.activeSelf&&!m.IsRemoved),Is.EqualTo(count));
        Assert.That(e.TryActivate(Player),Is.False,"Entry cannot retrigger an explicitly activated encounter");
    }
    static void Collect(string name)=>Assert.That(Pickup(name).TryCollect(Inventory),Is.True,name);
    static IEnumerator Learn(string weaponName)
    {
        var weapon=Weapon(weaponName); var skills=Run.GetComponent<PlayerSkillState>();
        if(!skills.HasSkill(weapon.requiredSkill))
        {
            var book=Object.FindObjectsByType<PickupItem>().Single(p=>p.Item is KnowledgeBookItemData b&&b.skill==weapon.requiredSkill);
            var item=book.Item; Assert.That(book.TryCollect(Inventory),Is.True);
            Resume(); yield return Seconds(.05f);
            Assert.That(Run.GetComponent<StarterAssets.StarterAssetsInputs>().CanProcessGameplayInput,Is.True,"Knowledge input blocked; suspension owners="+Run.GetComponent<GameplaySuspensionController>().OwnerCount);
            Inventory.UseItem(Inventory.Items.ToList().IndexOf(item));
            yield return EditorTestFrame.Next();Object.FindAnyObjectByType<KnowledgeAcquiredPresenter>().Close();Resume();yield return EditorTestFrame.Next();
        }
        Assert.That(skills.HasSkill(weapon.requiredSkill),Is.True);
    }
    static void SpendAndDefeat(string name,string weaponName,int shots,int hits,int limit=int.MaxValue)
    {
        var weapon=Weapon(weaponName);Inventory.UseItem(Inventory.Items.ToList().IndexOf(weapon));
        Assert.That(Inventory.SelectedItem,Is.SameAs(weapon));var state=Inventory.GetWeaponState(weapon);
        // Exercise the real inventory/magazine payment with a deterministic miss budget.
        // Damage is applied directly here; aim/ballistics are covered by the existing combat tests.
        float now=Mathf.Max(Time.time,state.NextFireTime)+10;
        foreach(var enemy in Encounter(name).Enemies.Where(e=>!e.IsRemoved).Take(limit))
        {
            for(int shot=0;shot<shots;shot++)
            {
                if(state.Rounds==0){Assert.That(Inventory.TryBeginReload(weapon,now),Is.True,"Underfunded "+name);now+=weapon.reloadTime+1;state.FinishReload(now);}
                Assert.That(state.TrySpendRound(now),Is.True);now+=1f/weapon.fireRate+.01f;
            }
            enemy.GetComponent<EnemyHealth>().TakeDamage(hits*weapon.damage);
            Assert.That(enemy.IsRemoved,Is.True);
        }
        // Drop pickups intentionally remain untouched: no luck is part of the guarantee.
        state.Restore(state.Rounds,Time.time);
    }
    static IEnumerator Review(string name,Vector3 playerPosition)
    {
        Place(playerPosition);yield return Seconds(.25f);
        foreach(var e in Encounter(name).Enemies.Where(e=>!e.IsRemoved))
            Assert.That(e.GetComponent<EnemyEquipment>().HasWeapon,Is.True,"Authored loadout initializes in play");
        Capture(name);
    }
    static void Capture(string name)
    {
        var camera=Camera.main;var rt=new RenderTexture(1280,720,24);rt.Create();var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;bool oldOrtho=camera.orthographic;float oldSize=camera.orthographicSize;
        var members=Encounter(name).Enemies;Vector3 center=members.Aggregate(Vector3.zero,(sum,e)=>sum+e.transform.position)/members.Count;
        camera.transform.position=center+(name=="CS_Repair_Route4"?new Vector3(15,24,15):new Vector3(10,24,-22));camera.transform.LookAt(center);
        camera.orthographic=true;camera.orthographicSize=name=="CS_Excavator_Return"?16:11;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();Directory.CreateDirectory("Logs/ConstructionCombat");File.WriteAllBytes("Logs/ConstructionCombat/"+name+".png",texture.EncodeToPNG());}
        finally{camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.orthographic=oldOrtho;camera.orthographicSize=oldSize;camera.targetTexture=oldTarget;RenderTexture.active=oldActive;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
    }
    static void Place(Vector3 p){var cc=Run.GetComponent<CharacterController>();cc.enabled=false;Run.transform.position=p;cc.enabled=true;Run.GetComponent<StarterAssets.ThirdPersonController>().ResetMotion();Physics.SyncTransforms();}
    static void Resume(){Run.SendMessage("OnApplicationPause",false);Run.SendMessage("OnApplicationFocus",true);Object.FindAnyObjectByType<InGameMenuController>().ResumeGame();}
    static IEnumerator Seconds(float duration)
    {
        float end=Time.time+duration;double deadline=EditorApplication.timeSinceStartup+45;
        while(Time.time<end){Resume();Run.GetComponent<PlayerHealth>().RestoreRunHealth(100,50);Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(deadline));yield return EditorTestFrame.Next();}
    }
    static IEnumerator Ready()
    {
        double deadline=EditorApplication.timeSinceStartup+45;
        while(Run==null||!Run.IsReady){Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(deadline),Run?.RestoreError);yield return EditorTestFrame.Next();}
        yield return EditorTestFrame.Next();yield return EditorTestFrame.Next();
    }
    static IEnumerator Continue()
    {
        Assert.That(Run.Save(),Is.True);var save=RunSaveService.ActiveStore.Read<ActiveRunSave>();
        var owners=Names.Select(n=>Encounter(n).GetComponent<RunWorldObject>().Id).Append(Chest.GetComponent<RunWorldObject>().Id).Append(Mission.GetComponent<RunWorldObject>().Id).ToArray();
        save.world=save.world.OrderBy(w=>owners.Contains(w.id)?0:1).ToList();RunSaveService.ActiveStore.Write(save);
        Run.PrepareToLeave();Assert.That(RunSaveService.Continue(),Is.True,RunSaveService.LastError);yield return EditorTestFrame.Next();yield return Ready();
        Assert.That(Object.FindAnyObjectByType<InGameMenuController>().IsOpen,Is.True);
    }
}

public sealed class ChestLootOnEnableProbe : MonoBehaviour
{
    public bool LootWasAvailable { get; private set; }
    private void OnEnable()
    {
        var pickups=Object.FindObjectsByType<PickupItem>();
        LootWasAvailable=pickups.Any(p=>p.Item==ConstructionCombatTests.Weapon("Rifle"))
            &&pickups.Any(p=>p.name.StartsWith("PlasmaCapsule_6")&&p.Quantity==6);
    }
}
