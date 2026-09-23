using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit one-time adoption of the current authored LevelStart. Routine repair never publishes visual edits.</summary>
public static class BeamTransportPrefabMigration
{
    private struct Pose
    {
        public Transform transform;
        public Vector3 position, scale, world;
        public Quaternion rotation;
        public bool active;
        public Pose(Transform t)
        {
            transform = t; position = t.localPosition; rotation = t.localRotation;
            scale = t.localScale; world = t.position; active = t.gameObject.activeSelf;
        }
        public void Restore()
        {
            transform.SetLocalPositionAndRotation(position, rotation);
            transform.localScale = scale;
            transform.gameObject.SetActive(active);
            PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(transform.gameObject);
        }
        public void Verify()
        {
            if (Vector3.Distance(transform.localPosition, position) > 0.00001f
                || Vector3.Distance(transform.position, world) > 0.0001f
                || Vector3.Distance(transform.localScale, scale) > 0.00001f
                || Quaternion.Angle(transform.localRotation, rotation) > 0.001f
                || transform.gameObject.activeSelf != active)
                throw new InvalidOperationException("Authored pose changed: " + transform.name);
        }
    }

    [MenuItem("Tools/Setup/Beam Transport/Adopt Authored LevelStart as Canonical Prefabs")]
    public static void MigrateActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before migration.");
        Scene scene = SceneManager.GetActiveScene();
        var sequence = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerBeamInSequence>(true)).Single();
        var level = sequence.gameObject;
        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(level) == BeamTransportSetup.LevelStartPath)
        {
            BeamTransportSetup.ConfigureScene(FindPlayer(scene));
            return; // Rerunning is repair only, never a second publication of scene overrides.
        }
        Directory.CreateDirectory(BeamTransportV2Review.Reports);
        string backup = BeamTransportV2Review.Reports + "/ConstructionSite-before-v2.unity";
        if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
        var poses = level.GetComponentsInChildren<Transform>(true).Select(t => new Pose(t)).ToArray();
        string[] timingNames = { "startHeight", "initialDelay", "descentDuration", "landingHold" };
        var serialized = new SerializedObject(sequence);
        var timings = timingNames.Select(n => serialized.FindProperty(n).floatValue).ToArray();
        var effect = level.GetComponentInChildren<BeamTransportVFX>(true);
        if (effect == null) throw new InvalidOperationException("Authored beam missing; refusing to recreate tuned content.");
        const string oldVfxPath = "Assets/Game/Prefabs/BeamTransportVFX.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BeamTransportSetup.VfxPath) == null)
        {
            string error = AssetDatabase.MoveAsset(oldVfxPath, BeamTransportSetup.VfxPath);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        BakeCone(effect);
        PrefabUtility.SaveAsPrefabAssetAndConnect(effect.gameObject, BeamTransportSetup.VfxPath, InteractionMode.AutomatedAction);
        // Root position belongs to an instance; all visual child transforms belong to the canonical source.
        var vfxContents = PrefabUtility.LoadPrefabContents(BeamTransportSetup.VfxPath);
        try
        {
            vfxContents.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            PrefabUtility.SaveAsPrefabAsset(vfxContents, BeamTransportSetup.VfxPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(vfxContents); }
        foreach (var pose in poses) pose.Restore();

        PrefabUtility.SaveAsPrefabAssetAndConnect(level, BeamTransportSetup.LevelStartPath, InteractionMode.AutomatedAction);
        var contents = PrefabUtility.LoadPrefabContents(BeamTransportSetup.LevelStartPath);
        try
        {
            contents.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var defaults = new SerializedObject(contents.GetComponent<PlayerBeamInSequence>());
            defaults.FindProperty("player").objectReferenceValue = null;
            defaults.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, BeamTransportSetup.LevelStartPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        foreach (var pose in poses) pose.Restore();
        BeamTransportSetup.ConfigureScene(FindPlayer(scene));
        // Explicit timing overrides also preserve authored values equal to prefab defaults.
        var modifications = (PrefabUtility.GetPropertyModifications(level) ?? Array.Empty<PropertyModification>()).ToList();
        var source = PrefabUtility.GetCorrespondingObjectFromSource(sequence);
        for (int i = 0; i < timingNames.Length; i++)
        {
            string property = timingNames[i];
            modifications.RemoveAll(m => m.target == source && m.propertyPath == property);
            modifications.Add(new PropertyModification { target = source, propertyPath = property,
                value = timings[i].ToString("R", CultureInfo.InvariantCulture) });
        }
        PrefabUtility.SetPropertyModifications(level, modifications.ToArray());
        foreach (var pose in poses) pose.Verify();
        serialized = new SerializedObject(sequence);
        for (int i = 0; i < timingNames.Length; i++)
            if (serialized.FindProperty(timingNames[i]).floatValue != timings[i]) throw new InvalidOperationException("Arrival timing changed.");
        File.WriteAllText(BeamTransportV2Review.Reports + "/authored-after.txt", BeamTransportV2Review.Describe(level));
        File.WriteAllText(BeamTransportV2Review.Reports + "/preservation.txt",
            $"PASS: {poses.Length} local/world transforms, active states and all four arrival timings preserved. Cone render mesh baked from existing ProBuilder vertices/faces. VFX GUID retained; LevelStart and nested VFX connected to canonical prefabs.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static PlayerCharacter FindPlayer(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<PlayerCharacter>(true)).Single();

    private static void BakeCone(BeamTransportVFX effect)
    {
        var cone = effect.GetComponentsInChildren<Transform>(true).Single(t => t.name == "BeamOuterCone");
        var filter = cone.GetComponent<MeshFilter>();
        var renderer = cone.GetComponent<MeshRenderer>();
        var materials = renderer.sharedMaterials;
        var editable = cone.GetComponent<ProBuilderMesh>();
        if (editable != null) { editable.ToMesh(); editable.Refresh(); }
        if (filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
            throw new InvalidOperationException("Authored cone has no recoverable geometry.");
        const string meshPath = "Assets/Game/Generated/BeamOuterCone.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Game/Generated")) AssetDatabase.CreateFolder("Assets/Game", "Generated");
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            mesh = Object.Instantiate(filter.sharedMesh); mesh.name = "BeamOuterCone";
            mesh.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(mesh, meshPath);
        }
        else { EditorUtility.CopySerialized(filter.sharedMesh, mesh); mesh.hideFlags = HideFlags.None; EditorUtility.SetDirty(mesh); }
        if (editable != null) editable.preserveMeshAssetOnDestroy = true;
        foreach (var component in cone.GetComponents<MonoBehaviour>())
            if (component != null && component.GetType().Namespace?.StartsWith("UnityEngine.ProBuilder") == true)
                Object.DestroyImmediate(component);
        filter.sharedMesh = mesh;
        filter.hideFlags = HideFlags.None;
        renderer.sharedMaterials = materials;
        AssetDatabase.SaveAssetIfDirty(mesh);
    }
}
