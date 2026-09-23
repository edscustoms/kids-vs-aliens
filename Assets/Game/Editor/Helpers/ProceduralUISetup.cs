using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Asset-only setup. Never edits items, skills, callbacks, world content or run data.
public static class ProceduralUISetup
{
    public const string PrefabFolder = "Assets/Game/UI/Primitives";
    [MenuItem("Tools/UI/Ensure Procedural Visual Assets")]
    public static void EnsureAssets()
    {
        Folder("Assets/Game/Resources"); Folder(PrefabFolder);
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Resources/NeonUI.mat");
        if (material == null) {
            material = new Material(Shader.Find("UI/Neon Surface"));
            AssetDatabase.CreateAsset(material, "Assets/Game/Resources/NeonUI.mat");
        }
        var items = ImportSheet("Assets/Game/UI/Icons/GameplayIcons.png", new[] {
            Region("Fighting",0,95,438,465), Region("PlasmaPistol",438,155,426,390),
            Region("Rifle",864,150,584,395), Region("ElectricGrenade",218,558,489,478),
            Region("Medkit",718,558,560,478)
        });
        var knowledge = ImportSheet("Assets/Game/UI/Icons/KnowledgeIcons.png", new[] {
            Region("BeamHoistKnowledge",0,70,494,490), Region("FightingKnowledge",494,70,462,490),
            Region("PistolKnowledge",956,85,492,475), Region("RifleKnowledge",195,560,516,470),
            Region("GrenadeKnowledge",738,558,553,472)
        });
        var catalog = AssetDatabase.LoadAssetAtPath<InterfaceIconCatalog>("Assets/Game/Resources/InterfaceIcons.asset");
        if (catalog == null) {
            catalog = ScriptableObject.CreateInstance<InterfaceIconCatalog>();
            AssetDatabase.CreateAsset(catalog, "Assets/Game/Resources/InterfaceIcons.asset");
            catalog.entries = new[] {
                Entry(InterfaceIcon.Fighting, items, "Assets/Game/Data/Items/UnarmedCombat/Fighting.asset"),
                Entry(InterfaceIcon.PlasmaPistol, items, "Assets/Game/Items/Weapons/PlasmaPistolItem.asset"),
                Entry(InterfaceIcon.Rifle, items, "Assets/Game/Items/Weapons/PlasmaRifleItem.asset"),
                Entry(InterfaceIcon.ElectricGrenade, items, "Assets/Game/Data/Items/Grenades/ElectricGrenade.asset"),
                Entry(InterfaceIcon.Medkit, items),
                Entry(InterfaceIcon.BeamHoistKnowledge, knowledge, null, "BeamHoist"),
                Entry(InterfaceIcon.FightingKnowledge, knowledge, null, "UnarmedCombat"),
                Entry(InterfaceIcon.PistolKnowledge, knowledge, null, "PistolHandling"),
                Entry(InterfaceIcon.RifleKnowledge, knowledge, null, "RifleHandling"),
                Entry(InterfaceIcon.GrenadeKnowledge, knowledge, null, "GrenadeHandling")
            };
            EditorUtility.SetDirty(catalog);
        }
        var theme = AssetDatabase.LoadAssetAtPath<UITheme>(MenuUISetup.ThemePath);
        InterfaceFactory.UseTheme(theme);
        CreatePrefab("NeonPanel", NeonShape.Panel, new(700,420), theme);
        CreatePrefab("NeonCard", NeonShape.Panel, new(340,260), theme);
        CreatePrefab("PrimaryButton", NeonShape.Button, new(420,92), theme, "CONTINUE");
        CreatePrefab("SecondaryButton", NeonShape.Button, new(300,76), theme, "BACK");
        CreatePrefab("DangerButton", NeonShape.Button, new(300,76), theme, "RESTART", true);
        CreatePrefab("CircleControl", NeonShape.Circle, new(112,112), theme, "");
        CreatePrefab("JoystickVisual", NeonShape.Joystick, new(260,260), theme);
        CreatePrefab("InventorySlot", NeonShape.Slot, new(120,120), theme);
        CreatePrefab("ResourceBar", NeonShape.Button, new(420,52), theme);
        CreatePrefab("NeonDivider", NeonShape.Divider, new(500,4), theme);
        CreatePrefab("NeonBadge", NeonShape.Badge, new(36,36), theme);
        AssetDatabase.SaveAssets();
        Debug.Log("PROCEDURAL UI ASSETS READY: shared SDF material, ten sprites, UI-only icon mappings, eleven reusable prefabs.");
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash=path.LastIndexOf('/'); Folder(path.Substring(0,slash));
        AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
    }
    private static SpriteMetaData Region(string name,int x,int y,int width,int height) => new() {
        name=name, rect=new Rect(x,1086-y-height,width,height), alignment=(int)SpriteAlignment.Center,pivot=new(.5f,.5f)
    };
    private static Dictionary<string,Sprite> ImportSheet(string path,SpriteMetaData[] regions)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(importer.spriteImportMode!=SpriteImportMode.Multiple) {
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Clamp;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            #pragma warning disable 0618
            importer.spritesheet=regions;
            #pragma warning restore 0618
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(sprite=>sprite.name);
    }
    private static InterfaceIconCatalog.Entry Entry(InterfaceIcon role,Dictionary<string,Sprite> sprites,string item=null,string skill=null)
        => new() {role=role,sprite=sprites[role.ToString()],item=item==null?null:AssetDatabase.LoadAssetAtPath<ItemData>(item),
            skill=skill==null?null:AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Game/Data/Progression/"+skill+".asset")};
    private static void CreatePrefab(string name,NeonShape shape,Vector2 size,UITheme theme,string label=null,bool danger=false)
    {
        string path=PrefabFolder+"/"+name+".prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)return;
        var root=new GameObject(name,typeof(RectTransform),typeof(NeonPanel));
        try {
            ((RectTransform)root.transform).sizeDelta=size;
            var panel=root.GetComponent<NeonPanel>();panel.ApplyTheme(theme,danger);panel.SetShape(shape);
            if(name=="PrimaryButton"){panel.border=2.2f;panel.glow=14;}
            if(name=="SecondaryButton"){panel.border=1.2f;panel.glow=8;}
            if(label!=null) {
                var button=root.AddComponent<Button>();button.targetGraphic=panel;
                NeonVisuals.Feedback(root,panel);
                if(label.Length>0)InterfaceFactory.Text(root.transform,"Label",label,new(.06f,.12f),new(.94f,.88f),28,Color.white,TMPro.TextAlignmentOptions.Center);
                else NeonVisuals.Symbol(root.transform,InterfaceSymbol.Pause,new(.2f,.2f),new(.8f,.8f));
            }
            if(name=="JoystickVisual") {
                var thumb=InterfaceFactory.Panel(root.transform,"Thumb",new(.28f,.28f),new(.72f,.72f));
                thumb.GetComponent<NeonPanel>().SetShape(NeonShape.Circle);thumb.GetComponent<NeonPanel>().raycastTarget=false;
                NeonVisuals.Symbol(thumb,InterfaceSymbol.Move,new(.2f,.2f),new(.8f,.8f));
            }
            if(name=="ResourceBar") {
                panel.radius=100;panel.details=false;
                NeonVisuals.Symbol(root.transform,InterfaceSymbol.Health,new(.035f,.15f),new(.12f,.85f)).color=theme.primary;
                var fill=InterfaceFactory.Panel(root.transform,"Fill",new(.17f,.3f),new(.85f,.7f)).GetComponent<NeonPanel>();
                fill.SetShape(NeonShape.Fill);fill.radius=100;fill.color=Color.white;fill.raycastTarget=false;
            }
            if(shape==NeonShape.Badge) {
                panel.accent=theme.success;panel.raycastTarget=false;
                InterfaceFactory.Text(root.transform,"Count","1",new(.15f,.15f),new(.85f,.85f),22,new Color(.01f,.04f,.02f),TMPro.TextAlignmentOptions.Center);
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        } finally {Object.DestroyImmediate(root);}
    }
}
