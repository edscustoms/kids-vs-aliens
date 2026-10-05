using System.Collections;
using System.Linq;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public sealed class AlienBikeTests
{
    [Test]
    public void JumpChargeClampsAndTurboExhaustionRequiresRelease()
    {
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new(6000,-.5f,6000);floor.transform.localScale=new(20,1,20);
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AlienBikeSetup.Prefabs+"/PF_RideableAlienBike.prefab"));
        try
        {
            var bike=go.GetComponent<AlienBikeController>();go.transform.position=new(6000,bike.hoverHeight,6000);Physics.SyncTransforms();
            Assert.That(bike.CheckGrounded(),Is.True);bike.StartDriving();
            bike.TickControls(bike.maxChargeTime*3,true,false);
            Assert.That(bike.JumpCharge01,Is.EqualTo(1));bike.TickControls(.01f,false,false);
            Assert.That(bike.Body.linearVelocity.y,Is.EqualTo(bike.maxJump).Within(.001));
            bike.TickControls(1,true,false);Assert.That(bike.IsCharging,Is.False,"No second launch in the air");
            bike.TickControls(bike.maxTurboCharge/bike.drainRate+1,false,true);
            Assert.That(bike.Turbo01,Is.Zero);Assert.That(bike.IsTurbo,Is.False);
            bike.TickControls(.1f,false,true);Assert.That(bike.IsTurbo,Is.False);
            string state=bike.CaptureRunState();bike.RestoreRunState(state);bike.CheckGrounded();
            bike.TickControls(1,false,false);Assert.That(bike.Turbo01,Is.GreaterThan(0));
            float before=bike.Turbo01;bike.TickControls(.1f,false,true);Assert.That(bike.Turbo01,Is.LessThan(before));
        }finally{Object.DestroyImmediate(go);Object.DestroyImmediate(floor);}
    }
    [Test]
    public void SharedVisualAndPhysicsHaveSeparateOwners()
    {
        var visual=AssetDatabase.LoadAssetAtPath<GameObject>(AlienBikeSetup.Prefabs+"/PF_AlienBikeVisual.prefab");
        Assert.That(visual.GetComponentInChildren<AlienBikeVisual>(),Is.Not.Null);
        Assert.That(visual.GetComponentsInChildren<Collider>(true),Is.Empty);
        Assert.That(visual.GetComponentsInChildren<AlienBikeController>(true),Is.Empty);
        Assert.That(visual.GetComponentsInChildren<AlienFlybyBike>(true),Is.Empty);
        var ride=AssetDatabase.LoadAssetAtPath<GameObject>(AlienBikeSetup.Prefabs+"/PF_RideableAlienBike.prefab");
        Assert.That(ride.GetComponent<Rigidbody>(),Is.Not.Null);
        Assert.That(ride.GetComponentsInChildren<WheelCollider>(),Is.Empty);
        Assert.That(ride.GetComponent<RunWorldObject>(),Is.Not.Null);
        Assert.That(ride.GetComponent<AlienBikeImpact>(),Is.Not.Null);
        var fly=AssetDatabase.LoadAssetAtPath<GameObject>(AlienBikeSetup.Prefabs+"/PF_AlienFlybyBike.prefab");
        Assert.That(fly.GetComponentsInChildren<Rigidbody>(true),Is.Empty);
        Assert.That(fly.GetComponentsInChildren<Collider>(true),Is.Empty);
        Assert.That(fly.GetComponentsInChildren<AlienBikeImpact>(true),Is.Empty);
        var mesh=visual.GetComponentInChildren<MeshFilter>().sharedMesh;
        Assert.That(mesh.triangles.Length/3,Is.LessThan(6000));
    }
    [Test]
    public void ConstructionRoutesValidateWholeVolumeAndInvalidateEditedPoints()
    {
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        try
        {
            var owner=Object.FindAnyObjectByType<AlienFlybyController>();
            var paths=owner.GetComponentsInChildren<AlienFlybyPath>(true);
            Assert.That(paths.Length,Is.InRange(8,12));
            Assert.That(paths.Count(p=>Enumerable.Range(0,101).Min(i=>p.Evaluate(i/100f).y)<=6.1f),Is.EqualTo(7),"Common routes have a low pass, with clearance climbs where needed");
            Assert.That(paths.Count(p=>new[]{p.start,p.control1,p.control2,p.end}.Max(t=>t.position.y)>20),Is.EqualTo(1),"Only one very high ambience route");
            var configured=new SerializedObject(owner).FindProperty("paths");
            var entries=Enumerable.Range(0,configured.arraySize).Select(i=>configured.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
            Assert.That(entries,Is.EquivalentTo(paths),"Uniform route selection keeps the 70/20/10 authored distribution");
            foreach(var path in paths)
            {
                Assert.That(path.IsValidated,Is.True,path.name+" authored validation survives reload");
                Assert.That(AlienFlybyValidation.Validate(path,owner.environmentRoots,out string failure),Is.True,failure);
                Vector3 end=path.PositionAtDistance(path.Length,false,out var tangent);
                Assert.That(Vector3.Distance(end,path.end.position),Is.LessThan(.001));
                Assert.That(tangent.sqrMagnitude,Is.EqualTo(1).Within(.001));
            }
            Assert.DoesNotThrow(() => new AlienFlybyBuildValidation().OnProcessScene(owner.gameObject.scene, null));
            var p=paths[0];p.control1.position+=Vector3.right;
            Assert.That(p.IsValidated,Is.False,"Moving an authored marker requires validation again");
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.SetParent(owner.environmentRoots[0]);blocker.transform.position=p.Evaluate(.5f)+Vector3.up*1.8f;
            blocker.transform.localScale=Vector3.one*.3f;Physics.SyncTransforms();
            Assert.That(AlienFlybyValidation.Validate(p,owner.environmentRoots,out _),Is.False,"Centerline is clear but bike volume is blocked");
            Assert.That(AlienFlybyValidation.Validate(paths[1],new Transform[]{null},out _),Is.False,"Missing environment wiring must not validate an empty world");
        }
        finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
    }
    [Test]
    public void BikeRestoreIsAbsoluteAndTransientFree()
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AlienBikeSetup.Prefabs+"/PF_RideableAlienBike.prefab"));
        try
        {
            var bike=go.GetComponent<AlienBikeController>();bike.CheckGrounded();
            go.transform.SetPositionAndRotation(new(4,1,9),Quaternion.Euler(0,65,0));bike.RememberSafePose(new(6,.1f,9));
            string state=bike.CaptureRunState();go.transform.position=Vector3.one*100;
            bike.RestoreRunState(state);bike.RestoreRunState(state);
            Assert.That(go.transform.position,Is.EqualTo(new Vector3(4,1,9)));
            Assert.That(bike.IsCharging||bike.IsTurbo,Is.False);Assert.That(bike.Rider,Is.Null);
            Assert.That(bike.Body.isKinematic,Is.True);Assert.That(bike.SafeExit,Is.EqualTo(new Vector3(6,.1f,9)));
        }finally{Object.DestroyImmediate(go);}
    }
    [UnityTest]
    public IEnumerator RidingLeaseAllowsOnlyMountedControlsAndComposesWithPause()
    {
        var go=new GameObject("Bike input lease fixture");var input=go.AddComponent<StarterAssetsInputs>();
        var owner=go.AddComponent<GameplaySuspensionController>();
        try
        {
            using(var ride=owner.Acquire(SuspensionReason.BikeRiding))
            {
                yield return EditorTestFrame.Next();
                Assert.That(input.CanProcessGameplayInput,Is.False);
                input.MoveInput(Vector2.up);input.JumpInput(true);input.SprintInput(true);input.ShootInput(true);
                Assert.That(input.move,Is.EqualTo(Vector2.up));Assert.That(input.jump&&input.sprint,Is.True);Assert.That(input.shoot,Is.True);
                using(var pause=owner.Acquire(SuspensionReason.ManualPause))
                {
                    Assert.That(owner.IsWorldPaused,Is.True);Assert.That(input.CanProcessBikeControls,Is.False);
                    Assert.That(input.move,Is.EqualTo(Vector2.zero));Assert.That(input.jump||input.sprint,Is.False);
                }
                yield return EditorTestFrame.Next();
                input.MoveInput(Vector2.up);Assert.That(input.move,Is.EqualTo(Vector2.zero),"Held controls require neutral after pause");
                input.MoveInput(Vector2.zero);input.MoveInput(Vector2.up);Assert.That(input.move,Is.EqualTo(Vector2.up));
                Assert.That(owner.IsWorldPaused,Is.False);
            }
            yield return EditorTestFrame.Next();Assert.That(input.CanProcessGameplayInput,Is.True);
        }finally{Object.DestroyImmediate(go);Time.timeScale=1;}
    }
    [TestCase("GamePoc")]
    [TestCase("ConstructionSite")]
    public void PlayerRepairPreservesAuthoredSettingsAndDoesNotDuplicate(string scene)
    {
        EditorSceneManager.OpenScene("Assets/Game/Scenes/"+scene+".unity");
        try
        {
            var player=Object.FindAnyObjectByType<PlayerCharacter>();AlienBikeSetup.ConfigurePlayer(player);
            var rider=player.GetComponent<PlayerBikeRider>();var data=new SerializedObject(rider);
            data.FindProperty("walkSpeed").floatValue=1.23f;data.ApplyModifiedPropertiesWithoutUndo();
            AlienBikeSetup.ConfigurePlayer(player);data.Update();
            Assert.That(player.GetComponents<PlayerBikeRider>().Length,Is.EqualTo(1));
            Assert.That(data.FindProperty("walkSpeed").floatValue,Is.EqualTo(1.23f));
            Assert.That(data.FindProperty("riderAnimation").objectReferenceValue,Is.Not.Null);
            var controller = data.FindProperty("riderAnimation").objectReferenceValue as UnityEditor.Animations.AnimatorController;
            Assert.That(controller.layers[0].iKPass, Is.True, "Mounted pistol uses the existing rider controller's Humanoid IK pass");
            foreach(var bike in Object.FindObjectsByType<AlienBikeController>(FindObjectsInactive.Include))
            {
                AlienBikeSetup.ConfigureRideable(bike);
                var impact = bike.GetComponent<AlienBikeImpact>();
                var tuning = new SerializedObject(impact);
                tuning.FindProperty("maximumImpactDamage").floatValue = 123;
                tuning.ApplyModifiedPropertiesWithoutUndo();
                AlienBikeSetup.ConfigureRideable(bike); AlienBikeSetup.ConfigureRideable(bike);
                Assert.That(bike.GetComponents<AlienBikeImpact>().Length, Is.EqualTo(1));
                tuning.Update(); Assert.That(tuning.FindProperty("maximumImpactDamage").floatValue, Is.EqualTo(123));
            }
        }finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
    }
}
