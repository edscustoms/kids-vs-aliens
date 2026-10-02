using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

// Test-only observer: EditorTestFrame intentionally does not promise one player frame per yield.
public sealed class GroundFlybyVisibilityProbe : MonoBehaviour
{
    public float VisibleSeconds { get; private set; }
    public int VisibleFrames { get; private set; }
    private void LateUpdate()
    {
        var camera=Camera.main;
        var point=camera.WorldToViewportPoint(transform.position);
        if(point.z>4 && point.x>.18f && point.x<.82f && point.y>.2f && point.y<.8f
            && !Physics.Linecast(camera.transform.position,transform.position,~0,QueryTriggerInteraction.Ignore))
        {
            VisibleSeconds+=Time.deltaTime;
            VisibleFrames++;
        }
    }
}

public sealed class AlienBikePlayTests
{
    private const string Key="AlienBikePlayTests";
    private static PlayerBikeRider Rider=>Object.FindAnyObjectByType<PlayerBikeRider>();
    private static AlienBikeController Bike=>Object.FindAnyObjectByType<AlienBikeController>();
    private static StarterAssetsInputs Input=>Rider.GetComponent<StarterAssetsInputs>();
    [UnitySetUp] public IEnumerator Setup()
    {
        SessionState.SetString(Key,Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY")??"");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",System.IO.Path.GetFullPath("Logs/AlienBike/TestSaves-"+Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        yield return new EnterPlayMode();Application.runInBackground=true;Time.timeScale=1;
        GameplayCameraSettings.Mode=GameplayCameraMode.Action;
        yield return Seconds(3);
        Assert.That(ActiveRunController.Instance.IsReady,Is.True);
        // Bike checks begin after the level arrival; its independent authored beam is tested elsewhere.
        Rider.GetComponent<BeamTransportController>().CancelTransport();
        ActiveRunController.Instance.SendMessage("OnApplicationFocus",true);
        Object.FindAnyObjectByType<InGameMenuController>().ResumeGame();
        yield return Until(()=>!Rider.GetComponent<GameplaySuspensionController>().IsSuspended,8);
        Teleport(Bike.mountApproaches[0].approachPoint.position+Vector3.left*1.1f+Vector3.up*.1f);
        yield return Seconds(.5f);
    }
    private static void Teleport(Vector3 p)
    {
        var cc=Rider.GetComponent<CharacterController>();cc.enabled=false;Rider.transform.position=p;cc.enabled=true;
        Rider.GetComponent<ThirdPersonController>().ResetMotion();Physics.SyncTransforms();
    }
    private static void Capture(string name)=>ProceduralUIReview.Capture("bike-"+name,1280,720);
    private static IEnumerator Mount()
    {
        Assert.That(Rider.NearbyBike,Is.SameAs(Bike));Capture("approach");
        Assert.That(Rider.TryMount(Bike),Is.True,"Mount eligibility, grounded bike and clear side");
        Assert.That(Input.CanProcessGameplayInput,Is.False);
        yield return Seconds(.2f);Capture("walk-to-bike");
        yield return Until(()=>Rider.IsDriving,5);
        yield return Seconds(.4f);Capture("mounted-idle");
        Assert.That(Rider.GetComponent<CharacterController>().enabled,Is.False);
        Assert.That(Rider.GetComponent<ThirdPersonController>().enabled,Is.False);
        Assert.That(Rider.GetComponent<PlayerShooter>().enabled,Is.False);
        Assert.That(Rider.GetComponent<PlayerInventory>().enabled,Is.False);
        AssertNeutralFeedback();
    }
    [UnityTest] public IEnumerator MountDriveJumpTurboCollisionDismountAndRepeatedUse()
    {
        yield return Mount();
        var left=GameObject.CreatePrimitive(PrimitiveType.Cube);left.name="Test dismount left blocker";
        left.transform.position=Bike.dismountLeft.position+Vector3.up;left.transform.localScale=new(1,2,1);
        Physics.SyncTransforms();Assert.That(Rider.TryFindDismount(out var rightExit),Is.True);
        Assert.That(Vector3.Distance(rightExit,Bike.dismountRight.position),Is.LessThan(.6));
        var right=Object.Instantiate(left);right.transform.position=Bike.dismountRight.position+Vector3.up;
        Physics.SyncTransforms();Assert.That(Rider.TryDismount(),Is.False,"Both sides obstructed");
        Object.Destroy(left);Object.Destroy(right);yield return EditorTestFrame.Next();
        Input.ShootInput(true);Assert.That(Input.shoot,Is.False);
        Input.MoveInput(new(.15f,1));yield return Seconds(.8f);Capture("driving");
        Assert.That(Bike.Speed,Is.GreaterThan(1));Assert.That(Rider.TryDismount(),Is.False);
        float charge=Bike.Turbo01;Input.SprintInput(true);yield return Seconds(.5f);Capture("turbo");
        Assert.That(Bike.IsTurbo,Is.True);Assert.That(Bike.Turbo01,Is.LessThan(charge));
        Assert.That(BikeRoll(),Is.LessThan(0));
        Assert.That(Mathf.Abs(BikeRoll()),Is.LessThanOrEqualTo(Bike.GetComponent<AlienBikeVisualFeedback>().maxBikeLean));
        Input.SprintInput(false);Input.MoveInput(Vector2.zero);
        yield return Until(()=>Bike.Speed<.8f && Bike.IsGrounded,8);
        Input.JumpInput(true);yield return Seconds(.8f);Capture("jump-charge");
        Assert.That(Bike.JumpCharge01,Is.GreaterThan(.45));Input.JumpInput(false);
        yield return Seconds(.12f);Capture("charged-jump");Assert.That(Bike.Body.linearVelocity.y,Is.GreaterThan(2));
        Input.JumpInput(true);yield return Seconds(.2f);Assert.That(Bike.IsCharging,Is.False,"No air jump");Input.JumpInput(false);
        yield return Until(()=>Bike.IsGrounded && Bike.Speed<.8f,8);
        Assert.That(Rider.TryDismount(),Is.True);yield return Until(()=>!Rider.IsBusy,4);Capture("dismounted");
        Assert.That(Rider.GetComponent<CharacterController>().enabled,Is.True);
        Assert.That(Rider.GetComponent<ThirdPersonController>().enabled,Is.True);
        AssertNeutralFeedback();
        yield return EditorTestFrame.Next();
        Assert.That(Input.CanProcessGameplayInput,Is.True);
        Assert.That(Object.FindObjectsByType<AlienBikeController>(FindObjectsInactive.Include).Length,Is.EqualTo(1));
        yield return Seconds(.3f);Assert.That(Rider.TryMount(Bike),Is.True);yield return Until(()=>Rider.IsDriving,5);
        Assert.That(Rider.TryDismount(),Is.True);yield return Until(()=>!Rider.IsBusy,4);
    }
    [UnityTest] public IEnumerator PauseCancelsChargeAndContinueRestoresGroundedBikeAndOnFootPlayer()
    {
        yield return Mount();
        Input.JumpInput(true);yield return Seconds(.5f);Assert.That(Bike.IsCharging,Is.True);
        var suspension=Rider.GetComponent<GameplaySuspensionController>();
        using(var pause=suspension.Acquire(SuspensionReason.ManualPause))
        {yield return EditorTestFrame.Next();Assert.That(Bike.IsCharging,Is.False);Assert.That(Input.CanProcessBikeControls,Is.False);}
        yield return Seconds(.2f);Assert.That(Bike.IsCharging,Is.False);
        Input.JumpInput(false);
        var run=ActiveRunController.Instance;var exit=Bike.SafeExit;var bikePosition=Bike.transform.position;
        Input.JumpInput(true);yield return Seconds(.8f);Input.JumpInput(false);
        Input.MoveInput(new(.5f,0));yield return Seconds(.18f);
        Assert.That(Mathf.Abs(BikeRoll()),Is.GreaterThan(.2f),"Save with an actual transient lean while airborne");
        Assert.That(Bike.transform.position.y,Is.GreaterThan(bikePosition.y+.2f),"Snapshot is actually airborne");
        Assert.That(run.Save(),Is.True);run.PrepareToLeave();
        Assert.That(RunSaveService.Continue(),Is.True,RunSaveService.LastError);
        yield return EditorTestFrame.Next();yield return Until(()=>ActiveRunController.Instance!=null&&ActiveRunController.Instance.IsReady,20,true);
        Assert.That(Rider.IsBusy,Is.False);Assert.That(Rider.GetComponent<CharacterController>().enabled,Is.True);
        Assert.That(Vector3.Distance(Rider.transform.position,exit),Is.LessThan(.15f));
        Assert.That(Vector3.Distance(Bike.transform.position,bikePosition),Is.LessThan(.3f));
        Assert.That(Bike.Body.isKinematic,Is.True);Assert.That(Bike.IsCharging||Bike.IsTurbo,Is.False);
        Assert.That(Object.FindObjectsByType<AlienBikeController>(FindObjectsInactive.Include).Length,Is.EqualTo(1));
        Assert.That(Time.timeScale,Is.Zero,"Continue returns paused");
        AssertNeutralFeedback();
        Object.FindAnyObjectByType<InGameMenuController>().ResumeGame();yield return Seconds(.2f);
        Assert.That(Input.CanProcessGameplayInput,Is.True);
        Assert.That(Rider.TryMount(Bike),Is.True);yield return Until(()=>Rider.IsDriving,5);
    }
    [UnityTest] public IEnumerator MountInterruptionReleasesControlsAndExistingFenceStopsBike()
    {
        Assert.That(Rider.TryMount(Bike),Is.True);
        yield return Seconds(.1f);
        Bike.enabled=false;
        yield return EditorTestFrame.Next();
        Assert.That(Rider.IsBusy,Is.False);
        Assert.That(Rider.GetComponent<CharacterController>().enabled,Is.True);
        Assert.That(Rider.GetComponent<GameplaySuspensionController>().OwnerCount,Is.Zero);
        Assert.That(Input.CanProcessGameplayInput,Is.True);
        Bike.enabled=true;
        Teleport(Bike.mountApproaches[0].approachPoint.position+Vector3.left*.8f+Vector3.up*.1f);
        yield return Seconds(.3f);yield return Mount();
        var origin=Bike.transform.position;var forward=Bike.transform.forward;
        var barrier=Physics.RaycastAll(origin+Vector3.up*.5f,forward,30,~0,QueryTriggerInteraction.Ignore)
            .Where(h=>!h.collider.transform.IsChildOf(Bike.transform)&&!h.collider.transform.IsChildOf(Rider.transform))
            .OrderBy(h=>h.distance).FirstOrDefault();
        Assert.That(barrier.collider,Is.Not.Null,"Existing ConstructionSite fence in front of the authored test bike");
        Input.MoveInput(Vector2.up);
        yield return Seconds(4);
        float travelled=Vector3.Dot(Bike.transform.position-origin,forward);
        Assert.That(travelled,Is.GreaterThan(1),"Actually drove toward the existing environment");
        Assert.That(travelled,Is.LessThan(barrier.distance),"Rigidbody must not cross the existing fence");
        Assert.That(Bike.Speed,Is.LessThan(1.2f),"Held throttle is stopped by physical contact");
        Capture("fence-contact");Input.MoveInput(Vector2.zero);
        Input.MoveInput(new(.7f,0));yield return Seconds(.3f);
        Assert.That(Mathf.Abs(BikeRoll()),Is.GreaterThan(.4f));
        Rider.GetComponent<GameplaySuspensionController>().ReleaseAll();
        yield return EditorTestFrame.Next();yield return EditorTestFrame.Next();
        Assert.That(Rider.IsBusy,Is.False,"Loss of the owned lease aborts the ride");
        Assert.That(Rider.GetComponent<CharacterController>().enabled,Is.True);
        Assert.That(Bike.Body.isKinematic,Is.True);
        Assert.That(Input.CanProcessGameplayInput,Is.True);
        AssertNeutralFeedback();
    }
    [UnityTest] public IEnumerator SteeringFeedbackIsCosmeticSmoothAndScopedToSeatedRiding()
    {
        yield return Mount();
        var feedback=Bike.GetComponent<AlienBikeVisualFeedback>();
        var visual=Rider.GetComponent<PlayerCharacter>().ActiveVisual.transform;
        var markers=Bike.mountApproaches.SelectMany(p=>new[]{p.approachPoint,p.mountPoint})
            .Concat(new[]{Bike.seatPoint,Bike.dismountLeft,Bike.dismountRight}).Distinct().ToArray();
        var markerPositions=markers.Select(t=>t.localPosition).ToArray();
        var markerRotations=markers.Select(t=>t.localRotation).ToArray();
        Input.MoveInput(new(.5f,0));yield return Seconds(.65f);
        float partial=Mathf.Abs(BikeRoll());
        Input.MoveInput(new(1,0));yield return Seconds(.65f);
        float slow=Mathf.Abs(BikeRoll());
        Assert.That(partial/slow,Is.InRange(.45f,.55f),"Continuous partial steering");
        Assert.That(slow,Is.InRange(.5f,2f),"Subtle stationary roll");
        Assert.That(BikeRoll(),Is.LessThan(0),"Right input rolls right");
        Assert.That(Mathf.DeltaAngle(0,feedback.steeringPivot.localEulerAngles.y),Is.GreaterThan(14));
        Input.MoveInput(Vector2.zero);yield return Seconds(.06f);
        Assert.That(Mathf.Abs(BikeRoll()),Is.InRange(.02f,slow*.9f),"Release interpolates instead of snapping");
        yield return Seconds(.8f);AssertNeutralFeedback();

        // Use the actual ConstructionSite camera and an open ground lane for the moving captures.
        Bike.Body.position=new Vector3(33,Bike.transform.position.y,-30);
        Bike.Body.rotation=Quaternion.identity;
        Input.MoveInput(Vector2.up);yield return Until(()=>Bike.ForwardSpeed>10,3);
        Capture("polish-neutral");
        Input.MoveInput(new(-1,1));yield return Seconds(.5f);
        Assert.That(BikeRoll(),Is.GreaterThan(5),"Left input rolls left at driving speed");
        Assert.That(Mathf.DeltaAngle(0,visual.localEulerAngles.z),Is.GreaterThan(6));
        Assert.That(Mathf.DeltaAngle(0,feedback.steeringPivot.localEulerAngles.y),Is.LessThan(-14));
        Capture("polish-hard-left");
        Bike.Body.position=new Vector3(33,Bike.transform.position.y,-20);
        Bike.Body.rotation=Quaternion.identity;
        Bike.Body.linearVelocity=Vector3.forward*12;
        Input.MoveInput(new(1,1));yield return Seconds(.65f);
        Assert.That(BikeRoll(),Is.LessThan(-5),"Right input rolls right at driving speed");
        Assert.That(Mathf.DeltaAngle(0,visual.localEulerAngles.z),Is.LessThan(-6));
        Capture("polish-hard-right");
        Assert.That(Mathf.Abs(BikeRoll()),Is.LessThanOrEqualTo(feedback.maxBikeLean+.01f));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0,visual.localEulerAngles.z))/Mathf.Abs(BikeRoll()),
            Is.EqualTo(feedback.maxRiderLean/feedback.maxBikeLean).Within(.02f));
        Assert.That(Vector3.Dot(Bike.transform.up,Vector3.up),Is.GreaterThan(.9999f));
        Assert.That(Quaternion.Angle(Rider.transform.rotation,Bike.seatPoint.rotation),Is.LessThan(.01f));
        for(int i=0;i<markers.Length;i++)
        {
            Assert.That(markers[i].localPosition,Is.EqualTo(markerPositions[i]));
            Assert.That(markers[i].localRotation,Is.EqualTo(markerRotations[i]));
        }
        feedback.enabled=false;AssertNeutralFeedback();
        feedback.enabled=true;yield return Seconds(.4f);
        Assert.That(Mathf.Abs(BikeRoll()),Is.GreaterThan(.5f));
        Input.MoveInput(Vector2.zero);yield return Until(()=>Bike.Speed<.8f && Bike.IsGrounded,8);
        yield return Seconds(.8f); // A collision can stop physics before the cosmetic return has settled.
        AssertNeutralFeedback();
        Input.MoveInput(new(-1,0));yield return Seconds(.3f);
        Assert.That(Mathf.Abs(BikeRoll()),Is.GreaterThan(.5f));
        Assert.That(Rider.TryDismount(),Is.True);yield return Until(()=>!Rider.IsBusy,4);
        AssertNeutralFeedback();
    }
    private static float BikeRoll()=>Mathf.DeltaAngle(0,Bike.GetComponent<AlienBikeVisualFeedback>().bikeLeanPivot.localEulerAngles.z);
    private static void AssertNeutralFeedback()
    {
        var feedback=Bike.GetComponent<AlienBikeVisualFeedback>();
        Assert.That(Quaternion.Angle(feedback.bikeLeanPivot.localRotation,Quaternion.identity),Is.LessThan(.04f));
        Assert.That(Quaternion.Angle(feedback.steeringPivot.localRotation,Quaternion.identity),Is.LessThan(.04f));
        Assert.That(Quaternion.Angle(Rider.GetComponent<PlayerCharacter>().ActiveVisual.transform.localRotation,Quaternion.identity),Is.LessThan(.04f));
    }
    [UnityTest] public IEnumerator FlybysReuseThreeBikesFollowCurvesAndWorldSpaceAudio()
    {
        var controller=Object.FindAnyObjectByType<AlienFlybyController>();controller.enabled=false;
        var pool=controller.GetComponentsInChildren<AlienFlybyBike>(true);
        foreach(var b in pool)b.gameObject.SetActive(false);
        var routes=controller.GetComponentsInChildren<AlienFlybyPath>(true).OrderBy(p=>p.name).ToArray();
        var camera=Camera.main;
        float originalFov=camera.fieldOfView;
        var groundViews=GroundViews();
        Assert.That(groundViews.Count,Is.GreaterThan(20));
        var visibilityFailures=new System.Collections.Generic.List<string>();
        foreach(int index in new[]{0,2,4,5,6,8,9})
        {
            var path=routes[index];
            Vector3 view=FindGroundView(path,groundViews);
            Teleport(view);
            yield return Seconds(.8f);
            Assert.That(Mathf.Abs(Rider.transform.position.y-view.y),Is.LessThan(.15f),"Amy stays on real ground");
            Assert.That(camera.fieldOfView,Is.EqualTo(originalFov));
            Assert.That(GameplayCameraSettings.Mode,Is.EqualTo(GameplayCameraMode.Action));
            float speed=23; // Shortest visibility window at the existing maximum speed.
            pool[0].Begin(path,false,speed,0,index%3);
            var source=pool[0].GetComponentInChildren<AudioSource>();
            Assert.That(source.spatialBlend,Is.EqualTo(1));Assert.That(source.maxDistance,Is.GreaterThan(35));
            // Observe every player frame. Editor coroutine yields can span several frames,
            // and PNG capture stalls the editor, so measure a separate uninterrupted pass.
            var visibility=pool[0].gameObject.AddComponent<GroundFlybyVisibilityProbe>();
            yield return Until(()=>!pool[0].IsFlying,path.Length/speed+2);
            float visibleSeconds=visibility.VisibleSeconds;
            int visibleFrames=visibility.VisibleFrames;
            Object.Destroy(visibility);
            Debug.Log($"GROUND FLYBY {path.name}: player={Rider.transform.position}, camera={camera.transform.position}, visible={visibleSeconds:F3}s / {visibleFrames} frames at {speed}m/s");
            // Allow one 60 Hz frame of entry/exit quantization around the 0.15 s window.
            if(visibleSeconds<.15f-1f/60 || visibleFrames<=2)
                visibilityFailures.Add(path.name+" visible for only "+visibleSeconds+"s / "+visibleFrames+" frames");
            pool[0].Begin(path,false,speed,0,index%3);
            bool captured=false;
            float end=Time.time+path.Length/speed+1;
            while(pool[0].IsFlying && Time.time<end)
            {
                Foreground();
                var point=Camera.main.WorldToViewportPoint(pool[0].transform.position);
                if(point.z>4 && point.x>.18f && point.x<.82f && point.y>.2f && point.y<.8f
                    && ClearView(camera.transform.position,pool[0].transform.position))
                {
                    if(!captured && point.x>.3f && point.x<.7f && point.y>.3f && point.y<.7f)
                    {Capture("followup-"+path.name+"-ground-action");captured=true;}
                }
                Assert.That(Vector3.Distance(source.transform.position,pool[0].transform.position),Is.LessThan(.01));
                yield return EditorTestFrame.Next();
            }
            Assert.That(captured,Is.True,path.name+" must be readable from normal ground gameplay");
            Assert.That(pool[0].IsFlying,Is.False);
        }
        for(int i=0;i<3;i++)pool[i].Begin(routes[0],false,18,i*.7f,i);
        yield return Seconds(1.5f);
        Assert.That(controller.GetComponentsInChildren<AlienFlybyBike>(true),Is.EquivalentTo(pool));
        Assert.That(pool.All(b=>b.IsFlying),Is.True);Assert.That(pool.All(b=>b.GetComponentsInChildren<Collider>().Length==0),Is.True);
        Assert.That(visibilityFailures,Is.Empty,"Common routes need more than a one-frame glimpse");
    }
    private static System.Collections.Generic.List<Vector3> GroundViews()
    {
        var result=new System.Collections.Generic.List<Vector3>();
        var cc=Rider.GetComponent<CharacterController>();
        for(int x=-37;x<=37;x+=2)for(int z=-47;z<=45;z+=2)
        {
            var support=Physics.RaycastAll(new Vector3(x,4,z),Vector3.down,7,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.collider.name.Contains("Main_Terrain")&&h.normal.y>.82f).OrderBy(h=>h.distance).FirstOrDefault();
            if(support.collider==null)continue;
            Vector3 feet=support.point+Vector3.up*.05f;
            float radius=cc.radius*.9f;
            Vector3 center=feet+cc.center;
            if(Physics.CheckCapsule(center-Vector3.up*(cc.height*.5f-cc.radius),center+Vector3.up*(cc.height*.5f-cc.radius),radius,~0,QueryTriggerInteraction.Ignore))continue;
            result.Add(feet);
        }
        return result;
    }
    private static Vector3 FindGroundView(AlienFlybyPath path,System.Collections.Generic.List<Vector3> candidates)
    {
        var camera=Camera.main;Vector3 offset=camera.transform.position-Rider.transform.position;
        var inverse=Quaternion.Inverse(camera.transform.rotation);
        float halfHeight=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
        float best=0;Vector3 selected=default;
        foreach(var view in candidates)
        {
            int visible=0;
            for(int i=1;i<100;i++)
            {
                Vector3 world=path.Evaluate(i/100f),p=inverse*(world-(view+offset));
                if(p.z<4)continue;
                float vx=.5f+p.x/(2*p.z*halfHeight*camera.aspect),vy=.5f+p.y/(2*p.z*halfHeight);
                if(vx>.3f&&vx<.7f&&vy>.3f&&vy<.7f&&ClearView(view+offset,world))visible++;
            }
            if(visible>best){best=visible;selected=view;}
        }
        Assert.That(best,Is.GreaterThan(0),path.name+" has no readable ground-level Action view");
        return selected;
    }
    private static bool ClearView(Vector3 from,Vector3 to)
    {
        return !Physics.Linecast(from,to,~0,QueryTriggerInteraction.Ignore);
    }
    [UnityTest] public IEnumerator RadialInteractionMountsFromEightSidesWithoutWalkingThroughHull()
    {
        for(int i=0;i<8;i++)
        {
            float angle=i*Mathf.PI/4;
            Vector3 point=Bike.transform.TransformPoint(new Vector3(Mathf.Sin(angle)*2.8f,-Bike.hoverHeight,Mathf.Cos(angle)*2.8f));
            Assert.That(Bike.TryGroundPoint(point,out var feet),Is.True);
            Teleport(feet);yield return Seconds(.25f);
            Assert.That(Rider.NearbyBike,Is.SameAs(Bike),"Radial RIDE visibility, direction "+i);
            var button=Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include).Single(b=>b.name=="BikeUse");
            Assert.That(button.gameObject.activeInHierarchy&&button.interactable,Is.True);
            button.onClick.Invoke();
            Assert.That(Rider.IsBusy,Is.True);
            if(i==2)Assert.That(Rider.SelectedMountPoint,Is.SameAs(Bike.mountApproaches[3].mountPoint));
            if(i==6)Assert.That(Rider.SelectedMountPoint,Is.SameAs(Bike.mountApproaches[0].mountPoint));
            if(i==0||i==2||i==4||i==6)Capture("followup-mount-side-"+i);
            yield return CheckWalkUntilSeated();
            Assert.That(Rider.TryDismount(),Is.True);yield return Until(()=>!Rider.IsBusy,4);yield return EditorTestFrame.Next();
        }
    }
    [UnityTest] public IEnumerator BlockedMountFallsBackAroundHullAndUnsafeGroundIsRejected()
    {
        var left=Bike.mountApproaches[0].mountPoint;var right=Bike.mountApproaches[3].mountPoint;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Blocked left mount";
        wall.transform.SetPositionAndRotation(left.position+Vector3.up,Bike.transform.rotation);wall.transform.localScale=new(.65f,2,.9f);
        Physics.SyncTransforms();
        Vector3 player=Bike.transform.TransformPoint(new Vector3(-2.8f,-Bike.hoverHeight,-.35f));
        Assert.That(Bike.TryGroundPoint(player,out var feet),Is.True);Teleport(feet);yield return Seconds(.25f);
        Assert.That(Rider.NearbyBike,Is.SameAs(Bike));Assert.That(Rider.TryMount(Bike),Is.True);
        Assert.That(Rider.SelectedMountPoint,Is.SameAs(right),"Blocked left side falls back to a reachable right-side mount");
        yield return CheckWalkUntilSeated();Capture("followup-fallback-right");
        Rider.AbortRide();Object.Destroy(wall);yield return Seconds(.2f);
        // Unsupported authoring markers cannot be selected even though their radial zone is nearby.
        left.position+=Vector3.up*4;right.position+=Vector3.up*4;
        Teleport(feet);yield return Seconds(.3f);
        Assert.That(Rider.NearbyBike,Is.Null);Assert.That(Rider.TryMount(Bike),Is.False);
    }
    private static IEnumerator CheckWalkUntilSeated()
    {
        float end=Time.time+12;var cc=Rider.GetComponent<CharacterController>();var hull=Bike.GetComponent<BoxCollider>();
        while(!Rider.IsDriving&&Time.time<end)
        {
            Foreground();
            if(Rider.Phase==BikeRidePhase.Approaching)
                Assert.That(Physics.ComputePenetration(cc,Rider.transform.position,Rider.transform.rotation,hull,Bike.transform.position,Bike.transform.rotation,out _,out float depth)&&depth>.015f,Is.False,"Walking capsule must stay outside the bike");
            yield return EditorTestFrame.Next();
        }
        Assert.That(Rider.IsDriving,Is.True,"Authored route completes without sliding through the hull or getting stuck");
    }
    private static IEnumerator Seconds(float duration)
    {float end=Time.time+duration;double timeout=EditorApplication.timeSinceStartup+duration*5+10;while(Time.time<end&&EditorApplication.timeSinceStartup<timeout){Foreground();yield return EditorTestFrame.Next();}Assert.That(Time.time,Is.GreaterThanOrEqualTo(end));}
    private static IEnumerator Until(Func<bool> condition,float seconds,bool unscaled=false)
    {double deadline=EditorApplication.timeSinceStartup+seconds*3+10;float end=(unscaled?Time.unscaledTime:Time.time)+seconds;while(!condition()&&(unscaled?Time.unscaledTime:Time.time)<end&&EditorApplication.timeSinceStartup<deadline){if(!unscaled)Foreground();yield return EditorTestFrame.Next();}Assert.That(condition(),Is.True,"Bike condition timed out; phase="+Rider?.Phase+" position="+Rider?.transform.position+" bike="+Bike?.transform.position);}
    private static void Foreground()
    {
        if(Time.timeScale!=0)return;
        ActiveRunController.Instance?.SendMessage("OnApplicationPause",false);
        ActiveRunController.Instance?.SendMessage("OnApplicationFocus",true);
        var menu=Object.FindAnyObjectByType<InGameMenuController>();if(menu!=null&&menu.IsOpen)menu.ResumeGame();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(Application.isPlaying){ActiveRunController.Instance?.PrepareToLeave();yield return new ExitPlayMode();}
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",SessionState.GetString(Key,""));SessionState.EraseString(Key);Time.timeScale=1;
    }
}
