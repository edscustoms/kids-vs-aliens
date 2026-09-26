using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Explicit one-time migration, separate from ordinary repair so authored poses are never overwritten.</summary>
public static class WeaponContractRefinement
{
    [MenuItem("Tools/Weapons/Verify Historical Geometry Migration")]
    public static void VerifyHistoricalGeometryMigration()
    {
        string path = Evidence + "/geometry-before.json";
        if (!File.Exists(path)) throw new System.InvalidOperationException("Historical migration evidence is unavailable. Normal regression does not require it.");
        var before = JsonUtility.FromJson<GeometrySet>(File.ReadAllText(path));
        foreach (var geometry in before.weapons)
        {
            var root = PrefabUtility.LoadPrefabContents(geometry.path);
            try { AssertGeometry(geometry, Capture(root, geometry.path)); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    public static readonly string[] WeaponPaths = {
        "Assets/Game/Prefabs/Weapons/PlasmaPistol/PlasmaPistol_Equipped.prefab",
        "Assets/Game/Prefabs/Weapons/PlasmaPistol/PlasmaPistol_Dropped.prefab",
        "Assets/Game/Prefabs/Weapons/PlasmaPistol/PlasmaPistol_MenuPreview.prefab",
        "Assets/Game/Prefabs/Weapons/PlasmaRifle/Plasma_Rifle_Equipped.prefab",
        "Assets/Game/Prefabs/Weapons/PlasmaRifle/Plasma_Rifle_Dropped.prefab",
        "Assets/Game/Prefabs/Weapons/PlasmaRifle/Plasma_Rifle_MenuPreview.prefab"
    };
    public static readonly string[] ActorPaths = {
        "Assets/Game/Prefabs/Player/Characters/Amy.prefab",
        "Assets/Game/Prefabs/Player/Characters/SportyGranny.prefab", EnemyCombatantSetup.PrefabPath
    };
    public const string Evidence = "Logs/WeaponRefinement";
    [Serializable] public sealed class MountPose { public int actor; public int style; public Vector3 position; public Quaternion rotation; }
    [Serializable] public sealed class MountPoses { public List<MountPose> poses = new(); }
    [Serializable] public sealed class Geometry { public string path; public List<string> meshes = new(); public List<Vector3> points = new(); }
    [Serializable] public sealed class GeometrySet { public List<Geometry> weapons = new(); }

    public static void Normalize()
    {
        Directory.CreateDirectory(Evidence + "/Before");
        var roots = new List<GameObject>(); var before = new GeometrySet(); var report = new List<string>();
        try
        {
            // Load/unpack every variant before saving any base prefab: inherited scale overrides cannot compound.
            foreach (string path in WeaponPaths)
            {
                string backup = Evidence + "/Before/" + Path.GetFileName(path);
                if (!File.Exists(backup)) File.Copy(path, backup);
                var root = PrefabUtility.LoadPrefabContents(path); roots.Add(root);
                before.weapons.Add(Capture(root, path));
                if(path.Contains("_MenuPreview"))continue; // Retain the shared equipped-prefab link.
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t != null && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                        PrefabUtility.UnpackPrefabInstance(t.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
            if (!File.Exists(Evidence + "/geometry-before.json")) File.WriteAllText(Evidence + "/geometry-before.json", JsonUtility.ToJson(before, true));
            for (int i = 0; i < roots.Count; i++)
            {
                var root = roots[i];
                if(WeaponPaths[i].Contains("_MenuPreview")){report.Add(WeaponPaths[i]+": inherits the normalized equipped prefab.");continue;}
                var contracts = root.GetComponentsInChildren<WeaponInstance>(true).Select(x => x.transform).ToList();
                if (!contracts.Contains(root.transform)) contracts.Add(root.transform);
                foreach (var contract in contracts.OrderByDescending(Depth)) NormalizeRoot(contract);
                var after = Capture(root, WeaponPaths[i]);
                AssertGeometry(before.weapons[i], after);
                PrefabUtility.SaveAsPrefabAsset(root, WeaponPaths[i]);
                report.Add(WeaponPaths[i] + ": all " + after.points.Count + " mesh-bound corners preserved; attachment/helper scale=1.");
            }
        }
        finally { foreach (var root in roots) PrefabUtility.UnloadPrefabContents(root); }
        foreach (string path in ActorPaths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { AuthorMounts(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var profile = AssetDatabase.LoadAssetAtPath<EnemyCombatProfile>(EnemyCombatantSetup.ProfilePath);
        profile.automaticBurstCount = 6; EditorUtility.SetDirty(profile);
        var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        foreach (var actor in UnityEngine.Object.FindObjectsByType<EnemyEquipment>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (actor.name != "PF_Enemy_Melee_POC_V1 (7)" && actor.name != "PF_Enemy_Melee_POC_V1 (8)") continue;
            var so = new SerializedObject(actor); so.FindProperty("dropWeaponOnDeath").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(actor);
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        File.WriteAllLines(Evidence + "/normalization.txt", report);
    }
    private static int Depth(Transform t) { int n=0; while(t.parent!=null){n++;t=t.parent;}return n; }
    private static void NormalizeRoot(Transform root)
    {
        if (root.Find("VisualRoot") != null && root.localScale == Vector3.one) return;
        Vector3 scale = root.localScale;
        if (Mathf.Abs(scale.x-scale.y)>.0001f || Mathf.Abs(scale.x-scale.z)>.0001f) throw new InvalidOperationException("Review nonuniform weapon scale: " + root.name);
        var children = new List<Transform>(); foreach (Transform child in root) children.Add(child);
        var visual = new GameObject("VisualRoot").transform; visual.SetParent(root,false); visual.localScale = scale;
        // A root mesh becomes the scaled visual. References still point to the same mesh/material assets.
        var filter=root.GetComponent<MeshFilter>(); var renderer=root.GetComponent<MeshRenderer>();
        if(filter!=null) { EditorUtility.CopySerialized(filter,visual.gameObject.AddComponent<MeshFilter>());UnityEngine.Object.DestroyImmediate(filter); }
        if(renderer!=null) { EditorUtility.CopySerialized(renderer,visual.gameObject.AddComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(renderer); }
        root.localScale=Vector3.one;
        foreach(var child in children) child.SetParent(visual,false);
        foreach(var child in children)
        {
            if(child.name!="GripPoint" && child.name!="LeftGripPoint" && child.name!="Muzzle")continue;
            child.SetParent(root,true); child.localScale=Vector3.one;
            // All weapon-side frames use the actual barrel's +Z and weapon's +Y, never a particular rig's wrist basis.
            child.localRotation=Quaternion.identity;
        }
        foreach(var collider in root.GetComponents<Collider>())
        {
            if(collider is BoxCollider box){box.center=Vector3.Scale(box.center,scale);box.size=Vector3.Scale(box.size,scale);}
            else if(collider is SphereCollider sphere){sphere.center=Vector3.Scale(sphere.center,scale);sphere.radius*=scale.x;}
            else if(collider is CapsuleCollider capsule){capsule.center=Vector3.Scale(capsule.center,scale);capsule.radius*=scale.x;capsule.height*=scale.x;}
            else throw new InvalidOperationException("Review collider migration: "+collider.GetType().Name);
        }
    }
    public static Geometry Capture(GameObject root,string path)
    {
        var result=new Geometry{path=path};
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter.sharedMesh==null)continue;
            string mesh=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(filter.sharedMesh))+":"+filter.sharedMesh.name;
            var bounds=filter.sharedMesh.bounds;
            for(int i=0;i<8;i++)
            {
                var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                result.meshes.Add(mesh);
                result.points.Add(Quaternion.Inverse(root.transform.rotation)*(filter.transform.TransformPoint(corner)-root.transform.position));
            }
        }
        return result;
    }
    public static void AssertGeometry(Geometry before,Geometry after)
    {
        if(before.points.Count!=after.points.Count)throw new InvalidOperationException("Mesh count changed: "+before.path);
        var used=new bool[after.points.Count];
        for(int i=0;i<before.points.Count;i++)
        {
            int match=-1;
            for(int j=0;j<after.points.Count;j++)if(!used[j]&&before.meshes[i]==after.meshes[j]&&Vector3.Distance(before.points[i],after.points[j])<.00025f){match=j;break;}
            if(match<0)throw new InvalidOperationException("Visible weapon geometry/size changed: "+before.path+" corner "+i);
            used[match]=true;
        }
    }
    public static void AuthorMounts(GameObject root)
    {
        var visual=root.GetComponentInChildren<CharacterVisual>(true);
        if(visual==null||visual.Animator==null)throw new InvalidOperationException("Missing CharacterVisual on "+root.name);
        var hand=visual.Animator.GetBoneTransform(HumanBodyBones.RightHand);
        var group=hand.Find("WeaponMounts");if(group==null){group=new GameObject("WeaponMounts").transform;group.SetParent(hand,false);}
        var serialized=new SerializedObject(visual);var array=serialized.FindProperty("weaponMounts");array.arraySize=2;
        for(int i=0;i<2;i++)
        {
            string name=i==0?"Pistol":"Rifle";var mount=group.Find(name);
            if(mount==null){mount=new GameObject(name).transform;mount.SetParent(group,false);}
            mount.localScale=Vector3.one;
            var field=array.GetArrayElementAtIndex(i);field.FindPropertyRelative("style").intValue=i+1;field.FindPropertyRelative("socket").objectReferenceValue=mount;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        // Retire the earlier enemy-only approximation; CharacterVisual now owns the shared contract.
        foreach(string legacy in new[]{"PistolLocomotionSocket","RifleLocomotionSocket"})
        {
            var old=hand.Find(legacy);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
    }
    public static void ApplyMountCalibration() => ApplyMountFile("mount-calibration.json");
    private static void ApplyMountFile(string file)
    {
        var poses=JsonUtility.FromJson<MountPoses>(File.ReadAllText(Evidence+"/"+file));
        for(int actor=0;actor<ActorPaths.Length;actor++)
        {
            var root=PrefabUtility.LoadPrefabContents(ActorPaths[actor]);
            try
            {
                var visual=root.GetComponentInChildren<CharacterVisual>(true);
                foreach(var pose in poses.poses.Where(p=>p.actor==actor))
                {
                    var mount=visual.GetWeaponMount((WeaponAnimationStyle)pose.style);
                    mount.localPosition=pose.position;mount.localRotation=pose.rotation;mount.localScale=Vector3.one;
                }
                PrefabUtility.SaveAsPrefabAsset(root,ActorPaths[actor]);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
    }
    public static void RefineRifleReferences()
    {
        foreach(string path in WeaponPaths.Skip(3).Take(2))
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                {
                    // Upper handle/trigger grip, and the underside of the foregrip, in normalized weapon metres.
                    if(t.name=="GripPoint")t.localPosition=new Vector3(.0009f,-.03f,-.05f);
                    if(t.name=="LeftGripPoint")t.localPosition=new Vector3(0,-.02f,.178f);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        ApplyMountFile("mount-refinement.json");
    }
    public static void RelinkMenuPreviews()
    {
        foreach(int index in new[]{2,5})
        {
            string path=WeaponPaths[index];var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var old=root.GetComponentInChildren<WeaponInstance>(true);
                if(old==null||root.transform.childCount!=1)throw new InvalidOperationException("Review unexpected menu hierarchy: "+path);
                if(PrefabUtility.IsAnyPrefabInstanceRoot(old.gameObject))continue;
                var before=Capture(root,path);Vector3 position=old.transform.position;Quaternion rotation=old.transform.rotation;
                var replacement=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPaths[index-2]),root.transform);
                replacement.transform.SetPositionAndRotation(position,rotation);replacement.transform.localScale=Vector3.one;
                var redundant=old.transform;while(redundant.parent!=root.transform)redundant=redundant.parent;
                UnityEngine.Object.DestroyImmediate(redundant.gameObject);
                AssertGeometry(before,Capture(root,path));
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
    }
}
