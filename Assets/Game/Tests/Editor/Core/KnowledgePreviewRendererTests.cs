using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;

public sealed class KnowledgePreviewRendererTests
{
    [Test]
    public void PreviewRepairAndGameplayFeatureSetupRemainSeparateAndIdempotent()
    {
        var preview = GameplayPresentationSetup.EnsurePreviewRenderer();
        var pipelines = new[] { "Mobile", "PC" }.Select(name =>
            AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                $"Assets/Game/Settings/Rendering/{name}_RPAsset.asset")).ToArray();
        var lists = pipelines.Select(p => p.rendererDataList.ToArray()).ToArray();
        var gameplayFeatures = pipelines.Select(p => p.rendererDataList[0].rendererFeatures.ToArray()).ToArray();

        // Reproduce accidental gameplay-feature contamination, without creating
        // or destroying a shared feature. Repair must only detach it from preview.
        preview.rendererFeatures.Add(gameplayFeatures[0].OfType<CameraOcclusionSilhouetteFeature>().Single());
        Assert.That(GameplayPresentationSetup.EnsurePreviewRenderer(), Is.SameAs(preview));
        for (int repeat = 0; repeat < 2; repeat++)
        {
            CameraOcclusionSilhouetteSetup.Ensure();
            Assert.That(GameplayPresentationSetup.EnsurePreviewRenderer(), Is.SameAs(preview));
            Assert.That(preview.rendererFeatures, Is.Empty);
            for (int i = 0; i < pipelines.Length; i++)
            {
                Assert.That(pipelines[i].rendererDataList.ToArray(), Is.EqualTo(lists[i]));
                Assert.That(pipelines[i].rendererDataList.ToArray().Count(r => r == preview), Is.EqualTo(1));
                Assert.That(pipelines[i].rendererDataList[0].rendererFeatures, Is.EqualTo(gameplayFeatures[i]));
                Assert.That(new SerializedObject(pipelines[i]).FindProperty("m_DefaultRendererIndex").intValue, Is.Zero);
            }
        }
    }

    [TestCase("ConstructionSite")]
    [TestCase("GamePoc")]
    [TestCase("Level_1")]
    public void AuthoredPreviewSelectsDedicatedRendererInBothQualityProfiles(string name)
    {
        var scene = EditorSceneManager.OpenPreviewScene($"Assets/Game/Scenes/{name}.unity");
        try
        {
            var stage = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<KnowledgePreviewStage>(true)).Single();
            foreach (string quality in new[] { "Mobile", "PC" })
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                    $"Assets/Game/Settings/Rendering/{quality}_RPAsset.asset");
                Assert.That(stage.TrySelectRenderer(pipeline), Is.True);
                int index = new SerializedObject(stage.PreviewCamera.GetUniversalAdditionalCameraData())
                    .FindProperty("m_RendererIndex").intValue;
                Assert.That(AssetDatabase.GetAssetPath(pipeline.rendererDataList[index]),
                    Is.EqualTo(GameplayPresentationSetup.LightweightRendererPath));
                Assert.That(pipeline.rendererDataList[index].rendererFeatures, Is.Empty);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
