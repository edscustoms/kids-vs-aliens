using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[TestFixture, Category("Core")]
public sealed class AudioSceneSetupTests
{
    [TestCase("Assets/Game/Scenes/GamePoc.unity")]
    [TestCase("Assets/Game/Scenes/ConstructionSite.unity")]
    public void SceneAudioRepair_IsIdempotentAndPreservesReferences(string path)
    {
        Scene scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerCharacter>(true)).Single();
            AudioSceneSetup.ConfigureScene(player);
            var presenter = player.GetComponent<BeamAudioPresentation>();
            var firstEmitter = new SerializedObject(presenter).FindProperty("emitter").objectReferenceValue;
            int count = scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Component>(true).Length);
            AudioSceneSetup.ConfigureScene(player);
            Assert.That(scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Component>(true).Length), Is.EqualTo(count));
            Assert.That(new SerializedObject(presenter).FindProperty("emitter").objectReferenceValue, Is.SameAs(firstEmitter));
            Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioService>(true)).Count(), Is.EqualTo(1));
            Assert.That(((AudioEmitter)firstEmitter).GetComponent<AudioSource>().playOnAwake, Is.False);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
