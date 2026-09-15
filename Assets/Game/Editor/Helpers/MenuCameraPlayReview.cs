using System;
using System.IO;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Opt-in batch Play Mode smoke test; never saves scenes or changes the player's preference.</summary>
[InitializeOnLoad]
public static class MenuCameraPlayReview
{
    private const string Active = "MenuCameraReview.Active";
    private static int stage,
        nextFrame;

    static MenuCameraPlayReview()
    {
        EditorApplication.update += Update;
    }

    public static void Begin()
    {
        SessionState.SetBool(Active, true);
        SessionState.SetBool(
            Active + ".HadPreference",
            PlayerPrefs.HasKey(GameplayCameraSettings.PreferenceKey)
        );
        SessionState.SetInt(
            Active + ".Preference",
            PlayerPrefs.GetInt(GameplayCameraSettings.PreferenceKey)
        );
        Directory.CreateDirectory("Logs/MenuCameraTask");
        EditorSceneManager.OpenScene(MenuUISetup.MenuPath);
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (
            !SessionState.GetBool(Active, false)
            || !EditorApplication.isPlaying
            || Time.frameCount < nextFrame
        )
            return;
        nextFrame = Time.frameCount + 20;
        try
        {
            if (stage == 0)
            {
                stage++;
                return;
            }
            if (stage == 1)
            {
                CaptureMenu("main-menu");
                stage++;
                return;
            }
            if (stage == 2)
            {
                Find<UIButton>("OptionsButton").OnClick.Invoke();
                Check(
                    GameObject.Find("Screen_Options") != null
                        && GameObject.Find("Screen_MainMenu") == null,
                    "Options did not route correctly"
                );
                Find<UIButton>("TACTICAL").OnClick.Invoke();
                Check(
                    GameplayCameraSettings.Mode == GameplayCameraMode.Tactical,
                    "Tactical UI click did not persist"
                );
                Check(
                    UnityEngine.Object.FindObjectsByType<UIButton>().Count(b => b.Selected) == 1,
                    "Selection visuals are not exclusive"
                );
                stage++;
                return;
            }
            if (stage == 3)
            {
                CaptureMenu("options");
                stage++;
                return;
            }
            if (stage == 4)
            {
                Find<UIButton>("BackButton").OnClick.Invoke();
                Check(
                    GameObject.Find("Screen_MainMenu") != null
                        && GameObject.Find("Screen_Options") == null,
                    "Back did not route correctly"
                );
                // Exercise carousel, selection, loadout preview, and the existing Exit-as-Back callback.
                var main = GameObject.Find("Screen_MainMenu");
                foreach (
                    var button in main.GetComponentsInChildren<UIButton>()
                        .Where(b => b.name == "NextButton" || b.name == "PreviousButton")
                )
                    button.OnClick.Invoke();
                Find<UIButton>("SelectButton").OnClick.Invoke();
                Find<UIButton>("PreviewButton").OnClick.Invoke();
                Check(Find<UIButton>("ExitButton").Text == "BACK", "Loadout preview did not enter");
                Find<UIButton>("ExitButton").OnClick.Invoke();
                Check(
                    Find<UIButton>("ExitButton").Text == "EXIT",
                    "Loadout preview did not return"
                );
                Find<UIButton>("PlayButton").OnClick.Invoke();
                stage++;
                return;
            }
            if (stage == 5)
            {
                Check(
                    SceneManager.GetActiveScene().name == "ConstructionSite",
                    "Play did not load ConstructionSite"
                );
                var controller = UnityEngine.Object.FindAnyObjectByType<GameplayCameraController>();
                Check(controller != null, "Gameplay camera controller missing");
                var rig = controller.GetComponent<CinemachineVirtualCamera>();
                Check(
                    Mathf.Approximately(rig.m_Lens.FieldOfView, 45),
                    "Stored Tactical preference was not applied"
                );
                Check(
                    GameObject.Find("GameplayPresentationV1") != null,
                    "Gameplay presentation missing"
                );
                GameplayCameraSettings.Mode = GameplayCameraMode.Isometric;
                stage++;
                return;
            }
            if (stage == 6)
            {
                Check(Camera.main.orthographic, "Cinemachine output is not orthographic");
                CaptureCamera(Camera.main, "isometric");
                stage++;
                return;
            }
            if (stage == 7)
            {
                GameplayCameraSettings.Mode = GameplayCameraMode.Action;
                stage++;
                return;
            }
            if (stage == 8)
            {
                Check(!Camera.main.orthographic, "Action did not restore perspective output");
                CaptureCamera(Camera.main, "action");
                stage++;
                return;
            }
            Debug.Log(
                "PLAY REVIEW PASSED: Options/Back, selection/persistence, carousel/Select/Preview/Exit-back, Play to ConstructionSite, camera output projection and Action restoration."
            );
            Finish(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    private static T Find<T>(string name)
        where T : Component =>
        UnityEngine.Object.FindObjectsByType<T>().Single(c => c.name == name);

    private static void CaptureMenu(string name)
    {
        var canvas = Find<Canvas>("MenuCanvas");
        var go = new GameObject("UI review camera", typeof(Camera));
        var camera = go.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << canvas.gameObject.layer;
        camera.orthographic = true;
        var mode = canvas.renderMode;
        var originalCamera = canvas.worldCamera;
        float distance = canvas.planeDistance;
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            CaptureCamera(camera, name);
        }
        finally
        {
            canvas.renderMode = mode;
            canvas.worldCamera = originalCamera;
            canvas.planeDistance = distance;
            UnityEngine.Object.DestroyImmediate(go);
            Canvas.ForceUpdateCanvases();
        }
    }

    private static void CaptureCamera(Camera camera, string name)
    {
        var target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var oldTarget = camera.targetTexture;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(
                camera,
                new UniversalRenderPipeline.SingleCameraRequest { destination = target }
            );
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            texture.Apply();
            File.WriteAllBytes("Logs/MenuCameraTask/" + name + ".png", texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok)
            throw new InvalidOperationException(message);
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(Active, false);
        if (SessionState.GetBool(Active + ".HadPreference", false))
            PlayerPrefs.SetInt(
                GameplayCameraSettings.PreferenceKey,
                SessionState.GetInt(Active + ".Preference", 0)
            );
        else
            PlayerPrefs.DeleteKey(GameplayCameraSettings.PreferenceKey);
        PlayerPrefs.Save();
        EditorApplication.Exit(code);
    }
}
