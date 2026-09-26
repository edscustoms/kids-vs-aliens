using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BeamHoistZoneSetup
{
    public const string PrefabPath = "Assets/Game/Prefabs/PF_BeamHoistZoneVFX.prefab";
    private const string MaterialPath = "Assets/Game/Materials/VFX/M_BeamHoistZone.mat";
    private const string MoteMaterialPath = "Assets/Game/Materials/VFX/M_BeamHoistZoneMotes.mat";

    public static void ConfigureScene(PlayerCharacter player)
    {
        EnsureAssets();
        var presenter = player.GetComponent<BeamHoistZonePresentation>() ?? Undo.AddComponent<BeamHoistZonePresentation>(player.gameObject);
        var data = new SerializedObject(presenter);
        if (data.FindProperty("zonePrefab").objectReferenceValue == null)
        {
            data.FindProperty("zonePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BeamHoistZoneVFX>(PrefabPath);
            data.ApplyModifiedProperties(); EditorUtility.SetDirty(presenter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(presenter);
        }
        BeamHoistSurfaceBaker.BakeScene(player.gameObject.scene);
    }

    public static void EnsureAssets()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        var shader = Shader.Find("Game/Beam Hoist Zone");
        if (shader == null) throw new System.InvalidOperationException("Hoist zone shader missing.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
        var moteMaterial = AssetDatabase.LoadAssetAtPath<Material>(MoteMaterialPath);
        if (moteMaterial == null)
        {
            moteMaterial = new Material(shader); moteMaterial.SetFloat("_Mote", 1);
            AssetDatabase.CreateAsset(moteMaterial, MoteMaterialPath);
        }
        var root = new GameObject("PF_BeamHoistZoneVFX");
        try
        {
            var visual = root.AddComponent<BeamHoistZoneVFX>();
            var ground = new GameObject("GroundHologram", typeof(MeshFilter), typeof(MeshRenderer));
            ground.transform.SetParent(root.transform, false);
            var renderer = ground.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.enabled = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var motes = new GameObject("EnergyMotes", typeof(ParticleSystem)); motes.transform.SetParent(root.transform, false);
            var particles = motes.GetComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.playOnAwake = false; main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 48;
            main.startLifetime = 1.6f; main.startSpeed = 0; main.startSize = .025f; main.gravityModifier = 0;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>(); particleRenderer.sharedMaterial = moteMaterial;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off; particleRenderer.receiveShadows = false;
            var data = new SerializedObject(visual);
            data.FindProperty("ground").objectReferenceValue = ground.GetComponent<MeshFilter>();
            data.FindProperty("groundRenderer").objectReferenceValue = renderer;
            data.FindProperty("energyMotes").objectReferenceValue = particles;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    // Explicit batch/Editor migration: only add the observer. Never run broad scene repair here.
    [MenuItem("Tools/Setup/Add Hoist Zone Presentation to ConstructionSite")]
    public static void InstallConstructionSite()
    {
        EnsureAssets();
        var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerCharacter>(true)).Single();
        var sequence = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerBeamInSequence>(true)).Single();
        string before = BeamTransportV2Review.Describe(sequence.gameObject);
        ConfigureScene(player); ConfigureScene(player);
        BeamHoistSurfaceBaker.BakeScene(scene, true);
        if (before != BeamTransportV2Review.Describe(sequence.gameObject)) throw new System.InvalidOperationException("Arrival presentation changed.");
        Directory.CreateDirectory("Logs/HoistZone");
        File.WriteAllText("Logs/HoistZone/arrival-preservation.txt", "PASS: LevelStart hierarchy, components, transforms and serialized settings unchanged by zone installation.");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
}
