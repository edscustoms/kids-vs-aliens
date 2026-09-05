#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using StarterAssets;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameplayPresentationSetup
{
    public const string MenuPath = "Tools/Kids VS Aliens/Setup/Knowledge Feedback & Pause V1";
    public const string DataFolder = "Assets/Game/Data/Presentation";
    public const string RootName = "GameplayPresentationV1";
    private const string LayerName = "KnowledgePreview";
    public const string LightweightRendererPath =
        "Assets/Game/Settings/Rendering/Mobile_Renderer.asset";
    private static readonly Color Navy = Hex("130B2D");
    private static readonly Color Indigo = Hex("1F1149");
    private static readonly Color Violet = Hex("8B2BB4");
    private static readonly Color Magenta = Hex("C073C5");
    private static readonly Color Cyan = new(0.2f, 0.9f, 1f, 1f);

    [MenuItem(MenuPath)]
    public static void SetupActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play Mode before setting up gameplay presentation.");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        PlayerCharacter[] players = InScene<PlayerCharacter>(scene).ToArray();
        if (players.Length != 1)
        {
            Debug.LogError(
                "Open a gameplay scene with exactly one PlayerCharacter before running setup."
            );
            return;
        }
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Setup Knowledge Feedback & Pause V1");
        GameObject root = ConfigureScene(players[0]);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = root;
        Debug.Log(
            $"Knowledge/Feedback/Pause V1 configured in {scene.name}. Existing objects and data assets were reused; generated UI layout/color defaults were reapplied. Save this scene, then test in Play Mode. After manual UI polish, avoid rerunning setup without an Undo/version-control checkpoint.",
            root
        );
    }

    // Also callable in the existing project in batch mode. No scenes opened,
    // copied or imported separately; only these small presentation data assets.
    public static void CreateInitialAssets()
    {
        EnsureFolder(DataFolder);
        EnsurePreviewLayer();
        EnsurePreviewRenderer();
        bool created;
        var catalog = Asset<FeedbackPresentationCatalog>(
            DataFolder + "/GameplayFeedbackCatalog.asset",
            out created
        );
        if (created)
        {
            var entries = new[]
            {
                Entry(
                    FeedbackCode.MissingSkill,
                    "Requires {skillName}",
                    FeedbackCategory.ActionDenied,
                    50
                ),
                Entry(
                    FeedbackCode.InventoryFull,
                    "Inventory full",
                    FeedbackCategory.ActionDenied,
                    50
                ),
                Entry(
                    FeedbackCode.KnowledgeAlreadyKnown,
                    "Already learned: {skillName}",
                    FeedbackCategory.Information,
                    30
                ),
                Entry(
                    FeedbackCode.GrenadeThrownInert,
                    "Requires {skillName} to activate.",
                    FeedbackCategory.Warning,
                    60
                ),
            };
            var serialized = new SerializedObject(catalog);
            SerializedProperty array = serialized.FindProperty("entries");
            array.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var item = array.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("code").intValue = (int)entries[i].code;
                item.FindPropertyRelative("template").stringValue = entries[i].template;
                item.FindPropertyRelative("localizationKey").stringValue =
                    "feedback." + entries[i].code;
                item.FindPropertyRelative("category").intValue = (int)entries[i].category;
                item.FindPropertyRelative("priority").intValue = entries[i].priority;
                item.FindPropertyRelative("duration").floatValue = entries[i].duration;
                item.FindPropertyRelative("cooldown").floatValue = 2f;
                item.FindPropertyRelative("replaceEqualPriority").boolValue = true;
                item.FindPropertyRelative("queueWhenBlocked").boolValue = false;
                item.FindPropertyRelative("queueLifetime").floatValue = 8f;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(catalog);
        }
        Asset<WeaponPreviewEquipmentAdapter>(DataFolder + "/WeaponPreviewAdapter.asset", out _);
        Asset<GrenadePreviewEquipmentAdapter>(DataFolder + "/GrenadePreviewAdapter.asset", out _);
        Tutorial(
            "PistolHandling",
            "Assets/Game/Items/Weapons/PlasmaPistolItem.asset",
            "You can now operate plasma pistols.",
            "Press FIRE to shoot."
        );
        Tutorial(
            "RifleHandling",
            "Assets/Game/Items/Weapons/PlasmaRifleItem.asset",
            "You can now operate plasma rifles.",
            "Hold FIRE to fire automatically."
        );
        Tutorial(
            "GrenadeHandling",
            "Assets/Game/Data/Items/Grenades/ElectricGrenade.asset",
            "You can now activate alien grenades.",
            "Select a grenade. Hold FIRE to charge; release to throw."
        );
    }

    public static GameObject ConfigureScene(PlayerCharacter player)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));
        Scene scene = player.gameObject.scene;
        CreateInitialAssets();
        int layer = EnsurePreviewLayer();
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
        if (root == null)
        {
            root = new GameObject(RootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create presentation root");
            SceneManager.MoveGameObjectToScene(root, scene);
        }
        var canvas = Component<Canvas>(root);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = Component<CanvasScaler>(root);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        var raycaster = Component<GraphicRaycaster>(root);
        var feedback = Component<PlayerFeedback>(player.gameObject);
        var suspension = Component<GameplaySuspensionController>(player.gameObject);
        var pointerFilter = Component<GameplayPointerInputFilter>(player.gameObject);
        ReferenceArray(pointerFilter, "blockingCanvases", new UnityEngine.Object[] { raycaster });
        Reference(suspension, "input", player.GetComponent<StarterAssetsInputs>());
        ReferenceArray(
            suspension,
            "gameplayBehaviours",
            player
                .GetComponents<Behaviour>()
                .Where(behaviour =>
                    behaviour is ThirdPersonController
                    || behaviour is PlayerAim
                    || behaviour is PlayerShooter
                    || behaviour is PlayerInventory
                )
                .Cast<UnityEngine.Object>()
                .ToArray()
        );
        Reference(Component<GameplayPresentationLifetime>(root), "player", player);

        RectTransform blocker = Panel(
            root.transform,
            "SuspensionInputBlocker",
            new Color(Navy.r, Navy.g, Navy.b, 0.18f),
            true
        );
        Stretch(blocker);
        blocker.SetAsFirstSibling();
        blocker.gameObject.SetActive(false);
        RectTransform safe = Child(root.transform, "SafeArea");
        Component<SafeAreaPanel>(safe.gameObject);
        RectTransform feedbackView = Panel(
            safe,
            "Feedback",
            new Color(Navy.r, Navy.g, Navy.b, 0.94f),
            false
        );
        Fixed(feedbackView, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(760, 84));
        Outline(feedbackView.gameObject, Violet);
        RectTransform feedbackAccent = Panel(feedbackView, "Accent", Cyan, false);
        Anchors(feedbackAccent, new Vector2(0, 0), new Vector2(0.009f, 1));
        var message = Text(feedbackView, "Message", string.Empty, 30);
        Anchors(message.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.97f, 0.92f));
        var feedbackPresenter = Component<GameplayFeedbackPresenter>(root);
        Reference(feedbackPresenter, "source", feedback);
        Reference(feedbackPresenter, "skills", player.GetComponent<PlayerSkillState>());
        Reference(
            feedbackPresenter,
            "catalog",
            AssetDatabase.LoadAssetAtPath<FeedbackPresentationCatalog>(
                DataFolder + "/GameplayFeedbackCatalog.asset"
            )
        );
        Reference(feedbackPresenter, "view", Component<CanvasGroup>(feedbackView.gameObject));
        Reference(feedbackPresenter, "message", message);
        Reference(feedbackPresenter, "accent", feedbackAccent.GetComponent<Image>());

        RectTransform pause = Panel(safe, "PauseButton", Indigo, true);
        Fixed(pause, new Vector2(1, 1), new Vector2(-24, -24), new Vector2(100, 88));
        Outline(pause.gameObject, Magenta);
        Button pauseButton = Button(pause.gameObject);
        RectTransform pauseIcon = Child(pause, "PauseIcon");
        Stretch(pauseIcon);
        Anchors(
            Panel(pauseIcon, "LeftBar", Cyan, false),
            new Vector2(0.32f, 0.28f),
            new Vector2(0.44f, 0.72f)
        );
        Anchors(
            Panel(pauseIcon, "RightBar", Cyan, false),
            new Vector2(0.56f, 0.28f),
            new Vector2(0.68f, 0.72f)
        );
        RectTransform playIcon = Child(pause, "PlayIcon");
        Anchors(playIcon, new Vector2(0.35f, 0.27f), new Vector2(0.7f, 0.73f));
        var triangle = Component<PlayIconGraphic>(playIcon.gameObject);
        triangle.color = Cyan;
        triangle.raycastTarget = false;
        playIcon.gameObject.SetActive(false);
        var manual = Component<ManualPauseButton>(pause.gameObject);
        Reference(manual, "suspension", suspension);
        Reference(manual, "button", pauseButton);
        Reference(manual, "input", player.GetComponent<StarterAssetsInputs>());
        Reference(manual, "pauseIcon", pauseIcon.gameObject);
        Reference(manual, "playIcon", playIcon.gameObject);

        RectTransform overlay = Panel(
            root.transform,
            "KnowledgeOverlay",
            new Color(Navy.r, Navy.g, Navy.b, 0.86f),
            true
        );
        Stretch(overlay);
        overlay.SetAsLastSibling();
        RectTransform modalSafe = Child(overlay, "SafeArea");
        Component<SafeAreaPanel>(modalSafe.gameObject);
        RectTransform card = Panel(
            modalSafe,
            "Card",
            new Color(Indigo.r, Indigo.g, Indigo.b, 0.98f),
            true
        );
        Anchors(card, new Vector2(0.08f, 0.045f), new Vector2(0.92f, 0.955f));
        Outline(card.gameObject, Magenta);
        RectTransform topAccent = Panel(card, "TopAccent", Cyan, false);
        Anchors(topAccent, new Vector2(0.12f, 0.985f), new Vector2(0.88f, 0.99f));
        RectTransform content = Child(card, "Content");
        Anchors(content, new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.965f));
        var layout = Component<VerticalLayoutGroup>(content.gameObject);
        layout.spacing = 10;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;
        TMP_Text heading = Text(content, "Heading", "KNOWLEDGE ACQUIRED", 38);
        heading.color = Cyan;
        Height(heading.gameObject, 52);
        RectTransform previewFrame = Panel(
            content,
            "PreviewFrame",
            new Color(Navy.r, Navy.g, Navy.b, 0.6f),
            false
        );
        var previewLayout = Component<LayoutElement>(previewFrame.gameObject);
        previewLayout.minHeight = 120;
        previewLayout.preferredHeight = 420;
        previewLayout.flexibleHeight = 1;
        Outline(previewFrame.gameObject, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
        RectTransform imageRect = Child(previewFrame, "CharacterRender");
        var image = Component<RawImage>(imageRect.gameObject);
        image.raycastTarget = false;
        var aspect = Component<AspectRatioFitter>(imageRect.gameObject);
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 1f;
        TMP_Text title = Text(content, "SkillTitle", "", 40);
        Height(title.gameObject, 56);
        TMP_Text description = Text(content, "Description", "", 28);
        Height(description.gameObject, 52);
        TMP_Text instructions = Text(content, "Instructions", "", 27);
        Height(instructions.gameObject, 56);
        RectTransform acknowledge = Panel(content, "Acknowledge", Hex("5E1885"), true);
        Height(acknowledge.gameObject, 76);
        Outline(acknowledge.gameObject, Cyan);
        TMP_Text gotIt = Text(acknowledge, "Label", "GOT IT", 30);
        Stretch(gotIt.rectTransform);
        Button acknowledgeButton = Button(acknowledge.gameObject);
        var knowledge = Component<KnowledgeAcquiredPresenter>(root);
        Reference(knowledge, "skills", player.GetComponent<PlayerSkillState>());
        Reference(knowledge, "suspension", suspension);
        Reference(knowledge, "view", overlay.gameObject);
        Reference(knowledge, "title", title);
        Reference(knowledge, "description", description);
        Reference(knowledge, "instructions", instructions);
        Reference(knowledge, "acknowledgeButton", acknowledgeButton);

        ConfigurePreview(root, player, knowledge, image, layer);
        var hud = Component<SuspensionHudBinding>(root);
        Reference(hud, "suspension", suspension);
        Reference(hud, "inputBlocker", blocker.gameObject);
        Reference(hud, "feedback", feedbackPresenter);
        overlay.gameObject.SetActive(false);
        Component<CanvasGroup>(feedbackView.gameObject).alpha = 0;
        if (!InScene<EventSystem>(scene).Any())
        {
            GameObject events = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
            Undo.RegisterCreatedObjectUndo(events, "Create missing EventSystem");
            SceneManager.MoveGameObjectToScene(events, scene);
            Debug.Log("Created missing EventSystem for this gameplay scene.", events);
        }
        return root;
    }

    private static void ConfigurePreview(
        GameObject root,
        PlayerCharacter player,
        KnowledgeAcquiredPresenter presenter,
        RawImage output,
        int layer
    )
    {
        // This is scene-owned but outside UI layout. Camera culling, collider
        // disabling and local VFX suppression provide isolation, not distance.
        RectTransform rig = Child(root.transform, "PreviewRig");
        rig.position = new Vector3(1000, -1000, 0);
        RectTransform actorRoot = Child(rig, "ActorRoot");
        actorRoot.localPosition = Vector3.zero;
        actorRoot.localScale = Vector3.one;
        actorRoot.gameObject.SetActive(false);
        Camera camera = Component<Camera>(Child(rig, "PreviewCamera").gameObject);
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0, 0, 0, 0);
        camera.cullingMask = 1 << layer;
        camera.fieldOfView = 30;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.useOcclusionCulling = false;
        var cameraData = Component<UniversalAdditionalCameraData>(camera.gameObject);
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;
        cameraData.volumeLayerMask = 0;
        Light light = Component<Light>(Child(rig, "PreviewLight").gameObject);
        // A local point light cannot compete for URP's gameplay main light.
        light.enabled = false;
        light.type = LightType.Point;
        light.intensity = 8f;
        light.range = 8f;
        light.color = new Color(0.82f, 0.91f, 1f);
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << layer;
        light.transform.localPosition = new Vector3(-1, 2.5f, 2);
        Component<UniversalAdditionalLightData>(light.gameObject).renderingLayers =
            PreviewVisualSafety.RenderingLayer;
        foreach (Camera other in InScene<Camera>(player.gameObject.scene))
            if (other != camera)
            {
                Undo.RecordObject(other, "Exclude preview layer");
                other.cullingMask &= ~(1 << layer);
            }
        foreach (Light other in InScene<Light>(player.gameObject.scene))
            if (other != light)
            {
                Undo.RecordObject(other, "Isolate preview lighting");
                other.cullingMask &= ~(1 << layer);
            }
        var stage = Component<KnowledgePreviewStage>(root);
        Reference(stage, "previewCamera", camera);
        Reference(stage, "previewLight", light);
        Reference(stage, "previewRoot", actorRoot);
        Reference(stage, "output", output);
        Reference(stage, "lightweightRenderer", EnsurePreviewRenderer());
        if (
            !stage.TrySelectRenderer(
                GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset
            )
        )
            Debug.LogWarning(
                "Active pipeline is not configured for Knowledge previews; configure a project URP quality level before testing.",
                stage
            );
        var serialized = new SerializedObject(stage);
        serialized.FindProperty("previewLayer").intValue = layer;
        serialized.ApplyModifiedProperties();
        var demo = Component<SkillDemoPlayer>(root);
        Reference(demo, "presenter", presenter);
        Reference(demo, "player", player);
        Reference(demo, "stage", stage);
        ReferenceArray(
            demo,
            "equipmentAdapters",
            new UnityEngine.Object[]
            {
                AssetDatabase.LoadAssetAtPath<WeaponPreviewEquipmentAdapter>(
                    DataFolder + "/WeaponPreviewAdapter.asset"
                ),
                AssetDatabase.LoadAssetAtPath<GrenadePreviewEquipmentAdapter>(
                    DataFolder + "/GrenadePreviewAdapter.asset"
                ),
            }
        );
    }

    private static ScriptableRendererData EnsurePreviewRenderer()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(
            LightweightRendererPath
        );
        if (renderer == null || renderer.rendererFeatures.Count != 0)
            throw new InvalidOperationException(
                "Knowledge preview requires the existing feature-free Mobile_Renderer."
            );
        var pipelines = new HashSet<UniversalRenderPipelineAsset>();
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultPipeline)
            pipelines.Add(defaultPipeline);
        for (int i = 0; i < QualitySettings.names.Length; i++)
            if (
                QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset pipeline
            )
                pipelines.Add(pipeline);
        foreach (var pipeline in pipelines)
        {
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer)
                    present = true;
            if (present)
                continue;
            int index = list.arraySize++;
            list.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(pipeline);
            Debug.Log(
                $"Registered existing lightweight preview renderer at index {index} in {pipeline.name}; gameplay default preserved."
            );
        }
        return renderer;
    }

    private static int EnsurePreviewLayer()
    {
        int existing = LayerMask.NameToLayer(LayerName);
        if (existing >= 8)
            return existing;
        var settings = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]
        );
        var layers = settings.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            var slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue))
                continue;
            slot.stringValue = LayerName;
            settings.ApplyModifiedProperties();
            return i;
        }
        throw new InvalidOperationException(
            "No unused Unity layer available for the Knowledge preview."
        );
    }

    private static FeedbackPresentation Entry(
        FeedbackCode code,
        string text,
        FeedbackCategory category,
        int priority
    ) =>
        new()
        {
            code = code,
            template = text,
            category = category,
            priority = priority,
            duration = category == FeedbackCategory.Warning ? 3 : 2,
        };

    private static void Tutorial(
        string skillName,
        string equipmentPath,
        string description,
        string instructions
    )
    {
        var skill = AssetDatabase.LoadAssetAtPath<SkillData>(
            $"Assets/Game/Data/Progression/{skillName}.asset"
        );
        if (skill == null)
        {
            Debug.LogWarning($"Initial tutorial skipped: {skillName} skill asset is missing.");
            return;
        }
        var tutorial = Asset<SkillTutorialData>(
            $"{DataFolder}/{skillName}Tutorial.asset",
            out bool created
        );
        if (created)
        {
            tutorial.shortDescription = description;
            tutorial.instructions = instructions;
            tutorial.equipment = AssetDatabase.LoadAssetAtPath<ItemData>(equipmentPath);
            tutorial.action = CharacterActionId.EquippedStance;
            tutorial.localizationKey = "knowledge." + skill.Id;
            EditorUtility.SetDirty(tutorial);
            AssetDatabase.SaveAssetIfDirty(tutorial);
        }
        if (skill.TutorialData == null)
        {
            Reference(skill, "tutorialData", tutorial);
            AssetDatabase.SaveAssetIfDirty(skill);
        }
    }

    private static T Asset<T>(string path, out bool created)
        where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        created = asset == null;
        if (!created)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        Debug.Log("Created " + path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }

    private static IEnumerable<T> InScene<T>(Scene scene)
        where T : UnityEngine.Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

    private static T Component<T>(GameObject go)
        where T : UnityEngine.Component
    {
        T existing = go.GetComponent<T>();
        // Unity's missing-component objects can be managed non-null in Editor.
        return existing != null ? existing : Undo.AddComponent<T>(go);
    }

    private static RectTransform Child(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return (RectTransform)existing;
        var child = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(child, "Create " + name);
        child.transform.SetParent(parent, false);
        return (RectTransform)child.transform;
    }

    private static RectTransform Panel(Transform parent, string name, Color color, bool raycast)
    {
        var rect = Child(parent, name);
        var image = Component<Image>(rect.gameObject);
        image.color = color;
        image.raycastTarget = raycast;
        return rect;
    }

    private static TMP_Text Text(Transform parent, string name, string text, float size)
    {
        var rect = Child(parent, name);
        var label = Component<TextMeshProUGUI>(rect.gameObject);
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"
        );
        label.text = text;
        label.fontSize = size;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * 0.7f;
        label.fontSizeMax = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.94f, 0.96f, 1);
        label.raycastTarget = false;
        return label;
    }

    private static void Height(GameObject go, float height)
    {
        var element = Component<LayoutElement>(go);
        element.minHeight = height;
        element.preferredHeight = height;
        element.flexibleHeight = 0;
    }

    private static void Stretch(RectTransform rect) => Anchors(rect, Vector2.zero, Vector2.one);

    private static void Anchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Fixed(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Outline(GameObject go, Color color)
    {
        var outline = Component<Outline>(go);
        outline.effectColor = color;
        outline.effectDistance = new Vector2(2, -2);
    }

    private static Button Button(GameObject go)
    {
        var button = Component<Button>(go);
        button.targetGraphic = go.GetComponent<Image>();
        var colors = button.colors;
        colors.highlightedColor = new Color(0.65f, 0.93f, 1);
        colors.pressedColor = Magenta;
        button.colors = colors;
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        return button;
    }

    private static void Reference(
        UnityEngine.Object target,
        string property,
        UnityEngine.Object value
    )
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static void ReferenceArray(
        UnityEngine.Object target,
        string property,
        UnityEngine.Object[] values
    )
    {
        var serialized = new SerializedObject(target);
        var array = serialized.FindProperty(property);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedProperties();
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }
}
#endif
