using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Batch migration and structural regression checks against the actual scenes.</summary>
public static class MenuCameraValidation
{
    public static void MigrateAndValidate()
    {
        Scene menu = EditorSceneManager.OpenScene(MenuUISetup.MenuPath);
        Canvas.ForceUpdateCanvases();
        var rects = InScene<RectTransform>(menu)
            .Where(r => r.GetComponent<Canvas>() == null)
            .ToDictionary(r => r, RectState);
        var images = InScene<Image>(menu).ToDictionary(i => i, ImageState);
        var texts = InScene<TMP_Text>(menu).ToDictionary(t => t, TextState);
        var buttons = InScene<Button>(menu).ToDictionary(b => b, ButtonState);
        MenuUISetup.ConfigureScene(menu);
        Canvas.ForceUpdateCanvases();
        foreach (var pair in rects)
            Check(RectState(pair.Key) == pair.Value, "RectTransform changed: " + pair.Key.name);
        foreach (var pair in images)
            Check(ImageState(pair.Key) == pair.Value, "Image changed: " + pair.Key.name);
        foreach (var pair in texts)
            Check(TextState(pair.Key) == pair.Value, "Text changed: " + pair.Key.name);
        // Compare the authored Button state and callback data (prefab identity itself is expected to change).
        foreach (var pair in buttons)
            Check(
                ButtonState(pair.Key) == pair.Value,
                "Button states/callbacks changed: " + pair.Key.name
            );
        int count = InScene<Component>(menu).Count();
        MenuUISetup.ConfigureScene(menu);
        Check(
            count == InScene<Component>(menu).Count(),
            "Menu repair duplicated objects/components"
        );
        var router = InScene<UIScreenRouter>(menu).Single();
        router.ShowOptions();
        Check(
            !InScene<Transform>(menu)
                .Single(t => t.name == "Screen_MainMenu")
                .gameObject.activeSelf,
            "Main menu stayed active"
        );
        router.ShowMainMenu();
        EditorSceneManager.SaveScene(menu);
        Debug.Log(
            $"MENU VALIDATION PASSED: {rects.Count} original RectTransforms, {images.Count} images, {texts.Count} labels, {buttons.Count} button state/callback sets; idempotent repair and routing."
        );

        foreach (
            string path in new[]
            {
                "Assets/Game/Scenes/ConstructionSite.unity",
                "Assets/Game/Scenes/GamePoc.unity",
            }
        )
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            PlayerCharacter player = InScene<PlayerCharacter>(scene).Single();
            var controller = GameplayCameraSetup.ConfigureScene(player);
            Check(controller != null, "No gameplay camera configured: " + path);
            Check(
                GameplayCameraSetup.ConfigureScene(player) == controller,
                "Camera repair duplicated controller"
            );
            EditorSceneManager.SaveScene(scene);
            var rig = controller.GetComponent<CinemachineVirtualCamera>();
            var body = rig.GetCinemachineComponent<CinemachineTransposer>();
            var baseline = body.m_FollowOffset;
            var lens = rig.m_Lens;
            controller.Apply(GameplayCameraMode.Tactical);
            Check(body.m_FollowOffset.magnitude > baseline.magnitude, "Tactical is not farther");
            controller.Apply(GameplayCameraMode.Isometric);
            Check(
                rig.m_Lens.ModeOverride == LensSettings.OverrideModes.Orthographic,
                "Isometric is not orthographic"
            );
            controller.Apply(GameplayCameraMode.Action);
            Check(
                body.m_FollowOffset == baseline && rig.m_Lens.FieldOfView == lens.FieldOfView,
                "Action baseline not restored"
            );
            Debug.Log("CAMERA VALIDATION PASSED: " + path);
        }
        // Discard runtime-style validation mutations. Only setup wiring above was saved.
        EditorSceneManager.OpenScene(MenuUISetup.MenuPath);
        AssetDatabase.SaveAssets();
    }

    private static string RectState(RectTransform r) =>
        $"{r.anchorMin:R}|{r.anchorMax:R}|{r.pivot:R}|{r.anchoredPosition:R}|{r.sizeDelta:R}|{r.localScale:R}|{r.localRotation:R}";

    private static string TextState(TMP_Text t) =>
        $"{t.text}|{t.font.GetEntityId()}|{t.fontSharedMaterial.GetEntityId()}|{t.fontSize:R}|{t.enableAutoSizing}|{t.color}|{t.alignment}|{t.margin:R}";

    private static string ImageState(Image i) =>
        $"{i.sprite.GetEntityId()}|{i.material.GetEntityId()}|{i.color}|{i.type}|{i.preserveAspect}|{i.raycastTarget}|{i.pixelsPerUnitMultiplier:R}";

    private static string ButtonState(Button b) =>
        JsonUtility.ToJson(b.colors)
        + "|"
        + b.interactable
        + "|"
        + b.transition
        + "|"
        + string.Join(
            ";",
            Enumerable
                .Range(0, b.onClick.GetPersistentEventCount())
                .Select(i =>
                    b.onClick.GetPersistentTarget(i).GetEntityId()
                    + ":"
                    + b.onClick.GetPersistentMethodName(i)
                )
        );

    private static IEnumerable<T> InScene<T>(Scene scene)
        where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
