using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using static InterfaceFactory;

// Visual-only harness. Fixture state uses a disposable save directory; no restore,
// lifecycle or device tests. Production callbacks and scene flow are not rewritten.
[InitializeOnLoad]
public static class ProceduralUIReview
{
    private const string Key="ProceduralUIReview.Running";
    private static GameObject gallery;
    private static int step;
    private static double next,deadline;
    private static bool failed;
    static ProceduralUIReview() { EditorApplication.playModeStateChanged+=OnMode; }
    public static void Run()
    {
        ProceduralUISetup.EnsureAssets();
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",Path.GetFullPath("Logs/UIVisualFixture-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory("Logs/ProceduralUI");
        File.WriteAllText("Logs/ProceduralUI/errors.txt",string.Empty);
        SessionState.SetBool(Key,true);
        UICleanupReview.Begin(EditorApplication.EnterPlaymode);
    }
    private static void OnMode(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode) { step=0;next=EditorApplication.timeSinceStartup+3;deadline=next+150;failed=false;EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog; }
        if(state==PlayModeStateChange.EnteredEditMode) { SessionState.SetBool(Key,false);UICleanupReview.Finish(failed); }
    }
    private static void OnLog(string message,string trace,LogType type)
    {
        if(type==LogType.Exception||type==LogType.Error) { failed=true;File.AppendAllText("Logs/ProceduralUI/errors.txt",message+"\n"+trace+"\n"); }
    }
    private static T Find<T>() where T:Object => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
    private static void Check(bool condition,string message) { if(!condition)throw new InvalidOperationException("UI VISUAL CHECK: "+message); }
    private static void Click(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b=>b.name==name).onClick.Invoke();
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup>deadline) { Fail(new TimeoutException("Visual check timed out at step "+step));return; }
        if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+1.5;
        try {
            var menu=Find<InGameMenuController>();
            switch(step) {
                case 0: gallery=BuildGallery();break;
                case 1: Capture("01-primitives-1080",1920,1080);Capture("02-primitives-720",1280,720);Capture("03-primitives-tablet",2048,1536);Object.Destroy(gallery);break;
                case 2: UICleanupReview.CheckMenuParity();Capture("04-menu",1920,1080);
                    var flow=Find<ActiveRunMenu>();flow.GetComponent<UIScreenRouter>().HideScreens();
                    var active=flow.transform.Find("Screen_ActiveRun");active.gameObject.SetActive(true);
                    active.Find("RunPanel/Metadata").GetComponent<TMP_Text>().text="Construction Site   /   00:12:04";
                    break;
                case 3: Capture("05-active-run",1920,1080);SceneManager.LoadScene("ConstructionSite");break;
                case 4:
                    if(ActiveRunController.Instance==null||!ActiveRunController.Instance.IsReady||Find<BeamTransportController>().IsTransporting)return;
                    var inventory=Find<PlayerInventory>();
                    foreach(var entry in InterfaceIconCatalog.Current.entries.Where(e=>e.item!=null))inventory.TryAddItem(entry.item);
                    inventory.TryAddItem(AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Game/Data/Items/KnowledgeBooks/BeamHoistBook.asset"));
                    var grenade=AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Game/Data/Items/ElectricGrenade.asset");
                    if(grenade==null)grenade=inventory.Items.First(i=>i is GrenadeItemData);
                    while(inventory.Items.Count<25)Check(inventory.TryAddItem(grenade),"Could not fill backpack");
                    break;
                case 5:
                    var quick=Find<InventoryUI>();
                    var labels=new SerializedObject(quick).FindProperty("slotTexts");
                    for(int i=0;i<labels.arraySize;i++)Check(!((TMP_Text)labels.GetArrayElementAtIndex(i).objectReferenceValue).enabled,"Quick-bar item name still visible");
                    Check(quick.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="Quantity"&&t.enabled&&t.text.Length>0),"Quick-bar quantities missing");
                    foreach(var icon in quick.GetComponentsInChildren<Image>().Where(i=>i.name=="ItemIcon"))Check(icon.preserveAspect&&!icon.raycastTarget,"HUD icon aspect/hit behavior changed");
                    Capture("06-hud",1920,1080);Capture("06-hud-720",1280,720);menu.OpenMenu();break;
                case 6: Capture("07-pause",1920,1080);Click("Inventory");break;
                case 7: UICleanupReview.CheckInventory();Find<InventoryManagementView>().Select(1,false);Capture("08-inventory",1920,1080);Capture("08-inventory-720",1280,720);Capture("08-inventory-tablet",2048,1536);Click("Back");Check(menu.IsOpen&&Time.timeScale==0,"Inventory Back changed pause");
                    Click("Resume");Check(!menu.IsOpen,"Resume did not close menu");
                    break;
                case 8:
                    if(!Find<PlayerSkillState>().HasSkill(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Game/Data/Progression/BeamHoist.asset"))){UICleanupReview.LearnFirstBook();return;}
                    Capture("09-knowledge-acquired",1920,1080);Capture("09-knowledge-acquired-720",1280,720);Click("Acknowledge");break;
                case 9: Click("LearnButton");Click("Entry0");break;
                case 10: Capture("10-knowledge-log",1920,1080);Find<KnowledgeLogView>().Close();menu.OpenMenu();menu.ShowRestart();break;
                case 11: Capture("11-danger",1920,1080);menu.ShowMenu();
                    Check(!ShaderUtil.GetShaderMessages(Shader.Find("UI/Neon Surface")).Any(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),"surface shader compile error");
                    Check(!ShaderUtil.GetShaderMessages(Shader.Find("Presentation/Tutorial Floor")).Any(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error),"floor shader compile error");
                    Click("Resume");UnlockFixture("UnarmedCombat");break;
                case 12: Capture("12-fighting-stage",1920,1080);Click("Acknowledge");break;
                case 13: if(!UICleanupReview.CheckFirstPistolPickup())return;break;
                case 14: Capture("14-pistol-stage",1920,1080);Click("Acknowledge");break;
                case 15: if(!UICleanupReview.CheckDuplicatePistolPickup())return;UnlockFixture("RifleHandling");break;
                case 16: Capture("16-rifle-stage",1920,1080);Click("Acknowledge");break;
                case 17: UnlockFixture("GrenadeHandling");break;
                case 18: Capture("18-grenade-stage",1920,1080);Click("Acknowledge");break;
                case 19: Click("LearnButton");break;
                case 20: Click("Entry0");break;
                case 21: Capture("20-knowledge-list-720",1280,720);Capture("20-knowledge-list-tablet",2048,1536);Find<KnowledgeLogView>().Close();
                    Debug.Log("PROCEDURAL UI PLAY REVIEW PASSED: gallery/screens at three resolutions, icon-only HUD with quantities, inventory/back/resume, all five Knowledge stages and acknowledgement buttons. No lifecycle/restore/device test.");
                    Finish();return;
            }
            step++;
        } catch(Exception error) { Fail(error); }
    }
    private static void UnlockFixture(string name) => Find<PlayerSkillState>().UnlockSkill(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Game/Data/Progression/"+name+".asset"));
    private static void Fail(Exception error) { failed=true;Debug.LogException(error);Finish(); }
    private static void Finish() { EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;EditorApplication.ExitPlaymode(); }

    [MenuItem("Tools/UI/Open Procedural Visual Gallery")]
    public static void OpenGallery()
    {
        ProceduralUISetup.EnsureAssets();
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        BuildGallery();
    }
    public static GameObject BuildGallery()
    {
        UseTheme(AssetDatabase.LoadAssetAtPath<UITheme>(MenuUISetup.ThemePath));
        var root=new GameObject("Procedural Visual Gallery",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new(1920,1080);scaler.matchWidthOrHeight=.5f;
        var background=Rect(root.transform,"Background",Vector2.zero,Vector2.one).gameObject.AddComponent<Image>();background.color=new(.004f,.008f,.026f);
        Text(root.transform,"Title","KIDS VS ALIENS  /  UI MATERIAL LIBRARY",new(.045f,.895f),new(.95f,.965f),38,Cyan);
        Text(root.transform,"Subtitle","PROCEDURAL SURFACES  /  TRANSPARENT SPRITES  /  LIVE TMP",new(.045f,.85f),new(.95f,.89f),19,Muted);
        var left=Panel(root.transform,"HUD and surfaces",new(.035f,.08f),new(.49f,.825f));
        Text(left,"Heading","SURFACES / HUD",new(.045f,.91f),new(.95f,.98f),25,Cyan);
        for(int i=0;i<2;i++) {
            var bar=Prefab("ResourceBar",left,new(.05f,.78f-i*.105f),new(.95f,.85f-i*.105f));
            if(i==1)bar.GetComponentInChildren<InterfaceGlyph>().symbol=InterfaceSymbol.Armor;
            Text(bar,"Number",i==0?"96":"50",new(.88f,.08f),new(.97f,.92f),21,null,TextAlignmentOptions.Center);
        }
        var states=new[]{NeonState.Normal,NeonState.Selected,NeonState.Empty,NeonState.Locked};
        for(int i=0;i<4;i++) {
            var slot=Prefab("InventorySlot",left,new(.055f+i*.235f,.405f),new(.24f+i*.235f,.625f));
            slot.GetComponent<NeonPanel>().SetState(states[i]);
            if(i<2)Icon(slot,InterfaceIconCatalog.Current.entries[i].sprite,new(.12f,.2f),new(.88f,.93f));
            if(i==3)NeonVisuals.Symbol(slot,InterfaceSymbol.Lock,new(.22f,.26f),new(.78f,.84f));
            Text(slot,"State",states[i].ToString().ToUpperInvariant(),new(.02f,.015f),new(.98f,.19f),16,Muted,TextAlignmentOptions.Center);
        }
        Prefab("PrimaryButton",left,new(.05f,.265f),new(.95f,.355f));
        Prefab("SecondaryButton",left,new(.05f,.13f),new(.48f,.22f));
        Prefab("DangerButton",left,new(.52f,.13f),new(.95f,.22f));
        Prefab("NeonDivider",left,new(.08f,.07f),new(.92f,.073f));
        var right=Panel(root.transform,"Controls and icons",new(.52f,.08f),new(.965f,.825f));
        Text(right,"Heading","CONTROLS / ICONS",new(.045f,.91f),new(.95f,.98f),25,Cyan);
        var symbols=new[]{InterfaceSymbol.Pause,InterfaceSymbol.Fire,InterfaceSymbol.Jump,InterfaceSymbol.Sprint,InterfaceSymbol.Book};
        for(int i=0;i<5;i++) {
            var circle=Prefab("CircleControl",right,new(.06f+i*.185f,.72f),new(.19f+i*.185f,.88f));
            circle.GetComponentInChildren<InterfaceGlyph>().symbol=symbols[i];
        }
        Prefab("JoystickVisual",right,new(.075f,.39f),new(.37f,.69f));
        var badge=Prefab("NeonBadge",right,new(.44f,.59f),new(.50f,.66f));
        Text(right,"BadgeText","UNREAD / SUCCESS",new(.54f,.59f),new(.95f,.66f),18,Green);
        var small=Panel(right,"Info",new(.44f,.43f),new(.94f,.55f));
        Text(small,"InfoText","SOFT GLOW  /  CRISP EDGES",new(.07f,.15f),new(.93f,.85f),18,Muted,TextAlignmentOptions.Center);
        for(int i=0;i<10;i++) {
            float x=.025f+(i%5)*.193f,y=i<5?.225f:.025f;
            var cell=Panel(right,"Icon"+i,new(x,y),new(x+.175f,y+.18f));cell.GetComponent<NeonPanel>().details=false;
            Icon(cell,InterfaceIconCatalog.Current.entries[i].sprite,new(.05f,.1f),new(.95f,.95f));
        }
        Text(root.transform,"Footer","ONE SHARED SDF MATERIAL  /  NO RASTER PANEL SHELLS",new(.045f,.025f),new(.95f,.065f),19,Muted);
        return root;
    }
    private static RectTransform Prefab(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(ProceduralUISetup.PrefabFolder+"/"+name+".prefab");
        var rect=(RectTransform)Object.Instantiate(source,parent).transform;
        rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
    }
    private static void Icon(Transform parent,Sprite sprite,Vector2 min,Vector2 max)
    {
        var image=Rect(parent,"Icon",min,max).gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
    }
    public static void Capture(string name,int width,int height)
    {
        Directory.CreateDirectory("Logs/ProceduralUI");
        var camera=Camera.main;
        var temporary=camera==null?new GameObject("UI Capture Camera",typeof(Camera)):null;
        if(camera==null){camera=temporary.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;}
        var target=new RenderTexture(width,height,24);target.Create();
        var previous=camera.targetTexture;var mask=camera.cullingMask;camera.cullingMask|=1<<5;camera.targetTexture=target;
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var cameras=canvases.Select(c=>c.worldCamera).ToArray();var planes=canvases.Select(c=>c.planeDistance).ToArray();
        try {
            foreach(var canvas in canvases) {
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                var scaler=canvas.GetComponent<CanvasScaler>();
                if(scaler!=null)typeof(CanvasScaler).GetMethod("Handle",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(scaler,null);
            }
            Canvas.ForceUpdateCanvases();camera.Render();
            var active=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new UnityEngine.Rect(0,0,width,height),0,0);texture.Apply();
            File.WriteAllBytes("Logs/ProceduralUI/"+name+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);RenderTexture.active=active;
        } finally {
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
            camera.targetTexture=previous;camera.cullingMask=mask;target.Release();Object.DestroyImmediate(target);if(temporary!=null)Object.DestroyImmediate(temporary);
        }
    }
}
