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

    // Reuse the exact visual language already used by the main menu.
    // GUIDs are stable inside this project even if the assets move folders.
    private const string NeonPillGuid = "de2a5b71f7b0c8649ababb4456243334";
    private const string NeonCircleGuid = "409fd95e15e45e347b7de10bf0d6ad1c";

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
            "Press FIRE to shoot.",
            SkillDemoType.WeaponFire,
            CharacterActionId.PistolFire,
            4,
            0.7f,
            2.0f
        );
        Tutorial(
            "RifleHandling",
            "Assets/Game/Items/Weapons/PlasmaRifleItem.asset",
            "You can now operate plasma rifles.",
            "Hold FIRE to fire automatically.",
            SkillDemoType.WeaponFire,
            CharacterActionId.RifleFire,
            6,
            0.14f,
            1.5f
        );
        Tutorial(
            "GrenadeHandling",
            "Assets/Game/Data/Items/Grenades/ElectricGrenade.asset",
            "You can now activate alien grenades.",
            "Select a grenade. Hold FIRE to charge; release to throw.",
            SkillDemoType.Stance,
            CharacterActionId.EquippedStance,
            0,
            0.7f,
            2.0f
        );
    }

    public static GameObject ConfigureScene(PlayerCharacter player)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));
        Scene scene = player.gameObject.scene;
        CreateInitialAssets();
        int layer = EnsurePreviewLayer();
        Sprite neonPill = SpriteByGuid(NeonPillGuid, "NeonPill");
        Sprite neonCircle = SpriteByGuid(NeonCircleGuid, "NeonCircle");

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

        // Input-only blocker. Keep it visually transparent so suspension never
        // tints the mobile HUD. Knowledge dimming is handled by a separate
        // low-sorting canvas beneath the gameplay HUD.
        RectTransform blocker = Panel(
            root.transform,
            "SuspensionInputBlocker",
            Color.clear,
            true
        );
        Stretch(blocker);
        blocker.SetAsFirstSibling();
        blocker.gameObject.SetActive(false);
        RectTransform safe = Child(root.transform, "SafeArea");
        // SafeAreaPanel applies the real device cutout at runtime, but the generated
        // scene must already look correct in Edit Mode instead of being a 100x100
        // centered RectTransform.
        Stretch(safe);
        Component<SafeAreaPanel>(safe.gameObject);

        RectTransform feedbackView = Panel(safe, "Feedback", Color.white, false);
        Fixed(feedbackView, new Vector2(0.5f, 0f), new Vector2(0, 190), new Vector2(700, 76));
        StyleSprite(feedbackView, neonPill, Image.Type.Sliced, new Color(1f, 1f, 1f, 0.94f));
        DisableOutline(feedbackView.gameObject);

        // V1 used a separate cyan Accent strip. With NeonPill the sprite already
        // provides the cyan/magenta edge, so the old Accent reads as a stray line.
        Transform oldFeedbackAccent = feedbackView.Find("Accent");
        if (oldFeedbackAccent != null)
            Undo.DestroyObjectImmediate(oldFeedbackAccent.gameObject);

        var message = Text(feedbackView, "Message", string.Empty, 28);
        message.fontStyle = FontStyles.Bold;
        Anchors(message.rectTransform, new Vector2(0.045f, 0.10f), new Vector2(0.955f, 0.90f));
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
        Reference(feedbackPresenter, "accent", null);

        RectTransform pause = Panel(safe, "PauseButton", Color.white, true);
        Fixed(pause, new Vector2(1, 1), new Vector2(-28, -28), new Vector2(96, 96));
        StyleSprite(pause, neonCircle, Image.Type.Simple, new Color(1f, 1f, 1f, 0.96f), true);
        DisableOutline(pause.gameObject);
        Button pauseButton = Button(pause.gameObject);

        RectTransform pauseIcon = Child(pause, "PauseIcon");
        Stretch(pauseIcon);
        Anchors(
            Panel(pauseIcon, "LeftBar", Color.white, false),
            new Vector2(0.34f, 0.29f),
            new Vector2(0.43f, 0.71f)
        );
        Anchors(
            Panel(pauseIcon, "RightBar", Color.white, false),
            new Vector2(0.57f, 0.29f),
            new Vector2(0.66f, 0.71f)
        );

        RectTransform playIcon = Child(pause, "PlayIcon");
        Anchors(playIcon, new Vector2(0.35f, 0.28f), new Vector2(0.70f, 0.72f));
        // PlayIconGraphic is a custom Graphic, so make the renderer explicit.
        // The previous generated object had no CanvasRenderer and therefore drew
        // an empty button while paused.
        Component<CanvasRenderer>(playIcon.gameObject);
        var triangle = Component<PlayIconGraphic>(playIcon.gameObject);
        triangle.color = Color.white;
        triangle.raycastTarget = false;
        playIcon.gameObject.SetActive(false);
        var manual = Component<ManualPauseButton>(pause.gameObject);
        Reference(manual, "suspension", suspension);
        Reference(manual, "button", pauseButton);
        Reference(manual, "input", player.GetComponent<StarterAssetsInputs>());
        Reference(manual, "pauseIcon", pauseIcon.gameObject);
        Reference(manual, "playIcon", playIcon.gameObject);

        // KnowledgeOverlay stays on the high presentation canvas so it can
        // block gameplay pointer input and keep the modal above the HUD, but
        // the overlay itself is transparent. This prevents the dark tint from
        // washing over joystick/action-button edges.
        RectTransform overlay = Panel(
            root.transform,
            "KnowledgeOverlay",
            Color.clear,
            true
        );
        Stretch(overlay);
        overlay.SetAsLastSibling();

        // Separate visual dimmer: nested canvas with its own sorting order,
        // below the normal HUD canvas (GamePoc HUD is order 0) but still as a
        // Screen Space Overlay canvas, so it darkens only the 3D world. Because
        // it is a child of KnowledgeOverlay it automatically follows the modal
        // active state without adding runtime presentation logic.
        RectTransform worldDimmer = Panel(
            overlay,
            "WorldDimmer",
            new Color(Navy.r, Navy.g, Navy.b, 0.58f),
            false
        );
        Stretch(worldDimmer);
        worldDimmer.SetAsFirstSibling();
        Canvas dimmerCanvas = Component<Canvas>(worldDimmer.gameObject);
        dimmerCanvas.overrideSorting = true;
        dimmerCanvas.sortingOrder = -100;

        RectTransform modalSafe = Child(overlay, "SafeArea");
        Stretch(modalSafe);
        Component<SafeAreaPanel>(modalSafe.gameObject);

        RectTransform card = Panel(modalSafe, "Card", Color.white, true);
        // Keep the modal inside the gameplay HUD gutters: above the inventory strip
        // and away from the left/right touch controls.
        Anchors(card, new Vector2(0.18f, 0.13f), new Vector2(0.82f, 0.94f));
        StyleSprite(card, neonPill, Image.Type.Sliced, Color.white);
        DisableOutline(card.gameObject);

        // NeonPill is primarily a glowing frame. Give the card an opaque dark
        // interior so paused gameplay HUD elements do not visually bleed through it.
        RectTransform cardFill = Panel(
            card,
            "CardFill",
            new Color(Indigo.r, Indigo.g, Indigo.b, 0.985f),
            false
        );
        Anchors(cardFill, new Vector2(0.018f, 0.028f), new Vector2(0.982f, 0.972f));
        cardFill.SetAsFirstSibling();

        RectTransform topAccent = Panel(card, "TopAccent", Cyan, false);
        Anchors(topAccent, new Vector2(0.34f, 0.972f), new Vector2(0.66f, 0.978f));

        RectTransform content = Child(card, "Content");
        Anchors(content, new Vector2(0.045f, 0.045f), new Vector2(0.955f, 0.955f));

        // The main menu is anchor-driven rather than layout-group driven. Manual
        // normalized anchors give the Knowledge card stable proportions at phone
        // and desktop aspect ratios instead of letting a VerticalLayoutGroup squash
        // the preview into a debug-looking strip.
        var layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
            layout.enabled = false;

        TMP_Text heading = Text(content, "Heading", "KNOWLEDGE ACQUIRED", 31);
        heading.color = Cyan;
        heading.fontStyle = FontStyles.Bold;
        Anchors(heading.rectTransform, new Vector2(0.18f, 0.885f), new Vector2(0.82f, 0.97f));

        RectTransform previewFrame = Panel(content, "PreviewFrame", Color.white, false);
        Anchors(previewFrame, new Vector2(0.27f, 0.38f), new Vector2(0.73f, 0.86f));
        StyleSprite(
            previewFrame,
            neonPill,
            Image.Type.Sliced,
            new Color(1f, 1f, 1f, 0.78f)
        );
        DisableOutline(previewFrame.gameObject);
        // CharacterRender uses a square RenderTexture/AspectRatioFitter. Mask it
        // to the neon frame so the preview can never bleed into title/text space.
        Component<RectMask2D>(previewFrame.gameObject);

        RectTransform imageRect = Child(previewFrame, "CharacterRender");
        Anchors(imageRect, new Vector2(0.055f, 0.08f), new Vector2(0.945f, 0.92f));
        var image = Component<RawImage>(imageRect.gameObject);
        image.color = Color.white;
        image.raycastTarget = false;
        var aspect = Component<AspectRatioFitter>(imageRect.gameObject);
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 1f;

        TMP_Text title = Text(content, "SkillTitle", "", 40);
        title.fontStyle = FontStyles.Bold;
        Anchors(title.rectTransform, new Vector2(0.10f, 0.285f), new Vector2(0.90f, 0.375f));

        TMP_Text description = Text(content, "Description", "", 25);
        description.color = new Color(0.90f, 0.92f, 1f, 1f);
        Anchors(
            description.rectTransform,
            new Vector2(0.10f, 0.205f),
            new Vector2(0.90f, 0.285f)
        );

        RectTransform instructionPlate = Panel(
            content,
            "InstructionPlate",
            Color.white,
            false
        );
        // Leave enough vertical room for two-line instructions (grenades and future
        // skills) while keeping the same compact NeonPill treatment for short text.
        Anchors(
            instructionPlate,
            new Vector2(0.17f, 0.105f),
            new Vector2(0.83f, 0.215f)
        );
        StyleSprite(
            instructionPlate,
            neonPill,
            Image.Type.Sliced,
            new Color(1f, 1f, 1f, 0.58f)
        );

        // V4 kept Instructions as a sibling of the plate. Move/reuse that generated
        // object inside the plate so wrapping/autosizing is constrained by the pill.
        Transform legacyInstructions = content.Find("Instructions");
        if (legacyInstructions != null && legacyInstructions.parent != instructionPlate)
        {
            Undo.RecordObject(legacyInstructions, "Reparent Knowledge instructions");
            legacyInstructions.SetParent(instructionPlate, false);
        }
        TMP_Text instructions = Text(instructionPlate, "Instructions", "", 22);
        instructions.color = new Color(0.92f, 0.97f, 1f, 1f);
        instructions.fontStyle = FontStyles.Bold;
        instructions.enableAutoSizing = true;
        instructions.fontSizeMin = 15f;
        instructions.fontSizeMax = 22f;
        Anchors(
            instructions.rectTransform,
            new Vector2(0.055f, 0.10f),
            new Vector2(0.945f, 0.90f)
        );

        RectTransform acknowledge = Panel(content, "Acknowledge", Color.white, true);
        Anchors(acknowledge, new Vector2(0.34f, 0.018f), new Vector2(0.66f, 0.095f));
        StyleSprite(acknowledge, neonPill, Image.Type.Sliced, Color.white);
        DisableOutline(acknowledge.gameObject);

        TMP_Text gotIt = Text(acknowledge, "Label", "GOT IT", 29);
        gotIt.fontStyle = FontStyles.Bold;
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
        // Three-quarter side view so future fire/throw demos read left-to-right
        // instead of aiming directly at the viewer.
        actorRoot.localRotation = Quaternion.Euler(0f, -40f, 0f);
        actorRoot.localScale = Vector3.one;
        actorRoot.gameObject.SetActive(false);
        Camera camera = Component<Camera>(Child(rig, "PreviewCamera").gameObject);
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        // The PreviewFrame owns the background. Keep the RenderTexture clear
        // transparent so its square 512x512 surface never appears as a dark box
        // inside the wider neon frame.
        camera.backgroundColor = new Color(Navy.r, Navy.g, Navy.b, 0f);
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
        string instructions,
        SkillDemoType demoType,
        CharacterActionId action,
        int shotsPerBurst,
        float shotInterval,
        float burstPause
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
            tutorial.localizationKey = "knowledge." + skill.Id;
        }

        // One-time migration for the initial generated tutorials. Once a tutorial
        // has a non-Stance demo type we preserve hand-tuned weapon timing on reruns.
        bool untouchedDemo =
            tutorial.demoType == SkillDemoType.Stance
            && tutorial.action == CharacterActionId.EquippedStance;

        if (created || (untouchedDemo && demoType != SkillDemoType.Stance))
        {
            tutorial.demoType = demoType;
            tutorial.action = action;

            if (demoType == SkillDemoType.WeaponFire)
            {
                tutorial.weaponInitialDelay = 0.35f;
                tutorial.weaponShotsPerBurst = Mathf.Max(1, shotsPerBurst);
                tutorial.weaponShotInterval = Mathf.Max(0.05f, shotInterval);
                tutorial.weaponBurstPause = Mathf.Max(0f, burstPause);
                tutorial.weaponBoltDistance = 2.5f;
                tutorial.weaponRecoilDistance = 0.035f;
                tutorial.weaponRecoilDuration = 0.12f;
            }
        }

        // The original generated framing was conservative (1.15; an earlier
        // menu-style pass used 0.85). 0.80 fits Amy/Granny well in the current
        // compact frame. Migrate only those known untouched defaults and preserve
        // any other hand-tuned value.
        if (
            Mathf.Approximately(tutorial.distanceMultiplier, 1.15f)
            || Mathf.Approximately(tutorial.distanceMultiplier, 0.85f)
        )
        {
            tutorial.distanceMultiplier = 0.80f;
        }

        if (created || untouchedDemo)
        {
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

        // Match the existing menu button transitions.
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.74f, 0.78f, 0.90f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.40f, 0.40f, 0.50f, 0.38f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        return button;
    }

    private static Sprite SpriteByGuid(string guid, string label)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new InvalidOperationException(
                $"Gameplay presentation could not load menu sprite '{label}' ({guid})."
            );
        return sprite;
    }

    private static void StyleSprite(
        RectTransform rect,
        Sprite sprite,
        Image.Type type,
        Color color,
        bool preserveAspect = false
    )
    {
        var image = Component<Image>(rect.gameObject);
        image.sprite = sprite;
        image.type = type;
        image.color = color;
        image.preserveAspect = preserveAspect;
    }

    private static void DisableOutline(GameObject go)
    {
        var outline = go.GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;
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
