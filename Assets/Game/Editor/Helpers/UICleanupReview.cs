using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Exercises real editor validation/deferred refresh and real uGUI pointer handlers.
// Used by ProceduralUIReview; all gameplay fixtures use its isolated save directory.
[InitializeOnLoad]
public static class UICleanupReview
{
    private static GameObject pistolPickup;
    private static int pistolStep, duplicateCount;
    private static KnowledgeBookItemData PistolBook => AssetDatabase.LoadAssetAtPath<KnowledgeBookItemData>("Assets/Game/Data/Items/KnowledgeBooks/PistolHandlingBook.asset");
    private static GameObject SpawnPistolPickup(PlayerInventory inventory)
    {
        var pickup=Object.Instantiate(PistolBook.worldPrefab,inventory.transform.position+Vector3.up*.5f,Quaternion.identity);
        // Kinematic body guarantees normal Unity trigger delivery for the fixture.
        pickup.AddComponent<Rigidbody>().isKinematic=true;
        RunWorldObject.TrackSpawn(pickup,PistolBook.worldPrefab);
        Physics.SyncTransforms();return pickup;
    }
    public static bool CheckFirstPistolPickup()
    {
        var inventory=Object.FindAnyObjectByType<PlayerInventory>();
        if(pistolStep==0) {
            Check(!Object.FindAnyObjectByType<PlayerSkillState>().HasSkill(PistolBook.skill),"Pistol fixture was already learned");
            while(inventory.Items.Count>23)inventory.TryConsumeGrenade(inventory.Items.OfType<GrenadeItemData>().First());
            pistolPickup=SpawnPistolPickup(inventory);pistolStep++;return false;
        }
        Check(pistolPickup==null&&inventory.Items.Contains(PistolBook),"Real first-time Pistol prefab trigger did not collect");
        inventory.UseItem(inventory.Items.ToList().IndexOf(PistolBook));
        Check(Object.FindAnyObjectByType<PlayerSkillState>().HasSkill(PistolBook.skill),"First-time Pistol book did not unlock");
        return true;
    }
    public static bool CheckDuplicatePistolPickup()
    {
        var inventory=Object.FindAnyObjectByType<PlayerInventory>();
        if(pistolStep==1) {
            // Reload permanent data/cache from this review's isolated files.
            typeof(PermanentProgress).GetMethod("Reset",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            typeof(PlayerSkillState).GetMethod("BeginSession",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            Check(Object.FindAnyObjectByType<PlayerSkillState>().HasSkill(PistolBook.skill),"Pistol learning did not persist");
            duplicateCount=inventory.Items.Count;pistolPickup=SpawnPistolPickup(inventory);pistolStep++;return false;
        }
        Check(pistolPickup!=null,"Duplicate Pistol world book was destroyed");
        Check(inventory.Items.Count==duplicateCount&&!inventory.Items.Contains(PistolBook),"Duplicate Pistol entered inventory");
        Check(!pistolPickup.GetComponent<RunWorldObject>().Capture().removed,"Rejected book persisted as removed");
        Object.Destroy(pistolPickup);Debug.Log("PISTOL PHYSICS PICKUP PASSED: first-time learn/acknowledge, permanent reload, duplicate rejected in world.");return true;
    }
    private const string Key="UICleanupReview";
    private static int step, count;
    private static double next;
    private static Action ready;
    static UICleanupReview() { Application.logMessageReceived+=Log; }
    public static void Begin(Action enterPlay)
    {
        typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries").GetMethod("Clear",BindingFlags.Static|BindingFlags.Public).Invoke(null,null);
        File.WriteAllText("Logs/ProceduralUI/console.txt", "Console cleared before opening Menu.\n");
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Failed",false);
        ready=enterPlay;step=0;next=EditorApplication.timeSinceStartup+2;
        EditorSceneManager.OpenScene(MenuUISetup.MenuPath);
        EditorApplication.update+=EditorTick;
    }
    private static void Log(string message,string trace,LogType type)
    {
        if(!SessionState.GetBool(Key,false)||type==LogType.Log)return;
        File.AppendAllText("Logs/ProceduralUI/console.txt",type+": "+message+"\n"+trace+"\n");
        if(type==LogType.Error||type==LogType.Exception||message.Contains("SendMessage cannot"))SessionState.SetBool(Key+"Failed",true);
    }
    private static UIButton[] Buttons()=>Object.FindObjectsByType<UIButton>(FindObjectsInactive.Include,FindObjectsSortMode.None);
    private static int Components()=>Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
        .Where(t=>t.gameObject.scene==UnityEngine.SceneManagement.SceneManager.GetActiveScene()).Sum(t=>t.GetComponents<Component>().Length);
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException("UI CLEANUP: "+message);}
    private static string StyleSnapshot()=>string.Join("\n",Buttons().Where(b=>b.transform.parent!=null&&b.transform.parent.name!="GameplayCamera")
        .Select(b=> {
            var p=b.transform.Find("ProceduralSurface")?.GetComponent<NeonPanel>();
            Check(p!=null,"Missing procedural editor surface on "+b.name);
            Check(b.GetComponents<NeonControlFeedback>().Length==1,"Duplicate feedback");
            Check(b.GetComponentsInChildren<NeonPanel>(true).Length==1,"Duplicate shell");
            return b.transform.parent.name+"/"+b.name+" "+p.shape+" "+p.radius+" "+p.border+" "+p.glow+" "+p.color+" "+p.accent+" "+p.secondary;
        }).OrderBy(s=>s));
    private static void EditorTick()
    {
        if(EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+1;
        try {
            switch(step++) {
                case 0:
                    foreach(var button in Buttons()) {
                        Selection.activeGameObject=button.gameObject;
                        var serialized=new SerializedObject(button);var selected=serialized.FindProperty("selected");
                        selected.boolValue=!selected.boolValue;serialized.ApplyModifiedProperties();
                        selected.boolValue=!selected.boolValue;serialized.ApplyModifiedProperties();
                    }
                    MenuUISetup.ConfigureScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());break;
                case 1:
                    // Include inactive Options controls in editor validation.
                    foreach(var button in Buttons())button.ApplyStyle();break;
                case 2:
                    count=Components();
                    MenuUISetup.ConfigureScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());break;
                case 3:
                    Check(count==Components(),"Second repair changed hierarchy/components");
                    ProceduralUIReview.Capture("00-menu-edit-720",1280,720);
                    ProceduralUIReview.Capture("00-menu-edit",1920,1080);
                    SessionState.SetString(Key+"Style",StyleSnapshot());
                    // Save the actual procedural shells so the scene is also authored accurately.
                    EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                    break;
                case 4:
                    Check(count==Components(),"Selection/resize generated extra hierarchy");
                    EditorApplication.update-=EditorTick;ready();break;
            }
        } catch(Exception error) { Debug.LogException(error);EditorApplication.update-=EditorTick;EditorApplication.Exit(1); }
    }
    public static void CheckMenuParity() => Check(SessionState.GetString(Key+"Style","")==StyleSnapshot(),"Edit/Play procedural style mismatch");
    public static void Finish(bool failed)
    {
        // Let OnEnable/validation/delayed refresh finish after exiting Play Mode.
        next=EditorApplication.timeSinceStartup+2;
        void Complete() {
            if(EditorApplication.timeSinceStartup<next)return;
            EditorApplication.update-=Complete;
            var entries=typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
            object[] counts={0,0,0};entries.GetMethod("GetCountsByType",BindingFlags.Public|BindingFlags.Static).Invoke(null,counts);
            File.AppendAllText("Logs/ProceduralUI/console.txt",$"Console after Edit/Play/Edit: errors={counts[0]}, warnings={counts[1]}, logs={counts[2]}\n");
            bool error=failed||SessionState.GetBool(Key+"Failed",false);
            SessionState.SetBool(Key,false);
            Debug.Log(error?"UI CLEANUP REVIEW FAILED":"UI CLEANUP REVIEW PASSED: cleared Console checked through open/select/resize/repair twice/Play entry/exit.");
            EditorApplication.Exit(error?1:0);
        }
        EditorApplication.update+=Complete;
    }

