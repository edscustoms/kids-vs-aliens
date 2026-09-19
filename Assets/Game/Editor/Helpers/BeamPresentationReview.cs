using System;
using System.IO;
using System.Linq;
using System.Reflection;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

// Explicit Unity-only review. All save data and scene-placement edits are disposable.
[InitializeOnLoad]
public static class BeamPresentationReview
{
    private const string Key="BeamPresentationReview";
    private const string ScenePath="Assets/Game/Scenes/ConstructionSite.unity";
    private static int step, repeats, fadeFrame;
    private static Vector3 landedAt, fadingAt;
    private static bool Timing => SessionState.GetBool(Key+"Timing", false);
    private static double deadline;
    private static bool failed, captured, ended;
    private static Vector3 endpoint, hoistStart, spiralStart;
    private static Quaternion facing;
    private static BeamTransportController transport;
    private static BeamTransportVFX arrival, hoist;
    private static BeamHoistPath path;
    private static ActiveRunSave saved;
    private static T Find<T>()where T:Object=>Object.FindAnyObjectByType<T>();
    private static void Check(bool value,string text){if(!value)throw new Exception("BEAM REVIEW: "+text);}
    static BeamPresentationReview(){EditorApplication.playModeStateChanged+=Mode;}
    public static void Run()=>Begin(false);
    public static void RunTiming(){SessionState.SetBool(Key+"Timing",true);Begin(false);}
    public static void RunArrivalFade(){SessionState.SetBool(Key+"ArrivalFadeOnly",true);RunTiming();}
    public static void RunSpirals()=>Begin(true);
    private static void Begin(bool spiralsOnly)
    {
        Directory.CreateDirectory("Logs/BeamRefinement");
        if(spiralsOnly)Directory.CreateDirectory("Logs/BeamSpirals");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",Path.GetFullPath("Logs/BeamRefinement/Save-"+Guid.NewGuid().ToString("N")));
        SessionState.SetBool(Key+"SpiralsOnly",spiralsOnly);
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Failed",false);SessionState.SetInt(Key+"Pass",spiralsOnly||Timing?1:0);Prepare();
    }
    private static void Prepare()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sequence=Find<PlayerBeamInSequence>();var data=new SerializedObject(sequence);
        var marker=sequence.ArrivalTransform;
        var controller=Find<BeamTransportController>();Vector3 original=marker.position;
        int pass=SessionState.GetInt(Key+"Pass",0);
        bool valid=Timing;
        foreach(var offset in new[]{new Vector3(3,0,0),new Vector3(-3,0,0),new Vector3(0,0,3),new Vector3(0,0,-3),new Vector3(5,0,2)}) {
            if(Timing)break;
            var p=original+offset*(pass+1);
            if(!controller.IsLandingSafe(p)||!controller.IsSegmentClear(p+Vector3.up*data.FindProperty("startHeight").floatValue,p))continue;
            marker.SetPositionAndRotation(p,Quaternion.Euler(0,pass==0?37:143,0));valid=true;break;
        }
        Check(valid,"No clear authoring fixture beside current arrival");
        // Save expected pose across domain reload, not to the actual scene asset.
        SessionState.SetString(Key+"Pose",JsonUtility.ToJson(new PoseData{position=marker.position,rotation=marker.rotation}));
        var player=Find<PlayerCharacter>();int count=sequence.GetComponentsInChildren<Transform>(true).Length;
        BeamTransportSetup.ConfigureScene(player);BeamTransportSetup.ConfigureScene(player);
        Check(count==sequence.GetComponentsInChildren<Transform>(true).Length,"Repair duplicates");
        Check(marker.position==JsonUtility.FromJson<PoseData>(SessionState.GetString(Key+"Pose","")).position,"Repair moved marker");
        EditorApplication.EnterPlaymode();
    }
    [Serializable]private class PoseData{public Vector3 position;public Quaternion rotation;}
    private static void Mode(PlayModeStateChange mode)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(mode==PlayModeStateChange.EnteredPlayMode){
            Application.runInBackground=true;
            if(Timing)Time.captureDeltaTime=1f/60f;
            EditorApplication.isPaused=false;
            step=0;repeats=0;captured=false;ended=false;deadline=EditorApplication.timeSinceStartup+100;
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;
            try {
                var pose=JsonUtility.FromJson<PoseData>(SessionState.GetString(Key+"Pose",""));endpoint=pose.position;facing=pose.rotation;
                transport=Find<BeamTransportController>();
                var data=new SerializedObject(Find<PlayerBeamInSequence>());
                arrival=(BeamTransportVFX)data.FindProperty("transportVfx").objectReferenceValue;
                Check(transport.IsTransporting,"Fresh entry did not start arrival");
                Check(Vector3.Distance(arrival.transform.position,endpoint)<.001f,"VFX did not use authored endpoint");
                Check(arrival.Direction==BeamTransportDirection.Down,"Arrival direction");
                transport.TransportEnded+=ArrivalEnded;
            }catch(Exception error){Fail(error);}
        }
        if(mode==PlayModeStateChange.EnteredEditMode){
            EditorApplication.delayCall+=()=>{
                failed=SessionState.GetBool(Key+"Failed",false);
                if(!failed&&SessionState.GetInt(Key+"Pass",0)==0){SessionState.SetInt(Key+"Pass",1);Prepare();}
                else {SessionState.SetBool(Key,false);SessionState.SetBool(Key+"Timing",false);SessionState.SetBool(Key+"ArrivalFadeOnly",false);EditorSceneManager.OpenScene(ScenePath);EditorApplication.Exit(failed?1:0);}
            };
        }
    }
    private static void ArrivalEnded()
    {
        try {
            Check(Vector3.Distance(transport.transform.position,endpoint)<.001f,"Arrival endpoint mismatch");
            Check(Quaternion.Angle(transport.transform.rotation,facing)<.01f,"Arrival facing mismatch");
            Check(arrival.Visibility==1f,"Arrival must release control before its shared landing fade");
            Check(!transport.GetComponent<StarterAssetsInputs>().GameplayInputBlocked,"Arrival did not release control");
            ended=true;Debug.Log("BEAM PASS: fresh authored position/facing "+endpoint+" / "+facing.eulerAngles);
        }catch(Exception error){Fail(error);}
    }
    private static void Tick()
    {
        try {
            Check(EditorApplication.timeSinceStartup<deadline,$"Timed out step {step}; timeScale={Time.timeScale}, editorPaused={EditorApplication.isPaused}, frame={Time.frameCount}, progress={transport?.PresentationProgress}");
            if(step==0){
                if(Time.timeScale==0){
                    // A hidden batch editor has no OS focus. Exercise the existing foreground/
                    // Resume path in the fixture; never bypass or change production leases.
                    var run=ActiveRunController.Instance;
                    if(run!=null){run.SendMessage("OnApplicationPause",false);run.SendMessage("OnApplicationFocus",true);}
                    var menu=Find<InGameMenuController>();if(menu!=null&&menu.IsOpen)menu.ResumeGame();
                    Check(Time.timeScale>0,"Fresh test remains paused after foreground/Resume");
                }
                if(!captured&&transport.PresentationProgress>.25f&&transport.PresentationProgress<.9f){Shot("arrival-"+SessionState.GetInt(Key+"Pass",0));captured=true;}
                if(!ended||transport.IsTransporting)return;
                if(arrival.Visibility>0){
                    if(Timing&&fadeFrame++%4==0){Capture(Camera.main,"arrival-fade-"+fadeFrame.ToString("D2"));Debug.Log("ARRIVAL FADE "+arrival.Visibility);}
                    return;
                }
                Check(arrival.GetComponentInChildren<ParticleSystem>(true).particleCount==0&&!arrival.transform.Find("BeamInVFX").gameObject.activeSelf,"Arrival fade did not clean up");
                if(SessionState.GetBool(Key+"ArrivalFadeOnly",false)){
                    Capture(Camera.main,"arrival-fade-cleared");
                    Debug.Log("ARRIVAL FADE PASS: original descent/hold and authored pose, control released with shared smooth fade, clean particle/visual reset.");
                    Finish();return;
                }
                transport.TransportEnded-=ArrivalEnded;Check(captured,"Arrival capture missed");
                if(SessionState.GetInt(Key+"Pass",0)==0){Finish();return;}
                PrepareHoist();step=1;return;
            }
            if(step==1){
                var demonstration=Find<KnowledgeAcquiredPresenter>();
                if(demonstration!=null&&demonstration.CurrentSkill!=null){demonstration.Close();return;}
                if(Time.timeScale==0){
                    var run=ActiveRunController.Instance;
                    if(run!=null){run.SendMessage("OnApplicationPause",false);run.SendMessage("OnApplicationFocus",true);}
                    var menu=Find<InGameMenuController>();if(menu!=null&&menu.IsOpen)menu.ResumeGame();
                }
                if(!transport.GetComponent<StarterAssetsInputs>().CanProcessGameplayInput)return;
                var input=transport.GetComponent<StarterAssetsInputs>();
                if(Timing){var motor=transport.GetComponent<ThirdPersonController>();motor.AudioFootsteps?.Play();motor.AudioFoley?.Play();}
                input.JumpInput(false);input.JumpInput(true);
                Check(transport.IsTransporting,"Contextual Jump did not start valid Hoist");
                path=(BeamHoistPath)typeof(BeamTransportController).GetField("hoistPath",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(transport);
                hoist=Object.FindObjectsByType<BeamTransportVFX>(FindObjectsInactive.Include).Single(v=>v!=arrival);
                Check(hoist.Direction==BeamTransportDirection.Up,"Hoist direction");
                var canonical=AssetDatabase.LoadAssetAtPath<BeamTransportVFX>(BeamTransportSetup.VfxPath);
                Check(new SerializedObject(transport).FindProperty("vfxPrefab").objectReferenceValue==canonical,"Hoist uses different prefab");
                var sharedRibbon=canonical.GetComponentInChildren<LineRenderer>(true).sharedMaterial;
                Check(hoist.GetComponentInChildren<LineRenderer>(true).sharedMaterial==sharedRibbon
                    &&arrival.GetComponentInChildren<LineRenderer>(true).sharedMaterial==sharedRibbon,"Arrival/Hoist do not share presentation");
                spiralStart=hoist.GetComponentInChildren<LineRenderer>().GetPosition(0);
                captured=false;fadeFrame=0;
                if(Timing){
                    Check(hoist.Visibility==0,"Hoist did not begin invisible");
                    Check(!input.CanProcessGameplayInput&&!transport.GetComponent<CharacterController>().enabled,"Hoist did not lock immediately");
                    Capture(Camera.main,"timing-"+repeats+"-start");
                }
                step=2;return;
            }
            if(step==2){
                if(transport.IsTransporting){
                    if(Timing&&transport.PresentationProgress==0){
                        Check(Vector3.Distance(transport.transform.position,path.start)<.001f,"Amy moved during materialization");
                        var locomotion=transport.GetComponent<ThirdPersonController>();
                        Check(!locomotion.enabled,"Locomotion remains enabled");
                        Check((locomotion.AudioFootsteps==null||!locomotion.AudioFootsteps.isPlaying)&&(locomotion.AudioFoley==null||!locomotion.AudioFoley.isPlaying),"Running audio during fade-in");
                        transport.GetComponent<StarterAssetsInputs>().MoveInput(Vector2.one);
                        if(fadeFrame++%3==0){Capture(Camera.main,"timing-"+repeats+"-in-"+fadeFrame.ToString("D2"));Debug.Log("BEAM FADE IN "+hoist.Visibility);}
                    }
                    if(Timing&&transport.PresentationProgress>0)Check(hoist.Visibility==1,"Travel before full visibility");
                    Check(Vector3.Distance(transport.transform.position,path.Evaluate(transport.PresentationProgress))<.002f,"Hoist departed its Bezier");
                    Check(Mathf.Abs(hoist.transform.position.x-transport.transform.position.x)<.001f&&Mathf.Abs(hoist.transform.position.z-transport.transform.position.z)<.001f,"Beam follow changed");
                    Check(Mathf.Abs(hoist.transform.position.y-hoistStart.y)<.001f,"Beam root left ground");
                    if(!captured&&transport.PresentationProgress>.35f){
                        Check(Vector3.Distance(spiralStart,hoist.GetComponentInChildren<LineRenderer>().GetPosition(0))>.01f,"Spiral is not rotating");
                        Shot("hoist-"+repeats);captured=true;
                    }
                    return;
                }
                if(Timing){
                    Check(hoist.Visibility>0,"Beam popped off at landing");
                    Check(!transport.GetComponent<StarterAssetsInputs>().GameplayInputBlocked&&transport.GetComponent<CharacterController>().enabled,"Control not returned at landing");
                    landedAt=transport.transform.position;fadingAt=hoist.transform.position;fadeFrame=0;
                    // Release the held-during-lock direction, then issue a fresh walk input.
                    transport.GetComponent<StarterAssetsInputs>().MoveInput(Vector2.zero);
                    transport.GetComponent<StarterAssetsInputs>().MoveInput(Vector2.right);
                    Capture(Camera.main,"timing-"+repeats+"-landing");step=6;return;
                }
                if(hoist.Visibility>0)return; // Normal landing now releases before the visual tail ends.
                Check(hoist.GetComponentInChildren<ParticleSystem>(true).particleCount==0,"Hoist particles lingered");
                Check(!hoist.transform.Find("BeamInVFX").gameObject.activeSelf,"Hoist visuals lingered");
                Check(captured,"Hoist capture missed");
                if(++repeats<3){PrepareHoist();step=1;return;}
                Debug.Log("BEAM PASS: three contextual Hoists; same canonical VFX, exact Bezier/follow, clean stop/reuse.");
                if(SessionState.GetBool(Key+"SpiralsOnly",false)){Finish();return;}
                step=3;return;
            }
            if(step==6){
                transport.GetComponent<StarterAssetsInputs>().MoveInput(Vector2.right);
                Check(hoist.transform.position==fadingAt,"Fading beam followed walking Amy");
                if(hoist.Visibility>0){
                    if(fadeFrame++%3==0){Capture(Camera.main,"timing-"+repeats+"-out-"+fadeFrame.ToString("D2"));Debug.Log("BEAM FADE OUT "+hoist.Visibility);}
                    return;
                }
                Check(Vector3.Distance(transport.transform.position,landedAt)>.05f,"Amy could not walk away during fade");
                Check(hoist.GetComponentInChildren<ParticleSystem>(true).particleCount==0&&!hoist.transform.Find("BeamInVFX").gameObject.activeSelf,"Fade did not clear and disable");
                transport.GetComponent<StarterAssetsInputs>().MoveInput(Vector2.zero);
                Capture(Camera.main,"timing-"+repeats+"-cleared");
                Debug.Log("BEAM TIMING PASS: locked silent prelude, full visibility before unchanged Bezier, immediate landing control, stationary independent fade-out and clean reset.");
                if(++repeats<2){PrepareHoist();step=1;return;}
                PrepareHoist();
                transport.GetComponent<StarterAssetsInputs>().JumpInput(false);transport.GetComponent<StarterAssetsInputs>().JumpInput(true);
                Check(transport.IsTransporting,"Cancel fixture failed");transport.Advance(.1f);transport.CancelTransport();
                Check(!transport.IsTransporting&&hoist.Visibility==0&&hoist.GetComponentInChildren<ParticleSystem>(true).particleCount==0,"Fade-in cancellation leaked");
                Finish();return;
            }
            if(step==3){
                if(ActiveRunController.Instance==null||!ActiveRunController.Instance.IsReady)return;
                Check(ActiveRunController.Instance.Save(),"Save fixture failed");Check(RunSaveService.TryReadActive(out saved)&&saved!=null,"Save fixture missing");
                Check(ActiveRunController.Instance.QuitToMenu(),"Quit fixture failed");step=4;return;
            }
            if(step==4){if(SceneManager.GetActiveScene().name!="Menu")return;Check(RunSaveService.Continue(),"Continue failed");step=5;return;}
            if(step==5){
                if(ActiveRunController.Instance==null||!ActiveRunController.Instance.IsReady)return;
                transport=Find<BeamTransportController>();Check(!transport.IsTransporting,"Continue replayed arrival");
                Check(Vector3.Distance(transport.transform.position,saved.player.position)<.01f,"Continue changed saved position");
                Check(Quaternion.Angle(transport.transform.rotation,saved.player.rotation)<.01f,"Continue changed saved facing");
                Check(Object.FindObjectsByType<BeamTransportVFX>(FindObjectsInactive.Include).All(v=>!v.transform.Find("BeamInVFX").gameObject.activeSelf),"Continue showed beam");
                Debug.Log("BEAM PRESENTATION REVIEW PASSED: two authored arrivals, repeated Hoists, real Continue restores saved pose without arrival.");
                Finish();
            }
        }catch(Exception error){Fail(error);}
    }
    private static void PrepareHoist()
    {
        var capsule=transport.GetComponent<CharacterController>();var ability=transport.GetComponent<BeamHoistAbility>();
        transport.GetComponent<PlayerSkillState>().UnlockSkill(ability.RequiredSkill);
        var tutorial=Find<KnowledgeAcquiredPresenter>();
        if(tutorial!=null&&tutorial.CurrentSkill!=null)tutorial.Close();
        foreach(var surface in Object.FindObjectsByType<BeamHoistSurface>(FindObjectsInactive.Exclude))
            for(int i=0;i<surface.CandidateCount;i++){
                Vector3 sample=surface.transform.TransformPoint(surface.GetBakedApproach(i).region.center);
                if(!Physics.Raycast(sample+Vector3.up,Vector3.down,out var hit,5,~0,QueryTriggerInteraction.Ignore))continue;
                Vector3 start=hit.point+Vector3.up*(.02f-transport.FeetOffset);
                capsule.enabled=false;transport.transform.position=start;capsule.enabled=true;
                transport.GetComponent<ThirdPersonController>().ResetMotion();
                if(!surface.TryGetCandidate(start,i,transport.FeetOffset,out var end,out float release))continue;
                var candidate=BeamHoistPath.Create(start,end,release,1.5f,.9f);
                if(!transport.IsLandingSafe(start)||!ability.IsWithinLimits(candidate)||!transport.CanHoist(candidate))continue;
                hoistStart=start;return;
            }
        throw new Exception("No valid authored Hoist fixture");
    }
    private static void Shot(string name)
    {
        var camera=Camera.main;
        Capture(camera,name);
        if(Timing&&step==2&&repeats==0){
            var swaps=new System.Collections.Generic.List<(Renderer renderer,Material original,Material temporary)>();
            try {
                foreach(var renderer in hoist.GetComponentsInChildren<Renderer>()){
                    var original=renderer.sharedMaterial;
                    if(original.shader.name!="KVA/Beam Lit Fade")continue;
                    var temporary=new Material(original){shader=Shader.Find("Universal Render Pipeline/Lit")};
                    renderer.sharedMaterial=temporary;swaps.Add((renderer,original,temporary));
                }
                Capture(camera,"timing-full-original-lit");
            }finally{foreach(var swap in swaps){swap.renderer.sharedMaterial=swap.original;Object.DestroyImmediate(swap.temporary);}}
            Capture(camera,"timing-full-fade-lit");
        }
        if(!SessionState.GetBool(Key+"SpiralsOnly",false))return;
        var beam=step==0?arrival:hoist;
        Check(beam.GetComponentsInChildren<LineRenderer>().Length==new SerializedObject(beam.GetComponentInChildren<BeamEnergyField>()).FindProperty("spiralCount").intValue,"Expected authored spiral count");
        var side=new GameObject("Disposable beam side review",typeof(Camera));
        try {
            var view=side.GetComponent<Camera>();view.CopyFrom(camera);view.enabled=false;
            var source=camera.GetUniversalAdditionalCameraData();var settings=view.GetUniversalAdditionalCameraData();
            settings.renderPostProcessing=source.renderPostProcessing;settings.volumeLayerMask=source.volumeLayerMask;
            Vector3 center=beam.transform.position+Vector3.up*7.3f;
            view.transform.position=center+Vector3.right*19;view.transform.LookAt(center);view.fieldOfView=52;
            Capture(view,name+"-side");
        }finally{Object.DestroyImmediate(side);}
    }
    private static void Capture(Camera camera,string name)
    {
        Debug.Log($"BEAM CAPTURE: HDR={camera.allowHDR}, post={camera.GetUniversalAdditionalCameraData().renderPostProcessing}");
        var target=new RenderTexture(1600,900,24,RenderTextureFormat.DefaultHDR);target.Create();
        var texture=new Texture2D(1600,900,TextureFormat.RGB24,false,true);
        var active=RenderTexture.active;
        try {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1600,900),0,0);
            // HDR capture preserves the camera's bloom; PNG needs sRGB encoding.
            var pixels=texture.GetPixels();for(int i=0;i<pixels.Length;i++)pixels[i]=pixels[i].gamma;
            texture.SetPixels(pixels);texture.Apply();
            string folder=SessionState.GetBool(Key+"SpiralsOnly",false)?"Logs/BeamSpirals/":"Logs/BeamRefinement/";
            File.WriteAllBytes(folder+name+".png",texture.EncodeToPNG());
        } finally {RenderTexture.active=active;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(texture);}
    }
    private static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception){failed=true;SessionState.SetBool(Key+"Failed",true);}}
    private static void Fail(Exception error){failed=true;SessionState.SetBool(Key+"Failed",true);Debug.LogException(error);Finish();}
    private static void Finish(){if(transport!=null)transport.TransportEnded-=ArrivalEnded;EditorApplication.update-=Tick;Application.logMessageReceived-=Log;EditorApplication.ExitPlaymode();}
}
