using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Menu-only authoring/Play Mode regression. Reset uses disposable saves.
[InitializeOnLoad]
public static class ResetMenuAuthoringReview
{
    private const string Key="ResetMenuAuthoringReview";
    private static int step;
    private static double next;
    private static bool failed;
    static ResetMenuAuthoringReview(){EditorApplication.playModeStateChanged+=Mode;}
    private static Transform Options => Object.FindAnyObjectByType<OptionsScreenController>(FindObjectsInactive.Include).transform;
    private static Transform Confirmation => Options.Find("ResetProgressConfirmation");
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException("RESET AUTHORING: "+message);}
    private static string Layout(Transform root) => string.Join("\n",root.GetComponentsInChildren<RectTransform>(true).Select(t=>
        t.name+" "+t.anchorMin.ToString("F4")+t.anchorMax.ToString("F4")+t.pivot.ToString("F4")+t.anchoredPosition3D.ToString("F4")+t.sizeDelta.ToString("F4")+t.localScale.ToString("F4")+t.localRotation.ToString("F4")));
    private static string Snapshot()=>Layout(Options.Find("ResetProgress"))+Layout(Confirmation)+string.Join("\n",Options.GetComponentsInChildren<NeonPanel>(true).Select(p=>p.name+" "+p.shape+" "+p.radius+" "+p.border+" "+p.glow+" "+p.color+" "+p.accent+" "+p.secondary));
    private static void CheckSnapshot()=>Check(Snapshot()==SessionState.GetString(Key+"Layout",""),"Authored layout/style changed");
    private static void Click(string name)=>Options.GetComponentsInChildren<Button>(true).Single(b=>b.name==name).onClick.Invoke();
    private static void CheckWiring()
    {
        foreach(string name in new[]{"ResetProgress","CancelReset","ConfirmReset"}){
            var buttons=Options.GetComponentsInChildren<Button>(true).Where(b=>b.name==name).ToArray();
            Check(buttons.Length==1&&buttons[0].onClick.GetPersistentEventCount()==1,"Duplicated/missing object or callback: "+name);
        }
        Check(Options.Cast<Transform>().Count(t=>t.name=="ResetProgressConfirmation")==1,"Duplicate confirmation");
    }
    public static void Run()
    {
        Directory.CreateDirectory("Logs/ResetAuthoring");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",Path.GetFullPath("Logs/ResetAuthoring/Save-"+Guid.NewGuid().ToString("N")));
        var scene=EditorSceneManager.OpenScene(MenuUISetup.MenuPath);
        MenuUISetup.ConfigureScene(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        // Reopen to prove references and callbacks survived serialization.
        scene=EditorSceneManager.OpenScene(MenuUISetup.MenuPath);CheckWiring();
        var button=(RectTransform)Options.Find("ResetProgress");button.anchoredPosition+=new Vector2(41,17);button.sizeDelta+=new Vector2(35,12);
        var confirmation=(RectTransform)Confirmation;confirmation.anchoredPosition+=new Vector2(-19,13);confirmation.sizeDelta+=new Vector2(-24,-18);
        var warning=(RectTransform)confirmation.Find("Warning");warning.anchoredPosition+=new Vector2(23,-11);warning.sizeDelta+=new Vector2(32,18);
        var cancel=(RectTransform)warning.Find("CancelReset");cancel.anchoredPosition+=new Vector2(8,4);cancel.sizeDelta+=new Vector2(-12,6);
        button.GetComponent<NeonPanel>().radius=31;button.GetComponent<NeonPanel>().glow=9;
        string expected=Snapshot();int count=Options.GetComponentsInChildren<Component>(true).Length;
        MenuUISetup.ConfigureScene(scene);MenuUISetup.ConfigureScene(scene);
        Check(Snapshot()==expected,"Setup changed manual edits");Check(count==Options.GetComponentsInChildren<Component>(true).Length,"Setup duplicated hierarchy");CheckWiring();
        SessionState.SetString(Key+"Layout",expected);
        Options.parent.Find("Screen_MainMenu").gameObject.SetActive(false);
        Options.gameObject.SetActive(true);Confirmation.gameObject.SetActive(true);
        ProceduralUIReview.Capture("reset-authored-edit",1920,1080);
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){next=EditorApplication.timeSinceStartup+3;step=0;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
        if(state==PlayModeStateChange.EnteredEditMode){
            SessionState.SetBool(Key,false);
            EditorApplication.delayCall+=()=>{EditorSceneManager.OpenScene(MenuUISetup.MenuPath);EditorApplication.Exit(failed?1:0);};
        }
    }
    private static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||message.Contains("SendMessage cannot"))failed=true;}
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
        try{
            switch(step++){
                case 0:
                    Object.FindAnyObjectByType<UIScreenRouter>().ShowOptions();CheckSnapshot();CheckWiring();
                    break;
                case 1:
                    RunSaveService.ActiveStore.Write(new ActiveRunSave{runId="authoring-fixture",sceneName="ConstructionSite"});
                    var permanent=new PermanentSave();permanent.skills.Add(new SavedSkill{id="unarmed_combat",xp=40});RunSaveService.PermanentStore.Write(permanent);
                    Click("ResetProgress");Check(Confirmation.gameObject.activeSelf,"Reset button did not open confirmation");break;
                case 2:
                    Check(Confirmation.gameObject.activeInHierarchy,"Confirmation did not stay open");
                    CheckSnapshot();ProceduralUIReview.Capture("reset-authored-play",1920,1080);
                    Click("CancelReset");Check(!Confirmation.gameObject.activeSelf,"Cancel failed");
                    Check(RunSaveService.TryReadActive(out var saved)&&saved!=null,"Cancel changed save");
                    Check(RunSaveService.PermanentStore.Read<PermanentSave>().skills.Count==1,"Cancel changed progression");
                    Click("ResetProgress");Click("ConfirmReset");break;
                case 3:
                    Check(RunSaveService.TryReadActive(out var cleared)&&cleared==null,"Confirm did not clear run");
                    Check(RunSaveService.PermanentStore.Read<PermanentSave>().skills.Count==0,"Confirm did not clear progression");
                    Debug.Log("RESET MENU AUTHORING PASSED: scene serialization, edited parent/child layouts, two repairs, style/layout Play Mode parity, open/cancel/confirmed reset callbacks.");Finish();break;
            }
        }catch(Exception error){failed=true;Debug.LogException(error);Finish();}
    }
    private static void Finish(){EditorApplication.update-=Tick;Application.logMessageReceived-=Log;EditorApplication.ExitPlaymode();}
}
