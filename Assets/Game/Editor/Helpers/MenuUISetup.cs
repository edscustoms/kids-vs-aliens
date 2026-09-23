using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Conservative, repeatable extraction of the existing Menu presentation.</summary>
public static class MenuUISetup
{
    public const string MenuPath = "Assets/Game/Scenes/Menu.unity";
    public const string ThemePath = "Assets/Game/UI/Themes/MenuTheme.asset";
    public const string PillPath = "Assets/Game/UI/Components/Buttons/Btn_Pill.prefab";
    public const string CirclePath = "Assets/Game/UI/Components/Buttons/Btn_IconCircle.prefab";

    [MenuItem("Tools/UI/Setup or Repair Active Menu")]
    public static void SetupActiveMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        ConfigureScene(SceneManager.GetActiveScene());
    }

    public static void ConfigureScene(Scene scene)
    {
        ProceduralUISetup.EnsureAssets();
        var canvas = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true))
            .FirstOrDefault(c => c.name == "MenuCanvas");
        if (canvas == null) throw new InvalidOperationException("The active scene has no MenuCanvas.");
        var main = canvas.transform.Find("Screen_MainMenu") as RectTransform;
        if (main == null)
        {
            Transform[] children = canvas.transform.Cast<Transform>().Where(t => t.name != "Background").ToArray();
            main = Root(canvas.transform, "Screen_MainMenu");
            foreach (Transform child in children) Undo.SetTransformParent(child, main, false, "Group menu screen");
        }
        var select = Find(main, "SelectButton").GetComponent<Button>();
        var circle = main.GetComponentsInChildren<Button>(true).First(b => b.name == "PreviousButton");
        var theme = EnsureTheme(select, circle);
        var pillPrefab = EnsurePrefab(select, PillPath, theme, UIButtonVariant.Pill);
        var circlePrefab = EnsurePrefab(circle, CirclePath, theme, UIButtonVariant.IconCircle);
        foreach (Button button in main.GetComponentsInChildren<Button>(true))
        {
            if (button.GetComponent<UIButton>() != null) continue;
            bool round = button.image != null && button.image.sprite == theme.iconCircle.sprite;
            Migrate(button, round ? circlePrefab : pillPrefab, theme,
                round ? UIButtonVariant.IconCircle : UIButtonVariant.Pill, button.GetComponentInChildren<TMP_Text>(true));
        }
        // The two nameplates are intentionally passive and keep their sibling labels.
        // Preserve those label RectTransforms and controller references exactly.
        foreach (string name in new[] { "TypeName", "ItemName" })
        {
            var plate = Find(main, name + "Plate");
            if (plate.GetComponent<UIButton>() != null) continue;
            var button = Undo.AddComponent<Button>(plate.gameObject);
            button.targetGraphic = plate.GetComponent<Image>();
            button.colors = theme.pill.colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Migrate(button, pillPrefab, theme, UIButtonVariant.Pill, Find(main, name).GetComponent<TMP_Text>());
            button.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
        }

        var options = canvas.transform.Find("Screen_Options") as RectTransform;
        bool createOptions = options == null;
        if (createOptions) options = Root(canvas.transform, "Screen_Options");
        var router = canvas.GetComponent<UIScreenRouter>() ?? Undo.AddComponent<UIScreenRouter>(canvas.gameObject);
        Undo.RecordObject(router, "Configure screen routing");
        router.Configure(main.gameObject, options.gameObject);

        if (main.Find("OptionsButton") == null)
        {
            var open = NewButton(pillPrefab, main, "OptionsButton", "OPTIONS", new Vector2(0.80f, 0.09f), new Vector2(330, 110));
            UnityEventTools.AddPersistentListener(open.OnClick, router.ShowOptions);
        }
        if (createOptions)
        {
            options.gameObject.SetActive(false);
            var template = select.GetComponentInChildren<TMP_Text>(true);
            Heading(template, options, "Title", "OPTIONS", 0.84f, 64);
            Heading(template, options, "GameplayHeading", "GAMEPLAY", 0.67f, 42);
            Heading(template, options, "CameraHeading", "GAMEPLAY CAMERA", 0.55f, 36);
            var row = Root(options, "GameplayCamera");
            var segmented = Undo.AddComponent<UISegmentedControl>(row.gameObject);
            string[] names = { "ACTION", "TACTICAL", "ISOMETRIC" };
            var values = new UISegmentedControl.Option[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                UIButton button = NewButton(pillPrefab, row, names[i], names[i], new Vector2(0.28f + i * 0.22f, 0.40f), new Vector2(370, 120));
                values[i] = new UISegmentedControl.Option { id = names[i].ToLowerInvariant(), button = button };
            }
            segmented.Configure(values);
            var controller = Undo.AddComponent<OptionsScreenController>(options.gameObject);
            controller.Configure(segmented);
            var back = NewButton(pillPrefab, options, "BackButton", "BACK", new Vector2(0.5f, 0.16f), new Vector2(330, 110));
            UnityEventTools.AddPersistentListener(back.OnClick, router.ShowMainMenu);
        }
        ProgressResetMenuSetup.Configure(options, theme);
        HapticsOptionView.Ensure(options, false);
        CameraShakeOptionView.Ensure(options, false);
        RunInterfaceSetup.ConfigureMenu(scene);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void Migrate(Button button, GameObject prefab, UITheme theme, UIButtonVariant variant, TMP_Text label)
    {
        bool externalLabel = label != null && !label.transform.IsChildOf(button.transform);
        PrefabUtility.ConvertToPrefabInstance(button.gameObject, prefab, new ConvertToPrefabInstanceSettings
        {
            objectMatchMode = ObjectMatchMode.ByHierarchy,
            recordPropertyOverridesOfMatches = true,
            componentsNotMatchedBecomesOverride = true,
            gameObjectsNotMatchedBecomesOverride = true,
            changeRootNameToAssetName = false
        }, InteractionMode.AutomatedAction);
        var ui = button.GetComponent<UIButton>();
        if (externalLabel)
        {
            var extra = button.transform.Find("Label");
            if (extra != null) Undo.DestroyObjectImmediate(extra.gameObject);
        }
        ui.Configure(theme, variant, label);
        PrefabUtility.RecordPrefabInstancePropertyModifications(ui);
    }

    private static UITheme EnsureTheme(Button pill, Button circle)
    {
        var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (theme != null) return theme;
        EnsureFolder("Assets/Game/UI/Themes");
        theme = ScriptableObject.CreateInstance<UITheme>();
        theme.pill.sprite = pill.image.sprite;
        theme.pill.colors = pill.colors;
        theme.iconCircle.sprite = circle.image.sprite;
        theme.iconCircle.colors = circle.colors;
        AssetDatabase.CreateAsset(theme, ThemePath);
        return theme;
    }

    private static GameObject EnsurePrefab(Button source, string path, UITheme theme, UIButtonVariant variant)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset != null) return asset;
        EnsureFolder("Assets/Game/UI/Components/Buttons");
        GameObject clone = Object.Instantiate(source.gameObject);
        try
        {
            clone.name = System.IO.Path.GetFileNameWithoutExtension(path);
            var button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.interactable = true;
            var label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.fontSharedMaterial != null && !AssetDatabase.Contains(label.fontSharedMaterial))
            {
                var material = new Material(label.fontSharedMaterial);
                AssetDatabase.CreateAsset(material, "Assets/Game/UI/Themes/" + clone.name + "_Label.mat");
                label.fontSharedMaterial = material;
            }
            var ui = clone.GetComponent<UIButton>() ?? clone.AddComponent<UIButton>();
            ui.Configure(theme, variant, label);
            return PrefabUtility.SaveAsPrefabAsset(clone, path);
        }
        finally { Object.DestroyImmediate(clone); }
    }

    private static UIButton NewButton(GameObject prefab, Transform parent, string name, string text, Vector2 anchor, Vector2 size)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(go, "Create UI button");
        go.name = name;
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        var ui = go.GetComponent<UIButton>();
        ui.Text = text;
        foreach (Component component in go.GetComponentsInChildren<Component>(true))
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        return ui;
    }

    private static void Heading(TMP_Text source, Transform parent, string name, string text, float y, float size)
    {
        GameObject go = Object.Instantiate(source.gameObject, parent);
        Undo.RegisterCreatedObjectUndo(go, "Create options heading");
        go.name = name;
        var label = go.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = size;
        label.enableAutoSizing = false;
        label.raycastTarget = false;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, y);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1000, 100);
    }

    private static RectTransform Root(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create screen root");
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
