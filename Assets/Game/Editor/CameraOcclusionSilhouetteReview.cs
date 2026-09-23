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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Disposable placements only. Production sampling, camera follow and fades run normally.
[InitializeOnLoad]
public static class CameraOcclusionSilhouetteReview
{
    private const string Key = "OcclusionSilhouetteReview";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static CameraOcclusionController controller;
    private static BeamTransportController player;
    private static Camera camera;
    private static int step, targetIndex, waitFrame, candidateIndex, viewIndex;
    private static double deadline;
    private static readonly List<Transform> targets = new();
    private static readonly List<Vector3> candidates = new();
    private static Renderer[] subject;
    private static Vector3 start;
    static CameraOcclusionSilhouetteReview() => EditorApplication.playModeStateChanged += Mode;
    public static void RunMobile(){SessionState.SetBool(Key+"Mobile",true);SessionState.SetInt(Key+"Quality",QualitySettings.GetQualityLevel());Run();}
    public static void Run()
    {
        CameraOcclusionSilhouetteSetup.Ensure();
        Directory.CreateDirectory("Logs/OcclusionSilhouettes");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", Path.GetFullPath("Logs/OcclusionSilhouettes/Save-"+Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        SessionState.SetBool(Key, true); SessionState.SetBool(Key+"Failed", false);
        EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            step = targetIndex = viewIndex = 0; deadline = EditorApplication.timeSinceStartup + 360;
            if(SessionState.GetBool(Key+"Mobile",false))QualitySettings.SetQualityLevel(0,true);
            Application.runInBackground = true;
            Time.captureDeltaTime = 1f/60;
            Application.logMessageReceived += Log;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            if(SessionState.GetBool(Key+"Mobile",false)){QualitySettings.SetQualityLevel(SessionState.GetInt(Key+"Quality",1),true);SessionState.SetBool(Key+"Mobile",false);}
            SessionState.SetBool(Key, false);
            EditorApplication.delayCall += () => { EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity"); EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0); };
        }
    }
    private static void Check(bool valid, string message) { if (!valid) throw new Exception("SILHOUETTE REVIEW: "+message); }
    private static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "Timeout step "+step+" target "+targetIndex);
            if (Time.timeScale == 0)
            {
                var run = ActiveRunController.Instance;
                if (run != null) { run.SendMessage("OnApplicationPause", false); run.SendMessage("OnApplicationFocus", true); }
                var menu = Object.FindAnyObjectByType<InGameMenuController>();
                if (menu != null && menu.IsOpen) menu.ResumeGame();
                return;
            }
            if (step == 0)
            {
                player = Object.FindAnyObjectByType<BeamTransportController>();
                if (player == null || player.IsTransporting) return;
                camera = Camera.main; controller = CameraOcclusionController.Active;
                Check(controller != null, "No existing occlusion controller");
                var map = (IDictionary)typeof(CameraOcclusionController).GetField("targetByRoot", Private).GetValue(controller);
                var roots = map.Keys.Cast<Transform>().ToArray();
                File.WriteAllLines("Logs/OcclusionSilhouettes/targets.txt", roots.Select(t=>t.name+" | "+t.position));
                targets.Clear();
                foreach (string label in new[]{"Excavator", "Stairs", "Container", "Substation", "02_ConstructionBuilding"})
                {
                    var target = roots.FirstOrDefault(t => t.name.IndexOf(label,StringComparison.OrdinalIgnoreCase)>=0);
                    if (target == null) target = controller.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.IndexOf(label,StringComparison.OrdinalIgnoreCase)>=0&&t.GetComponentsInChildren<Renderer>().Length>0);
                    Check(target != null, "No scene subject "+label); targets.Add(target);
                }
                if(SessionState.GetBool(Key+"Mobile",false))targets.RemoveRange(1,targets.Count-1);
                Prepare(); step = 1; return;
            }
            if (Time.frameCount < waitFrame) return;
            if (step == 1)
            {
                bool faded = subject.Any(r => controller.IsOccluded(r));
                if (!faded) { NextCandidate(); return; }
                Capture(targetIndex+"-"+targets[targetIndex].name+"-v"+viewIndex+"-behind");
                CaptureSolid(targetIndex+"-"+targets[targetIndex].name+"-v"+viewIndex+"-solid");
                Check(CameraOcclusionSilhouetteFeature.LastMaskDraws > 0, "Faded subject did not submit renderer geometry");
                start = player.transform.position;
                player.GetComponent<StarterAssetsInputs>().MoveInput(Vector2.zero);
                player.GetComponent<StarterAssetsInputs>().MoveInput(Vector2.right);
                waitFrame = Time.frameCount+32; step = 2; return;
            }
            if (step == 2)
            {
                Capture(targetIndex+"-"+targets[targetIndex].name+"-v"+viewIndex+"-moving");
                Debug.Log($"SILHOUETTE SUBJECT {targets[targetIndex].name}: player moved {Vector3.Distance(start,player.transform.position):F3}m; mask draws={CameraOcclusionSilhouetteFeature.LastMaskDraws}; triangles={CameraOcclusionSilhouetteFeature.LastMaskTriangles}; mask={CameraOcclusionSilhouetteFeature.LastMaskSize}");
                player.GetComponent<StarterAssetsInputs>().MoveInput(Vector2.zero);
                if ((targetIndex==1||targetIndex==4) && ++viewIndex<(targetIndex==1?6:3) && candidateIndex+3<candidates.Count)
                { candidateIndex+=2;NextCandidate();step=1;return; }
                viewIndex=0;
                if (++targetIndex == targets.Count) { Debug.Log("SILHOUETTE REVIEW COMPLETE"); Finish(); return; }
                Prepare(); step = 1;
            }
        }
        catch (Exception e) { SessionState.SetBool(Key+"Failed",true); Debug.LogException(e); Finish(); }
    }
    private static void Prepare()
    {
        subject = targets[targetIndex].GetComponentsInChildren<Renderer>();
        var bounds = subject[0].bounds; foreach (var r in subject) bounds.Encapsulate(r.bounds);
        Vector3 forward = camera.transform.forward; forward.y=0; forward.Normalize();
        Vector3 right = camera.transform.right; right.y=0; right.Normalize();
        candidates.Clear();
        float reach = Mathf.Max(bounds.extents.x,bounds.extents.z);
        // Search valid ground behind/within the subject; never change the controller thresholds.
        foreach (float distance in new[]{reach+.7f,reach*.6f,0f,-reach*.6f,reach+2f})
        foreach (float side in new[]{0f,-.5f,.5f,-1.5f,1.5f})
        {
            Vector3 p=bounds.center+forward*distance+right*side;
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x,bounds.max.y+3,p.z),Vector3.down,bounds.size.y+10,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.point.y))
            {
                var location = hit.point+Vector3.up*(.025f-player.FeetOffset);
                if (player.IsLandingSafe(location)) { candidates.Add(location); break; }
            }
        }
        candidateIndex = -1; NextCandidate();
    }
    private static void NextCandidate()
    {
        Check(++candidateIndex<candidates.Count,"No valid naturally faded view for "+targets[targetIndex].name);
        var capsule=player.GetComponent<CharacterController>(); capsule.enabled=false;
        player.transform.position=candidates[candidateIndex];capsule.enabled=true;
        player.GetComponent<ThirdPersonController>().ResetMotion();
        waitFrame=Time.frameCount+50;
    }
    private static void CaptureSolid(string name)
    {
        var changed=new List<(Material material, string property, float value, Color color)>();
        foreach(var renderer in subject)
        foreach(var material in renderer.sharedMaterials)
        {
            if(changed.Any(c=>c.material==material))continue;
            if(material.HasProperty("_Fade")){changed.Add((material,"_Fade",material.GetFloat("_Fade"),default));material.SetFloat("_Fade",1);}
            else if(material.HasProperty("_BaseColor")){var color=material.GetColor("_BaseColor");changed.Add((material,"_BaseColor",0,color));material.SetColor("_BaseColor",new Color(color.r,color.g,color.b,1));}
        }
        try{Capture(name);}finally{foreach(var c in changed){if(c.property=="_Fade")c.material.SetFloat(c.property,c.value);else c.material.SetColor(c.property,c.color);}}
    }
    private static void Capture(string name)
    {
        var target=new RenderTexture(1600,900,24,RenderTextureFormat.DefaultHDR);target.Create();
        var texture=new Texture2D(1600,900,TextureFormat.RGB24,false,true);
        var previous=RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1600,900),0,0);
            var pixels=texture.GetPixels();for(int i=0;i<pixels.Length;i++)pixels[i]=pixels[i].gamma;
            texture.SetPixels(pixels);texture.Apply();
            File.WriteAllBytes("Logs/OcclusionSilhouettes/"+(SessionState.GetBool(Key+"Mobile",false)?"mobile-":"")+name+".png",texture.EncodeToPNG());
        }
        finally {RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(texture);}
    }
    private static void Log(string message,string trace,LogType type)
    { if(type==LogType.Error||type==LogType.Exception)SessionState.SetBool(Key+"Failed",true); }
    private static void Finish()
    { EditorApplication.update-=Tick;Application.logMessageReceived-=Log;EditorApplication.ExitPlaymode(); }
}
