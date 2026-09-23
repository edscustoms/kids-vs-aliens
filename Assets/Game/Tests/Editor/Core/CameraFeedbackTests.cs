using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[TestFixture, Category("Core")]
public sealed class CameraFeedbackTests
{
    private readonly List<Object> owned = new();
    private bool hadPreference;
    private int savedPreference;
    private float savedTimeScale;
    private static CameraFeedbackProfile Profile(string name) => AssetDatabase.LoadAssetAtPath<CameraFeedbackProfile>("Assets/Game/Resources/CameraFeedback/" + name + ".asset");
    private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static int Count(CameraFeedbackController c) => (int)c.GetType().GetField("count", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c);

    [SetUp] public void SetUp()
    {
        hadPreference = PlayerPrefs.HasKey(CameraFeedbackSettings.PreferenceKey);
        savedPreference = PlayerPrefs.GetInt(CameraFeedbackSettings.PreferenceKey);
        savedTimeScale = Time.timeScale; Time.timeScale = 1;
        CameraFeedbackSettings.Enabled = true;
    }
    [TearDown] public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
        {
            if (owned[i] == null) continue;
            // These EditMode fixtures invoke lifecycle manually; Unity does not pair their OnDisable.
            if (owned[i] is GameObject root)
            {
                foreach (var c in root.GetComponentsInChildren<CameraFeedbackController>(true)) Invoke(c, "OnDisable");
                foreach (var c in root.GetComponentsInChildren<CameraShakeOptionView>(true)) Invoke(c, "OnDisable");
            }
            Object.DestroyImmediate(owned[i]);
        }
        owned.Clear(); Time.timeScale = savedTimeScale;
        if (hadPreference) PlayerPrefs.SetInt(CameraFeedbackSettings.PreferenceKey, savedPreference);
        else PlayerPrefs.DeleteKey(CameraFeedbackSettings.PreferenceKey);
        PlayerPrefs.Save();
    }

    private CameraFeedbackController Create(out Camera camera, out ThirdPersonController player, out GameplaySuspensionController suspension)
    {
        var root = new GameObject("Feedback player", typeof(StarterAssetsInputs), typeof(ThirdPersonController), typeof(GameplaySuspensionController));
        owned.Add(root); player = root.GetComponent<ThirdPersonController>(); suspension = root.GetComponent<GameplaySuspensionController>();
        var cameraRoot = new GameObject("Feedback camera", typeof(Camera)); owned.Add(cameraRoot);
        camera = cameraRoot.GetComponent<Camera>();
        camera.transform.SetPositionAndRotation(new Vector3(1, 4, -6), Quaternion.Euler(35, 10, 0));
        var feedback = cameraRoot.AddComponent<CameraFeedbackController>();
        Invoke(feedback, "OnDisable"); Invoke(feedback, "OnEnable"); feedback.Configure(player, suspension);
        return feedback;
    }

    [Test] public void RenderPose_RestoresExactly_DoesNotDrift_AndExpires()
    {
        var feedback = Create(out var camera, out var player, out _);
        Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        Ray ray = camera.ScreenPointToRay(new Vector3(100, 100));
        Vector3 playerPosition = player.transform.position;
        var random = Random.state;
        for (int i = 0; i < 40; i++) CameraFeedbackService.Play(Profile("RifleFire"));
        Assert.That(Count(feedback), Is.LessThanOrEqualTo(16));
        Assert.That(camera.transform.position, Is.EqualTo(position)); Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
        Assert.That(Random.state, Is.EqualTo(random), "Controlled recoil must not consume gameplay random numbers.");
        for (int i = 0; i < 5; i++)
        {
            Invoke(feedback, "BeginRender", default(ScriptableRenderContext), camera);
            Assert.That(Quaternion.Angle(rotation, camera.transform.rotation), Is.GreaterThan(.01f));
            Assert.That(Quaternion.Angle(rotation, camera.transform.rotation), Is.LessThanOrEqualTo(1.51f));
            Invoke(feedback, "EndRender", default(ScriptableRenderContext), camera);
            Assert.That(camera.transform.rotation, Is.EqualTo(rotation)); Assert.That(camera.transform.position, Is.EqualTo(position));
        }
        Ray after = camera.ScreenPointToRay(new Vector3(100, 100));
        Assert.That(after.origin, Is.EqualTo(ray.origin)); Assert.That(after.direction, Is.EqualTo(ray.direction));
        Assert.That(player.transform.position, Is.EqualTo(playerPosition));
        // Exercise finite expiry without depending on Editor wall-clock frame timing.
        Invoke(feedback, "Evaluate", Time.unscaledTime + 2, Vector3.zero, Vector3.zero);
        Invoke(feedback, "BeginRender", default(ScriptableRenderContext), camera);
        Assert.That(Count(feedback), Is.Zero); Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
        // Subsequent Cinemachine movement is the next effect's base, never the old authored pose.
        camera.transform.position += Vector3.right; position = camera.transform.position;
        CameraFeedbackService.Play(Profile("PlayerDamage"));
        Invoke(feedback, "BeginRender", default(ScriptableRenderContext), camera);
        Invoke(feedback, "EndRender", default(ScriptableRenderContext), camera);
        Assert.That(camera.transform.position, Is.EqualTo(position));
    }

    [TestCase("setting")]
    [TestCase("pause")]
    [TestCase("background")]
    [TestCase("focus")]
    [TestCase("disable")]
    [TestCase("mode")]
    public void Interruptions_ClearOffsetsAndNeverReplay(string reason)
    {
        var feedback = Create(out var camera, out _, out var suspension);
        Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        CameraFeedbackService.Play(Profile("PlayerDamage"));
        Invoke(feedback, "BeginRender", default(ScriptableRenderContext), camera);
        Assert.That(camera.transform.position, Is.Not.EqualTo(position));
        switch (reason)
        {
            case "setting": CameraFeedbackSettings.Enabled = false; CameraFeedbackSettings.Enabled = true; break;
            case "pause": var lease = suspension.Acquire(SuspensionReason.ManualPause); lease.Dispose(); break;
            case "background": Invoke(feedback, "OnApplicationPause", true); Invoke(feedback, "OnApplicationPause", false); break;
            case "focus": Invoke(feedback, "OnApplicationFocus", false); Invoke(feedback, "OnApplicationFocus", true); break;
            case "disable": Invoke(feedback, "OnDisable"); Invoke(feedback, "OnEnable"); break;
            case "mode": Invoke(feedback, "OnCameraMode", GameplayCameraMode.Tactical); break;
        }
        Assert.That(camera.transform.position, Is.EqualTo(position)); Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
        Assert.That(Count(feedback), Is.Zero);
        Invoke(feedback, "BeginRender", default(ScriptableRenderContext), camera);
        Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
    }

    [Test] public void LandingThresholds_IgnoreStepsAndSmallJumps_AndScaleHardFalls()
    {
        var values = new List<float>();
        System.Action<CameraFeedbackProfile, float, Vector3> record = (profile, strength, direction) =>
        { Assert.That(profile, Is.SameAs(Profile("HardLanding"))); values.Add(strength); };
        CameraFeedbackService.Requested += record;
        try
        {
            foreach (float speed in new[] { -5f, 0f, 2f, 6f, 8f }) CameraFeedbackService.Landed(speed);
            Assert.That(values, Is.Empty);
            CameraFeedbackService.Landed(10); CameraFeedbackService.Landed(16); CameraFeedbackService.Landed(30);
            CollectionAssert.AreEqual(new[] { .25f, 1f, 1f }, values);
        }
        finally { CameraFeedbackService.Requested -= record; }
    }

    [TestCase(false)] [TestCase(true)]
    public void Setting_DefaultsOn_PersistsAndBothOptionBuildersAreSilentAndIdempotent(bool inGame)
    {
        PlayerPrefs.DeleteKey(CameraFeedbackSettings.PreferenceKey); Assert.That(CameraFeedbackSettings.Enabled, Is.True);
        int requests = 0; System.Action<CameraFeedbackProfile, float, Vector3> record = (_, __, ___) => requests++;
        CameraFeedbackService.Requested += record;
        try
        {
            var root = new GameObject("Options", typeof(RectTransform)); owned.Add(root);
            HapticsOptionView.Ensure(root.transform, inGame);
            CameraShakeOptionView.Ensure(root.transform, inGame); int children = root.transform.childCount;
            CameraShakeOptionView.Ensure(root.transform, inGame); Assert.That(root.transform.childCount, Is.EqualTo(children));
            var view = root.GetComponentInChildren<CameraShakeOptionView>(); Invoke(view, "OnDisable"); Invoke(view, "OnEnable");
            view.GetComponent<Button>().onClick.Invoke();
            Assert.That(PlayerPrefs.GetInt(CameraFeedbackSettings.PreferenceKey), Is.Zero); Assert.That(CameraFeedbackSettings.Enabled, Is.False);
            Assert.That(view.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("CAMERA SHAKE: OFF"));
            Assert.That(requests, Is.Zero);
        }
        finally { CameraFeedbackService.Requested -= record; }
    }

    [Test] public void Damage_RejectsNonFiniteValues_AndUsesExistingHitDirection()
    {
        var root = new GameObject("Damage direction", typeof(PlayerHealth)); owned.Add(root);
        var health = root.GetComponent<PlayerHealth>(); Invoke(health, "Awake");
        var directions = new List<Vector3>();
        System.Action<CameraFeedbackProfile, float, Vector3> record = (profile, strength, direction) => directions.Add(direction);
        CameraFeedbackService.Requested += record;
        try
        {
            health.TakeDamage(float.NaN); health.TakeDamage(float.PositiveInfinity); health.TakeDamage(float.NegativeInfinity);
            Assert.That(directions, Is.Empty); Assert.That(health.CurrentHealth, Is.EqualTo(100)); Assert.That(health.CurrentArmor, Is.EqualTo(50));
            health.ReceiveDamage(new HitInfo(10, Vector3.zero, Vector3.up, Vector3.right, root));
            health.TakeDamage(10);
            CollectionAssert.AreEqual(new[] { Vector3.right, Vector3.zero }, directions);
            Assert.That(health.CurrentArmor, Is.EqualTo(30));
        }
        finally { CameraFeedbackService.Requested -= record; }
    }

    [TestCase("GamePoc")] [TestCase("ConstructionSite")]
    public void SceneRepair_IsIdempotentAndWiresLocalMovementAndSuspension(string name)
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Game/Scenes/" + name + ".unity");
        try
        {
            var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerCharacter>(true)).Single();
            var first = CameraFeedbackSetup.ConfigureScene(player);
            Assert.That(CameraFeedbackSetup.ConfigureScene(player), Is.SameAs(first));
            Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CameraFeedbackController>(true)).Count(), Is.EqualTo(1));
            var serialized = new SerializedObject(first);
            Assert.That(serialized.FindProperty("player").objectReferenceValue, Is.SameAs(player.GetComponent<ThirdPersonController>()));
            Assert.That(serialized.FindProperty("suspension").objectReferenceValue, Is.SameAs(player.GetComponent<GameplaySuspensionController>()));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [UnityTest] public IEnumerator ActualCollisionLanding_UsesPreCollisionDownwardVelocity()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        float savedCaptureDelta = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f / 60f;
        try
        {
        var feedback = Create(out var camera, out var player, out _);
        camera.tag = "MainCamera";
        Set(player, "_mainCamera", camera.gameObject);
        player.CinemachineCameraTarget = new GameObject("Camera target"); owned.Add(player.CinemachineCameraTarget);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); owned.Add(ground);
        ground.transform.position = new Vector3(0, -.5f, 0); ground.transform.localScale = new Vector3(40, 1, 40);
        var capsule = player.GetComponent<CharacterController>(); capsule.center = Vector3.up; capsule.height = 2; capsule.radius = .3f;
        foreach (float height in new[] { 1.2f, 10f })
        {
            capsule.enabled = false; player.transform.position = Vector3.up * height; capsule.enabled = true;
            player.ResetMotion(); player.Grounded = false;
            float landingSpeed = 0; int landings = 0;
            System.Action<float> landed = speed => { landingSpeed = speed; landings++; };
            player.Landed += landed;
            float deadline = Time.realtimeSinceStartup + 8;
            while (landings == 0 && Time.realtimeSinceStartup < deadline) yield return EditorTestFrame.Next();
            player.Landed -= landed;
            Assert.That(landings, Is.EqualTo(1)); Assert.That(player.Grounded, Is.True);
            Assert.That(landingSpeed, height < 2 ? Is.LessThan(8f) : Is.GreaterThan(12f));
            Assert.That(CameraFeedbackService.Config.LandingStrength(landingSpeed), height < 2 ? Is.Zero : Is.GreaterThan(.5f));
        }
        VerifyUrpRender(feedback, camera);
        }
        finally { Time.captureDeltaTime = savedCaptureDelta; }
        yield return new ExitPlayMode();
    }

    private static void VerifyUrpRender(CameraFeedbackController feedback, Camera camera)
    {
        Invoke(feedback, "OnApplicationFocus", true);
        var target = new RenderTexture(64, 64, 24); target.Create();
        var profile = Profile("PistolFire");
        var original = camera.transform.rotation; var ray = camera.ScreenPointToRay(Vector3.one * 20);
        bool sawKick = false;
        System.Action<ScriptableRenderContext, Camera> observe = (context, rendered) =>
        { if (rendered == camera) sawKick = Quaternion.Angle(original, rendered.transform.rotation) > .01f; };
        RenderPipelineManager.beginCameraRendering += observe;
        try
        {
            CameraFeedbackService.Play(profile);
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            Assert.That(sawKick, Is.True, "URP must observe the temporary render pose.");
            Assert.That(camera.transform.rotation, Is.EqualTo(original));
            Assert.That(camera.ScreenPointToRay(Vector3.one * 20).direction, Is.EqualTo(ray.direction));
        }
        finally { RenderPipelineManager.beginCameraRendering -= observe; target.Release(); Object.DestroyImmediate(target); }
    }

    [UnityTearDown] public IEnumerator ExitAfterFailure() { if (Application.isPlaying) yield return new ExitPlayMode(); }
}
