#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CharacterSetupHelper : EditorWindow
{
    private const string DefaultAnimatorControllerPath =
        "Assets/Game/Animations/Player/HumanoidShooter.controller";

    private const string DefaultMenuCatalogPath = "Assets/Game/UI/Menu/MainMenuCatalog.asset";

    private const string CharacterPrefabFolder = "Assets/Game/Prefabs/Player/Characters";

    private const string MenuCharacterPrefabFolder =
        "Assets/Game/Prefabs/Menu/Characters";

    private const string MenuItemFolder = "Assets/Game/UI/Menu";

    [SerializeField]
    private GameObject modelAsset;

    [SerializeField]
    private RuntimeAnimatorController animatorController;

    [SerializeField]
    private MenuPreviewCatalog menuCatalog;

    [SerializeField]
    private string characterName = "";

    [SerializeField]
    private string displayName = "";

    [SerializeField]
    private string characterId = "";

    [SerializeField]
    private Color auraColor = Color.magenta;

    [SerializeField]
    private Vector3 previewEulerAngles = new Vector3(0f, 180f, 0f);

    [SerializeField]
    private bool createMenuItem = true;

    [SerializeField]
    private bool addToMenuCatalog = true;

    private Vector2 scrollPosition;
    private string statusMessage = "";

    [MenuItem("Tools/Kids VS Aliens/Helpers/Character Setup")]
    public static void Open()
    {
        GetWindow<CharacterSetupHelper>("Character Setup");
    }

    private void OnEnable()
    {
        if (animatorController == null)
        {
            animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                DefaultAnimatorControllerPath
            );
        }

        if (menuCatalog == null)
        {
            menuCatalog = AssetDatabase.LoadAssetAtPath<MenuPreviewCatalog>(DefaultMenuCatalogPath);
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Kids VS Aliens Character Setup", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Takes a Mixamo/model FBX and performs the full setup:\n"
                + "• Humanoid rig\n"
                + "• Extract embedded textures/materials\n"
                + "• Animator controller\n"
                + "• RightHand/WeaponSocket\n"
                + "• Gameplay CharacterVisual prefab\n"
                + "• Separate menu preview wrapper prefab\n"
                + "• MenuPreviewSettings on menu wrapper\n"
                + "• MenuPreviewItem linked to BOTH preview + gameplay character\n"
                + "• Optional MainMenuCatalog entry\n\n"
                + "Safe to run again: existing prefab/menu assets are NOT overwritten.",
            MessageType.Info
        );

        EditorGUILayout.Space(8);

        DrawSource();
        DrawIdentity();
        DrawCharacterOptions();
        DrawMenuOptions();
        DrawOutput();

        EditorGUILayout.Space(12);

        using (new EditorGUI.DisabledScope(modelAsset == null))
        {
            if (GUILayout.Button("FULL SETUP / REPAIR", GUILayout.Height(42)))
            {
                RunFullSetup();
            }
        }

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSource()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        GameObject newModel = (GameObject)
            EditorGUILayout.ObjectField("Model FBX", modelAsset, typeof(GameObject), false);

        if (EditorGUI.EndChangeCheck())
        {
            modelAsset = newModel;

            if (modelAsset != null)
            {
                AutoPopulateIdentity(modelAsset.name);
            }
        }

        animatorController = (RuntimeAnimatorController)
            EditorGUILayout.ObjectField(
                "Animator Controller",
                animatorController,
                typeof(RuntimeAnimatorController),
                false
            );

        EditorGUILayout.Space(8);
    }

    private void DrawIdentity()
    {
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);

        characterName = EditorGUILayout.TextField("Character Name", characterName);

        displayName = EditorGUILayout.TextField("Display Name", displayName);

        characterId = EditorGUILayout.TextField("ID", characterId);

        EditorGUILayout.Space(8);
    }

    private void DrawCharacterOptions()
    {
        EditorGUILayout.LabelField("Character", EditorStyles.boldLabel);

        auraColor = EditorGUILayout.ColorField("Aura Color", auraColor);

        previewEulerAngles = EditorGUILayout.Vector3Field(
            "Menu Preview Rotation",
            previewEulerAngles
        );

        EditorGUILayout.Space(8);
    }

    private void DrawMenuOptions()
    {
        EditorGUILayout.LabelField("Menu", EditorStyles.boldLabel);

        createMenuItem = EditorGUILayout.Toggle("Create Menu Item", createMenuItem);

        using (new EditorGUI.DisabledScope(!createMenuItem))
        {
            addToMenuCatalog = EditorGUILayout.Toggle("Add To Catalog", addToMenuCatalog);

            using (new EditorGUI.DisabledScope(!addToMenuCatalog))
            {
                menuCatalog = (MenuPreviewCatalog)
                    EditorGUILayout.ObjectField(
                        "Menu Catalog",
                        menuCatalog,
                        typeof(MenuPreviewCatalog),
                        false
                    );
            }
        }

        EditorGUILayout.Space(8);
    }

    private void DrawOutput()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        string safeName = GetSafeCharacterName();

        EditorGUILayout.SelectableLabel(
            $"Game: {CharacterPrefabFolder}/{safeName}.prefab",
            EditorStyles.textField,
            GUILayout.Height(EditorGUIUtility.singleLineHeight)
        );

        EditorGUILayout.SelectableLabel(
            $"Menu: {MenuCharacterPrefabFolder}/{safeName}_MenuPreview.prefab",
            EditorStyles.textField,
            GUILayout.Height(EditorGUIUtility.singleLineHeight)
        );

        if (createMenuItem)
        {
            EditorGUILayout.SelectableLabel(
                $"Item: {MenuItemFolder}/{safeName}_MenuItem.asset",
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight)
            );
        }

        if (modelAsset != null)
        {
            string modelPath = AssetDatabase.GetAssetPath(modelAsset);
            string modelFolder = GetAssetFolder(modelPath);

            EditorGUILayout.SelectableLabel(
                $"{modelFolder}/Textures",
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight)
            );

            EditorGUILayout.SelectableLabel(
                $"{modelFolder}/Materials",
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight)
            );
        }
    }

    private void RunFullSetup()
    {
        statusMessage = "";

        if (!ValidateInputs(out string validationError))
        {
            EditorUtility.DisplayDialog("Character Setup", validationError, "OK");
            return;
        }

        string modelPath = AssetDatabase.GetAssetPath(modelAsset);
        string safeName = GetSafeCharacterName();

        string gameplayPrefabPath =
            $"{CharacterPrefabFolder}/{safeName}.prefab";

        string menuPreviewPrefabPath =
            $"{MenuCharacterPrefabFolder}/{safeName}_MenuPreview.prefab";

        string menuItemPath =
            $"{MenuItemFolder}/{safeName}_MenuItem.asset";

        try
        {
            // Always repair/import source first.
            ConfigureAndRepairModel(modelPath);

            GameObject refreshedModel =
                AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            if (refreshedModel == null)
            {
                throw new InvalidOperationException(
                    "Unity could not reload the model after import."
                );
            }

            EnsureFolder(CharacterPrefabFolder);
            EnsureFolder(MenuCharacterPrefabFolder);
            EnsureFolder(MenuItemFolder);

            bool gameplayPrefabCreated = false;
            bool menuPreviewPrefabCreated = false;
            bool menuItemCreated = false;
            bool catalogAdded = false;

            // =====================================================
            // 1. GAMEPLAY CHARACTER
            // =====================================================

            GameObject gameplayPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    gameplayPrefabPath
                );

            if (gameplayPrefab == null)
            {
                gameplayPrefab =
                    CreateGameplayCharacterPrefab(
                        refreshedModel,
                        safeName,
                        gameplayPrefabPath
                    );

                gameplayPrefabCreated = true;
            }

            CharacterVisual gameplayCharacter =
                gameplayPrefab.GetComponent<CharacterVisual>();

            if (gameplayCharacter == null)
            {
                throw new InvalidOperationException(
                    $"Gameplay prefab '{gameplayPrefab.name}' has no CharacterVisual component."
                );
            }

            // =====================================================
            // 2. MENU PREVIEW WRAPPER
            //
            // This prefab REFERENCES the gameplay prefab as a nested
            // prefab instance. Meshes/textures/materials are not copied.
            // =====================================================

            GameObject menuPreviewPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    menuPreviewPrefabPath
                );

            if (menuPreviewPrefab == null)
            {
                menuPreviewPrefab =
                    CreateMenuPreviewPrefab(
                        gameplayPrefab,
                        safeName,
                        menuPreviewPrefabPath
                    );

                menuPreviewPrefabCreated = true;
            }

            // =====================================================
            // 3. MENU ITEM = THE CONNECTION
            // =====================================================

            MenuPreviewItem menuItem = null;

            if (createMenuItem)
            {
                menuItem =
                    AssetDatabase.LoadAssetAtPath<MenuPreviewItem>(
                        menuItemPath
                    );

                if (menuItem == null)
                {
                    menuItem =
                        ScriptableObject.CreateInstance<MenuPreviewItem>();

                    AssetDatabase.CreateAsset(
                        menuItem,
                        menuItemPath
                    );

                    menuItemCreated = true;
                }

                ConfigureCharacterMenuItem(
                    menuItem,
                    menuPreviewPrefab,
                    gameplayCharacter
                );

                if (addToMenuCatalog)
                {
                    catalogAdded =
                        AddMenuItemToCatalog(
                            menuItem,
                            menuCatalog
                        );
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = menuPreviewPrefab;
            EditorGUIUtility.PingObject(menuPreviewPrefab);

            string modelFolder = GetAssetFolder(modelPath);

            StringBuilder result = new StringBuilder();

            result.AppendLine("Character setup / repair complete.");
            result.AppendLine();

            result.AppendLine("Source:");
            result.AppendLine($"• {modelPath}");
            result.AppendLine("• Humanoid / Create From This Model");
            result.AppendLine("• Optimize GameObjects OFF");
            result.AppendLine($"• Textures → {modelFolder}/Textures");
            result.AppendLine($"• Materials → {modelFolder}/Materials");

            result.AppendLine();
            result.AppendLine(
                $"Gameplay prefab: {(gameplayPrefabCreated ? "created" : "kept existing")}"
            );
            result.AppendLine($"• {gameplayPrefabPath}");

            result.AppendLine(
                $"Menu preview: {(menuPreviewPrefabCreated ? "created" : "kept existing")}"
            );
            result.AppendLine($"• {menuPreviewPrefabPath}");

            if (createMenuItem)
            {
                result.AppendLine(
                    $"Menu item: {(menuItemCreated ? "created" : "updated")}"
                );
                result.AppendLine($"• {menuItemPath}");
                result.AppendLine("• previewPrefab → menu preview wrapper");
                result.AppendLine("• characterPrefab → gameplay CharacterVisual");

                if (addToMenuCatalog)
                {
                    result.AppendLine(
                        $"Catalog: {(catalogAdded ? "added" : "already present")}"
                    );
                }
            }

            statusMessage = result.ToString();
            Debug.Log(statusMessage, menuPreviewPrefab);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Character Setup Failed",
                exception.Message,
                "OK"
            );
        }
    }

    private bool ValidateInputs(out string error)
    {
        if (modelAsset == null)
        {
            error = "Select a model FBX.";
            return false;
        }

        string modelPath = AssetDatabase.GetAssetPath(modelAsset);

        if (string.IsNullOrWhiteSpace(modelPath))
        {
            error = "The selected model is not a project asset.";
            return false;
        }

        if (AssetImporter.GetAtPath(modelPath) is not ModelImporter)
        {
            error = "The selected asset is not an FBX/model handled by ModelImporter.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(characterName))
        {
            error = "Character Name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = "Display Name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(characterId))
        {
            error = "ID is required.";
            return false;
        }

        if (animatorController == null)
        {
            error =
                "Animator Controller is missing. "
                + "Assign HumanoidShooter or another controller.";
            return false;
        }

        if (createMenuItem && addToMenuCatalog && menuCatalog == null)
        {
            error = "Add To Catalog is enabled, but no Menu Catalog is assigned.";
            return false;
        }

        error = "";
        return true;
    }

    // =====================================================
    // MODEL IMPORT / MATERIALS / TEXTURES
    // =====================================================

    private void ConfigureAndRepairModel(string modelPath)
    {
        ModelImporter importer = GetModelImporter(modelPath);

        bool importerChanged = false;

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;

            importerChanged = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            importerChanged = true;
        }

        if (importer.optimizeGameObjects)
        {
            importer.optimizeGameObjects = false;
            importerChanged = true;
        }

        if (
            importer.materialImportMode
            != ModelImporterMaterialImportMode.ImportViaMaterialDescription
        )
        {
            importer.materialImportMode =
                ModelImporterMaterialImportMode.ImportViaMaterialDescription;

            importerChanged = true;
        }

        if (importerChanged)
        {
            importer.SaveAndReimport();
        }

        ExtractEmbeddedTextures(modelPath);
        CreateAndRemapExternalMaterials(modelPath);
    }

    private void ExtractEmbeddedTextures(string modelPath)
    {
        string modelFolder = GetAssetFolder(modelPath);

        string texturesFolder = $"{modelFolder}/Textures";

        EnsureFolder(texturesFolder);

        ModelImporter importer = GetModelImporter(modelPath);

        // Unity performs the same core extraction used by
        // "Materials > Extract Textures..." in the FBX inspector.
        //
        // If the model has no embedded textures, this is simply a no-op.
        importer.ExtractTextures(texturesFolder);

        AssetDatabase.Refresh();

        AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
    }

    private void CreateAndRemapExternalMaterials(string modelPath)
    {
        string modelFolder = GetAssetFolder(modelPath);

        string materialsFolder = $"{modelFolder}/Materials";

        EnsureFolder(materialsFolder);

        // Re-load after texture extraction/reimport.
        ModelImporter importer = GetModelImporter(modelPath);

        UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(modelPath);

        bool remapChanged = false;

        foreach (UnityEngine.Object subAsset in subAssets)
        {
            if (subAsset is not Material sourceMaterial)
                continue;

            string materialName = MakeSafeAssetFileName(sourceMaterial.name);

            string materialPath = $"{materialsFolder}/{materialName}.mat";

            Material externalMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (externalMaterial == null)
            {
                // Copy the imported material exactly as Unity built it.
                // At this point its texture references point at the extracted
                // texture assets, so the character keeps the original colors.
                externalMaterial = new Material(sourceMaterial) { name = sourceMaterial.name };

                AssetDatabase.CreateAsset(externalMaterial, materialPath);
            }

            AssetImporter.SourceAssetIdentifier sourceId = new AssetImporter.SourceAssetIdentifier(
                typeof(Material),
                sourceMaterial.name
            );

            importer.AddRemap(sourceId, externalMaterial);

            remapChanged = true;
        }

        if (remapChanged)
        {
            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private ModelImporter GetModelImporter(string modelPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;

        if (importer == null)
        {
            throw new InvalidOperationException($"ModelImporter not found for:\n{modelPath}");
        }

        return importer;
    }

    // =====================================================
    // PREFAB
    // =====================================================

    private GameObject CreateGameplayCharacterPrefab(
        GameObject model,
        string safeName,
        string prefabPath
    )
    {
        GameObject root = new GameObject(safeName);

        try
        {
            int playerLayer = LayerMask.NameToLayer("Player");

            if (playerLayer < 0)
            {
                throw new InvalidOperationException(
                    "Project layer 'Player' does not exist."
                );
            }

            root.layer = playerLayer;

            GameObject modelInstance =
                PrefabUtility.InstantiatePrefab(model) as GameObject;

            if (modelInstance == null)
            {
                throw new InvalidOperationException(
                    "Could not instantiate the source model."
                );
            }

            modelInstance.transform.SetParent(root.transform, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            SetLayerRecursively(modelInstance, playerLayer);

            Animator animator =
                modelInstance.GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                animator = modelInstance.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = animatorController;
            animator.applyRootMotion = false;

            Transform rightHand =
                animator.GetBoneTransform(HumanBodyBones.RightHand);

            if (rightHand == null)
            {
                throw new InvalidOperationException(
                    "Humanoid RightHand bone was not found. "
                        + "Open Rig > Configure and verify the avatar mapping."
                );
            }

            Transform weaponSocket = rightHand.Find("WeaponSocket");

            if (weaponSocket == null)
            {
                GameObject socketObject =
                    new GameObject("WeaponSocket");

                socketObject.layer = playerLayer;

                weaponSocket = socketObject.transform;

                weaponSocket.SetParent(rightHand, false);
                weaponSocket.localPosition = Vector3.zero;
                weaponSocket.localRotation = Quaternion.identity;
                weaponSocket.localScale = Vector3.one;
            }

            CharacterVisual characterVisual =
                root.AddComponent<CharacterVisual>();

            SerializedObject visualSO =
                new SerializedObject(characterVisual);

            visualSO.FindProperty("animator").objectReferenceValue =
                animator;

            visualSO.FindProperty("weaponSocket").objectReferenceValue =
                weaponSocket;

            visualSO.FindProperty("auraColor").colorValue =
                auraColor;

            visualSO.ApplyModifiedPropertiesWithoutUndo();

            // IMPORTANT:
            // Gameplay prefab intentionally has NO MenuPreviewSettings.
            // Menu presentation now lives in a separate wrapper prefab.

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath
                );

            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "Unity failed to save the gameplay character prefab."
                );
            }

            return prefab;
        }
        finally
        {
            DestroyImmediate(root);
        }
    }

    private GameObject CreateMenuPreviewPrefab(
        GameObject gameplayPrefab,
        string safeName,
        string prefabPath
    )
    {
        GameObject root =
            new GameObject($"{safeName}_MenuPreview");

        try
        {
            GameObject gameplayInstance =
                PrefabUtility.InstantiatePrefab(
                    gameplayPrefab
                ) as GameObject;

            if (gameplayInstance == null)
            {
                throw new InvalidOperationException(
                    "Could not instantiate the gameplay character prefab for menu preview."
                );
            }

            gameplayInstance.transform.SetParent(
                root.transform,
                false
            );

            gameplayInstance.transform.localPosition =
                Vector3.zero;

            gameplayInstance.transform.localRotation =
                Quaternion.identity;

            gameplayInstance.transform.localScale =
                Vector3.one;

            MenuPreviewSettings previewSettings =
                root.AddComponent<MenuPreviewSettings>();

            previewSettings.localOffset =
                Vector3.zero;

            previewSettings.localEulerAngles =
                previewEulerAngles;

            previewSettings.scaleMultiplier =
                1f;

            previewSettings.cameraTargetOffset =
                Vector3.zero;

            previewSettings.cameraDistanceMultiplier =
                1f;

            previewSettings.rotationSensitivity =
                0.25f;

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath
                );

            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "Unity failed to save the menu preview prefab."
                );
            }

            return prefab;
        }
        finally
        {
            DestroyImmediate(root);
        }
    }

    // =====================================================
    // MENU ITEM / CATALOG
    // =====================================================

    private void ConfigureCharacterMenuItem(
        MenuPreviewItem item,
        GameObject menuPreviewPrefab,
        CharacterVisual gameplayCharacterPrefab
    )
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        item.id = characterId.Trim();
        item.displayName = displayName.Trim();
        item.type = MenuPreviewType.Character;

        // What the menu displays.
        item.previewPrefab = menuPreviewPrefab;

        // What GamePoc actually spawns.
        item.characterPrefab = gameplayCharacterPrefab;

        // This is a character entry, so weapon data must be empty.
        item.weaponItemData = null;

        EditorUtility.SetDirty(item);
    }

    private bool AddMenuItemToCatalog(MenuPreviewItem menuItem, MenuPreviewCatalog catalog)
    {
        if (catalog == null || menuItem == null)
            return false;

        SerializedObject serializedCatalog = new SerializedObject(catalog);

        SerializedProperty items = serializedCatalog.FindProperty("items");

        if (items == null)
        {
            throw new InvalidOperationException("MenuPreviewCatalog.items field was not found.");
        }

        for (int i = 0; i < items.arraySize; i++)
        {
            if (items.GetArrayElementAtIndex(i).objectReferenceValue == menuItem)
            {
                return false;
            }
        }

        int newIndex = items.arraySize;

        items.InsertArrayElementAtIndex(newIndex);

        items.GetArrayElementAtIndex(newIndex).objectReferenceValue = menuItem;

        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(catalog);

        return true;
    }

    // =====================================================
    // NAMING / FOLDERS
    // =====================================================

    private void AutoPopulateIdentity(string modelName)
    {
        string baseName = RemoveBaseSuffix(modelName);

        string[] words = baseName.Split(
            new[] { '_', '-', ' ' },
            StringSplitOptions.RemoveEmptyEntries
        );

        if (words.Length == 0)
            return;

        StringBuilder pascal = new StringBuilder();

        StringBuilder readable = new StringBuilder();

        StringBuilder idBuilder = new StringBuilder();

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];

            if (string.IsNullOrWhiteSpace(word))
                continue;

            string normalized = char.ToUpperInvariant(word[0]) + word.Substring(1);

            pascal.Append(normalized);

            if (readable.Length > 0)
                readable.Append(' ');

            readable.Append(normalized);

            if (idBuilder.Length > 0)
                idBuilder.Append('_');

            idBuilder.Append(word.ToLowerInvariant());
        }

        characterName = pascal.ToString();

        displayName = readable.ToString();

        characterId = idBuilder.ToString();
    }

    private string RemoveBaseSuffix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        string result = value.Trim();

        string[] suffixes = { "_Base", "-Base", " Base" };

        foreach (string suffix in suffixes)
        {
            if (result.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                result = result.Substring(0, result.Length - suffix.Length);

                break;
            }
        }

        return result;
    }

    private string GetSafeCharacterName()
    {
        string value = characterName.Trim();

        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidCharacter.ToString(), "");
        }

        return value;
    }

    private string MakeSafeAssetFileName(string value)
    {
        string result = string.IsNullOrWhiteSpace(value) ? "Material" : value.Trim();

        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalidCharacter.ToString(), "");
        }

        return result;
    }

    private string GetAssetFolder(string assetPath)
    {
        string folder = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");

        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new InvalidOperationException(
                $"Could not determine asset folder for:\n{assetPath}"
            );
        }

        return folder;
    }

    private void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;

        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || folderPath == "Assets")
        {
            return;
        }

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");

        string folderName = Path.GetFileName(folderPath);

        if (string.IsNullOrWhiteSpace(parent))
            parent = "Assets";

        EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
#endif