using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Real scene/Play Mode integration checks; never uses the player's save directory.
[InitializeOnLoad]
public static class LoadoutIntegrationReview
{
    private const string Key="LoadoutIntegrationReview";
    private static int step;
    private static double next,deadline;
    private static bool failed;
    private static ActiveRunSave expected;
    private static WeaponItemData pistol;
    private static T Find<T>() where T:Object => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
    static LoadoutIntegrationReview(){EditorApplication.playModeStateChanged+=Mode;}
    public static void Run()
    {
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",Path.GetFullPath("Logs/LoadoutReviewSave-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        InGameMenuSetup.RepairGameplayScenes();
        EditorSceneManager.OpenScene("Assets/Game/Scenes/Menu.unity");
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    public static void RunAndBuild(){SessionState.SetBool(Key+"Build",true);Run();}
    private static void Mode(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){step=0;next=EditorApplication.timeSinceStartup+3;deadline=next+240;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
        if(state==PlayModeStateChange.EnteredEditMode){
            SessionState.SetBool(Key,false);
            EditorApplication.delayCall+=()=>{
                try { if(!failed&&SessionState.GetBool(Key+"Build",false))RunInterfaceValidation.BuildAndroid(); }
                catch(Exception error){failed=true;Debug.LogException(error);}
                SessionState.SetBool(Key+"Build",false);EditorApplication.Exit(failed?1:0);
            };
        }
    }
    private static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception)failed=true;}
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException("LOADOUT REVIEW: "+message);}
    private static void Click(string name)=>Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b=>b.name==name).onClick.Invoke();
    private static void Shot(string name)=>ProceduralUIReview.Capture("loadout-"+name,1920,1080);
    private static bool Ready()=>ActiveRunController.Instance!=null&&ActiveRunController.Instance.IsReady&&!Find<BeamTransportController>().IsTransporting;
    private static void Fullscreen(Transform root)
    {
        Canvas.ForceUpdateCanvases();
        var corners=new Vector3[4];var canvasCorners=new Vector3[4];
        ((RectTransform)root).GetWorldCorners(corners);
        ((RectTransform)root.GetComponentInParent<Canvas>().rootCanvas.transform).GetWorldCorners(canvasCorners);
        for(int i=0;i<4;i++)Check(Vector3.Distance(corners[i],canvasCorners[i])<.02f,"Backdrop does not match Canvas: "+root.name);
    }
    private static void Tick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+1.5;
        try {
            Check(EditorApplication.timeSinceStartup<deadline,"Timed out at step "+step);
            var inventory=Find<PlayerInventory>();var menu=Find<InGameMenuController>();
            var equipment=Find<PlayerEquipment>();var skills=Find<PlayerSkillState>();
            var book=AssetDatabase.LoadAssetAtPath<KnowledgeBookItemData>(UnarmedCombatSetup.BookPath);
            switch(step) {
                case 0:
                    pistol=RunContentCatalog.Instance.entries.Select(e=>e.asset).OfType<WeaponItemData>().First(w=>w.itemName.Contains("Pistol"));
                    var entry=AssetDatabase.FindAssets("t:MenuPreviewItem").Select(g=>AssetDatabase.LoadAssetAtPath<MenuPreviewItem>(AssetDatabase.GUIDToAssetPath(g))).First(e=>e.weaponItemData==pistol);
                    typeof(MenuController).GetField("selectedWeaponItem",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(Find<MenuController>(),entry);
                    Find<MenuController>().PlayGame();break;
                case 1:
                    if(!Ready())return;
                    Check(inventory.Items.Count==1&&inventory.Items[0]==pistol,"Fresh menu pistol is not owned exactly once");
                    Check(inventory.QuickSlotItem(0)==pistol&&equipment.EquippedWeapon==pistol,"Fresh pistol assignment/equipment");
                    Shot("01-starting-pistol");
                    Check(inventory.TryAddItem(book),"Book pickup rejected");inventory.UseItem(1);break;
                case 2:
                    Check(skills.HasSkill(book.skill)&&inventory.Items.Count==1,"Fighting consumed backpack capacity");
                    Check(Find<KnowledgeAcquiredPresenter>().CurrentSkill==book.skill,"Knowledge presentation missing");
                    Fullscreen(Find<KnowledgeAcquiredPresenter>().transform.Find("KnowledgeOverlay/WorldDimmer"));
                    Shot("02-knowledge");Find<KnowledgeAcquiredPresenter>().Close();break;
                case 3:
                    Check(inventory.AssignQuickSlot(1,PlayerInventory.CombatEntry),"Fighting assignment rejected");inventory.UseQuickSlot(1);
                    Check(equipment.EquippedWeapon==null&&Find<PlayerMeleeController>().SelectedItem==book.grantedItem,"Fighting did not holster pistol");
                    Check(Find<PlayerMeleeController>().TryAttack(),"Existing unarmed attack unavailable");break;
                case 4:
                    for(int i=0;i<3;i++){
                        inventory.UseQuickSlot(0);Check(equipment.EquippedWeapon==pistol,"Pistol lost after switching");
                        inventory.UseQuickSlot(1);Check(equipment.EquippedWeapon==null,"Fighting switch failed");
                    }
                    Check(inventory.Items.Count==1,"Switching changed ownership");
                    menu.OpenMenu();Fullscreen(menu.MenuScreen.transform);Shot("03-pause");menu.ShowInventory();break;
                case 5:
                    Fullscreen(Find<InventoryManagementView>().transform);Shot("04-owned-assigned");Click("Category7");break;
                case 6:
                    var view=Find<InventoryManagementView>();
                    Check(view.ItemFor(0,false)==book.grantedItem,"Combat category missing Fighting");
                    view.Select(0,false);Shot("05-combat-capability");Click("Assign");view.Select(3,true);
                    Check(inventory.QuickSlotItem(3)==book.grantedItem&&inventory.QuickSlotItem(1)==null,"UI assignment duplicated capability");
                    menu.ShowMenu();menu.ResumeGame();inventory.UseQuickSlot(3);
                    Check(ActiveRunController.Instance.Save(),"Save failed");Check(RunSaveService.TryReadActive(out expected),"Read failed");
                    Check(ActiveRunController.Instance.QuitToMenu(),"Quit failed");break;
                case 7:
                    if(SceneManager.GetActiveScene().name!="Menu")return;
                    PlayerLoadoutState.SelectWeapon(RunContentCatalog.Instance.entries.Select(e=>e.asset).OfType<WeaponItemData>().First(w=>w!=pistol));
                    Check(RunSaveService.Continue(),"Continue failed");break;
                case 8:
                    if(!Ready())return;
                    Check(inventory.Items.Select(i=>RunContentCatalog.Instance.Id(i)).SequenceEqual(expected.player.items),"Continue injected menu loadout");
                    Check(inventory.CaptureQuickSlots().SequenceEqual(expected.player.quickSlots),"Continue lost assignments");
                    Check(inventory.SelectedItem==book.grantedItem&&equipment.EquippedWeapon==null,"Continue lost active Fighting choice");
                    Shot("06-continue");menu.ResumeGame();break;
                case 9:
                    // Gameplay input deliberately stays blocked on the frame a modal closes.
                    inventory.UseQuickSlot(0);Check(equipment.EquippedWeapon==pistol,"Restored pistol unavailable");
                    menu.OpenMenu();menu.ShowOptions();break;
                case 10:
                    Click("ResetProgress");Shot("07-reset-confirmation");Click("CancelReset");
                    Check(skills.HasSkill(book.skill)&&RunSaveService.TryReadActive(out var kept)&&kept!=null,"Cancel reset changed progress");
                    PlayerPrefs.SetInt("LoadoutReview.Settings",73);Click("ResetProgress");Click("ConfirmReset");break;
                case 11:
                    if(SceneManager.GetActiveScene().name!="Menu")return;
                    Check(RunSaveService.TryReadActive(out var cleared)&&cleared==null,"Reset kept active run");
                    typeof(PermanentProgress).GetMethod("Reset",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
                    typeof(PlayerSkillState).GetMethod("BeginSession",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
                    Check(!PlayerSkillState.LearnedSkills.Any()&&PermanentProgress.Data.skills.Count==0,"Disk/cache reload resurrected progression");
                    Check(PlayerPrefs.GetInt("LoadoutReview.Settings")==73,"Reset changed settings");PlayerPrefs.DeleteKey("LoadoutReview.Settings");
                    PlayerLoadoutState.SelectWeapon(null);Check(RunSaveService.StartFresh("ConstructionSite",false),"None fresh start failed: "+RunSaveService.LastError);break;
                case 12:
                    if(!Ready())return;
                    Check(inventory.Items.Count==0&&equipment.EquippedWeapon==null,"Explicit None injected weapon");
                    Check(inventory.LearnedCombat==null,"Reset left Fighting available");
                    Debug.Log("LOADOUT PLAY REVIEW PASSED: menu pistol ownership, learned virtual Fighting, repeated switching/attack, UI assignment, Continue, reset cancel/confirm/disk reload/settings, explicit None.");Finish();return;
            }
            Debug.Log("LOADOUT REVIEW step "+step+" passed");step++;
        } catch(Exception error){failed=true;Debug.LogException(error);Finish();}
    }
    private static void Finish(){EditorApplication.update-=Tick;Application.logMessageReceived-=Log;EditorApplication.ExitPlaymode();}
}
