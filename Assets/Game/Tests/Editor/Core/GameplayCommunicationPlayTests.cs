using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class GameplayCommunicationPlayTests
{
    const string Folder="Assets/GameplayAuthoringTestFixture", ScenePath=Folder+"/GameplayAuthoringLab.unity", Key="GameplayAuthoring.Play";
    [Serializable] private class BuildBackup { public string[] paths; public bool[] enabled; }
    static ActiveRunController Run=>ActiveRunController.Instance;
    static PlayerCharacter Player=>Run.GetComponent<PlayerCharacter>();
    static DialoguePlayer Dialogue=>DialoguePlayer.Instance;
    static ObjectiveController Objectives=>ObjectiveController.Instance;
    static ObjectiveDefinition Count=>AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>(Folder+"/Count.asset");
    static GameplayTrigger Trigger(string name)=>Object.FindObjectsByType<GameplayTrigger>(FindObjectsInactive.Include).Single(t=>t.name==name);
    static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);

    [UnitySetUp] public IEnumerator Setup()
    {
        CreateFixture();
        yield return new EnterPlayMode();
        Application.runInBackground=true;
        yield return Ready();
        ConfigureCatalog();
        Run.GetComponent<BeamTransportController>().CancelTransport(); Resume();
        yield return EditorTestFrame.Next();
    }
    static void CreateFixture()
    {
        Assert.That(AssetDatabase.IsValidFolder(Folder),Is.False);
        AssetDatabase.CreateFolder("Assets","GameplayAuthoringTestFixture");
        SessionState.SetString(Key,Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY")??"");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",Path.GetFullPath("Logs/AuthoringTest-"+Guid.NewGuid().ToString("N")));
        SessionState.SetString(Key+".Build",JsonUtility.ToJson(new BuildBackup{paths=EditorBuildSettings.scenes.Select(s=>s.path).ToArray(),enabled=EditorBuildSettings.scenes.Select(s=>s.enabled).ToArray()}));
        AssetDatabase.CopyAsset("Assets/Game/Scenes/ConstructionSite.unity",ScenePath);
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var count=ScriptableObject.CreateInstance<ObjectiveDefinition>(); count.id="test-count"; count.title="REPAIR TEST DEVICE"; count.progressMode=ObjectiveProgressMode.Count; count.targetCount=3;
        AssetDatabase.CreateAsset(count,Folder+"/Count.asset");
        var origin=Object.FindAnyObjectByType<PlayerBeamInSequence>().transform.position;
        foreach(string name in new[]{"OneShot","Repeat"})
        {
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GameplayAuthoringSetup.TriggerPath));
            root.name=name; root.transform.position=origin+Vector3.right*(name=="OneShot"?15:22);
            var trigger=root.GetComponent<GameplayTrigger>();
            Set(trigger,"behavior",name=="OneShot"?GameplayTriggerBehavior.OneShot:GameplayTriggerBehavior.Repeatable);
            Set(trigger,"actions",new GameplayActions{dialogue=name=="OneShot"?AssetDatabase.LoadAssetAtPath<DialogueMessage>(GameplayAuthoringSetup.MessagePath):null,
                objective=count,objectiveAction=name=="OneShot"?ObjectiveAction.Start:ObjectiveAction.Update,systemMessage=name=="OneShot"?"AUTHORING EVENT":""});
            GameplayAuthoringSetup.PrepareIdentities(root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(trigger); PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
    }
    static void ConfigureCatalog()
    {
        // Private fixture catalog stays in memory; production catalog/assets are never saved.
        var catalog=Object.Instantiate(RunContentCatalog.Instance);
        catalog.entries=catalog.entries.Concat(new[]{new RunContentCatalog.Entry{id="fixture-count",asset=Count}}).ToArray();
        typeof(RunContentCatalog).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,catalog);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(Application.isPlaying){Run?.PrepareToLeave();yield return new ExitPlayMode();}
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var backup=JsonUtility.FromJson<BuildBackup>(SessionState.GetString(Key+".Build","{}"));
        if(backup.paths!=null)EditorBuildSettings.scenes=backup.paths.Select((p,i)=>new EditorBuildSettingsScene(p,backup.enabled[i])).ToArray();
        AssetDatabase.DeleteAsset(Folder);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",SessionState.GetString(Key,""));
        SessionState.EraseString(Key); SessionState.EraseString(Key+".Build"); Time.timeScale=1;
    }

    [UnityTest] public IEnumerator TriggerActionsConditionsRepeatVisitsAndRealContinuePreserveObjectives()
    {
        var opening=AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>(GameplayAuthoringSetup.ObjectivePath);
        Assert.That(Objectives.ActiveObjective,Is.SameAs(opening));
        Assert.That(Dialogue.CurrentMessage,Is.Null,"Opening objective does not invent dialogue");
        var once=Trigger("OneShot"); Place(once.transform.position);
        Set(once,"requiredObjective",opening); Set(once,"requiredState",ObjectiveState.Completed);
        Assert.That(once.Visit(Player),Is.False,"Objective state condition");
        Set(once,"requiredState",ObjectiveState.Active);
        yield return Seconds(.1f);
        Assert.That(once.HasFired,Is.True,"Real physics entry starts the combined actions");
        Assert.That(Objectives.StateOf(Count),Is.EqualTo(ObjectiveState.Active));
        Assert.That(Dialogue.CurrentLine.text,Is.Not.Empty);
        Assert.That(once.Visit(Player),Is.False);
        Dialogue.Stop();
        var repeat=Trigger("Repeat"); Set(repeat,"requiredObjective",Count);
        var skill=AssetDatabase.LoadAssetAtPath<KnowledgeBookItemData>("Assets/Game/Data/Items/KnowledgeBooks/BeamHoistBook.asset").skill;
        Set(repeat,"requiredKnowledge",skill); Place(repeat.transform.position);
        Assert.That(repeat.Visit(Player),Is.False,"Knowledge condition");
        Leave(repeat);
        Player.GetComponent<PlayerSkillState>().UnlockSkill(skill); yield return EditorTestFrame.Next();
        Object.FindAnyObjectByType<KnowledgeAcquiredPresenter>().Close(); Resume(); yield return EditorTestFrame.Next();
        Place(repeat.transform.position);
        Assert.That(repeat.Visit(Player),Is.True); Assert.That(Objectives.ProgressOf(Count),Is.EqualTo(1));
        Assert.That(repeat.Visit(Player),Is.False,"Standing still does not spam repeat actions");
        Leave(repeat); Place(repeat.transform.position); Assert.That(repeat.Visit(Player),Is.True);
        Assert.That(Objectives.ProgressOf(Count),Is.EqualTo(2));
        Place(Run.GetComponent<PlayerCharacter>().transform.position+Vector3.forward*4);
        yield return Continue();
        Assert.That(Objectives.ActiveObjective,Is.SameAs(Count)); Assert.That(Objectives.ProgressOf(Count),Is.EqualTo(2));
        Assert.That(Dialogue.CurrentMessage,Is.Null); Assert.That(Trigger("OneShot").HasFired,Is.True);
        Resume(); yield return EditorTestFrame.Next();
        once=Trigger("OneShot"); Place(once.transform.position); Assert.That(once.Visit(Player),Is.False);
        repeat=Trigger("Repeat"); Place(repeat.transform.position); Assert.That(repeat.Visit(Player),Is.True);
        Assert.That(Objectives.StateOf(Count),Is.EqualTo(ObjectiveState.Completed)); Assert.That(Objectives.ActiveObjective,Is.Null);
        yield return Continue();
        Assert.That(Objectives.StateOf(Count),Is.EqualTo(ObjectiveState.Completed)); Assert.That(Objectives.ProgressOf(Count),Is.EqualTo(3));
        Assert.That(Objectives.StateOf(opening),Is.EqualTo(ObjectiveState.Inactive),"Continue did not reinitialize the opening objective");
        Assert.That(Run.RestartFromBeginning(),Is.True); yield return EditorTestFrame.Next(); yield return Ready();
        Assert.That(Objectives.ActiveObjective,Is.SameAs(opening)); Assert.That(Objectives.StateOf(Count),Is.EqualTo(ObjectiveState.Inactive));
        Assert.That(Trigger("OneShot").HasFired,Is.False);
    }

    [UnityTest] public IEnumerator DialogueOnlySystemOnlyEmptyAndMultilineStayNonBlocking()
    {
        var trigger=Trigger("Repeat"); var intro=AssetDatabase.LoadAssetAtPath<DialogueMessage>(GameplayAuthoringSetup.MessagePath);
        var initial=Objectives.ActiveObjective;
        Set(trigger,"actions",new GameplayActions{dialogue=intro}); Place(trigger.transform.position);
        Assert.That(trigger.Visit(Player),Is.True); Assert.That(Objectives.ActiveObjective,Is.SameAs(initial));
        Assert.That(Dialogue.CurrentMessage,Is.SameAs(intro)); Dialogue.Stop();
        Leave(trigger); Set(trigger,"actions",new GameplayActions()); Place(trigger.transform.position);
        Assert.That(trigger.Visit(Player),Is.True); Assert.That(Dialogue.CurrentMessage,Is.Null);
        Leave(trigger); Set(trigger,"actions",new GameplayActions{systemMessage="SYSTEM ONLY"}); Place(trigger.transform.position);
        int notices=0; Action<string> handler=s=>{if(s=="SYSTEM ONLY")notices++;}; RunSaveService.Feedback+=handler;
        Assert.That(trigger.Visit(Player),Is.True); Assert.That(trigger.Visit(Player),Is.False);
        RunSaveService.Feedback-=handler; Assert.That(notices,Is.EqualTo(1)); Assert.That(Dialogue.CurrentMessage,Is.Null);
        var message=ScriptableObject.CreateInstance<DialogueMessage>();
        message.girl.lines=new[]{new DialogueMessage.Line{text="First line",displayDuration=.25f,delayAfter=.15f},new DialogueMessage.Line{text="Second line",displayDuration=.4f}};
        var speaker=Player.ActiveVisual.DialogueIdentity;
        Assert.That(Dialogue.Play(message,speaker),Is.True); Assert.That(Dialogue.CurrentLine.text,Is.EqualTo("First line"));
        Assert.That(Player.GetComponent<GameplaySuspensionController>().IsSuspended,Is.False);
        Assert.That(Player.GetComponent<StarterAssets.StarterAssetsInputs>().CanProcessGameplayInput,Is.True);
        yield return Seconds(.29f); Assert.That(Dialogue.CurrentLine,Is.Null,"Authored inter-line gap");
        yield return Seconds(.15f); Assert.That(Dialogue.CurrentLine.text,Is.EqualTo("Second line"));
        yield return Seconds(.5f); Assert.That(Dialogue.CurrentMessage,Is.Null);
        Assert.That(Player.GetComponent<StarterAssets.ThirdPersonController>().enabled,Is.True);
        Object.Destroy(message);
    }

    [UnityTest] public IEnumerator OptionalSemanticVoiceUsesItsDurationAndStopsOnDisable()
    {
        AudioService.Instance.enabled=false;
        var root=new GameObject("Voice fixture"); root.SetActive(false);
        var service=root.AddComponent<AudioService>(); var library=ScriptableObject.CreateInstance<AudioLibrary>();
        var sound=ScriptableObject.CreateInstance<SoundEvent>(); sound.category=SoundCategory.Voice; sound.spatial=false; sound.pitch=Vector2.one;
        var clip=AudioClip.Create("Silent test voice",22050,1,44100,false); sound.variants=new[]{clip}; library.events.Add(sound);
        Set(service,"library",library); root.SetActive(true);
        var message=ScriptableObject.CreateInstance<DialogueMessage>(); message.girl.lines=new[]{new DialogueMessage.Line{text="Voiced transcript",voice=sound}};
        Assert.That(Dialogue.Play(message,Player.ActiveVisual.DialogueIdentity),Is.True);
        Assert.That(Dialogue.GetComponent<AudioEmitter>().PlaybackDuration,Is.EqualTo(.5f).Within(.01));
        yield return Seconds(.2f); Assert.That(Dialogue.CurrentLine.text,Is.EqualTo("Voiced transcript"));
        var dialogueOwner=Dialogue;
        dialogueOwner.enabled=false; Assert.That(dialogueOwner.GetComponent<AudioSource>().clip,Is.Null);
        Assert.That(Dialogue,Is.Null,"Disabled speech owner releases its singleton");
        Assert.That(Player.GetComponent<GameplaySuspensionController>().IsSuspended,Is.False);
        Object.Destroy(root); Object.Destroy(message); Object.Destroy(sound); Object.Destroy(clip); Object.Destroy(library);
    }

    [UnityTest] public IEnumerator ThreeChannelsAndBothSystemMessagesRemainSeparateAtMobileSizes()
    {
        Objectives.StartObjective(Count); Objectives.AddProgress(Count);
        var message=ScriptableObject.CreateInstance<DialogueMessage>();
        message.girl.lines=new[]{new DialogueMessage.Line{text="There should be a fuse near the electrical area.",displayDuration=60}};
        Assert.That(Dialogue.Play(message,Player.ActiveVisual.DialogueIdentity),Is.True);
        RunSaveService.Notify("RUN SAVED"); Player.GetComponent<PlayerFeedback>().Report(new GameplayFeedbackEvent(FeedbackCode.HealthAlreadyFull));
        yield return Seconds(.3f);
        foreach(var size in new[]{new Vector2Int(1600,720),new Vector2Int(1280,720),new Vector2Int(1024,768)}) CaptureAndCheck(size);
        var melee=Player.GetComponent<PlayerMeleeController>(); Player.GetComponent<PlayerSkillState>().UnlockSkill(melee.DefaultCombatItem.requiredSkill);
        yield return EditorTestFrame.Next(); Object.FindAnyObjectByType<KnowledgeAcquiredPresenter>().Close(); Resume(); yield return EditorTestFrame.Next();
        Assert.That(melee.SelectCombatItem(melee.DefaultCombatItem),Is.True); Assert.That(melee.TryAttack(),Is.True,"CC does not block combat");
        Assert.That(Player.GetComponent<GameplaySuspensionController>().OwnerCount,Is.Zero);
        Object.Destroy(message);
    }
    static void CaptureAndCheck(Vector2Int size)
    {
        var camera=Camera.main; var target=new RenderTexture(size.x,size.y,24); target.Create();
        var previous=camera.targetTexture; camera.targetTexture=target;
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var oldCameras=canvases.Select(c=>c.worldCamera).ToArray(); var planes=canvases.Select(c=>c.planeDistance).ToArray();
        try
        {
            foreach(var safe in Object.FindObjectsByType<SafeAreaPanel>(FindObjectsInactive.Exclude))
            { safe.enabled=false; var rect=(RectTransform)safe.transform; rect.anchorMin=new(.04f,.04f); rect.anchorMax=new(.96f,.96f); }
            foreach(var canvas in canvases)
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1;
                var scaler=canvas.GetComponent<CanvasScaler>();
                if(scaler!=null)typeof(CanvasScaler).GetMethod("Handle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(scaler,null);
            }
            Canvas.ForceUpdateCanvases();
            var safeRoot=Object.FindAnyObjectByType<GameplayInterface>().transform.Find("SafeArea");
            var names=new[]{"ObjectiveTracker","RunFeedback","Feedback","DialogueCC"};
            var rects=names.Select(n=>(RectTransform)safeRoot.Find(n)).ToArray();
            foreach(var rect in rects)
            {
                Assert.That(rect.gameObject.activeInHierarchy,Is.True,rect.name);
                var bounds=ScreenRect(rect,camera);
                Assert.That(bounds.xMin,Is.GreaterThanOrEqualTo(size.x*.04f)); Assert.That(bounds.xMax,Is.LessThanOrEqualTo(size.x*.96f));
                Assert.That(bounds.yMin,Is.GreaterThanOrEqualTo(size.y*.04f)); Assert.That(bounds.yMax,Is.LessThanOrEqualTo(size.y*.96f));
                foreach(var text in rect.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing,Is.False,$"{size}: {text.name}"); }
            }
            for(int i=0;i<rects.Length;i++)for(int j=i+1;j<rects.Length;j++)Assert.That(ScreenRect(rects[i],camera).Overlaps(ScreenRect(rects[j],camera)),Is.False,$"{names[i]} overlaps {names[j]} at {size}");
            var controls=Object.FindObjectsByType<UIVirtualButton>(FindObjectsInactive.Exclude).Select(c=>(RectTransform)c.transform)
                .Concat(Object.FindObjectsByType<UIVirtualJoystick>(FindObjectsInactive.Exclude).Select(c=>(RectTransform)c.transform));
            var slots=new SerializedObject(Object.FindAnyObjectByType<InventoryUI>()).FindProperty("slotButtons");
            for(int i=0;i<slots.arraySize;i++)
            {
                var slot=(Button)slots.GetArrayElementAtIndex(i).objectReferenceValue;
                foreach(var rect in rects)Assert.That(ScreenRect(rect,camera).Overlaps(ScreenRect((RectTransform)slot.transform,camera)),Is.False,$"{rect.name} overlaps quick slot {i+1}");
            }
            foreach(var control in controls)foreach(var rect in rects)Assert.That(ScreenRect(rect,camera).Overlaps(ScreenRect(control,camera)),Is.False,$"{rect.name} overlaps {control.name}");
            foreach(var graphic in rects[0].GetComponentsInChildren<Graphic>().Concat(rects[3].GetComponentsInChildren<Graphic>()))Assert.That(graphic.raycastTarget,Is.False);
            camera.Render(); var old=RenderTexture.active; RenderTexture.active=target;
            var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0); texture.Apply();
            Directory.CreateDirectory("Logs/GameplayAuthoringV1"); File.WriteAllBytes($"Logs/GameplayAuthoringV1/communication-{size.x}x{size.y}.png",texture.EncodeToPNG());
            RenderTexture.active=old; Object.DestroyImmediate(texture);
        }
        finally
        {
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=oldCameras[i];canvases[i].planeDistance=planes[i];}
            camera.targetTexture=previous; target.Release(); Object.DestroyImmediate(target);
        }
    }
    static Rect ScreenRect(RectTransform rect,Camera camera)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
        return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
    }
    static void Place(Vector3 p){var c=Player.GetComponent<CharacterController>();c.enabled=false;Player.transform.position=p;c.enabled=true;Player.GetComponent<StarterAssets.ThirdPersonController>().RestoreRunVerticalVelocity(0);Physics.SyncTransforms();}
    static void Leave(GameplayTrigger t){Place(t.transform.position+Vector3.forward*5);Assert.That(t.Visit(Player),Is.False);}
    static void Resume(){Run.SendMessage("OnApplicationPause",false);Run.SendMessage("OnApplicationFocus",true);Object.FindAnyObjectByType<InGameMenuController>().ResumeGame();}
    static IEnumerator Continue(){Assert.That(Run.Save(),Is.True);Run.PrepareToLeave();Assert.That(RunSaveService.Continue(),Is.True,RunSaveService.LastError);yield return EditorTestFrame.Next();yield return Ready();}
    static IEnumerator Ready(){double end=EditorApplication.timeSinceStartup+30;while(Run==null||!Run.IsReady){Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(end),Run?.RestoreError);yield return EditorTestFrame.Next();}yield return EditorTestFrame.Next();}
    static IEnumerator Seconds(float seconds){float end=Time.time+seconds;double timeout=EditorApplication.timeSinceStartup+30;while(Time.time<end){Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(timeout));yield return EditorTestFrame.Next();}}
}
