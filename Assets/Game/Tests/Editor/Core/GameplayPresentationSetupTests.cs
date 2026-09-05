using System.Linq;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[TestFixture, Category("Core")]
public sealed class GameplayPresentationSetupTests
{
    [Test]
    public void SetupIsIdempotent_AndReferencesOneIsolatedPreview()
    {
        Scene previous = SceneManager.GetActiveScene();
        // A transient Editor preview scene is isolated even when the user's
        // active scene is untitled/unsaved. It never becomes a scene asset.
        Scene scene = EditorSceneManager.NewPreviewScene();
        var player = new GameObject("Setup test player");
        SceneManager.MoveGameObjectToScene(player, scene);
        player.AddComponent<StarterAssetsInputs>();
        player.AddComponent<PlayerSkillState>();
        PlayerCharacter character = player.AddComponent<PlayerCharacter>();
        player.AddComponent<PlayerGrenadeController>();
        player.AddComponent<PlayerPrimaryActionRouter>();
        try
        {
            GameObject first = GameplayPresentationSetup.ConfigureScene(character);
            int components = first.GetComponentsInChildren<Component>(true).Length;
            int playerComponents = player.GetComponents<Component>().Length;
            GameObject second = GameplayPresentationSetup.ConfigureScene(character);
            Assert.That(second, Is.SameAs(first));
            Assert.That(
                second.GetComponentsInChildren<Component>(true).Length,
                Is.EqualTo(components)
            );
            Assert.That(player.GetComponents<Component>().Length, Is.EqualTo(playerComponents));
            Camera[] cameras = second.GetComponentsInChildren<Camera>(true);
            Assert.That(cameras.Length, Is.EqualTo(1));
            Assert.That(cameras[0].enabled, Is.False);
            Assert.That(
                cameras[0].targetTexture,
                Is.Null,
                "Runtime texture must not allocate while hidden."
            );
            Assert.That(
                cameras[0].cullingMask,
                Is.EqualTo(1 << LayerMask.NameToLayer("KnowledgePreview"))
            );
            Assert.That(second.transform.Find("KnowledgeOverlay").gameObject.activeSelf, Is.False);
            Assert.That(
                scene
                    .GetRootGameObjects()
                    .Count(go => go.name == GameplayPresentationSetup.RootName),
                Is.EqualTo(1)
            );
            Assert.That(player.GetComponents<GameplaySuspensionController>().Length, Is.EqualTo(1));
            var suspended = new SerializedObject(
                player.GetComponent<GameplaySuspensionController>()
            ).FindProperty("gameplayBehaviours");
            for (int i = 0; i < suspended.arraySize; i++)
                Assert.That(
                    suspended.GetArrayElementAtIndex(i).objectReferenceValue,
                    Is.Not.InstanceOf<PlayerGrenadeController>()
                        .And.Not.InstanceOf<PlayerPrimaryActionRouter>()
                );
            var stage = second.GetComponent<KnowledgePreviewStage>();
            foreach (string name in new[] { "PC", "Mobile" })
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                    $"Assets/Game/Settings/Rendering/{name}_RPAsset.asset"
                );
                Assert.That(stage.TrySelectRenderer(pipeline), Is.True);
                int index = new SerializedObject(cameras[0].GetUniversalAdditionalCameraData())
                    .FindProperty("m_RendererIndex")
                    .intValue;
                var renderer = pipeline.rendererDataList[index];
                Assert.That(
                    AssetDatabase.GetAssetPath(renderer),
                    Is.EqualTo(GameplayPresentationSetup.LightweightRendererPath)
                );
                Assert.That(
                    renderer.rendererFeatures,
                    Is.Empty,
                    "Post-processing off does not disable renderer features such as SSAO."
                );
                Assert.That(
                    new SerializedObject(pipeline).FindProperty("m_DefaultRendererIndex").intValue,
                    Is.Zero
                );
            }
            Assert.That(scene.path, Is.Empty, "Setup must not save the scene.");
            Assert.That(second.GetComponentsInChildren<SafeAreaPanel>(true).Length, Is.EqualTo(2));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
        }
    }

    [Test]
    public void DesktopPauseUsesExistingPlayerInputMap()
    {
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/StarterAssets/InputSystem/StarterAssets.inputactions"
        );
        var pause = actions.FindAction("Player/Pause", true);
        Assert.That(pause.bindings.Any(binding => binding.path == "<Keyboard>/escape"), Is.True);
    }

    [TestCase("PistolHandling")]
    [TestCase("RifleHandling")]
    [TestCase("GrenadeHandling")]
    public void InitialKnowledgeReferencesActualDataAndStance(string skillName)
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillData>(
            $"Assets/Game/Data/Progression/{skillName}.asset"
        );
        Assert.That(skill, Is.Not.Null);
        Assert.That(skill.TutorialData, Is.Not.Null);
        Assert.That(skill.TutorialData.equipment, Is.Not.Null);
        Assert.That(skill.TutorialData.action, Is.EqualTo(CharacterActionId.EquippedStance));
        Assert.That(skill.TutorialData.instructions, Is.Not.Empty);
    }
}
