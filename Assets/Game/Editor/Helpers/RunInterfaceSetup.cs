using System;
using System.Collections.Generic;
using System.Linq;
using KidsVsAliens.Environment;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RunInterfaceSetup
{
    private const string CatalogPath = "Assets/Game/Resources/RunContentCatalog.asset";
    private const string FloorPath = "Assets/Game/Materials/TutorialFloor.mat";
    public static void ConfigureGameplay(PlayerCharacter player, GameObject presentation)
    {
        EnsureCatalog();
        if(player.GetComponent<ActiveRunController>()==null)Undo.AddComponent<ActiveRunController>(player.gameObject);
        var ui=presentation.GetComponent<GameplayInterface>()??Undo.AddComponent<GameplayInterface>(presentation);
        ui.Configure(player,AssetDatabase.LoadAssetAtPath<UITheme>(MenuUISetup.ThemePath));EditorUtility.SetDirty(ui);
        var scene=player.gameObject.scene;var ids=new HashSet<string>();
        var components=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
        var owners = components.Where(component => component is PickupItem || component is EnemyHealth || component is LootChest || component is EnemySpawner || component is IRunStateParticipant)
            .Select(component => component.gameObject).Distinct();
        foreach(var owner in owners)
        {
            var entity=owner.GetComponent<RunWorldObject>()??Undo.AddComponent<RunWorldObject>(owner);
            if(string.IsNullOrEmpty(entity.Id)||!ids.Add(entity.Id))
            {
                string id=GlobalObjectId.GetGlobalObjectIdSlow(owner).ToString();
                if(!ids.Add(id))id=Guid.NewGuid().ToString("N");
                entity.ConfigureIdentity(id);EditorUtility.SetDirty(entity);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(entity);
        }
        var floor=AssetDatabase.LoadAssetAtPath<Material>(FloorPath);
        if(floor==null){floor=new Material(Shader.Find("Presentation/Tutorial Floor"));AssetDatabase.CreateAsset(floor,FloorPath);}
        foreach(var stage in presentation.GetComponentsInChildren<KnowledgePreviewStage>(true))
        {
            var serialized=new SerializedObject(stage);serialized.FindProperty("floorMaterial").objectReferenceValue=floor;serialized.ApplyModifiedPropertiesWithoutUndo();
            stage.EnsureBackdrop();
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }
    public static void ConfigureMenu(Scene scene)
    {
        EnsureCatalog();
        var router=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<UIScreenRouter>(true)).Single();
        var flow = router.GetComponent<ActiveRunMenu>() ?? Undo.AddComponent<ActiveRunMenu>(router.gameObject);
        flow.Configure(AssetDatabase.LoadAssetAtPath<UITheme>(MenuUISetup.ThemePath));EditorUtility.SetDirty(flow);
        if(!scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Camera>(true)).Any(camera=>camera.targetTexture==null))
        {
            var background=new GameObject("Menu Display Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(background,scene);
            Undo.RegisterCreatedObjectUndo(background,"Menu display camera");
            var camera=background.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=InterfaceFactory.Navy;camera.cullingMask=0;camera.depth=-100;
        }
        AudioSceneSetup.ConfigureUiAudio(scene, true);
        EditorSceneManager.MarkSceneDirty(scene);
    }
    public static void EnsureCatalog()
    {
        if(!AssetDatabase.IsValidFolder("Assets/Game/Resources"))AssetDatabase.CreateFolder("Assets/Game","Resources");
        var catalog=AssetDatabase.LoadAssetAtPath<RunContentCatalog>(CatalogPath);
        if(catalog==null){catalog=ScriptableObject.CreateInstance<RunContentCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
        var entries=new List<RunContentCatalog.Entry>();
        foreach(string filter in new[]{"t:ItemData","t:SkillData"})
            foreach(string guid in AssetDatabase.FindAssets(filter,new[]{"Assets/Game"}))
                entries.Add(new RunContentCatalog.Entry{id=guid,asset=AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid))});
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game/Prefabs"}))
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if(prefab.GetComponent<PickupItem>()!=null||prefab.GetComponent<EnemyHealth>()!=null)
                entries.Add(new RunContentCatalog.Entry{id=guid,asset=prefab});
            var character=prefab.GetComponent<CharacterVisual>();
            if(character!=null)entries.Add(new RunContentCatalog.Entry{id=guid+"#character",asset=character});
        }
        var sorted=entries.OrderBy(e=>e.id).ToArray();
        if(catalog.entries.Length!=sorted.Length||catalog.entries.Where((entry,i)=>i>=sorted.Length||entry.id!=sorted[i].id||entry.asset!=sorted[i].asset).Any())
        {catalog.entries=sorted;EditorUtility.SetDirty(catalog);}
    }
    [MenuItem("Tools/UI/Repair Run Interface in Current Scene")]
    public static void RepairActive()
    {
        var scene=SceneManager.GetActiveScene();
        var player=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<PlayerCharacter>(true)).SingleOrDefault();
        if(player==null){MenuUISetup.ConfigureScene(scene);ConfigureMenu(scene);return;}
        var presentation=scene.GetRootGameObjects().FirstOrDefault(root=>root.name==GameplayPresentationSetup.RootName)
            ??GameplayPresentationSetup.ConfigureScene(player);
        ConfigureGameplay(player,presentation);
    }
    public static void RepairExistingScenes()
    {
        foreach(string name in new[]{"Menu","GamePoc","ConstructionSite"})
        {
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/"+name+".unity");RepairActive();
            var ids=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<RunWorldObject>(true)).Select(o=>o.Id).ToArray();
            if(ids.Distinct().Count()!=ids.Length)throw new InvalidOperationException("Duplicate identities in "+name);
            int count=scene.GetRootGameObjects().Sum(root=>root.GetComponentsInChildren<Component>(true).Length);
            RepairActive();
            if(count!=scene.GetRootGameObjects().Sum(root=>root.GetComponentsInChildren<Component>(true).Length))throw new InvalidOperationException("Repair duplicated components.");
            EditorSceneManager.SaveScene(scene);Debug.Log("RUN INTERFACE REPAIRED: "+name+"; world identities="+ids.Length);
        }
        AssetDatabase.SaveAssets();
    }
}

public sealed class RunCatalogBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder=>0;
    public void OnPreprocessBuild(BuildReport report){RunInterfaceSetup.EnsureCatalog();AssetDatabase.SaveAssets();}
}
