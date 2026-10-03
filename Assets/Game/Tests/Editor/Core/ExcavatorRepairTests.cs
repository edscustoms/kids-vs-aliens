using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StarterAssets;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class ExcavatorRepairTests
{
    [Test] public void AuthoredMissionUsesNormalDormantItemsAndClearGroundInTheTwoBuildings()
    {
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        try
        {
            var mission=Object.FindAnyObjectByType<ExcavatorRepairMission>();
            Assert.That(mission,Is.Not.Null);
            var pickups=mission.GetComponentsInChildren<PickupItem>(true);
            Assert.That(pickups.Length,Is.EqualTo(3));
            foreach(var pickup in pickups)
            {
                Assert.That(pickup.gameObject.activeSelf,Is.False);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(pickup),Is.Not.Null);
                var item=(ItemData)new SerializedObject(pickup).FindProperty("item").objectReferenceValue;
                Assert.That(item,Is.TypeOf<GenericItemData>());
                Assert.That(item.icon,Is.Not.Null);
                Assert.That(RunContentCatalog.Instance.Id(item),Is.Not.Empty);
                var p=pickup.transform.position;
                Assert.That(Physics.Raycast(p+Vector3.up*.1f,Vector3.down,out var ground,.4f,~0,QueryTriggerInteraction.Ignore),Is.True);
                Assert.That(Physics.CheckCapsule(p+Vector3.up*.4f,p+Vector3.up*1.6f,.28f,~0,QueryTriggerInteraction.Ignore),Is.False,pickup.name);
                if(item.name=="HydraulicFluid")
                {
                    Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(ground.collider),Is.Not.Null);
                    Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(ground.collider)),Is.EqualTo("Assets/Game/Prefabs/Environment/Buildings/PF_ConstructionSite_Office.prefab"));
                    Assert.That(p.x,Is.InRange(23,28)); Assert.That(p.z,Is.InRange(-33,-28));
                }
                else { Assert.That(p.x,Is.InRange(-23,-13)); Assert.That(p.z,Is.InRange(-44,-35)); }
            }
            var trigger=mission.GetComponentInChildren<GameplayTrigger>();
            Assert.That(new SerializedObject(trigger).FindProperty("behavior").enumValueIndex,Is.EqualTo((int)GameplayTriggerBehavior.Repeatable));
            Assert.That(new SerializedObject(trigger).FindProperty("actions.actionTarget").objectReferenceValue,Is.SameAs(mission));
            Assert.That(mission, Is.InstanceOf<IGameplayAction>());
            var ids=mission.GetComponentsInChildren<RunWorldObject>(true).Select(o=>o.Id).ToArray();
            Assert.That(ids.All(id=>!string.IsNullOrEmpty(id)) && ids.Distinct().Count()==ids.Length,Is.True);
            var state=mission.CaptureRunState(); mission.RestoreRunState(state); mission.RestoreRunState(state);
            Assert.That(mission.CaptureRunState(),Is.EqualTo(state));
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
    }
    internal static Vector3 EntryPoint(GameplayTrigger trigger)
    {
        // The authored collider center can differ from the trigger object's origin.
        // Pick clear ground inside the current authored volume, without retuning it.
        Physics.SyncTransforms();
        foreach(var volume in trigger.Volumes)
        {
            var b=volume.bounds;
            for(float z=b.min.z+.6f;z<b.max.z-.3f;z+=.8f)
            for(float x=b.min.x+.6f;x<b.max.x-.3f;x+=.8f)
            {
                if(!NavMesh.SamplePosition(new Vector3(x,0,z),out var hit,.5f,NavMesh.AllAreas))continue;
                var p=hit.position+Vector3.up*.05f;
                if(trigger.ContainsPoint(p)&&!Physics.CheckCapsule(p+Vector3.up*.4f,p+Vector3.up*1.5f,.3f,~0,QueryTriggerInteraction.Ignore))return p;
            }
        }
        throw new InvalidOperationException("No clear ground inside the authored excavator trigger");
    }
}

