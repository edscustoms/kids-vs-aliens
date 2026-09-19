using System;
using System.IO;
using System.Linq;
using KidsVsAliens.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Batch Play Mode smoke test. Uses a disposable save directory, never the player's save.
[InitializeOnLoad]
public static class RunInterfaceValidation
{
    private const string EnabledKey="RunInterfaceValidation.Enabled";
    private static double next,deadline;
    private static int step;
    private static ActiveRunSave expected;
    private static string removedPickup,deadEnemy,chestId;
    private static bool failed;
    static RunInterfaceValidation(){EditorApplication.playModeStateChanged+=OnMode;}
    public static void BuildAndroid()
    {
        Directory.CreateDirectory("Builds/RunInterface");
        var report=UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions {
            scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            locationPathName="Builds/RunInterface/KidsVsAliens-Development.apk",
            target=BuildTarget.Android, options=BuildOptions.Development
        });
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("Android build failed: "+report.summary.result);
        Debug.Log("RUN INTERFACE ANDROID BUILD PASSED: "+report.summary.outputPath);
    }
    public static void RepairAndRun() { RunInterfaceSetup.RepairExistingScenes(); Run(); }
    public static void Run()
    {
        string path=Path.GetFullPath("Logs/RunValidationSave-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",path);
        Directory.CreateDirectory("Logs/RunInterfaceShots");
        SessionState.SetBool(EnabledKey,true);
        EditorSceneManager.OpenScene("Assets/Game/Scenes/Menu.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void OnMode(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(EnabledKey,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){step=0;next=EditorApplication.timeSinceStartup+3;deadline=next+160;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(EnabledKey,false);EditorApplication.Exit(failed?1:0);}
    }
    private static void OnLog(string message,string trace,LogType type)
    {
        if(type==LogType.Exception||type==LogType.Error){failed=true;File.AppendAllText("Logs/RunInterfaceSmokeErrors.txt",message+"\n"+trace+"\n");}
    }
    private static T Find<T>() where T:Object=>Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
    private static void Check(bool valid,string message){if(!valid)throw new InvalidOperationException("RUN SMOKE: "+message);}
    private static void Shot(string name)
    {
        var camera=Camera.main;
        var temporary=camera==null?new GameObject("CaptureCamera",typeof(Camera)):null;
        if(camera==null){camera=temporary.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<5;}
        var target=new RenderTexture(1600,900,24);target.Create();
        var previous=camera.targetTexture;camera.targetTexture=target;
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var cameras=canvases.Select(c=>c.worldCamera).ToArray();var planes=canvases.Select(c=>c.planeDistance).ToArray();
        try {
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
            Canvas.ForceUpdateCanvases();camera.Render();
            var active=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
            File.WriteAllBytes("Logs/RunInterfaceShots/"+name+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);RenderTexture.active=active;
        }finally{
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
            camera.targetTexture=previous;target.Release();Object.DestroyImmediate(target);if(temporary!=null)Object.DestroyImmediate(temporary);
        }
    }
    private static void Click(string name)
    {
        var button=Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b=>b.name==name);button.onClick.Invoke();
    }
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup>deadline){Fail(new TimeoutException("Run interface smoke timed out at step "+step));return;}
        if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+1.2;
        try
        {
            var run=ActiveRunController.Instance;
            var player=Find<PlayerInventory>();
            var menu=Find<InGameMenuController>();
            var presenter=Find<KnowledgeAcquiredPresenter>();
            switch(step)
            {
                case 0:
                    Check(Find<MenuController>()!=null,"menu missing");Shot("01-menu");break;
                case 1:
                    Find<MenuController>().PlayGame();break;
                case 2:
                    if(run==null||!run.IsReady||Find<BeamTransportController>().IsTransporting)return;
                    Check(!RunSaveService.IsRestoringScene("ConstructionSite"),"fresh entry marked resume");Shot("02-hud");
                    menu.OpenMenu();break;
                case 3:
                    Check(menu.IsOpen&&Time.timeScale==0,"pause missing");Shot("03-pause");
                    var items=RunContentCatalog.Instance.entries.Select(e=>e.asset).OfType<ItemData>().Where(i=>i is WeaponItemData||i is GrenadeItemData).Take(4).ToArray();
                    foreach(var item in items)player.TryAddItem(item);
                    player.AssignQuickSlot(4,0);player.SwapItems(0,1);menu.ShowInventory();break;
                case 4:
                    Check(Find<InventoryManagementView>().gameObject.activeInHierarchy,"inventory hidden");Shot("04-inventory");
                    menu.ShowMenu();Check(Time.timeScale==0,"inventory back unpaused");menu.ResumeGame();
                    var skill=RunContentCatalog.Instance.entries.Select(e=>e.asset).OfType<SkillData>().First(s=>s.DisplayName.Contains("Beam"));
                    Find<PlayerSkillState>().UnlockSkill(skill);break;
                case 5:
                    Check(presenter.CurrentSkill!=null,"knowledge did not open");Shot("05-knowledge");presenter.Close();break;
                case 6:
                    menu.OpenMenu();
                    var pickup=Find<PickupItem>();removedPickup=pickup.GetComponent<RunWorldObject>().Id;
                    pickup.GetComponent<RunWorldObject>().MarkRemoved();Object.Destroy(pickup.gameObject);
                    var enemy=Find<EnemyHealth>();if(enemy!=null){deadEnemy=enemy.GetComponent<RunWorldObject>().Id;enemy.TakeDamage(100000);}
                    var chest=Find<LootChest>();if(chest!=null){chestId=chest.GetComponent<RunWorldObject>().Id;chest.RestoreRunOpen(true);}
                    Find<PlayerEquipment>().EquipWeapon(player.Items.OfType<WeaponItemData>().First(w=>w.itemName.Contains("Rifle")));
                    Find<PlayerShooter>().RestoreRunAmmo(3);
                    Find<PlayerHealth>().RestoreRunHealth(73,21);
                    Check(run.Save(),"snapshot write failed");Check(RunSaveService.TryReadActive(out expected),"saved snapshot unreadable");
                    Check(run.QuitToMenu(),"safe quit failed");break;
                case 7:
                    if(SceneManager.GetActiveScene().name!="Menu"||Find<ActiveRunMenu>()==null)return;
                    Find<MenuController>().PlayGame();break;
                case 8:
                    Check(SceneManager.GetActiveScene().name=="Menu","Play bypassed active screen");Shot("06-active-run");Click("Continue");break;
                case 9:
                    if(run==null||!run.IsReady)return;
                    Check(!Find<BeamTransportController>().IsTransporting,"resume replayed arrival");
                    Check(Vector3.Distance(player.transform.position,expected.player.position)<.02f,"player position changed");
                    Check(Find<PlayerHealth>().CurrentHealth==73,"health not restored");
                    Check(Find<PlayerShooter>().CurrentAmmo==3,"ammo not restored");
                    Check(RunContentCatalog.Instance.Id(Find<PlayerEquipment>().EquippedWeapon)==expected.player.equipped,"equipment not restored");
                    Check(player.CaptureQuickSlots().SequenceEqual(expected.player.quickSlots),"assignments not restored");
                    Check(player.Items.Select(i=>RunContentCatalog.Instance.Id(i)).SequenceEqual(expected.player.items),"items not restored");
                    var world=Object.FindObjectsByType<RunWorldObject>(FindObjectsInactive.Include);
                    foreach (var savedEntity in expected.world.Where(e => !e.removed)) {
                        var restoredEntity = world.Single(e => e.Id == savedEntity.id).Capture();
                        Check(restoredEntity.removed == savedEntity.removed, "restore changed enemy resolution: " + savedEntity.id);
                        Check(Mathf.Approximately(restoredEntity.health, savedEntity.health), "world health not restored: " + savedEntity.id);
                    }
                    Check(!world.Single(e=>e.Id==removedPickup).gameObject.activeSelf,"collected pickup respawned");
                    if(deadEnemy!=null)Check(!world.Single(e=>e.Id==deadEnemy).gameObject.activeSelf,"dead enemy respawned");
                    if(chestId!=null)Check(world.Single(e=>e.Id==chestId).GetComponent<LootChest>().IsOpen,"chest reopened");
                    Shot("07-restored");
                    run.SendMessage("OnApplicationPause",true);run.SendMessage("OnApplicationPause",false);
                    Check(Time.timeScale==0&&menu.IsOpen,"lifecycle resumed unattended");
                    menu.ShowRestart();break;
                case 10:
                    Shot("08-restart-confirmation");menu.ShowMenu();Check(menu.IsOpen,"cancel restart unpaused");
                    int learned=PermanentProgress.Data.skills.Count;SessionState.SetInt("RunValidation.Learned",learned);
                    menu.ShowRestart();menu.ConfirmRestart();break;
                case 11:
                    if(run==null||!run.IsReady||Find<BeamTransportController>().IsTransporting)return;
                    Check(PermanentProgress.Data.skills.Count==SessionState.GetInt("RunValidation.Learned",-1),"restart lost Knowledge");
                    Check(player.Items.Count==(string.IsNullOrEmpty(expected.startingWeapon)?0:1),"restart did not restore only the starting loadout");
                    if(player.Items.Count==1)Check(RunContentCatalog.Instance.Id(player.Items[0])==expected.startingWeapon,"restart inventory differs from starting loadout");
                    Check(RunContentCatalog.Instance.Id(Find<PlayerEquipment>().EquippedWeapon)==expected.startingWeapon,"restart retained acquired equipment");
                    Check(Object.FindObjectsByType<RunWorldObject>(FindObjectsInactive.Include).Single(e=>e.Id==removedPickup).gameObject.activeSelf,"restart did not reset pickups");
                    Debug.Log("RUN INTERFACE PLAY SMOKE PASSED: fresh, pause, inventory, knowledge, safe quit, Continue, world restore, lifecycle pause, confirmed restart.");
                    Finish();return;
            }
            step++;
        }
        catch(Exception error){Fail(error);}
    }
    private static void Fail(Exception error){failed=true;Debug.LogException(error);Finish();}
    private static void Finish(){EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;EditorApplication.ExitPlaymode();}
}
