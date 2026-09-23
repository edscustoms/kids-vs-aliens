using System.Collections;
using System.Linq;
using System.Reflection;
using Cinemachine;
using NUnit.Framework;
using StarterAssets;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

[TestFixture, Category("Core")]
public sealed class InGameMenuTests
{
    [Test]
    public void NavigationKeepsOneLease_ResumeKeepsOtherOwners_AndTeardownClosesScreens()
    {
        float scale = Time.timeScale;
        var player = new GameObject(
            "Menu owner",
            typeof(StarterAssetsInputs),
            typeof(GameplaySuspensionController)
        );
        var root = new GameObject("Menu controller", typeof(InGameMenuController));
        var main = new GameObject("Menu screen");
        var options = new GameObject("Options screen");
        var owner = player.GetComponent<GameplaySuspensionController>();
        var menu = root.GetComponent<InGameMenuController>();
        menu.Configure(owner, main, options, null);
        Invoke(menu, "OnEnable");
        try
        {
            Assert.That(main.activeSelf || options.activeSelf, Is.False);
            var knowledge = owner.Acquire(SuspensionReason.KnowledgePresentation);
            menu.OpenMenu();
            Assert.That(menu.IsOpen, Is.False);
            knowledge.Dispose();
            menu.OpenMenu();
            menu.OpenMenu();
            Assert.That(owner.OwnerCount, Is.EqualTo(1));
            Assert.That(main.activeSelf, Is.True);
            menu.ShowOptions();
            Assert.That(options.activeSelf && !main.activeSelf, Is.True);
            menu.ShowMenu();
            Assert.That(main.activeSelf && !options.activeSelf, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            knowledge = owner.Acquire(SuspensionReason.KnowledgePresentation);
            menu.ResumeGame();
            Assert.That(owner.OwnerCount, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.Zero);
            knowledge.Dispose();
            Assert.That(Time.timeScale, Is.EqualTo(scale));
            menu.OpenMenu();
            owner.ReleaseAll();
            Assert.That(main.activeSelf || options.activeSelf || menu.IsOpen, Is.False);
        }
        finally
        {
            Invoke(menu, "OnDisable");
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(main);
            Object.DestroyImmediate(options);
            Object.DestroyImmediate(player);
            Time.timeScale = scale;
        }
    }

    [TestCase("ConstructionSite")]
    [TestCase("GamePoc")]
    public void SetupRepairsCallbacksWithoutDuplicatingOrRetuningControls(string name)
    {
        Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Game/Scenes/" + name + ".unity");
        try
        {
            var player = InScene<PlayerCharacter>(scene).Single();
            var presentation = scene
                .GetRootGameObjects()
                .Single(g => g.name == GameplayPresentationSetup.RootName);
            var first = InGameMenuSetup.ConfigureScene(player, presentation);
            int count = InScene<Component>(scene).Count();
            var resume = first
                .transform.Find("Screen_InGameMenu/ResumeButton")
                .GetComponent<UIButton>();
            var rect = (RectTransform)resume.transform;
            rect.sizeDelta = new Vector2(415, 107);
            resume.OnClick.RemoveAllListeners();
            Assert.That(InGameMenuSetup.ConfigureScene(player, presentation), Is.SameAs(first));
            Assert.That(InScene<Component>(scene).Count(), Is.EqualTo(count));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(415, 107)));
            Assert.That(resume.OnClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(
                resume.OnClick.GetPersistentMethodName(0),
                Is.EqualTo(nameof(InGameMenuController.ResumeGame))
            );
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [UnityTest]
    public IEnumerator ActualScenes_CameraOutputChangesWhilePaused_AndOnlyResumeUnblocksInput()
    {
        yield return new EnterPlayMode();
        bool hadPreference = PlayerPrefs.HasKey(GameplayCameraSettings.PreferenceKey);
        int saved = PlayerPrefs.GetInt(GameplayCameraSettings.PreferenceKey);
        try
        {
            foreach (string name in new[] { "ConstructionSite", "GamePoc" })
            {
                bool arrivalOwnedBeforeStart = false;
                // sceneLoaded runs after Awake and before Start/the first player Update.
                UnityEngine.Events.UnityAction<Scene, LoadSceneMode> observeStartup = (scene, mode) =>
                {
                    if (scene.name != "ConstructionSite") return;
                    var sequence = Object.FindAnyObjectByType<PlayerBeamInSequence>();
                    var data = new SerializedObject(sequence);
                    var start = sequence.ArrivalTransform;
                    var beam = Object.FindAnyObjectByType<BeamTransportController>();
                    arrivalOwnedBeforeStart = beam.IsTransporting
                        && !beam.GetComponent<CharacterController>().enabled
                        && beam.GetComponent<StarterAssetsInputs>().GameplayInputBlocked
                        && Vector3.Distance(beam.transform.position,
                            start.position + Vector3.up * data.FindProperty("startHeight").floatValue) < 0.001f;
                };
                SceneManager.sceneLoaded += observeStartup;
                try
                {
                    EditorSceneManager.LoadSceneInPlayMode(
                        "Assets/Game/Scenes/" + name + ".unity",
                        new LoadSceneParameters(LoadSceneMode.Single)
                    );
                    yield return null;
                    yield return null;
                }
                finally { SceneManager.sceneLoaded -= observeStartup; }
                if (name == "ConstructionSite") Assert.That(arrivalOwnedBeforeStart, Is.True);
                var menu = Object.FindAnyObjectByType<InGameMenuController>();
                var owner = Object.FindAnyObjectByType<GameplaySuspensionController>();
                var input = owner.GetComponent<StarterAssetsInputs>();
                var beamTransport = owner.GetComponent<BeamTransportController>();
                bool arrivalActive = beamTransport != null && beamTransport.IsTransporting;
                var presentation = GameObject.Find(GameplayPresentationSetup.RootName);
                var camera = Camera.main;
                Assert.That(menu, Is.Not.Null);
                Assert.That(Time.timeScale, Is.EqualTo(1));
                presentation
                    .transform.Find("SafeArea/PauseButton")
                    .GetComponent<UIButton>()
                    .OnClick.Invoke();
                Assert.That(menu.IsOpen && input.GameplayInputBlocked, Is.True);
                Assert.That(
                    presentation.transform.Find("SuspensionInputBlocker").gameObject.activeSelf,
                    Is.True
                );
                Assert.That(Time.timeScale, Is.Zero);
                input.PauseInput();
                Assert.That(menu.IsOpen, Is.True);
                menu.transform.Find("Screen_InGameMenu/OptionsButton")
                    .GetComponent<UIButton>()
                    .OnClick.Invoke();
                var options = menu.transform.Find("Screen_InGameOptions");
                var label = options.Find("CurrentCameraModeLabel").GetComponent<TMP_Text>();
                var previous = options.Find("PreviousCameraButton").GetComponent<UIButton>();
                var next = options.Find("NextCameraButton").GetComponent<UIButton>();
                GameplayCameraSettings.Mode = GameplayCameraMode.Action;
                yield return null;
                yield return null;
                Vector3 actionPosition = camera.transform.position;
                Vector3 playerPosition = owner.transform.position;
                previous.OnClick.Invoke();
                yield return null;
                yield return null;
                Assert.That(GameplayCameraSettings.Mode, Is.EqualTo(GameplayCameraMode.Isometric));
                Assert.That(label.text, Is.EqualTo("ISOMETRIC"));
                Assert.That(
                    camera.orthographic,
                    Is.True,
                    name + " output must change while paused"
                );
                Assert.That(
                    Vector3.Distance(actionPosition, camera.transform.position),
                    Is.GreaterThan(1)
                );
                next.OnClick.Invoke();
                yield return null;
                yield return null;
                Assert.That(GameplayCameraSettings.Mode, Is.EqualTo(GameplayCameraMode.Action));
                Assert.That(camera.orthographic, Is.False);
                next.OnClick.Invoke();
                yield return null;
                yield return null;
                Assert.That(GameplayCameraSettings.Mode, Is.EqualTo(GameplayCameraMode.Tactical));
                Assert.That(label.text, Is.EqualTo("TACTICAL"));
                Assert.That(
                    Vector3.Distance(actionPosition, camera.transform.position),
                    Is.GreaterThan(1)
                );
                Assert.That(
                    PlayerPrefs.GetInt(GameplayCameraSettings.PreferenceKey),
                    Is.EqualTo((int)GameplayCameraMode.Tactical)
                );
                Assert.That(owner.transform.position, Is.EqualTo(playerPosition));
                Assert.That(Time.timeScale, Is.Zero);
                // Simulated focus loss/return must not release the menu's lease.
                Invoke(input, "OnApplicationFocus", false);
                Invoke(input, "OnApplicationFocus", true);
                Assert.That(menu.IsOpen && input.GameplayInputBlocked, Is.True);
                options.Find("BackButton").GetComponent<UIButton>().OnClick.Invoke();
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(
                    menu.transform.Find("Screen_InGameMenu").gameObject.activeSelf,
                    Is.True
                );
                menu.transform.Find("Screen_InGameMenu/ResumeButton")
                    .GetComponent<UIButton>()
                    .OnClick.Invoke();
                Assert.That(Time.timeScale, Is.EqualTo(1));
                Assert.That(menu.IsOpen, Is.False);
                Assert.That(owner.IsSuspended, Is.EqualTo(arrivalActive));
                Assert.That(input.GameplayInputBlocked, Is.EqualTo(arrivalActive));
                Assert.That(
                    presentation.transform.Find("SuspensionInputBlocker").gameObject.activeSelf,
                    Is.EqualTo(arrivalActive)
                );
                // Resuming a menu cannot release a still-active arrival's lease.
                float deadline = Time.realtimeSinceStartup + 10f;
                while (beamTransport != null && beamTransport.IsTransporting && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(owner.IsSuspended || input.GameplayInputBlocked, Is.False);
            }
        }
        finally
        {
            if (hadPreference)
                PlayerPrefs.SetInt(GameplayCameraSettings.PreferenceKey, saved);
            else
                PlayerPrefs.DeleteKey(GameplayCameraSettings.PreferenceKey);
            PlayerPrefs.Save();
            Time.timeScale = 1;
        }
        yield return new ExitPlayMode();
    }

    private static T[] InScene<T>(Scene scene)
        where T : Component =>
        scene
            .GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();

    private static void Invoke(object target, string method, params object[] args) =>
        target
            .GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(target, args);
}
