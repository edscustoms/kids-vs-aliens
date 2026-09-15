using System.Linq;
using Cinemachine;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[TestFixture, Category("Core")]
public sealed class MenuCameraTests
{
    private static void InvokeLifecycle(UISegmentedControl control, string method) =>
        typeof(UISegmentedControl)
            .GetMethod(
                method,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )
            .Invoke(control, null);

    [Test]
    public void Segments_KeepOneSelection_AndDoNotDuplicateCallbacksAfterReenable()
    {
        var root = new GameObject("Segments");
        try
        {
            root.SetActive(false);
            var control = root.AddComponent<UISegmentedControl>();
            var options = new UISegmentedControl.Option[4];
            for (int i = 0; i < options.Length; i++)
            {
                var go = new GameObject(
                    "Option " + i,
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button),
                    typeof(UIButton)
                );
                go.transform.SetParent(root.transform);
                options[i] = new UISegmentedControl.Option
                {
                    id = "value " + i,
                    button = go.GetComponent<UIButton>(),
                };
            }
            control.Configure(options);
            int changes = 0;
            control.SelectionChanged.AddListener(_ => changes++);
            root.SetActive(true);
            // EditMode does not dispatch MonoBehaviour lifecycle callbacks.
            InvokeLifecycle(control, "OnEnable");
            options[2].button.OnClick.Invoke();
            options[2].button.OnClick.Invoke();
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(options.Count(o => o.button.Selected), Is.EqualTo(1));
            Assert.That(control.SelectedId, Is.EqualTo("value 2"));
            root.SetActive(false);
            InvokeLifecycle(control, "OnDisable");
            root.SetActive(true);
            InvokeLifecycle(control, "OnEnable");
            options[3].button.OnClick.Invoke();
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(options.Count(o => o.button.Selected), Is.EqualTo(1));
            control.Select(-1, false);
            Assert.That(options[0].button.Selected, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void Preference_RoundTripsEveryMode_AndInvalidDataFallsBackToAction()
    {
        bool existed = PlayerPrefs.HasKey(GameplayCameraSettings.PreferenceKey);
        int saved = PlayerPrefs.GetInt(GameplayCameraSettings.PreferenceKey);
        try
        {
            foreach (GameplayCameraMode mode in System.Enum.GetValues(typeof(GameplayCameraMode)))
            {
                GameplayCameraSettings.Mode = mode;
                Assert.That(
                    PlayerPrefs.GetInt(GameplayCameraSettings.PreferenceKey),
                    Is.EqualTo((int)mode)
                );
                Assert.That(GameplayCameraSettings.Mode, Is.EqualTo(mode));
            }
            PlayerPrefs.SetInt(GameplayCameraSettings.PreferenceKey, 100);
            Assert.That(GameplayCameraSettings.Mode, Is.EqualTo(GameplayCameraMode.Action));
        }
        finally
        {
            if (existed)
                PlayerPrefs.SetInt(GameplayCameraSettings.PreferenceKey, saved);
            else
                PlayerPrefs.DeleteKey(GameplayCameraSettings.PreferenceKey);
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void Presets_RestoreAuthoredActionIncludingProjection_AfterIsometric()
    {
        var output = new GameObject("Output", typeof(Camera), typeof(CinemachineBrain));
        var root = new GameObject("Rig");
        var target = new GameObject("Target");
        var profile = ScriptableObject.CreateInstance<GameplayCameraProfile>();
        try
        {
            root.SetActive(false);
            var rig = root.AddComponent<CinemachineVirtualCamera>();
            var body = rig.AddCinemachineComponent<CinemachineTransposer>();
            rig.Follow = target.transform;
            body.m_FollowOffset = new Vector3(0, 3, -7);
            body.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
            root.transform.rotation = Quaternion.Euler(30, 0, 0);
            var controller = root.AddComponent<GameplayCameraController>();
            controller.Configure(profile);
            var originalLens = rig.m_Lens;
            controller.Apply(GameplayCameraMode.Tactical);
            Assert.That(
                body.m_FollowOffset.magnitude,
                Is.GreaterThan(new Vector3(0, 3, -7).magnitude)
            );
            controller.Apply(GameplayCameraMode.Isometric);
            Assert.That(
                rig.m_Lens.ModeOverride,
                Is.EqualTo(LensSettings.OverrideModes.Orthographic)
            );
            Assert.That(
                Vector3.Dot(root.transform.forward, new Vector3(1, -1, 1).normalized),
                Is.GreaterThan(0.99999f)
            );
            // Simulate the brain having pushed the orthographic mode to its output.
            output.GetComponent<Camera>().orthographic = true;
            controller.Apply(GameplayCameraMode.Action);
            Assert.That(output.GetComponent<Camera>().orthographic, Is.False);
            Assert.That(body.m_FollowOffset, Is.EqualTo(new Vector3(0, 3, -7)));
            Assert.That(rig.m_Lens.FieldOfView, Is.EqualTo(originalLens.FieldOfView));
            Assert.That(rig.m_Lens.ModeOverride, Is.EqualTo(originalLens.ModeOverride));
            Assert.That(
                Quaternion.Angle(root.transform.rotation, Quaternion.Euler(30, 0, 0)),
                Is.LessThan(0.001f)
            );
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(output);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void SavedMenu_UsesPrefabVariants_AndExternalRoutingCallbacks()
    {
        Scene scene = EditorSceneManager.OpenPreviewScene(MenuUISetup.MenuPath);
        try
        {
            var canvas = scene
                .GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Canvas>(true))
                .Single(c => c.name == "MenuCanvas");
            var main = canvas.transform.Find("Screen_MainMenu");
            var options = canvas.transform.Find("Screen_Options");
            Assert.That(main, Is.Not.Null);
            Assert.That(options, Is.Not.Null);
            foreach (UIButton button in main.GetComponentsInChildren<UIButton>(true))
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    button.gameObject
                );
                Assert.That(
                    path == MenuUISetup.PillPath || path == MenuUISetup.CirclePath,
                    Is.True,
                    button.name
                );
            }
            var open = main.Find("OptionsButton").GetComponent<UIButton>();
            var back = options.Find("BackButton").GetComponent<UIButton>();
            var router = canvas.GetComponent<UIScreenRouter>();
            Assert.That(open.OnClick.GetPersistentTarget(0), Is.SameAs(router));
            Assert.That(
                open.OnClick.GetPersistentMethodName(0),
                Is.EqualTo(nameof(UIScreenRouter.ShowOptions))
            );
            Assert.That(
                back.OnClick.GetPersistentMethodName(0),
                Is.EqualTo(nameof(UIScreenRouter.ShowMainMenu))
            );
            router.ShowOptions();
            Assert.That(main.gameObject.activeSelf, Is.False);
            Assert.That(options.gameObject.activeSelf, Is.True);
            router.ShowMainMenu();
            Assert.That(main.gameObject.activeSelf, Is.True);
            Assert.That(options.gameObject.activeSelf, Is.False);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
