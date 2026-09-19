using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class WeaponAlignmentTests
{
    const string Folder = WeaponContractRefinement.Evidence;
    [UnityTest, Timeout(300000)] public IEnumerator CalibrateMounts() => Review(true);
    [UnityTest, Timeout(300000)] public IEnumerator AuthoredAlignment() => Review(false);
    [UnityTest, Timeout(300000)] public IEnumerator RifleGripAudition() => Review(false,true);
    [UnityTearDown] public IEnumerator Teardown()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
    IEnumerator Review(bool calibrate,bool audition=false)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        Time.timeScale = 1; Application.runInBackground = true;
        Directory.CreateDirectory(Folder);
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.45f,.45f,.5f);
        var light = new GameObject("Key", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional; light.intensity = 2.5f; light.transform.rotation = Quaternion.Euler(30,-40,0);
        var fill = new GameObject("Fill", typeof(Light)).GetComponent<Light>();
        fill.type = LightType.Directional; fill.intensity = 1.5f; fill.transform.rotation = Quaternion.Euler(20,140,0);
        var camera = new GameObject("Review camera",typeof(Camera)).GetComponent<Camera>();
        camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f,.14f,.18f); camera.fieldOfView = 32;
        var poses = new WeaponContractRefinement.MountPoses(); var report = new List<string>();
        for(int actor=0; actor<WeaponContractRefinement.ActorPaths.Length; actor++)
        for(int style=audition?2:1;style<=2;style++)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponContractRefinement.ActorPaths[actor]);
            Assert.That(source, Is.Not.Null);
            var visual = Object.Instantiate(source.GetComponentInChildren<CharacterVisual>(true));
            visual.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var animator = visual.Animator;
            if(actor==2) animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyCombatantSetup.ControllerPath);
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; animator.applyRootMotion=false;
            yield return EditorTestFrame.Next();
            animator.SetInteger("WeaponStyle",style);
            string state=style==1?"Base Layer.PistolLocomotion":"Base Layer.RifleLocomotion";
            animator.Play(state,0,0);
            float until=Time.time+.65f; while(Time.time<until) yield return EditorTestFrame.Next();
            var weapon=EnemyCombatantSetup.Weapon(style==1?"PlasmaPistolItem":"PlasmaRifleItem");
            var instance=WeaponInstance.SpawnAttached(weapon,visual);
            Assert.That(instance,Is.Not.Null);
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var finger=animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            var mount=visual.GetWeaponMount((WeaponAnimationStyle)style);
            if(audition)
            {
                instance.GripPoint.localPosition=new Vector3(.0009f,-.03f,-.05f);
                mount.position+=visual.transform.right*(actor==2?.035f:actor==0?.025f:.015f);
                instance.AttachTo(visual,(WeaponAnimationStyle)style);
                poses.poses.Add(new WeaponContractRefinement.MountPose{actor=actor,style=style,position=mount.localPosition,rotation=mount.localRotation});
            }
            if(calibrate)
            {
                // Evaluate the actual retargeted ready pose. Weapon +Z is the real barrel axis.
                mount.SetPositionAndRotation(Vector3.Lerp(hand.position,finger.position,.45f),Quaternion.LookRotation(visual.transform.forward,visual.transform.up));
                instance.AttachTo(visual,(WeaponAnimationStyle)style);
                poses.poses.Add(new WeaponContractRefinement.MountPose{actor=actor,style=style,position=mount.localPosition,rotation=mount.localRotation});
            }
            var head=animator.GetBoneTransform(HumanBodyBones.Head);
            var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            float angle=Vector3.Angle(instance.Muzzle.forward,visual.transform.forward);
            float gripError=Vector3.Distance(instance.GripPoint.position,mount.position);
            var support=instance.transform.Find("LeftGripPoint");
            string label=(audition?"audition-":calibrate?"calibrated-":"authored-")+actor+"-"+style;
            report.Add(label+" barrelAngle="+angle+" gripError="+gripError+" hand="+hand.position.ToString("F3")+" head="+head.position.ToString("F3")+" supportDistance="+(support==null?0:Vector3.Distance(support.position,left.position)));
            Assert.That(angle,Is.LessThan(12),label+" barrel should follow ready facing");
            Assert.That(gripError,Is.LessThan(.001f));
            Assert.That(Vector3.Distance(instance.transform.localScale,Vector3.one),Is.LessThan(.001f));
            var center=(head.position+hand.position)*.5f;
            for(int view=0;view<3;view++)
            {
                Vector3 offset=view==0?new Vector3(1.2f,.25f,1.5f):view==1?new Vector3(1.8f,.12f,0):new Vector3(-1.2f,.25f,1.5f);
                camera.transform.position=center+offset;camera.transform.LookAt(center);
                Capture(camera,Folder+"/"+label+"-"+view+".png");
            }
            if(!calibrate)
            {
                animator.SetFloat("MoveY",.55f);animator.SetFloat("MoveX",.3f);
                for(int frame=0;frame<3;frame++)
                {
                    float end=Time.time+.25f;while(Time.time<end)yield return EditorTestFrame.Next();
                    Assert.That(Vector3.Distance(instance.GripPoint.position,mount.position),Is.LessThan(.001f),"Grip remains attached through moving poses");
                    camera.transform.position=center+new Vector3(1.2f,.25f,1.5f);camera.transform.LookAt(center);
                    Capture(camera,Folder+"/"+label+"-moving-"+frame+".png");
                }
            }
            Object.Destroy(visual.gameObject);yield return EditorTestFrame.Next();
        }
        File.WriteAllLines(Folder+"/"+(calibrate?"calibration":"authored-alignment")+".txt",report);
        if(calibrate)File.WriteAllText(Folder+"/mount-calibration.json",JsonUtility.ToJson(poses,true));
        if(audition)File.WriteAllText(Folder+"/mount-refinement.json",JsonUtility.ToJson(poses,true));
    }
    static void Capture(Camera camera,string path)
    {
        var rt=new RenderTexture(1000,1000,24);rt.Create();var previous=RenderTexture.active;
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;
        var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();
        File.WriteAllBytes(path,texture.EncodeToPNG());RenderTexture.active=previous;Object.DestroyImmediate(texture);rt.Release();Object.DestroyImmediate(rt);
    }
    [Test] public void WeaponGeometryAndReferencesPreserved()
    {
        var before=JsonUtility.FromJson<WeaponContractRefinement.GeometrySet>(File.ReadAllText(Folder+"/geometry-before.json"));
        foreach(var geometry in before.weapons)
        {
            var root=PrefabUtility.LoadPrefabContents(geometry.path);
            try
            {
                WeaponContractRefinement.AssertGeometry(geometry,WeaponContractRefinement.Capture(root,geometry.path));
                Assert.That(root.transform.localScale,Is.EqualTo(Vector3.one));
                foreach(var instance in root.GetComponentsInChildren<WeaponInstance>(true))
                {
                    Assert.That(instance.transform.localScale,Is.EqualTo(Vector3.one));
                    Assert.That(instance.GripPoint,Is.Not.Null);Assert.That(instance.Muzzle,Is.Not.Null);
                    Assert.That(instance.GripPoint.localScale,Is.EqualTo(Vector3.one));Assert.That(instance.Muzzle.localScale,Is.EqualTo(Vector3.one));
                    Assert.That(Vector3.Angle(instance.Muzzle.forward,instance.transform.forward),Is.LessThan(.1f));
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(string name in new[]{"PlasmaPistolItem","PlasmaRifleItem"})
        {
            var data=EnemyCombatantSetup.Weapon(name);Assert.That(data.equippedPrefab,Is.Not.Null);Assert.That(data.worldPrefab,Is.Not.Null);
        }
    }
}
