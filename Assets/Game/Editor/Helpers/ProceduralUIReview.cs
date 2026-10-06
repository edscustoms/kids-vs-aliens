using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using static InterfaceFactory;

// Reusable visual gallery and capture utility. Historical scripted walkthroughs are retired.
public static class ProceduralUIReview
{
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
    public static void Capture(string name,int width,int height,bool preserveHdr=false)
    {
        Directory.CreateDirectory("Logs/ProceduralUI");
        var camera=Camera.main;
        var temporary=camera==null?new GameObject("UI Capture Camera",typeof(Camera)):null;
        if(camera==null){camera=temporary.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;}
        // URP uses an explicit target's format for its intermediate color buffer.
        // HDR world reviews must preserve emissive values until bloom/tonemapping.
        var target=new RenderTexture(width,height,24,preserveHdr?RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default);target.Create();
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
            var texture=new Texture2D(width,height,preserveHdr?TextureFormat.RGBAFloat:TextureFormat.RGB24,false);texture.ReadPixels(new UnityEngine.Rect(0,0,width,height),0,0);texture.Apply();
            if(preserveHdr)
            {
                var pixels=texture.GetPixels();
                if(QualitySettings.activeColorSpace==ColorSpace.Linear)
                    for(int i=0;i<pixels.Length;i++) pixels[i]=pixels[i].gamma;
                Object.DestroyImmediate(texture);
                texture=new Texture2D(width,height,TextureFormat.RGB24,false);
                texture.SetPixels(pixels);texture.Apply();
            }
            File.WriteAllBytes("Logs/ProceduralUI/"+name+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);RenderTexture.active=active;
        } finally {
            for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
            camera.targetTexture=previous;camera.cullingMask=mask;target.Release();Object.DestroyImmediate(target);if(temporary!=null)Object.DestroyImmediate(temporary);
        }
    }
}
