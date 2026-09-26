using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class CameraOcclusionSilhouetteTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);

    [Test]
    public void NonReadableMeshParticipatesWithoutGeneratedGeometryOrPropertyBlockChanges()
    {
        var root = new GameObject("Silhouette fixture");
        var cameraObject = new GameObject("Silhouette camera", typeof(Camera));
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.transform.SetParent(root.transform);
        var mesh = Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
        mesh.UploadMeshData(true); cube.GetComponent<MeshFilter>().sharedMesh = mesh;
        var controller = root.AddComponent<CameraOcclusionController>();
        var camera = cameraObject.GetComponent<Camera>();
        var renderer = cube.GetComponent<Renderer>();
        var original = renderer.sharedMaterials;
        var block = new MaterialPropertyBlock(); block.SetFloat("_Fade", .37f); renderer.SetPropertyBlock(block);
        try
        {
            Set(controller,"gameplayCamera",camera);
            typeof(CameraOcclusionController).GetMethod("BuildLevelCache",Private).Invoke(controller,null);
            var states = (IDictionary)typeof(CameraOcclusionController).GetField("rendererStates",Private).GetValue(controller);
            var active = (HashSet<Renderer>)typeof(CameraOcclusionController).GetField("activeFadeRenderers",Private).GetValue(controller);
            var state=states[renderer]; state.GetType().GetField("currentFade").SetValue(state,.1f); active.Add(renderer);
            var result=new List<CameraOcclusionController.SilhouetteRenderer>();
            controller.CollectSilhouetteRenderers(camera,result);
            Assert.That(result.Count,Is.EqualTo(1));
            Assert.That(result[0].renderer,Is.SameAs(renderer));
            Assert.That(mesh.isReadable,Is.False,"No CPU readability requirement");
            Assert.That(root.GetComponentsInChildren<MeshFilter>().Length,Is.EqualTo(1),"No outline proxy mesh");
            renderer.GetPropertyBlock(block); Assert.That(block.GetFloat("_Fade"),Is.EqualTo(.37f));
            Assert.That(renderer.sharedMaterials,Is.EqualTo(original));
            controller.CollectSilhouetteRenderers(null,result); Assert.That(result,Is.Empty,"Only the gameplay camera");
            renderer.enabled=false; controller.CollectSilhouetteRenderers(camera,result); Assert.That(result,Is.Empty);
            renderer.enabled=true; state.GetType().GetField("currentFade").SetValue(state,1f);
            controller.CollectSilhouetteRenderers(camera,result); Assert.That(result,Is.Empty,"No work for restored renderers");
        }
        finally {Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);Object.DestroyImmediate(cameraObject);}
    }

    [TestCase("Assets/Game/Scenes/ConstructionSite.unity")]
    [TestCase("Assets/Game/Scenes/GamePoc.unity")]
    public void SharedRendererSetupIsIdempotentForGameplayScenes(string scenePath)
    {
        var scene=EditorSceneManager.OpenPreviewScene(scenePath);
        using var fixture = new DisposableTestAssets();
        try
        {
            var paths=new[]{"PC", "Mobile"}.Select(name => fixture.Copy($"Assets/Game/Settings/Rendering/{name}_Renderer.asset")).ToArray();
            foreach (var path in paths) CameraOcclusionSilhouetteSetup.Ensure(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path));
            var before=paths.Select(p=>AssetDatabase.LoadAssetAtPath<UniversalRendererData>(p).rendererFeatures.ToArray()).ToArray();
            foreach (var path in paths) CameraOcclusionSilhouetteSetup.Ensure(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path));
            for(int i=0;i<paths.Length;i++)
            {
                var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(paths[i]);
                Assert.That(data.rendererFeatures,Is.EqualTo(before[i]));
                Assert.That(data.rendererFeatures.Count(f=>f is CameraOcclusionSilhouetteFeature),Is.EqualTo(1));
                Assert.That(data.rendererFeatures.OfType<CameraOcclusionSilhouetteFeature>().Single().isActive,Is.True);
            }
            Assert.That(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Any(),Is.True);
        }
        finally {EditorSceneManager.ClosePreviewScene(scene);}
    }
}
