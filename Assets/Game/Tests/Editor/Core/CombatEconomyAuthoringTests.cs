using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[Category("CombatEconomy")]
public sealed class CombatEconomyAuthoringTests
{
    [TestCase("GamePoc")]
    [TestCase("ConstructionSite")]
    public void SceneEconomyRepairPreservesAuthoredLootAndRequiredInterface(string name)
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Game/Scenes/" + name + ".unity");
        try
        {
            var actors = scene
                .GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<EnemyEquipment>(true))
                .ToArray();
            foreach (var actor in actors)
            {
                var loot = actor.GetComponent<EnemyPlasmaLoot>();
                var data = new SerializedObject(loot);
                data.FindProperty("plasmaDropChance").floatValue = .23f;
                data.ApplyModifiedPropertiesWithoutUndo();
                string before = EditorJsonUtility.ToJson(loot);
                Assert.That(EditorJsonUtility.ToJson(loot), Is.EqualTo(before));
                Assert.That(actor.GetComponents<EnemyPlasmaLoot>().Length, Is.EqualTo(1));
            }
            Assert.That(
                scene
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<GameplayInterface>(true))
                    .Count(),
                Is.EqualTo(1)
            );
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void CapsulePrefabsRenderDistinctShellsAndCoresWithoutRuntimeMaterialCopies()
    {
        var preview = new PreviewRenderUtility();
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            string[] paths =
            {
                CombatEconomyTests.PlasmaPrefabPath,
                CombatEconomyTests.ArmorPrefabPath,
            };
            for (int i = 0; i < paths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                var root = Object.Instantiate(prefab);
                preview.AddSingleGO(root);
                root.transform.position = new Vector3(i == 0 ? -.38f : .38f, 0, 0);
                Assert.That(root.GetComponent<PickupItem>().Item, Is.TypeOf<CapsuleItemData>());
                Assert.That(root.GetComponent<SphereCollider>().isTrigger, Is.True);
                var shell = root.transform.Find("Translucent shell").GetComponent<Renderer>();
                var core = root.transform.Find("Energy core").GetComponent<Renderer>();
                Assert.That(shell.sharedMaterial.GetFloat("_Surface"), Is.EqualTo(1));
                Assert.That(core.sharedMaterial.IsKeywordEnabled("_EMISSION"), Is.True);
                Assert.That(AssetDatabase.Contains(core.sharedMaterial), Is.True);
                Assert.That(
                    ShaderUtil
                        .GetShaderMessages(core.sharedMaterial.shader)
                        .Any(m =>
                            m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error
                        ),
                    Is.False
                );
            }
            var camera = preview.camera;
            camera.transform.position = new Vector3(0, .25f, -3);
            camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true;
            camera.orthographicSize = .7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.008f, .012f, .025f);
            preview.lights[0].enabled = true;
            preview.lights[0].intensity = 1.5f;
            preview.lights[0].transform.rotation = Quaternion.Euler(30, -30, 0);
            preview.BeginPreview(new Rect(0, 0, 768, 512), GUIStyle.none);
            preview.Render(true);
            RenderTexture.active = (RenderTexture)preview.EndPreview();
            image = new Texture2D(768, 512, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 768, 512), 0, 0);
            image.Apply();
            System.IO.Directory.CreateDirectory("Logs/CombatEconomy");
            System.IO.File.WriteAllBytes("Logs/CombatEconomy/capsules.png", image.EncodeToPNG());
            var pixels = image.GetPixels32();
            Assert.That(pixels.Count(p => p.b > 100 && p.b > p.r * 2), Is.GreaterThan(100));
            Assert.That(pixels.Count(p => p.g > 100 && p.g > p.b * 2), Is.GreaterThan(100));
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(image);
            preview.Cleanup();
        }
    }
}