    public static void CheckInventory()
    {
        var view=Object.FindAnyObjectByType<InventoryManagementView>();
        var inventory=Object.FindAnyObjectByType<PlayerInventory>();
        for(int i=0;i<5;i++)inventory.AssignQuickSlot(i,-1);
        Check(inventory.Capacity==25&&inventory.QuickSlotCount==5,"Capacity changed incorrectly");
        var slots=view.GetComponentsInChildren<InventoryDragSlot>();
        var bag=slots.Where(s=>!s.IsQuick).OrderBy(s=>s.Index).ToArray();
        var quick=slots.Where(s=>s.IsQuick).OrderBy(s=>s.Index).ToArray();
        Check(bag.Length==25&&quick.Length==5,"Incorrect grid slot count");
        var scroll=view.GetComponentInChildren<ScrollRect>();Canvas.ForceUpdateCanvases();
        Check(scroll.content.rect.height>scroll.viewport.rect.height,"Backpack does not overflow viewport");
        Check(bag[0].transform.localPosition.y==bag[4].transform.localPosition.y&&bag[5].transform.localPosition.y<bag[0].transform.localPosition.y,"Not a five-column grid");
        var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pointerId=-1,scrollDelta=new Vector2(0,-2),position=new Vector2(600,300)};
        scroll.OnScroll(e);Check(scroll.content.anchoredPosition.y>0,"Mouse wheel failed");
        scroll.verticalNormalizedPosition=1;
        var touch=new ExtendedPointerEventData(EventSystem.current){pointerType=UIPointerType.Touch,button=PointerEventData.InputButton.Left,position=new Vector2(600,300)};
        bag[0].OnPointerDown(touch);bag[0].OnInitializePotentialDrag(touch);bag[0].OnBeginDrag(touch);
        touch.position+=new Vector2(0,120);bag[0].OnDrag(touch);bag[0].OnEndDrag(touch);
        Check(scroll.content.anchoredPosition.y>0,"Touch swipe failed");
        Check(GameObject.Find("DraggedItem")==null,"Swipe incorrectly started item drag");
        scroll.verticalNormalizedPosition=1;
        // Simulate the elapsed hold interval without sleeping the editor thread.
        bag[0].OnPointerDown(touch);
        typeof(InventoryDragSlot).GetField("pressedAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(bag[0],Time.unscaledTime-.4f);
        touch.pointerDrag=bag[0].gameObject;bag[0].OnBeginDrag(touch);quick[4].OnDrop(touch);bag[0].OnEndDrag(touch);
        Check(inventory.QuickSlotItem(4)==inventory.Items[0],"Held touch item assignment failed");
        e.pointerDrag=bag[0].gameObject;bag[0].OnBeginDrag(e);
        var first=view.ItemFor(0,false);int destination=view.OwnedIndexFor(1,false);bag[1].OnDrop(e);bag[0].OnEndDrag(e);
        Check(inventory.Items[destination]==first,"Item reorder failed");
        e.pointerDrag=bag[1].gameObject;bag[1].OnBeginDrag(e);quick[4].OnDrop(e);bag[1].OnEndDrag(e);
        Check(inventory.QuickSlotItem(4)==first,"Quick assignment failed");
        Check(!Enumerable.Range(0,25).Any(i=>view.ItemFor(i,false)==first),"Assigned unique item still visible in backpack");
        view.Select(4,true);view.transform.Find("InventoryPanel/Details/Unassign").GetComponent<Button>().onClick.Invoke();
        Check(inventory.QuickSlotIndex(4)==-1,"Quick clear failed");
        int displayIndex=Enumerable.Range(0,25).First(i=>view.ItemFor(i,false)==first);view.Select(displayIndex,false);
        Check(view.transform.Find("InventoryPanel/Details/Description").GetComponent<TMPro.TMP_Text>().text.Contains(first.itemName),"Details selection failed");
        scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
        ProceduralUIReview.Capture("08-inventory-last-row",1920,1080);
        scroll.verticalNormalizedPosition=1;
    }
    public static void LearnFirstBook()
    {
        var inventory=Object.FindAnyObjectByType<PlayerInventory>();
        var book=AssetDatabase.LoadAssetAtPath<KnowledgeBookItemData>("Assets/Game/Data/Items/KnowledgeBooks/BeamHoistBook.asset");
        int index=inventory.Items.ToList().IndexOf(book);Check(index>=0,"First book missing");
        inventory.UseItem(index);
        Check(Object.FindAnyObjectByType<PlayerSkillState>().HasSkill(book.skill),"First book did not teach skill");
        int countBefore=inventory.Items.Count;
        var pickup=new GameObject("Duplicate book fixture");
        var component=pickup.AddComponent<PickupItem>();
        var serialized=new SerializedObject(component);serialized.FindProperty("item").objectReferenceValue=book;serialized.ApplyModifiedProperties();
        typeof(PickupItem).GetMethod("OnTriggerEnter",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(component,new object[]{inventory.GetComponent<Collider>()});
        Check(inventory.Items.Count==countBefore,"Duplicate book entered inventory");
        Check(!(bool)typeof(PickupItem).GetField("collected",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(component),"Rejected pickup was collected");
        Check(PermanentProgress.Data.skills.Count(s=>s.id==book.skill.Id)==1,"Duplicate progression");
        Object.Destroy(pickup);
    }
}
