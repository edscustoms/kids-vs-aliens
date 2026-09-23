using System;
using System.Linq;
using StarterAssets;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Repairs only menu wiring; existing presentation geometry and tuned controls are retained.</summary>
public static class InGameMenuSetup
{
    // Batch entry point: migrate just this feature in existing presentation roots.
    public static void RepairGameplayScenes()
    {
        foreach (string name in new[] { "ConstructionSite", "GamePoc" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/" + name + ".unity");
            var player = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PlayerCharacter>(true))
                .Single();
            var presentation = scene
                .GetRootGameObjects()
                .FirstOrDefault(root => root.name == GameplayPresentationSetup.RootName);
            if (presentation == null)
                presentation = GameplayPresentationSetup.ConfigureScene(player);
            var controller = ConfigureScene(player, presentation);
            int count = presentation.GetComponentsInChildren<Component>(true).Length;
            ConfigureScene(player, presentation);
            if (count != presentation.GetComponentsInChildren<Component>(true).Length)
                throw new InvalidOperationException("Menu repair duplicated scene content.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("IN-GAME MENU REPAIR PASSED: " + name, controller);
        }
        AssetDatabase.SaveAssets();
    }

    public static InGameMenuController ConfigureScene(
        PlayerCharacter player,
        GameObject presentation
    )
    {
        var safe = presentation.transform.Find("SafeArea");
        if (safe == null)
            throw new InvalidOperationException(
                "Gameplay presentation requires its existing SafeArea."
            );
        // Modal shade must span the real Canvas, never the cutout-safe HUD rect.
        var legacy = safe.Find("InGameMenuRoot");
        if (legacy != null) Undo.SetTransformParent(legacy, presentation.transform, "Fullscreen menu root");
        var root = Root(presentation.transform, "InGameMenuRoot");
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        var main = Root(root, "Screen_InGameMenu");
        var options = Root(root, "Screen_InGameOptions");
        HapticsOptionView.Ensure(options, true);
        CameraShakeOptionView.Ensure(options, true);
        var controller =
            root.GetComponent<InGameMenuController>()
            ?? Undo.AddComponent<InGameMenuController>(root.gameObject);
        Heading(main, "Title", "MENU", new Vector2(0.5f, 0.76f), new Vector2(500, 100), 48);
        var openOptions = Button(
            main,
            "OptionsButton",
            false,
            "OPTIONS",
            new Vector2(0.5f, 0.58f),
            new Vector2(370, 110)
        );
        var resume = Button(
            main,
            "ResumeButton",
            false,
            "RESUME",
            new Vector2(0.5f, 0.42f),
            new Vector2(370, 110)
        );
        Heading(
            options,
            "CameraLabel",
            "CAMERA",
            new Vector2(0.5f, 0.76f),
            new Vector2(500, 100),
            48
        );
        var label = Heading(
            options,
            "CurrentCameraModeLabel",
            "ACTION",
            new Vector2(0.5f, 0.60f),
            new Vector2(420, 100),
            38
        );
        var previous = Button(
            options,
            "PreviousCameraButton",
            true,
            "\u2039",
            new Vector2(0.34f, 0.60f),
            new Vector2(96, 96)
        );
        var next = Button(
            options,
            "NextCameraButton",
            true,
            "\u203a",
            new Vector2(0.66f, 0.60f),
            new Vector2(96, 96)
        );
        var back = Button(
            options,
            "BackButton",
            false,
            "BACK",
            new Vector2(0.5f, 0.23f),
            new Vector2(330, 110)
        );
        Undo.RecordObject(controller, "Wire in-game menu");
        controller.Configure(
            player.GetComponent<GameplaySuspensionController>(),
            main.gameObject,
            options.gameObject,
            label
        );
        EditorUtility.SetDirty(controller);
        Wire(openOptions, controller.ShowOptions);
        Wire(resume, controller.ResumeGame);
        Wire(back, controller.ShowMenu);
        Wire(previous, controller.PreviousCamera);
        Wire(next, controller.NextCamera);

        var pause = safe.Find("PauseButton") as RectTransform;
        if (pause == null)
        {
            pause = (RectTransform)
                Button(safe, "PauseButton", true, "", Vector2.one, new Vector2(96, 96)).transform;
            pause.pivot = Vector2.one;
            pause.anchoredPosition = new Vector2(-28, -28);
        }
        if (!PrefabUtility.IsPartOfPrefabInstance(pause))
            PrefabUtility.ConvertToPrefabInstance(
                pause.gameObject,
                Prefab(true),
                new ConvertToPrefabInstanceSettings
                {
                    objectMatchMode = ObjectMatchMode.ByHierarchy,
                    recordPropertyOverridesOfMatches = true,
                    componentsNotMatchedBecomesOverride = true,
                    gameObjectsNotMatchedBecomesOverride = true,
                    changeRootNameToAssetName = false,
                },
                InteractionMode.AutomatedAction
            );
        var ui = pause.GetComponent<UIButton>() ?? Undo.AddComponent<UIButton>(pause.gameObject);
        var unusedLabel = pause.Find("Label");
        if (unusedLabel != null)
            unusedLabel.gameObject.SetActive(false);
        ui.Configure(
            AssetDatabase.LoadAssetAtPath<UITheme>(MenuUISetup.ThemePath),
            UIButtonVariant.IconCircle
        );
        // Keep the existing pause bars until a menu icon asset exists. The old play indicator is obsolete.
        var pauseIcon = pause.Find("PauseIcon") as RectTransform;
        if (pauseIcon == null)
        {
            pauseIcon = Root(pause, "PauseIcon");
            Bar(pauseIcon, "LeftBar", new Vector2(0.34f, 0.29f), new Vector2(0.43f, 0.71f));
            Bar(pauseIcon, "RightBar", new Vector2(0.57f, 0.29f), new Vector2(0.66f, 0.71f));
        }
        pauseIcon.gameObject.SetActive(true);
        var playIcon = pause.Find("PlayIcon");
        if (playIcon != null)
            playIcon.gameObject.SetActive(false);
        var manual =
            pause.GetComponent<ManualPauseButton>()
            ?? Undo.AddComponent<ManualPauseButton>(pause.gameObject);
        var serialized = new SerializedObject(manual);
        serialized.FindProperty("menu").objectReferenceValue = controller;
        serialized.FindProperty("input").objectReferenceValue =
            player.GetComponent<StarterAssetsInputs>();
        serialized.FindProperty("button").objectReferenceValue =
            pause.GetComponent<UnityEngine.UI.Button>();
        serialized.ApplyModifiedProperties();
        Record(pause.gameObject);
        main.gameObject.SetActive(false);
        options.gameObject.SetActive(false);
        return controller;
    }

    private static void Wire(UIButton button, UnityAction action)
    {
        // These generated controls each own exactly one navigation callback.
        Undo.RecordObject(button.Button, "Repair menu callback");
        for (int i = button.OnClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.OnClick, i);
        UnityEventTools.AddPersistentListener(button.OnClick, action);
        Record(button.gameObject);
    }

    private static GameObject Prefab(bool circle) =>
        AssetDatabase.LoadAssetAtPath<GameObject>(
            circle ? MenuUISetup.CirclePath : MenuUISetup.PillPath
        ) ?? throw new InvalidOperationException("Reusable UI button prefabs are missing.");

    private static UIButton Button(
        Transform parent,
        string name,
        bool circle,
        string text,
        Vector2 anchor,
        Vector2 size
    )
    {
        var existing = parent.Find(name);
        if (existing != null)
        {
            var repaired = existing.GetComponent<UIButton>();
            if (repaired == null)
            {
                repaired = Undo.AddComponent<UIButton>(existing.gameObject);
                repaired.Configure(
                    AssetDatabase.LoadAssetAtPath<UITheme>(MenuUISetup.ThemePath),
                    circle ? UIButtonVariant.IconCircle : UIButtonVariant.Pill,
                    existing.GetComponentInChildren<TMP_Text>(true)
                );
            }
            return repaired;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(circle), parent);
        Undo.RegisterCreatedObjectUndo(go, "Create menu control");
        go.name = name;
        Fixed((RectTransform)go.transform, anchor, size);
        var button = go.GetComponent<UIButton>();
        button.Text = text;
        Record(go);
        return button;
    }

    private static TMP_Text Heading(
        Transform parent,
        string name,
        string text,
        Vector2 anchor,
        Vector2 size,
        float fontSize
    )
    {
        var existing = parent.Find(name);
        if (existing != null)
            return existing.GetComponent<TMP_Text>();
        var source = Prefab(false).GetComponentInChildren<TMP_Text>(true);
        var go = UnityEngine.Object.Instantiate(source.gameObject, parent);
        Undo.RegisterCreatedObjectUndo(go, "Create menu label");
        go.name = name;
        var label = go.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.raycastTarget = false;
        Fixed(label.rectTransform, anchor, size);
        return label;
    }

    private static RectTransform Root(Transform parent, string name)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null)
            return existing;
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create menu root");
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static void Bar(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var rect = Root(parent, name);
        rect.anchorMin = min;
        rect.anchorMax = max;
        var image = Undo.AddComponent<Image>(rect.gameObject);
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private static void Fixed(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void Record(GameObject go)
    {
        foreach (var component in go.GetComponentsInChildren<Component>(true))
        {
            EditorUtility.SetDirty(component);
            if (PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            if (
                component is Transform
                && PrefabUtility.IsPartOfPrefabInstance(component.gameObject)
            )
                PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject);
        }
        if (PrefabUtility.IsPartOfPrefabInstance(go))
            PrefabUtility.RecordPrefabInstancePropertyModifications(go);
    }
}
