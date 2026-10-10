using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class GraphicsSettingsSetup
{
    public const string ConfigurationPath = "Assets/Game/Resources/GraphicsQuality.asset";
    public static SceneGraphicsSettings ConfigureScene(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var owners = roots.SelectMany(r => r.GetComponentsInChildren<SceneGraphicsSettings>(true)).ToArray();
        if (owners.Length > 1) throw new System.InvalidOperationException("Scene has duplicate graphics settings owners.");
        var owner = owners.FirstOrDefault();
        if (owner == null)
        {
            var root = new GameObject("Scene Graphics Settings");
            Undo.RegisterCreatedObjectUndo(root, "Add scene graphics settings");
            SceneManager.MoveGameObjectToScene(root, scene);
            owner = Undo.AddComponent<SceneGraphicsSettings>(root);
        }
        Undo.RecordObject(owner, "Wire scene graphics targets");
        owner.Configure(roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).ToArray(),
            roots.SelectMany(r => r.GetComponentsInChildren<Volume>(true)).ToArray());
        EditorUtility.SetDirty(owner);
        return owner;
    }

    // Explicit one-time authoring, also usable in an isolated project for validation.
    public static void AuthorDefaults()
    {
        var config = AssetDatabase.LoadAssetAtPath<GraphicsQualityConfiguration>(ConfigurationPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GraphicsQualityConfiguration>();
            var low = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { name = "Terrain Low", enableInstancing = true };
            const string lowPath = "Assets/Game/Settings/Rendering/Terrain Low.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(lowPath);
            if (existing != null) Object.DestroyImmediate(low); else { AssetDatabase.CreateAsset(low, lowPath); existing = low; }
            config.terrainMaterials = new[] { new GraphicsQualityConfiguration.TerrainMaterials {
                original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Environment/Foundation/Terrain Rock Projection.mat"), low = existing,
                medium = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Environment/Medium/Terrain Medium.mat") } };
            AssetDatabase.CreateAsset(config, ConfigurationPath);
        }
        foreach (var name in new[] { "ConstructionSite", "BikeRoute" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/" + name + ".unity");
            ConfigureScene(scene);
            EditorSceneManager.SaveScene(scene);
        }
        // URP 17 strips variants from build-time features; a runtime toggle is not discovered.
        // Retain its supported ON/OFF variants rather than risking missing Android shaders.
        var stripping = GraphicsSettings.GetRenderPipelineSettings<URPShaderStrippingSetting>();
        stripping.stripUnusedVariants = false;
        var global = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Game/Settings/Rendering/UniversalRenderPipelineGlobalSettings.asset");
        EditorUtility.SetDirty(global); AssetDatabase.SaveAssetIfDirty(global);
        AssetDatabase.SaveAssetIfDirty(config);
    }
}

/// <summary>Fail clearly if build stripping is later re-enabled against runtime graphics switches.</summary>
public sealed class GraphicsSettingsBuildCheck : IPreprocessBuildWithReport
{
    // Must precede CorePreprocessBuild (MinValue + 50) and URP (MinValue + 100).
    public int callbackOrder => int.MinValue;
    public void OnPreprocessBuild(BuildReport report)
    {
        if (GraphicsSettingsController.Instance != null) GraphicsSettingsController.Instance.RestoreOriginalPipeline();
        UnityEditor.Rendering.CoreBuildData.instance.Dispose();
        Validate();
    }
    public static void Validate()
    {
        if (AssetDatabase.LoadAssetAtPath<GraphicsQualityConfiguration>(GraphicsSettingsSetup.ConfigurationPath) == null)
            throw new BuildFailedException("Missing GraphicsQuality configuration.");
        if (GraphicsSettings.GetRenderPipelineSettings<URPShaderStrippingSetting>().stripUnusedVariants)
            throw new BuildFailedException("Graphics settings require URP Strip Unused Variants OFF to preserve runtime shadow variants.");
    }
}
