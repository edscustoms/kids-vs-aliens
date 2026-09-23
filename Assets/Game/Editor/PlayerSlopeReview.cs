using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Real CharacterController.Move in Play Mode, using the saved ConstructionSite motor tuning.
// The scene and all ramps are disposable. Never saves a production scene or prefab.
[InitializeOnLoad]
public static class PlayerSlopeReview
{
    const string Key = "PlayerSlopeReview";
    static IEnumerator run;
    static int lastFrame;
    static double deadline;
    static string output;
    static readonly List<string> failures = new List<string>();
    static readonly List<GameObject> fixture = new List<GameObject>();
    static ThirdPersonController motor;
    static StarterAssetsInputs input;
    static CharacterController capsule;
    static Camera camera;
    static Tuning tuning;
    static bool strict;
    static PropertyInfo steepProperty = typeof(ThirdPersonController).GetProperty("OnSteepSlope");
    [Serializable] class Tuning
    {
        public float walk, sprint, jump, gravity, timeout, height, radius, skin, step, slope;
        public Vector3 center;
    }
    static PlayerSlopeReview()
    {
        EditorApplication.playModeStateChanged += Mode;
        EditorApplication.update += Tick;
    }
    public static void Baseline() => Begin(false);
    public static void Validate() => Begin(true);
    static void Begin(bool validate)
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in a separate batch Editor; this review replaces the open scene temporarily.");
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        var source = Object.FindAnyObjectByType<ThirdPersonController>();
        var cc = source.GetComponent<CharacterController>();
        tuning = new Tuning { walk=source.MoveSpeed, sprint=source.SprintSpeed, jump=source.JumpHeight,
            gravity=source.Gravity, timeout=source.JumpTimeout, height=cc.height, radius=cc.radius,
            skin=cc.skinWidth, step=cc.stepOffset, slope=cc.slopeLimit, center=cc.center };
        SessionState.SetString(Key+"Tuning",JsonUtility.ToJson(tuning));
        SessionState.SetBool(Key,true); SessionState.SetBool(Key+"Strict",validate);
        SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Mode(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false)||state!=PlayModeStateChange.EnteredPlayMode)return;
        tuning=JsonUtility.FromJson<Tuning>(SessionState.GetString(Key+"Tuning", ""));
        strict=SessionState.GetBool(Key+"Strict",false);
        output="Logs/PlayerSlopes/"+(strict?"Fixed":"Baseline");Directory.CreateDirectory(output);
        File.WriteAllText(output+"/report.txt",JsonUtility.ToJson(tuning)+"\n");
        File.WriteAllText(output+"/frames.csv","case,frame,x,y,z,grounded,steep,vertical\n");
        Time.captureDeltaTime=1f/60;Time.timeScale=1;Application.runInBackground=true;
        failures.Clear();lastFrame=-1;deadline=EditorApplication.timeSinceStartup+240;run=Cases();
    }
    static void Tick()
    {
        if(SessionState.GetBool(Key+"Done",false)&&!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key,false);
            EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);return;
        }
        if(run==null||!EditorApplication.isPlaying||Time.frameCount==lastFrame)return;
        lastFrame=Time.frameCount;
        try
        {
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Slope review timeout");
            if(run.MoveNext())return;
        }
        catch(Exception e){failures.Add(e.ToString());Debug.LogException(e);}
        run=null;Log("RESULT failures="+failures.Count+"\n"+string.Join("\n",failures));
        SessionState.SetBool(Key+"Failed",strict&&failures.Count>0);
        SessionState.SetBool(Key+"Done",true);Time.captureDeltaTime=0;EditorApplication.ExitPlaymode();
    }
    static void Log(string s){Debug.Log("SLOPE REVIEW "+s);File.AppendAllText(output+"/report.txt",s+"\n");}
    static void Check(bool valid,string s){Log((valid?"PASS ":"FAIL ")+s);if(!valid)failures.Add(s);}
    static bool Steep => steepProperty!=null&&(bool)steepProperty.GetValue(motor);
    static GameObject Add(GameObject o){fixture.Add(o);return o;}
    static GameObject Box(string name,Vector3 position,Vector3 scale,Quaternion rotation)
    {
        var o=Add(GameObject.CreatePrimitive(PrimitiveType.Cube));o.name=name;
        o.transform.SetPositionAndRotation(position,rotation);o.transform.localScale=scale;return o;
    }
    static void Setup(float angle,bool stairs=false)
    {
        foreach(var o in fixture)if(o!=null)Object.DestroyImmediate(o);fixture.Clear();
        camera=Add(new GameObject("MainCamera",typeof(Camera))).GetComponent<Camera>();camera.tag="MainCamera";
        camera.transform.SetPositionAndRotation(new Vector3(7,10,-12),Quaternion.LookRotation(new Vector3(-7,-7,15)));
        // Movement input stays aligned to the ramp while the separate capture camera looks from the side.
        camera.transform.rotation=Quaternion.Euler(25,0,0);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var light=Add(new GameObject("Light",typeof(Light))).GetComponent<Light>();light.type=LightType.Directional;
        light.transform.rotation=Quaternion.Euler(40,-35,0);
        Box("Flat",new Vector3(0,-.5f,0),new Vector3(100,1,100),Quaternion.identity);
        if(angle>0)
        {
            var q=Quaternion.Euler(-angle,0,0);var along=q*Vector3.forward;var n=q*Vector3.up;
            Box("Slope "+angle,along*150-n*.2f,new Vector3(12,.4f,300),q);
        }
        if(stairs)for(int i=0;i<8;i++)Box("Step "+i,new Vector3(0,(i+1)*.1f,i*.65f+.325f),
            new Vector3(6,(i+1)*.2f,.65f),Quaternion.identity);
        var player=Add(new GameObject("Actual player motor"));player.SetActive(false);
        capsule=player.AddComponent<CharacterController>();capsule.height=tuning.height;capsule.radius=tuning.radius;
        capsule.center=tuning.center;capsule.skinWidth=tuning.skin;capsule.stepOffset=tuning.step;capsule.slopeLimit=tuning.slope;
        input=player.AddComponent<StarterAssetsInputs>();motor=player.AddComponent<ThirdPersonController>();
        player.GetComponent<PlayerInput>().enabled=false;
        motor.MoveSpeed=tuning.walk;motor.SprintSpeed=tuning.sprint;motor.JumpHeight=tuning.jump;
        motor.Gravity=tuning.gravity;motor.JumpTimeout=tuning.timeout;motor.LockCameraPosition=true;
        motor.CinemachineCameraTarget=new GameObject("CameraTarget");motor.CinemachineCameraTarget.transform.SetParent(player.transform);
        var visual=GameObject.CreatePrimitive(PrimitiveType.Capsule);Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(player.transform);visual.transform.localPosition=tuning.center;
        visual.transform.localScale=new Vector3(tuning.radius*2,tuning.height*.5f,tuning.radius*2);
        player.SetActive(true);motor.Grounded=false;Place(new Vector3(0,2,-2));Physics.SyncTransforms();
    }
    static void Place(Vector3 p)
    {
        capsule.enabled=false;motor.transform.position=p;capsule.enabled=true;motor.ResetMotion();motor.Grounded=false;
        Physics.SyncTransforms();
    }
    static Vector3 OnRamp(float angle,float distance)
    {
        var q=Quaternion.Euler(-angle,0,0);
        return q*Vector3.forward*distance+q*Vector3.up*(tuning.radius+tuning.skin)
            -Vector3.up*(tuning.center.y-tuning.height*.5f+tuning.radius);
    }
    static void Frame(string name,int f)
    {
        var p=motor.transform.position;
        File.AppendAllText(output+"/frames.csv",$"{name},{f},{p.x:F4},{p.y:F4},{p.z:F4},{motor.Grounded},{Steep},{motor.RunVerticalVelocity:F4}\n");
    }
    static IEnumerator Cases()
    {
        foreach(float angle in new[]{30f,44f,45f,46f,60f,75f})
        {
            Setup(angle);yield return null;Place(OnRamp(angle,100));
            for(int f=0;f<45;f++){Frame("settle-"+angle,f);yield return null;}
            Vector3 start=motor.transform.position;int supported=0;float maximumY=start.y;int jumps=0;
            float previousV=motor.RunVerticalVelocity;
            for(int f=0;f<120;f++)
            {
                if(motor.Grounded)supported++;
                maximumY=Mathf.Max(maximumY,motor.transform.position.y);
                Frame("idle-"+angle,f);yield return null;
            }
            var delta=motor.transform.position-start;
            Log($"ANGLE {angle} idle delta={delta} groundedFrames={supported}");
            if(angle<=45)Check(supported>110&&Mathf.Abs(delta.y)<.1f,angle+" stable walkable support");
            else Check(supported==0&&delta.y<-.2f&&delta.z<-.1f,angle+" ungrounded downhill slide");
            Place(OnRamp(angle,100));for(int f=0;f<30;f++)yield return null;
            start=motor.transform.position;maximumY=start.y;previousV=motor.RunVerticalVelocity;
            for(int f=0;f<150;f++)
            {
                input.MoveInput(Vector2.up);input.SprintInput(angle>45);input.JumpInput(angle>45&&f%24<3);
                if(motor.RunVerticalVelocity>2&&previousV<=0)jumps++;
                previousV=motor.RunVerticalVelocity;maximumY=Mathf.Max(maximumY,motor.transform.position.y);
                Frame("uphill-"+angle,f);yield return null;
            }
            Log($"ANGLE {angle} uphill delta={motor.transform.position-start} maxRise={maximumY-start.y:F3} launches={jumps}");
            if(angle<=45)Check(motor.transform.position.y>start.y+.5f,angle+" walks uphill");
            else Check(jumps==0&&maximumY<=start.y+.12f,angle+" uphill jump spam cannot climb");
            Capture("slope-"+angle);
            if(angle<=45)
            {
                input.MoveInput(Vector2.zero);input.JumpInput(false);
                for(int f=0;f<60;f++)yield return null;
                float baseY=motor.transform.position.y,peakY=baseY;
                input.JumpInput(true);yield return null;input.JumpInput(false);
                for(int f=0;f<120;f++){peakY=Mathf.Max(peakY,motor.transform.position.y);yield return null;}
                Check(peakY>baseY+1f&&motor.Grounded&&!Steep,angle+" normal jump and landing");
            }
        }
        foreach(float angle in new[]{60f,75f})
        {
            Setup(angle);yield return null;Place(new Vector3(0,.03f,-1));
            for(int f=0;f<60;f++)yield return null;
            float peak=0;int steepLaunches=0;float previousV=0;
            for(int f=0;f<480;f++)
            {
                input.MoveInput(Vector2.up);input.SprintInput(true);input.JumpInput(f%24<3);
                if(motor.RunVerticalVelocity>2&&previousV<=0&&Steep)steepLaunches++;
                previousV=motor.RunVerticalVelocity;peak=Mathf.Max(peak,motor.transform.position.y);
                Frame("repeated-landing-"+angle,f);yield return null;
            }
            Check(steepLaunches==0&&peak<tuning.jump+.5f,$"Repeated jumps onto {angle}: peak={peak:F3}, steep launches={steepLaunches}");
            input.MoveInput(Vector2.zero);input.JumpInput(false);
            for(int f=0;f<240;f++)yield return null;
            Check(motor.Grounded&&!Steep,angle+" recovers on valid ground");
            input.JumpInput(true);yield return null;input.JumpInput(false);
            Check(motor.RunVerticalVelocity>2f,angle+" can jump again after reaching valid ground");
        }
        // Exercise a triangle-mesh slope at a different frame rate as well as box ramps.
        Time.captureDeltaTime=1f/30;
        Setup(60);yield return null;
        var ramp=fixture.Find(o=>o.name=="Slope 60");
        Object.DestroyImmediate(ramp.GetComponent<BoxCollider>());
        ramp.AddComponent<MeshCollider>().sharedMesh=ramp.GetComponent<MeshFilter>().sharedMesh;
        Place(OnRamp(60,100));
        for(int f=0;f<15;f++)yield return null;
        var meshStart=motor.transform.position;bool meshInvalid=false;
        for(int f=0;f<60;f++)
        {
            input.MoveInput(Vector2.up);input.JumpInput(f%12<2);input.SprintInput(true);
            meshInvalid |= motor.Grounded||motor.RunVerticalVelocity>0||!Steep;
            Frame("mesh-60-30fps",f);yield return null;
        }
        Check(!meshInvalid&&motor.transform.position.y<meshStart.y-1f,"60 degree MeshCollider: slide/no uphill jumps at 30 fps");
        ramp.GetComponent<MeshCollider>().enabled=false;
        for(int f=0;f<5;f++){input.JumpInput(f%2==0);yield return null;}
        Check(Steep&&!motor.Grounded&&motor.RunVerticalVelocity<0,"Steep jump lock persists through contact loss");
        Time.captureDeltaTime=1f/60;
        Setup(0);yield return null;Place(new Vector3(0,.1f,-10));for(int f=0;f<60;f++)yield return null;
        foreach(bool sprint in new[]{false,true})
        {
            input.MoveInput(Vector2.up);input.SprintInput(sprint);for(int f=0;f<60;f++)yield return null;
            float start=motor.transform.position.z;for(int f=0;f<60;f++)yield return null;
            float speed=motor.transform.position.z-start;float expected=sprint?tuning.sprint:tuning.walk;
            Check(Mathf.Abs(speed-expected)<.1f,$"Flat {(sprint?"sprint":"walk")} speed {speed:F3}, expected {expected}");
        }
        input.MoveInput(Vector2.zero);for(int f=0;f<60;f++)yield return null;
        float groundY=motor.transform.position.y,apex=groundY;input.JumpInput(true);yield return null;input.JumpInput(false);
        for(int f=0;f<120;f++){apex=Mathf.Max(apex,motor.transform.position.y);yield return null;}
        Check(Mathf.Abs(apex-groundY-tuning.jump)<.12f&&motor.Grounded,$"Normal jump apex={apex-groundY:F3}, authored={tuning.jump}");
        Setup(0,true);yield return null;Place(new Vector3(0,.1f,-1));for(int f=0;f<45;f++)yield return null;
        input.MoveInput(Vector2.up);for(int f=0;f<120;f++){Frame("stairs",f);yield return null;}
        Check(motor.transform.position.y>1.4f&&motor.Grounded&&!Steep,"Authored stepOffset still climbs 0.2m stairs");
        Capture("stairs");
    }
    static void Capture(string name)
    {
        var p=motor.transform.position;var oldPos=camera.transform.position;var oldRot=camera.transform.rotation;
        camera.transform.position=p+new Vector3(6,3,-7);camera.transform.LookAt(p+Vector3.up);
        var rt=new RenderTexture(960,540,24);rt.Create();
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(960,540,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();File.WriteAllBytes(output+"/"+name+".png",tex.EncodeToPNG());
        RenderTexture.active=previous;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);
        camera.transform.SetPositionAndRotation(oldPos,oldRot);
    }
}