public sealed class ExcavatorRepairPlayTests
{
    const string Key="ExcavatorRepairTests.Saves";
    static ActiveRunController Run=>ActiveRunController.Instance;
    static ExcavatorRepairMission Mission=>Object.FindAnyObjectByType<ExcavatorRepairMission>();
    static PlayerInventory Inventory=>Run.GetComponent<PlayerInventory>();
    static ObjectiveController Objectives=>ObjectiveController.Instance;
    static DialoguePlayer Dialogue=>DialoguePlayer.Instance;
    static GameplayTrigger Trigger=>Mission.GetComponentInChildren<GameplayTrigger>();
    static ExcavatorMotionController Motion=>Object.FindAnyObjectByType<ExcavatorMotionController>();
    static ObjectiveDefinition FindParts=>AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>("Assets/Game/Data/Objectives/CS_FindRepairParts.asset");
    static ObjectiveDefinition Return=>AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>("Assets/Game/Data/Objectives/CS_ReturnToExcavator.asset");
    static ItemData Item(string name)=>AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Game/Items/Generic/"+name+".asset");
    static PickupItem Pickup(string name)=>Mission.GetComponentsInChildren<PickupItem>(true).Single(p=>p.name=="PF_Item_"+name);
    static int Count(ItemData item) { for(int i=0;i<Inventory.Items.Count;i++) if(Inventory.Items[i]==item)return Inventory.CountAt(i); return 0; }

