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
        // Presets change scale/distance, not pipeline capabilities. URP keeps the
        // supported shadow variant and uses an empty map when shadow distance is zero.
        var stripping = GraphicsSettings.GetRenderPipelineSettings<URPShaderStrippingSetting>();
        stripping.stripUnusedVariants = true;
        foreach (var path in new[] { "Assets/Game/Settings/Rendering/Mobile_Renderer.asset", "Assets/Game/Settings/Rendering/PC_Renderer.asset" })
            EnsureAlphaOutputVariants(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path));
        var global = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Game/Settings/Rendering/UniversalRenderPipelineGlobalSettings.asset");
        EditorUtility.SetDirty(global); AssetDatabase.SaveAssetIfDirty(global);
        AssetDatabase.SaveAssetIfDirty(config);
    }

    public static void EnsureAlphaOutputVariants(UniversalRendererData renderer)
    {
        if (renderer == null || !EditorUtility.IsPersistent(renderer))
            throw new System.InvalidOperationException("Alpha variant retention requires a saved renderer asset.");
        if (renderer.rendererFeatures.OfType<PostProcessingAlphaVariants>().Any()) return;
        var feature = ScriptableObject.CreateInstance<PostProcessingAlphaVariants>();
        feature.name = "Post-processing alpha variants";
        AssetDatabase.AddObjectToAsset(feature, renderer);
        renderer.rendererFeatures.Add(feature);
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssetIfDirty(renderer);
    }
}

/// <summary>Validate runtime material inclusion without retaining unused URP features.</summary>
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
        var configuration = AssetDatabase.LoadAssetAtPath<GraphicsQualityConfiguration>(GraphicsSettingsSetup.ConfigurationPath);
        if (configuration == null)
            throw new BuildFailedException("Missing GraphicsQuality configuration.");
        if (!GraphicsSettings.GetRenderPipelineSettings<URPShaderStrippingSetting>().stripUnusedVariants)
            throw new BuildFailedException("Enable URP Strip Unused Variants. Graphics presets retain pipeline capabilities; disabling stripping compiles unused renderer features.");
        foreach (var pipeline in UnityEditor.Rendering.CoreBuildData.instance.renderPipelineAssets.OfType<UniversalRenderPipelineAsset>())
        {
            if (!pipeline.allowPostProcessAlphaOutput) continue;
            var renderers = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            bool retained = false;
            for (int i = 0; i < renderers.arraySize; i++)
            {
                var renderer = renderers.GetArrayElementAtIndex(i).objectReferenceValue as UniversalRendererData;
                retained |= renderer != null && renderer.rendererFeatures.OfType<PostProcessingAlphaVariants>().Any(f => f.isActive);
            }
            if (!retained)
                throw new BuildFailedException($"{pipeline.name}: add PostProcessingAlphaVariants to a renderer to retain opaque-display and alpha-target post-processing variants.");
        }
        // Resources/GraphicsQuality explicitly references every runtime terrain tier,
        // preserving their shader features and textures without Always Included Shaders.
        foreach (var slot in configuration.terrainMaterials)
        {
            if (slot == null) continue;
            ValidateMaterial(slot.original);
            if (slot.medium != null) ValidateMaterial(slot.medium);
            if (slot.low != null) ValidateMaterial(slot.low);
            if (slot.experimentalMedium != null)
            {
                ValidateMaterial(slot.experimentalMedium);
                ValidateMaterial(slot.experimentalBasemapVariants);
                if (slot.experimentalBasemapVariants.shader != slot.experimentalMedium.shader.GetDependency("BaseMapGenShader")
                    || !slot.experimentalBasemapVariants.IsKeywordEnabled("_MASKMAP"))
                    throw new BuildFailedException("Experimental terrain must retain its basemap generator's _MASKMAP material variant.");
            }
        }
    }

    private static void ValidateMaterial(Material material)
    {
        if (material == null || material.shader == null || !EditorUtility.IsPersistent(material))
            throw new BuildFailedException("Graphics terrain tiers must reference persistent materials with valid shaders.");
    }
}
