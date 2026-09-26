using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[Category("AuditRemediation")]
public sealed class RepairPreservationTests
{
    [Test] public void ValidRendererRepairDoesNotFlushExistingDirtyState()
    {
        using var fixture = new DisposableTestAssets();
        string path = fixture.Copy("Assets/Game/Settings/Rendering/PC_Renderer.asset");
        var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(path);
        CameraOcclusionSilhouetteSetup.Ensure(renderer);
        byte[] before = File.ReadAllBytes(path);
        renderer.name = "Authored unsaved name"; EditorUtility.SetDirty(renderer);
        CameraOcclusionSilhouetteSetup.Ensure(renderer);
        CameraOcclusionSilhouetteSetup.Ensure(renderer);
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        Assert.That(EditorUtility.IsDirty(renderer), Is.True);
        Assert.That(renderer.name, Is.EqualTo("Authored unsaved name"));
    }

    [Test] public void RequiredArraysKeepAuthoredOrderAndExtrasWithoutDuplicates()
    {
        var root = new GameObject("Dependencies");
        var canvas = new GameObject("Authored", typeof(Canvas), typeof(GraphicRaycaster));
        var required = new GameObject("Required", typeof(Canvas), typeof(GraphicRaycaster));
        try
        {
            var filter = root.AddComponent<GameplayPointerInputFilter>();
            var suspension = root.AddComponent<GameplaySuspensionController>();
            var authored = root.AddComponent<AudioSource>();
            var shooter = root.AddComponent<PlayerShooter>();
            VerifyArray(filter, "blockingCanvases", canvas.GetComponent<GraphicRaycaster>(), required.GetComponent<GraphicRaycaster>());
            VerifyArray(suspension, "gameplayBehaviours", authored, shooter);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(canvas); Object.DestroyImmediate(required); }
    }
    private static void VerifyArray(Object target, string field, Object extra, Object required)
    {
        var serialized = new SerializedObject(target); var array = serialized.FindProperty(field);
        array.arraySize = 2; array.GetArrayElementAtIndex(0).objectReferenceValue = extra;
        array.GetArrayElementAtIndex(1).objectReferenceValue = extra; serialized.ApplyModifiedPropertiesWithoutUndo();
        var repair = typeof(GameplayPresentationSetup).GetMethod("ReferenceArray", BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < 2; i++)
        {
            repair.Invoke(null, new object[] { target, field, new[] { required } });
            serialized.Update(); Assert.That(array.arraySize, Is.EqualTo(2));
            Assert.That(array.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(extra));
            Assert.That(array.GetArrayElementAtIndex(1).objectReferenceValue, Is.SameAs(required));
        }
    }
    [Test] public void FloatingRepairPreservesAuthoredImporterAndIsIdempotent()
    {
        using var fixture = new DisposableTestAssets();
        var path = fixture.Copy(FloatingAnimationSetup.ClipPath);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        foreach (var clip in clips) clip.loopPose = false;
        importer.clipAnimations = clips; importer.SaveAndReimport();
        byte[] before = File.ReadAllBytes(path + ".meta");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(fixture.Folder + "/Test.controller");
        var motion = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        FloatingAnimationSetup.Ensure(controller, motion);
        byte[] first = File.ReadAllBytes(AssetDatabase.GetAssetPath(controller));
        FloatingAnimationSetup.Ensure(controller, motion);
        Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(before));
        Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(controller)), Is.EqualTo(first));
        Assert.That(controller.layers.Count(l => l.name == FloatingAnimationSetup.LayerName), Is.EqualTo(1));
    }
    [Test] public void DuplicateArrivalFailsPreflightWithoutSavingUnrelatedDirtyAsset()
    {
        using var fixture = new DisposableTestAssets();
        var previous = EditorSceneManager.GetSceneManagerSetup();
        var scene = EditorSceneManager.OpenScene(fixture.Copy("Assets/Game/Scenes/ConstructionSite.unity"));
        var dirty = ScriptableObject.CreateInstance<SkillData>();
        string path = fixture.Folder + "/Dirty.asset";
        AssetDatabase.CreateAsset(dirty, path); byte[] before = File.ReadAllBytes(path);
        dirty.name = "Intentionally unsaved"; EditorUtility.SetDirty(dirty);
        try
        {
            foreach (string name in new[] { "First", "Duplicate" })
            {
                var owner = new GameObject(name, typeof(PlayerBeamInSequence));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owner, scene);
            }
            Assert.Throws<System.InvalidOperationException>(() => GameplaySceneSetup.SetupOrRepairActiveScene());
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            Assert.That(EditorUtility.IsDirty(dirty), Is.True);
        }
        finally
        {
            if (previous.Length > 0 && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