    [UnitySetUp] public IEnumerator Setup()
    {
        SessionState.SetString(Key,Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY")??"");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",Path.GetFullPath("Logs/ExcavatorRepairTest-"+Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        yield return new EnterPlayMode(); Application.runInBackground=true;
        yield return Ready(); Run.GetComponent<BeamTransportController>().CancelTransport();
        Resume(); yield return EditorTestFrame.Next(); Dialogue.Stop();
        Assert.That(Object.FindObjectsByType<PickupItem>().Single(p=>p.name=="CS_Crash_FirstPistol").TryCollect(Inventory),Is.True);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(Application.isPlaying) { Run?.PrepareToLeave(); yield return new ExitPlayMode(); }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",SessionState.GetString(Key,"")); SessionState.EraseString(Key); Time.timeScale=1;
    }

    [UnityTest] public IEnumerator PhysicalCollectionProgressContinueReturnAndCompletionPreserveOwners()
    {
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.NotStarted));
        Assert.That(Pickup("BatteryCables").TryCollect(Inventory),Is.False);
        yield return Continue();
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.NotStarted));
        Assert.That(Mission.GetComponentsInChildren<PickupItem>(true).All(p=>!p.gameObject.activeSelf),Is.True);
        Resume(); yield return Seconds(.1f);
        yield return EnterArea();
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.Collecting));
        Assert.That(Dialogue.CurrentMessage.name,Is.EqualTo("CS_Excavator_RepairStart"));
        Assert.That(Dialogue.Speaker.displayName.ToUpperInvariant(),Is.EqualTo("AMY"));
        Assert.That(Dialogue.CurrentLine.text,Is.EqualTo("Maybe I can get this thing running."));
        Assert.That(Trigger.Visit(Run.GetComponent<PlayerCharacter>()),Is.False);
        Assert.That(Run.GetComponent<GameplaySuspensionController>().IsSuspended,Is.False);
        Assert.That(Mission.GetComponentsInChildren<PickupItem>(true).All(p=>p.gameObject.activeSelf),Is.True);
        Assert.That(Objectives.ProgressOf(FindParts),Is.Zero);
        yield return Continue(); // Collecting 0/3, not just NotStarted.
        Assert.That(Dialogue.CurrentMessage,Is.Null); Assert.That(Objectives.ActiveObjective,Is.SameAs(FindParts));
        Resume(); yield return Seconds(.1f);
        yield return Collect("BatteryCables");
        Assert.That(Objectives.ProgressOf(FindParts),Is.EqualTo(1));
        yield return Continue();
        Assert.That(Mission.OwnedPartCount,Is.EqualTo(1)); Assert.That(Objectives.ProgressOf(FindParts),Is.EqualTo(1));
        Assert.That(Pickup("BatteryCables").gameObject.activeSelf,Is.False);
        Assert.That(Dialogue.CurrentMessage,Is.Null);
        Resume(); yield return Seconds(.1f); yield return Collect("IndustrialFuse");
        Assert.That(Objectives.ProgressOf(FindParts),Is.EqualTo(2));
        Assert.That(Dialogue.CurrentMessage.name,Is.EqualTo("CS_Excavator_OfficeClue"));
        yield return Continue();
        Assert.That(Mission.OwnedPartCount,Is.EqualTo(2)); Assert.That(Dialogue.CurrentMessage,Is.Null);
        Assert.That(Objectives.ProgressOf(FindParts),Is.EqualTo(2));
        Resume(); yield return Seconds(.1f);
        Inventory.TryAddItem(Item("BatteryCables")); // Duplicate stack neither increments distinct progress nor repeats clue.
        Assert.That(Objectives.ProgressOf(FindParts),Is.EqualTo(2)); Assert.That(Dialogue.CurrentMessage,Is.Null);
        yield return Collect("HydraulicFluid");
        Assert.That(Objectives.ProgressOf(FindParts),Is.EqualTo(3));
        Assert.That(Objectives.StateOf(FindParts),Is.EqualTo(ObjectiveState.Completed));
        Assert.That(Objectives.ActiveObjective,Is.SameAs(Return));
        yield return Continue();
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.ReturnToExcavator));
        Assert.That(Mission.GetComponentsInChildren<PickupItem>(true).All(p=>!p.gameObject.activeSelf),Is.True);
        Resume(); yield return Seconds(.1f);
        var unrelated=Inventory.Items.Where(i=>i!=Item("BatteryCables")&&i!=Item("IndustrialFuse")&&i!=Item("HydraulicFluid")).ToArray();
        int capsules=Inventory.PlasmaCapsules;
        yield return EnterArea();
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.RunningFinale));
        Assert.That(Motion.State,Is.EqualTo(ExcavatorMotionState.Moving));
        Assert.That(Count(Item("BatteryCables")),Is.EqualTo(1));
        Assert.That(Count(Item("IndustrialFuse")),Is.Zero); Assert.That(Count(Item("HydraulicFluid")),Is.Zero);
        Assert.That(Inventory.PlasmaCapsules,Is.EqualTo(capsules));
        foreach(var item in unrelated) Assert.That(Inventory.Items,Does.Contain(item));
        yield return Seconds(Motion.CompletionTime+.1f);
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.Completed)); Assert.That(Motion.IsExitClear,Is.True);
        var view=Object.FindAnyObjectByType<GameplayCommunicationView>();
        var check=view.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CompletionCheck");
        Assert.That(check.gameObject.activeInHierarchy,Is.True);
        Assert.That(Objectives.ActiveObjective,Is.Null);
        CaptureCompletion();
        yield return Seconds(2.6f); Assert.That(check.gameObject.activeInHierarchy,Is.False);
        yield return Continue();
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.Completed)); Assert.That(Motion.IsExitClear,Is.True);
        Assert.That(Motion.State,Is.EqualTo(ExcavatorMotionState.Completed)); Assert.That(Objectives.ActiveObjective,Is.Null);
        Assert.That(Dialogue.CurrentMessage,Is.Null); Assert.That(Count(Item("BatteryCables")),Is.EqualTo(1));
        Assert.That(Mission.GetComponentsInChildren<PickupItem>(true).All(p=>!p.gameObject.activeSelf),Is.True);
        Assert.That(Run.RestartFromBeginning(),Is.True); yield return EditorTestFrame.Next(); yield return Ready();
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.NotStarted)); Assert.That(Motion.State,Is.EqualTo(ExcavatorMotionState.Idle));
        Assert.That(Motion.IsExitClear,Is.False); Assert.That(Mission.GetComponentsInChildren<PickupItem>(true).All(p=>!p.gameObject.activeSelf),Is.True);
        Assert.That(Mission.OwnedPartCount,Is.Zero);
    }

    [UnityTest] public IEnumerator MidFinaleContinueRestoresSilentCompletedEndpointWithoutDoubleConsumption()
    {
        yield return EnterArea(); Dialogue.Stop();
        foreach(string name in new[]{"BatteryCables","IndustrialFuse","HydraulicFluid"}) Assert.That(Pickup(name).TryCollect(Inventory),Is.True);
        Place(Trigger.transform.position-Vector3.forward*4); yield return Seconds(.1f);
        yield return EnterArea(); yield return Seconds(1);
        Assert.That(Motion.IsExitClear,Is.False);
        var safe=(Transform)new SerializedObject(Mission).FindProperty("finaleSavePoint").objectReferenceValue;
        Vector3 endpoint=safe.position;
        yield return Continue();
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.Completed));
        Assert.That(Motion.State,Is.EqualTo(ExcavatorMotionState.Completed)); Assert.That(Motion.IsExitClear,Is.True);
        Assert.That(Vector3.Distance(Run.transform.position,endpoint),Is.LessThan(.1f));
        Assert.That(Mission.OwnedPartCount,Is.Zero); Assert.That(Objectives.ActiveObjective,Is.Null); Assert.That(Dialogue.CurrentMessage,Is.Null);
        Assert.That(Objectives.StateOf(Return),Is.EqualTo(ObjectiveState.Completed));
        Assert.That(Object.FindAnyObjectByType<GameplayCommunicationView>().GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CompletionCheck").gameObject.activeInHierarchy,Is.False);
        int completionEvents=0; Motion.OnSwingCompleted.AddListener(()=>completionEvents++);
        string saved=Mission.CaptureRunState(); Mission.RestoreRunState(saved); Mission.RestoreRunState(saved);
        yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
        Assert.That(completionEvents,Is.Zero); Assert.That(Mission.OwnedPartCount,Is.Zero);
        yield return Continue(); Assert.That(Motion.IsExitClear,Is.True);
        Resume(); yield return Seconds(.1f);
        // Existing locomotion crosses the restored central exit; solid rubble remains to its sides.
        Place(new Vector3(31.425f,.08f,47.3f));
        yield return Walk(new Vector3(31.425f,0,51.9f),4);
        Assert.That(Run.transform.position.z,Is.GreaterThan(51.4f));
        Assert.That(Motion.Blocker.GetComponentsInChildren<MeshCollider>().All(c=>c.enabled),Is.True);
    }

    [UnityTest] public IEnumerator OwnedCountCanDecreaseAndReturnRechecksPartsBeforePayment()
    {
        yield return EnterArea(); Dialogue.Stop();
        Inventory.TryAddItem(Item("BatteryCables")); Assert.That(Objectives.ProgressOf(FindParts),Is.EqualTo(1));
        Inventory.TryConsumeItem(Item("BatteryCables")); Assert.That(Objectives.ProgressOf(FindParts),Is.Zero);
        foreach(string name in new[]{"BatteryCables","IndustrialFuse","HydraulicFluid"}) Inventory.TryAddItem(Item(name));
        Assert.That(Mission.State,Is.EqualTo(ExcavatorRepairState.ReturnToExcavator));
        Inventory.TryConsumeItem(Item("IndustrialFuse"));
        Place(Trigger.transform.position-Vector3.forward*4); yield return Seconds(.1f); yield return EnterArea();
        Assert.That(Motion.State,Is.EqualTo(ExcavatorMotionState.Idle)); Assert.That(Count(Item("BatteryCables")),Is.EqualTo(1));
        Inventory.TryAddItem(Item("IndustrialFuse"));
        Place(Trigger.transform.position-Vector3.forward*4); yield return Seconds(.1f); yield return EnterArea();
        Assert.That(Motion.State,Is.EqualTo(ExcavatorMotionState.Moving)); Assert.That(Mission.OwnedPartCount,Is.Zero);
    }

    static IEnumerator EnterArea()
    {
        Assert.That(Inventory.GetWeaponState(AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/PlasmaPistolItem.asset")),Is.Not.Null,"Crash pistol acquired before repair");
        Place(ExcavatorRepairTests.EntryPoint(Trigger)); yield return Seconds(.15f);
    }
    static void CaptureCompletion()
    {
        var camera=Camera.main; var target=new RenderTexture(1280,720,24); target.Create();
        var previous=camera.targetTexture; var oldActive=RenderTexture.active;
        var canvases=Object.FindObjectsByType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var oldCameras=canvases.Select(c=>c.worldCamera).ToArray(); var planes=canvases.Select(c=>c.planeDistance).ToArray();
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target;
            foreach(var canvas in canvases) { canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1; }
            Canvas.ForceUpdateCanvases(); Object.FindAnyObjectByType<GameplayCommunicationView>().RefreshLayout(); Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            Directory.CreateDirectory("Logs/ExcavatorRepair"); File.WriteAllBytes("Logs/ExcavatorRepair/completed.png",image.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++) { canvases[i].renderMode=RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera=oldCameras[i]; canvases[i].planeDistance=planes[i]; }
            camera.targetTexture=previous; RenderTexture.active=oldActive; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
        }
    }
    static IEnumerator Collect(string name)
    {
        var pickup=Pickup(name); Assert.That(pickup.gameObject.activeInHierarchy,Is.True);
        Vector3 position=pickup.transform.position;
        Place(position-Vector3.forward*1.1f+Vector3.up*.05f);
        yield return Walk(position,2);
        Assert.That(Count(Item(name)),Is.GreaterThan(0),"Normal CharacterController/pickup contact: "+name);
    }
    static IEnumerator Walk(Vector3 destination,float seconds)
    {
        float end=Time.time+seconds; double deadline=EditorApplication.timeSinceStartup+30;
        var input=Run.GetComponent<StarterAssetsInputs>();
        while(Time.time<end)
        {
            Resume(); Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(deadline));
            Vector3 d=destination-Run.transform.position; d.y=0; if(d.magnitude<.12f)break;
            var camera=Camera.main.transform;
            input.MoveInput(new Vector2(Vector3.Dot(d.normalized,Vector3.ProjectOnPlane(camera.right,Vector3.up).normalized),Vector3.Dot(d.normalized,Vector3.ProjectOnPlane(camera.forward,Vector3.up).normalized)));
            yield return EditorTestFrame.Next();
        }
        input.MoveInput(Vector2.zero);
    }
    static void Place(Vector3 p)
    {
        var cc=Run.GetComponent<CharacterController>(); cc.enabled=false; Run.transform.position=p; cc.enabled=true;
        Run.GetComponent<ThirdPersonController>().ResetMotion(); Physics.SyncTransforms();
    }
    static void Resume() { Run.SendMessage("OnApplicationPause",false); Run.SendMessage("OnApplicationFocus",true); Object.FindAnyObjectByType<InGameMenuController>().ResumeGame(); }
    static IEnumerator Seconds(float seconds)
    {
        float end=Time.time+seconds; double deadline=EditorApplication.timeSinceStartup+35;
        while(Time.time<end) { Resume(); Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(deadline)); yield return EditorTestFrame.Next(); }
    }
    static IEnumerator Ready()
    {
        double deadline=EditorApplication.timeSinceStartup+40;
        while(Run==null||!Run.IsReady) { Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(deadline),Run?.RestoreError); yield return EditorTestFrame.Next(); }
        yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
    }
    static IEnumerator Continue()
    {
        Assert.That(Run.Save(),Is.True);
        var save=RunSaveService.ActiveStore.Read<ActiveRunSave>(); string id=Mission.GetComponent<RunWorldObject>().Id;
        save.world=save.world.OrderBy(o=>o.id==id?0:1).ToList(); RunSaveService.ActiveStore.Write(save);
        Run.PrepareToLeave(); Assert.That(RunSaveService.Continue(),Is.True,RunSaveService.LastError);
        yield return EditorTestFrame.Next(); yield return Ready();
        Assert.That(Object.FindAnyObjectByType<InGameMenuController>().IsOpen,Is.True);
        Assert.That(Object.FindObjectsByType<ExcavatorRepairMission>().Length,Is.EqualTo(1));
    }
}
