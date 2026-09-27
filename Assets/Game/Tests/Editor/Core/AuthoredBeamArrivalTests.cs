using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class AuthoredBeamArrivalAuthoringTests
{
    [Test] public void PrefabPreviewCannotBakeAgainstUnrelatedSceneGround()
    {
        var root=PrefabUtility.LoadPrefabContents("Assets/Game/Prefabs/Environment/PF_HealingPodBeamIn.prefab");
        try
        {
            var arrival=root.GetComponent<AuthoredBeamArrival>();
            string before=EditorJsonUtility.ToJson(arrival);
            AuthoredBeamArrivalEditor.Validate(arrival);
            Assert.That(EditorJsonUtility.ToJson(arrival),Is.EqualTo(before));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    [Test] public void CompoundBoxesRegenerateLocalCandidatesAfterNormalAuthoringChanges()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Environment/PF_HealingPodBeamIn.prefab"));
        try
        {
            root.transform.position = new Vector3(80,0,120);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = root.transform.position + Vector3.down*.5f;
            ground.transform.localScale = new Vector3(100,1,100);
            var arrival = root.GetComponent<AuthoredBeamArrival>();
            var trigger = root.GetComponent<GameplayTrigger>();
            var a = trigger.Volumes[0]; a.transform.localPosition = new Vector3(0,1.5f,0); a.size = new Vector3(16,3,8);
            CheckBake(arrival,1);
            var b = new GameObject("Box_B").AddComponent<BoxCollider>();
            b.transform.SetParent(trigger.VolumeRoot,false); b.transform.localPosition = new Vector3(4,1.5f,6); b.size = new Vector3(8,3,16);
            CheckBake(arrival,2); // L-shaped union, no registration or child component.
            var c = Object.Instantiate(b,trigger.VolumeRoot); c.name="Box_C"; c.transform.localPosition += Vector3.left*4;
            CheckBake(arrival,3);
            var before = arrival.Candidates.Select(p=>p.localPosition).ToArray();
            root.transform.position += new Vector3(4,0,3);
            root.transform.rotation = Quaternion.Euler(0,35,0);
            Assert.That(AuthoredBeamArrivalEditor.NeedsValidation(arrival),Is.True);
            CheckBake(arrival,3);
            Assert.That(arrival.Candidates.Length,Is.EqualTo(before.Length));
            for(int i=0;i<before.Length;i++) Assert.That(Vector3.Distance(arrival.Candidates[i].localPosition,before[i]),Is.LessThan(.001f),"Flat support follows the local authored region");
            b.transform.localPosition += Vector3.forward*2; b.transform.localRotation = Quaternion.Euler(0,20,0); b.size += Vector3.right*3;
            Assert.That(AuthoredBeamArrivalEditor.NeedsValidation(arrival),Is.True); CheckBake(arrival,3);
            Object.DestroyImmediate(c.gameObject); CheckBake(arrival,2);
            foreach(var volume in trigger.Volumes) Assert.That(volume.GetComponents<MonoBehaviour>(),Is.Empty);
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
    }
    private static void CheckBake(AuthoredBeamArrival arrival,int boxes)
    {
        AuthoredBeamArrivalEditor.Validate(arrival);
        var trigger = arrival.GetComponent<GameplayTrigger>();
        Assert.That(trigger.Volumes.Length,Is.EqualTo(boxes)); Assert.That(arrival.Candidates.Length,Is.GreaterThan(0));
        Assert.That(AuthoredBeamArrivalEditor.NeedsValidation(arrival),Is.False);
        foreach(var record in arrival.Candidates) Assert.That(trigger.ContainsPoint(arrival.transform.TransformPoint(record.localPosition)+Vector3.up*.1f),Is.True);
        string first = EditorJsonUtility.ToJson(arrival);
        AuthoredBeamArrivalEditor.Validate(arrival);
        Assert.That(EditorJsonUtility.ToJson(arrival),Is.EqualTo(first),"Repeated validation is deterministic");
    }
    [Test] public void SelectionPrefersVisibleThenNearestSafeAndKeepsAuthoredTies()
    {
        using var fixture = new DisposableTestAssets();
        EditorSceneManager.OpenScene(fixture.Copy("Assets/Game/Scenes/ConstructionSite.unity"));
        try
        {
            var arrival = Object.FindAnyObjectByType<AuthoredBeamArrival>();
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            player.transform.position = new Vector3(1000,0,1000);
            var data = new SerializedObject(arrival);
            var list = data.FindProperty("landingCandidates");
            var region = arrival.GetComponent<GameplayTrigger>();
            region.VolumeRoot.position = player.transform.position;
            var volume = region.Volumes[0]; volume.center = new Vector3(0,2,0); volume.size = new Vector3(100,10,100);
            list.arraySize = 3;
            for (int i = 0; i < list.arraySize; i++)
            {
                var record = list.GetArrayElementAtIndex(i);
                Vector3 position = player.transform.position + (i == 0 ? Vector3.forward * 8.5f : Vector3.right * 12);
                record.FindPropertyRelative("localPosition").vector3Value = arrival.transform.InverseTransformPoint(position);
                record.FindPropertyRelative("localRotation").quaternionValue = Quaternion.identity;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            var camera = new GameObject("Selection test camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 5; camera.aspect = 1;
            camera.transform.SetPositionAndRotation(player.transform.position + new Vector3(12,20,0),Quaternion.Euler(90,0,0));
            Vector3 cameraPosition = camera.transform.position;
            Assert.That(arrival.SelectCandidate(player,camera),Is.EqualTo(1),"Visible beats nearer off-camera; ties keep first authored point");
            camera.orthographicSize = 24;
            Assert.That(arrival.SelectCandidate(player,camera),Is.EqualTo(0),"When both are visible, prefer the nearer safe point");
            camera.transform.rotation = Quaternion.identity;
            Assert.That(arrival.SelectCandidate(player,camera),Is.EqualTo(0),"No visible point falls back to nearest safe, never farthest");
            camera.transform.rotation = Quaternion.Euler(90,0,0);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = player.transform.position + new Vector3(4,10,4); blocker.transform.localScale = new Vector3(8,1,14);
            Physics.SyncTransforms();
            Assert.That(arrival.IsCandidateVisible(player.transform.position + Vector3.forward * 8.5f,camera),Is.False,"Fully occluded is not a visible arrival");
            Assert.That(arrival.SelectCandidate(player,camera),Is.EqualTo(1));
            data.FindProperty("minimumPlayerDistance").floatValue = 11; data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(arrival.SelectCandidate(player,camera),Is.EqualTo(1),"Visibility never bypasses safety distance");
            Assert.That(camera.transform.position,Is.EqualTo(cameraPosition));
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
    }
    [Test] public void SceneUsesValidatedAuthoredCandidatesAndPreservesThePodTuning()
    {
        using var fixture = new DisposableTestAssets();
        EditorSceneManager.OpenScene(fixture.Copy("Assets/Game/Scenes/ConstructionSite.unity"));
        try
        {
            var arrival = Object.FindAnyObjectByType<AuthoredBeamArrival>();
            Assert.That(arrival, Is.Not.Null);
            var data = new SerializedObject(arrival);
            var gate = (GameObject)data.FindProperty("payloadGate").objectReferenceValue;
            var payload = (GameObject)data.FindProperty("payload").objectReferenceValue;
            Assert.That(gate.activeSelf, Is.False);
            Assert.That(payload.GetComponent<HealingPodController>(), Is.Not.Null);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource(payload), Is.EqualTo(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Environment/PF_HealingPod.prefab")));
            var tuning = new SerializedObject(payload.GetComponent<HealingPodController>());
            Assert.That(tuning.FindProperty("openCloseDuration").floatValue, Is.EqualTo(1));
            Assert.That(tuning.FindProperty("healDuration").floatValue, Is.EqualTo(1.5f));
            Assert.That(arrival.GetComponent<BoxCollider>().isTrigger, Is.True);
            AuthoredBeamArrivalEditor.Validate(arrival); data.Update();
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            float authoredDistance = data.FindProperty("minimumPlayerDistance").floatValue;
            float duration = data.FindProperty("beamArrivalDuration").floatValue;
            Assert.That(arrival.RequiredPlayerDistance(player), Is.GreaterThanOrEqualTo(authoredDistance));
            Assert.That(arrival.RequiredPlayerDistance(player), Is.GreaterThan(player.GetComponent<StarterAssets.ThirdPersonController>().SprintSpeed * duration));
            var candidates = data.FindProperty("landingCandidates");
            Assert.That(candidates.arraySize, Is.GreaterThan(1));
            for(int i = 0; i < candidates.arraySize; i++)
            {
                player.transform.position = arrival.CandidatePosition(i);
                Assert.That(arrival.SelectCandidate(player), Is.Not.EqualTo(i), "Cannot select the occupied point");
            }
            Assert.That(AuthoredBeamArrivalEditor.NeedsValidation(arrival),Is.False);
            arrival.transform.position += Vector3.right;
            Assert.That(AuthoredBeamArrivalEditor.NeedsValidation(arrival),Is.True,"World support is rechecked after moving the root");
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }
}

public sealed class AuthoredBeamArrivalPlayTests
{
    private const string Key = "AuthoredBeamArrivalTests.Saves";
    private static ActiveRunController Run => ActiveRunController.Instance;
    private static PlayerCharacter Player => Run.GetComponent<PlayerCharacter>();
    private static AuthoredBeamArrival Arrival => Object.FindAnyObjectByType<AuthoredBeamArrival>();
    private static HealingPodController Pod => Object.FindAnyObjectByType<HealingPodController>(FindObjectsInactive.Include);
    private static GameplayTrigger Trigger => Arrival.GetComponent<GameplayTrigger>();
    private static T Reference<T>(string field) where T : Object => (T)new SerializedObject(Arrival).FindProperty(field).objectReferenceValue;
    private static Transform Marker(string name) => Pod.transform.Find("PlayerMarkers/" + name);

    [UnitySetUp] public IEnumerator Setup()
    {
        SessionState.SetString(Key, Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", System.IO.Path.GetFullPath("Logs/ArrivalTest-" + Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return Ready();
        Run.GetComponent<BeamTransportController>().CancelTransport(); Resume();
        yield return EditorTestFrame.Next();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(Application.isPlaying) { Run?.PrepareToLeave(); yield return new ExitPlayMode(); }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", SessionState.GetString(Key, ""));
        SessionState.EraseString(Key); Time.timeScale = 1;
    }

    [UnityTest] public IEnumerator PhysicalEntryArrivesOnceWithoutControlOwnershipOrPrematureInteraction()
    {
        Assert.That(Pod.gameObject.activeInHierarchy, Is.False);
        Place(new Vector3(-37.2f,.2f,-10.6f));
        int expected = Arrival.SelectCandidate(Player);
        Assert.That(expected, Is.GreaterThanOrEqualTo(0));
        yield return Seconds(.12f);
        Assert.That(Arrival.State, Is.EqualTo(AuthoredArrivalState.Targeting));
        Assert.That(Arrival.SelectedIndex, Is.EqualTo(expected));
        Assert.That(Trigger.HasFired, Is.True);
        Assert.That(Pod.enabled, Is.False);
        Assert.That(Pod.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True);
        Assert.That(Pod.TryBeginHealing(Player), Is.False);
        Assert.That(Player.GetComponent<GameplaySuspensionController>().OwnerCount, Is.Zero);
        Assert.That(Player.GetComponent<StarterAssets.StarterAssetsInputs>().CanProcessGameplayInput, Is.True);
        Assert.That(Reference<BeamTransportVFX>("beam").Visibility, Is.GreaterThan(0));
        yield return UntilState(AuthoredArrivalState.Arriving); yield return Seconds(.35f);
        Capture("arrival-gameplay", Camera.main);
        CaptureArrivalOverview("arrival-overview");
        yield return Seconds(1f);
        Assert.That(Arrival.HasArrived, Is.True);
        Assert.That(Pod.enabled && Pod.gameObject.activeInHierarchy, Is.True);
        Assert.That(Pod.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger), Is.True);
        Assert.That(Reference<BeamTransportVFX>("beam").Visibility, Is.Zero);
        Assert.That(Trigger.Visit(Player), Is.False);
        Assert.That(Object.FindObjectsByType<HealingPodController>(FindObjectsInactive.Include).Length, Is.EqualTo(1));
        Assert.That(Pod.RemainingCapacity, Is.EqualTo(1));
        Assert.That(Trigger.ContainsPoint(Pod.transform.position+Vector3.up*.1f),Is.True);
        CaptureArrivalOverview("arrived-overview");
    }

    [UnityTest] public IEnumerator LeftStripApproachUsesActualCamera() => ApproachStripEnd(-37.2f,"left");
    [UnityTest] public IEnumerator RightStripApproachUsesActualCamera() => ApproachStripEnd(-22.5f,"right");

    private static IEnumerator ApproachStripEnd(float x, string side)
    {
        // Settle the unchanged follow camera outside the trigger, then physically cross it.
        Place(new Vector3(x,.3f,-12)); yield return Seconds(1);
        var capsule = Player.GetComponent<CharacterController>();
        float deadline = Time.time + 4;
        while (Arrival.State == AuthoredArrivalState.Available && Time.time < deadline)
        {
            capsule.Move(Vector3.forward * (2 * Time.deltaTime));
            yield return EditorTestFrame.Next();
        }
        TestContext.WriteLine($"{side} entry player {Player.transform.position}, contains {Trigger.ContainsPoint(Player.transform.position)}");
        Capture("approach-"+side+"-gameplay",Camera.main);
        Assert.That(Arrival.State,Is.EqualTo(AuthoredArrivalState.Targeting));
        Vector3 selected = Arrival.CandidatePosition(Arrival.SelectedIndex);
        bool visible = Arrival.IsCandidateVisible(selected,Camera.main);
        TestContext.WriteLine($"{side}: candidate {Arrival.SelectedIndex}, position {selected}, distance {Vector3.Distance(Player.transform.position,selected)}, camera-visible {visible}");
        for(int i=0;i<Arrival.Candidates.Length;i++)
            TestContext.WriteLine($"candidate {i}: viewport {Camera.main.WorldToViewportPoint(Arrival.CandidatePosition(i))}, visible {Arrival.IsCandidateVisible(Arrival.CandidatePosition(i),Camera.main)}");
        yield return UntilState(AuthoredArrivalState.Arriving); yield return Seconds(.35f); Capture("arrival-"+side+"-gameplay",Camera.main);
        CaptureArrivalOverview("arrival-"+side+"-overview");
        for(int i=0;i<Arrival.Candidates.Length;i++)
            if (Vector3.ProjectOnPlane(Arrival.CandidatePosition(i)-Player.transform.position,Vector3.up).magnitude >= Arrival.RequiredPlayerDistance(Player)
                && Arrival.IsCandidateVisible(Arrival.CandidatePosition(i),Camera.main))
                Assert.That(visible,Is.True,"A safe visible candidate always wins; the unchanged region may require the deterministic fallback");
        yield return Seconds(.8f); Assert.That(Arrival.HasArrived,Is.True);
    }

    [UnityTest] public IEnumerator PendingRetargetsSmoothlyThenLocksAndWaitsForSafeCompletion()
    {
        Trigger.enabled=false;
        var data = new SerializedObject(Arrival);
        data.FindProperty("minimumPlayerDistance").floatValue=1000; data.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(Arrival.TryBegin(Player),Is.True);
        yield return Seconds(.5f);
        Assert.That(Arrival.State,Is.EqualTo(AuthoredArrivalState.Targeting));
        Assert.That(Arrival.SelectedIndex,Is.EqualTo(-1)); Assert.That(Pod.gameObject.activeInHierarchy,Is.False);
        data.FindProperty("minimumPlayerDistance").floatValue=8.1f; data.ApplyModifiedPropertiesWithoutUndo();
        yield return Seconds(.17f);
        int first=Arrival.SelectedIndex; Assert.That(first,Is.GreaterThanOrEqualTo(0));
        Vector3 firstPosition=Arrival.CandidatePosition(first);
        var beam=Reference<BeamTransportVFX>("beam");
        Vector3 previewStart=beam.transform.position;
        Place(firstPosition); yield return Seconds(.18f);
        Assert.That(Arrival.SelectedIndex,Is.Not.EqualTo(first),"Approaching an uncommitted target selects another baked point");
        Assert.That(Arrival.State,Is.EqualTo(AuthoredArrivalState.Targeting));
        Assert.That(Pod.gameObject.activeInHierarchy,Is.False);
        Assert.That(Vector3.Distance(previewStart,beam.transform.position),Is.LessThan(5),"Preview travels instead of jumping to the new point");
        yield return UntilState(AuthoredArrivalState.Arriving);
        Vector3 locked=Reference<GameObject>("payloadGate").transform.position;
        Place(locked); yield return Seconds(1.2f);
        Assert.That(Arrival.State,Is.EqualTo(AuthoredArrivalState.Arriving));
        Assert.That(Arrival.HasArrived,Is.False); Assert.That(Pod.enabled,Is.False);
        Assert.That(Pod.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled),Is.True);
        Assert.That(Reference<GameObject>("payloadGate").transform.position,Is.EqualTo(locked));
        Assert.That(Vector3.ProjectOnPlane(Player.transform.position-locked,Vector3.up).magnitude,Is.LessThan(.01f));
        var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.transform.position=locked+Vector3.up;
        Place(new Vector3(-30,.2f,-22)); yield return Seconds(1.3f);
        Assert.That(Arrival.HasArrived,Is.False,"A dynamic blocker delays completion without repositioning the pod");
        Object.Destroy(blocker); yield return Seconds(.5f);
        Assert.That(Arrival.HasArrived,Is.True); Assert.That(Pod.enabled,Is.True);
    }

    [UnityTest] public IEnumerator DisableRollsBackTargetingAndMaterializationWithoutOwningAmy()
    {
        Trigger.enabled=false;
        Assert.That(Arrival.TryBegin(Player),Is.True); Arrival.enabled=false;
        Assert.That(Arrival.State,Is.EqualTo(AuthoredArrivalState.Available));
        Assert.That(Pod.gameObject.activeInHierarchy,Is.False);
        Arrival.enabled=true; Assert.That(Arrival.TryBegin(Player),Is.True);
        yield return UntilState(AuthoredArrivalState.Arriving);
        Arrival.enabled=false;
        Assert.That(Pod.gameObject.activeInHierarchy,Is.False);
        Assert.That(Player.GetComponent<GameplaySuspensionController>().OwnerCount,Is.Zero);
        Arrival.enabled=true; Assert.That(Arrival.TryBegin(Player),Is.True);
        yield return Seconds(2); Assert.That(Arrival.HasArrived,Is.True);
    }

    [UnityTest] public IEnumerator WarmCandidateEvaluationDoesNotAllocateManagedMemory()
    {
        var arrival=Arrival; var player=Player; var camera=Camera.main;
        for(int i=0;i<16;i++) arrival.SelectCandidate(player,camera);
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<200;i++) arrival.SelectCandidate(player,camera);
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.That(allocated,Is.Zero,"Low-frequency selection reuses physics buffers and baked data");
        yield return EditorTestFrame.Next();
    }

    [UnityTest] public IEnumerator RealContinueRollsBackUncommittedArrivalAndPreservesArrivedPodCapacity()
    {
        yield return Continue();
        Assert.That(Arrival.State, Is.EqualTo(AuthoredArrivalState.Available));
        Assert.That(Pod.gameObject.activeInHierarchy, Is.False);
        Resume(); yield return EditorTestFrame.Next();
        Place(new Vector3(-37.2f,.2f,-10.6f)); Assert.That(Trigger.Visit(Player), Is.True);
        yield return Seconds(.15f); yield return Continue();
        Assert.That(Arrival.State, Is.EqualTo(AuthoredArrivalState.Available));
        Assert.That(Trigger.HasFired, Is.False); Assert.That(Pod.gameObject.activeInHierarchy, Is.False);
        Assert.That(Reference<BeamTransportVFX>("beam").Visibility, Is.Zero);
        // Continue remains paused; the volume can retry only after ordinary Resume.
        Assert.That(Player.GetComponent<GameplaySuspensionController>().IsSuspended, Is.True);
        Resume(); yield return UntilState(AuthoredArrivalState.Arriving);
        yield return Continue();
        Assert.That(Arrival.HasArrived,Is.False,"A locked presentation is not a successfully completed/persisted arrival");
        Assert.That(Pod.gameObject.activeInHierarchy,Is.False);
        Resume(); yield return Seconds(2);
        Assert.That(Arrival.HasArrived, Is.True);
        var health = Player.GetComponent<PlayerHealth>(); health.RestoreRunHealth(40,17);
        Place(Marker("PlayerExitPoint").position); yield return Seconds(1.2f);
        Place(Marker("PlayerAlignPoint").position); yield return Seconds(7);
        Assert.That(health.HealthNormalized, Is.EqualTo(1).Within(.001));
        Assert.That(health.CurrentArmor, Is.EqualTo(17));
        Assert.That(Pod.RemainingCapacity, Is.EqualTo(.4f).Within(.001));
        string id = Pod.GetComponent<RunWorldObject>().Id;
        yield return Continue();
        Assert.That(Arrival.HasArrived, Is.True); Assert.That(Pod.gameObject.activeInHierarchy, Is.True);
        Assert.That(Pod.GetComponent<RunWorldObject>().Id, Is.EqualTo(id));
        Assert.That(Pod.RemainingCapacity, Is.EqualTo(.4f).Within(.001));
        Assert.That(Reference<BeamTransportVFX>("beam").Visibility, Is.Zero);
        Assert.That(Object.FindObjectsByType<HealingPodController>(FindObjectsInactive.Include).Length, Is.EqualTo(1));
        Assert.That(Run.RestartFromBeginning(), Is.True); yield return EditorTestFrame.Next(); yield return Ready();
        Assert.That(Arrival.State, Is.EqualTo(AuthoredArrivalState.Available));
        Assert.That(Pod.RemainingCapacity, Is.EqualTo(1)); Assert.That(Pod.gameObject.activeInHierarchy, Is.False);
    }

    private static IEnumerator UntilState(AuthoredArrivalState state)
    {
        double deadline=EditorApplication.timeSinceStartup+10;
        while(Arrival.State!=state) { Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(deadline),Arrival.State.ToString()); yield return EditorTestFrame.Next(); }
    }
    private static void Place(Vector3 p) { var capsule=Player.GetComponent<CharacterController>(); capsule.enabled=false;Player.transform.position=p;capsule.enabled=true;Player.GetComponent<StarterAssets.ThirdPersonController>().ResetMotion();Physics.SyncTransforms(); }
    private static void CaptureArrivalOverview(string name)
    {
        var root = new GameObject("Arrival inspection camera");
        try
        {
            var camera = root.AddComponent<Camera>(); camera.CopyFrom(Camera.main);
            Vector3 target = Vector3.Lerp(Player.transform.position, Pod.transform.position, .5f);
            camera.transform.position = target + new Vector3(10, 18, -18);
            camera.transform.LookAt(target);
            camera.orthographic = true; camera.orthographicSize = 11;
            Capture(name, camera);
        }
        finally { Object.DestroyImmediate(root); }
    }
    internal static void Capture(string name, Camera camera)
    {
        var target = new RenderTexture(1280,720,24);
        var previous = RenderTexture.active;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
        try
        {
            target.Create();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
            System.IO.Directory.CreateDirectory("Logs/ConstructionPolish");
            System.IO.File.WriteAllBytes("Logs/ConstructionPolish/" + name + ".png",image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target); }
    }
    private static void Resume() { Run.SendMessage("OnApplicationPause",false);Run.SendMessage("OnApplicationFocus",true);Object.FindAnyObjectByType<InGameMenuController>().ResumeGame(); }
    private static IEnumerator Continue() { Assert.That(Run.Save(),Is.True);Run.PrepareToLeave();Assert.That(RunSaveService.Continue(),Is.True,RunSaveService.LastError);yield return EditorTestFrame.Next();yield return Ready(); }
    private static IEnumerator Ready() { double end=EditorApplication.timeSinceStartup+30;while(Run==null||!Run.IsReady){Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(end),Run?.RestoreError);yield return EditorTestFrame.Next();}yield return EditorTestFrame.Next(); }
    private static IEnumerator Seconds(float duration) { float end=Time.time+duration;double deadline=EditorApplication.timeSinceStartup+duration+20;while(Time.time<end){Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(deadline));yield return EditorTestFrame.Next();} }
}
