using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KidsVsAliens.Environment;
using KidsVsAliens.Interaction;
using UnityEditor;
using UnityEngine;

namespace KidsVsAliens.EditorTools
{
    public sealed class AlienChestPrefabBuilderWindow : EditorWindow
    {
        private const string DefaultWorldFolder =
            "Assets/Game/Prefabs/Environment/Chest/World";

        private const string DefaultMenuFolder =
            "Assets/Game/Prefabs/Environment/Chest/Menu";

        private const string VisualChildName = "Visual";
        private const string GlowPrefix = "GLOW_Rarity_";

        [SerializeField] private GameObject sourceModel;

        [Header("Output")]
        [SerializeField] private string worldOutputFolder =
            DefaultWorldFolder;

        [SerializeField] private string menuOutputFolder =
            DefaultMenuFolder;

        [SerializeField] private string basePrefabName = "";

        [Header("Visual")]
        [SerializeField] private Vector3 modelRotationOffset =
            new Vector3(-90f, 180f, 0f);

        [SerializeField, Min(0.01f)]
        private float modelScale = 0.60f;

        [Header("World Chest Defaults")]
        [SerializeField] private bool generateBaseCollider = true;

        [SerializeField, Min(0f)]
        private float colliderPadding = 0.02f;

        [SerializeField, Min(0.35f)]
        private float proximityRadius = 0.75f;

        [SerializeField, Min(0.05f)]
        private float proximityHoldDuration = 3.0f;

        [Tooltip(
            "Optional. Used only when LootChest is added for the first time. " +
            "Leave empty to configure loot later on the generated world prefab."
        )]
        [SerializeField]
        private GameObject initialLootPrefab;

        [MenuItem(
            "Tools/Kids VS Aliens/Helpers/Alien Chest Prefab Builder")]
        public static void Open()
        {
            GetWindow<AlienChestPrefabBuilderWindow>(
                false,
                "Alien Chest Prefab Builder",
                true);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Alien Chest Prefab Builder V2",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Creates TWO prefabs from one chest FBX:\n\n" +
                "WORLD: collider + LootChest + proximity hold + progress ring.\n" +
                "MENU: clean visual wrapper only (no collider, loot or proximity gameplay).\n\n" +
                "Both keep the FBX nested so later FBX reimports update automatically.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            sourceModel =
                (GameObject)EditorGUILayout.ObjectField(
                    "Source Model",
                    sourceModel,
                    typeof(GameObject),
                    false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected Model"))
                    UseSelectedModel();

                if (GUILayout.Button("Ping Source") &&
                    sourceModel != null)
                {
                    EditorGUIUtility.PingObject(sourceModel);
                }
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(
                "Output",
                EditorStyles.boldLabel);

            worldOutputFolder =
                EditorGUILayout.TextField(
                    "World Folder",
                    worldOutputFolder);

            menuOutputFolder =
                EditorGUILayout.TextField(
                    "Menu Folder",
                    menuOutputFolder);

            basePrefabName =
                EditorGUILayout.TextField(
                    "Base Prefab Name",
                    basePrefabName);

            EditorGUILayout.HelpBox(
                "Example source KVA_AlienChest_POC_V1 becomes:\n" +
                "PF_AlienChest_POC_V1\n" +
                "PF_AlienChest_POC_V1_MenuPreview",
                MessageType.None);

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(
                "Visual",
                EditorStyles.boldLabel);

            modelRotationOffset =
                EditorGUILayout.Vector3Field(
                    "Rotation Offset",
                    modelRotationOffset);

            modelScale =
                Mathf.Max(
                    0.01f,
                    EditorGUILayout.FloatField(
                        "Model Scale",
                        modelScale));

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(
                "World Chest Defaults",
                EditorStyles.boldLabel);

            generateBaseCollider =
                EditorGUILayout.Toggle(
                    "Generate Base Collider",
                    generateBaseCollider);

            using (new EditorGUI.DisabledScope(
                       !generateBaseCollider))
            {
                colliderPadding =
                    Mathf.Max(
                        0f,
                        EditorGUILayout.FloatField(
                            "Collider Padding",
                            colliderPadding));
            }

            proximityRadius =
                Mathf.Max(
                    0.35f,
                    EditorGUILayout.FloatField(
                        "Proximity Radius",
                        proximityRadius));

            proximityHoldDuration =
                Mathf.Max(
                    0.05f,
                    EditorGUILayout.FloatField(
                        "Hold Duration",
                        proximityHoldDuration));

            initialLootPrefab =
                (GameObject)EditorGUILayout.ObjectField(
                    "Initial Loot Prefab",
                    initialLootPrefab,
                    typeof(GameObject),
                    false);

            EditorGUILayout.Space(10);

            using (new EditorGUI.DisabledScope(
                       sourceModel == null))
            {
                if (GUILayout.Button(
                        "Create / Update WORLD + MENU Prefabs",
                        GUILayout.Height(38)))
                {
                    BuildBoth();
                }
            }
        }

        private void UseSelectedModel()
        {
            GameObject selected =
                Selection.activeObject as GameObject;

            if (selected == null)
            {
                Debug.LogWarning(
                    "Select the imported chest FBX/model asset in the Project window first.");

                return;
            }

            string path =
                AssetDatabase.GetAssetPath(selected);

            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning(
                    "The selected object is not a Project asset.");

                return;
            }

            sourceModel = selected;

            if (string.IsNullOrWhiteSpace(basePrefabName))
            {
                basePrefabName =
                    BuildBaseName(sourceModel.name);
            }
        }

        private void BuildBoth()
        {
            if (!ValidateSource())
                return;

            EnsureFolder(worldOutputFolder);
            EnsureFolder(menuOutputFolder);

            string logicalName =
                string.IsNullOrWhiteSpace(basePrefabName)
                    ? BuildBaseName(sourceModel.name)
                    : NormalizeBaseName(basePrefabName);

            string worldName =
                $"PF_{logicalName}";

            string menuName =
                $"PF_{logicalName}_MenuPreview";

            string worldPath =
                $"{worldOutputFolder}/{worldName}.prefab";

            string menuPath =
                $"{menuOutputFolder}/{menuName}.prefab";

            BuildWorldPrefab(
                worldPath,
                worldName);

            BuildMenuPrefab(
                menuPath,
                menuName);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject worldPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    worldPath);

            GameObject menuPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    menuPath);

            Selection.objects =
                new UnityEngine.Object[]
                {
                    worldPrefab,
                    menuPrefab
                };

            if (worldPrefab != null)
                EditorGUIUtility.PingObject(worldPrefab);

            Debug.Log(
                "Alien chest prefabs ready:\n" +
                $"WORLD: {worldPath}\n" +
                $"MENU:  {menuPath}");
        }

        private void BuildWorldPrefab(
            string prefabPath,
            string prefabName)
        {
            GameObject root = null;
            bool loadedPrefabContents = false;

            try
            {
                root =
                    LoadOrCreateRoot(
                        prefabPath,
                        prefabName,
                        out loadedPrefabContents);

                ReplaceVisual(root.transform);

                ChestVisualRig rig =
                    ConfigureVisualRig(root);

                LootChest chest =
                    root.GetComponent<LootChest>();

                bool addedLootChest = chest == null;

                if (chest == null)
                    chest = root.AddComponent<LootChest>();

                if (addedLootChest)
                    ConfigureNewLootChest(chest);

                if (generateBaseCollider)
                    CreateOrUpdateWorldCollider(root);
                else
                    RemoveRootCollider(root);

                SetupWorldProximity(
                    root,
                    rig,
                    chest);

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath);
            }
            finally
            {
                CleanupRoot(
                    root,
                    loadedPrefabContents);
            }
        }

        private void BuildMenuPrefab(
            string prefabPath,
            string prefabName)
        {
            GameObject root = null;
            bool loadedPrefabContents = false;

            try
            {
                root =
                    LoadOrCreateRoot(
                        prefabPath,
                        prefabName,
                        out loadedPrefabContents);

                ReplaceVisual(root.transform);

                // Keep ONLY the clean visual cache on menu preview.
                ConfigureVisualRig(root);

                RemoveMenuGameplay(root);

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath);
            }
            finally
            {
                CleanupRoot(
                    root,
                    loadedPrefabContents);
            }
        }

        private GameObject LoadOrCreateRoot(
            string prefabPath,
            string prefabName,
            out bool loadedPrefabContents)
        {
            loadedPrefabContents = false;

            if (File.Exists(prefabPath))
            {
                GameObject loaded =
                    PrefabUtility.LoadPrefabContents(
                        prefabPath);

                loadedPrefabContents = true;
                loaded.name = prefabName;
                return loaded;
            }

            return new GameObject(prefabName);
        }

        private void ReplaceVisual(
            Transform prefabRoot)
        {
            Transform oldVisual =
                prefabRoot.Find(
                    VisualChildName);

            if (oldVisual != null)
                DestroyImmediate(oldVisual.gameObject);

            GameObject visualObject =
                new GameObject(
                    VisualChildName);

            Transform visual =
                visualObject.transform;

            visual.SetParent(
                prefabRoot,
                false);

            visual.localPosition =
                Vector3.zero;

            visual.localRotation =
                Quaternion.identity;

            visual.localScale =
                Vector3.one;

            GameObject modelInstance =
                PrefabUtility.InstantiatePrefab(
                    sourceModel) as GameObject;

            if (modelInstance == null)
            {
                DestroyImmediate(visualObject);

                throw new InvalidOperationException(
                    "Unity could not instantiate the selected model.");
            }

            Transform modelTransform =
                modelInstance.transform;

            modelTransform.SetParent(
                visual,
                false);

            modelTransform.localPosition =
                Vector3.zero;

            modelTransform.localRotation =
                Quaternion.Euler(
                    modelRotationOffset);

            modelTransform.localScale =
                Vector3.one * modelScale;
        }

        private ChestVisualRig ConfigureVisualRig(
            GameObject root)
        {
            ChestVisualRig rig =
                root.GetComponent<ChestVisualRig>();

            if (rig == null)
                rig = root.AddComponent<ChestVisualRig>();

            Transform visualRoot =
                root.transform.Find(
                    VisualChildName);

            Transform lidPivot =
                FindRecursive(
                    visualRoot,
                    "LidPivot");

            Transform interactionAnchor =
                FindRecursive(
                    visualRoot,
                    "InteractionAnchor");

            Transform lootSpawnAnchor =
                FindRecursive(
                    visualRoot,
                    "LootSpawnAnchor");

            Renderer[] glowRenderers =
                visualRoot
                    .GetComponentsInChildren<Renderer>(true)
                    .Where(IsRarityGlowRenderer)
                    .Distinct()
                    .ToArray();

            rig.Configure(
                visualRoot,
                lidPivot,
                interactionAnchor,
                lootSpawnAnchor,
                glowRenderers);

            EditorUtility.SetDirty(rig);

            WarnAboutMissingReferences(
                lidPivot,
                interactionAnchor,
                lootSpawnAnchor,
                glowRenderers);

            return rig;
        }

        private void ConfigureNewLootChest(
            LootChest chest)
        {
            SerializedObject serialized =
                new SerializedObject(chest);

            SerializedProperty openOnStart =
                serialized.FindProperty(
                    "openOnStart");

            if (openOnStart != null)
                openOnStart.boolValue = false;

            if (initialLootPrefab != null)
            {
                SerializedProperty loot =
                    serialized.FindProperty(
                        "possibleLootPrefabs");

                if (loot != null)
                {
                    loot.arraySize = 1;

                    loot.GetArrayElementAtIndex(0)
                        .objectReferenceValue =
                            initialLootPrefab;
                }

                SerializedProperty min =
                    serialized.FindProperty(
                        "minimumLootCount");

                SerializedProperty max =
                    serialized.FindProperty(
                        "maximumLootCount");

                if (min != null)
                    min.intValue = 1;

                if (max != null)
                    max.intValue = 1;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetupWorldProximity(
            GameObject root,
            ChestVisualRig rig,
            LootChest chest)
        {
            Transform sensorTransform =
                root.transform.Find(
                    "ProximityHoldSensor");

            GameObject sensorObject;

            if (sensorTransform == null)
            {
                sensorObject =
                    new GameObject(
                        "ProximityHoldSensor");

                sensorTransform =
                    sensorObject.transform;

                sensorTransform.SetParent(
                    root.transform,
                    false);
            }
            else
            {
                sensorObject =
                    sensorTransform.gameObject;
            }

            if (rig.InteractionAnchor != null)
            {
                sensorTransform.position =
                    rig.InteractionAnchor.position;
            }
            else
            {
                sensorTransform.localPosition =
                    Vector3.zero;
            }

            sensorTransform.localRotation =
                Quaternion.identity;

            sensorTransform.localScale =
                Vector3.one;

            SphereCollider sphere =
                sensorObject.GetComponent<
                    SphereCollider>();

            if (sphere == null)
                sphere =
                    sensorObject.AddComponent<
                        SphereCollider>();

            sphere.isTrigger = true;
            sphere.radius = proximityRadius;

            ProximityHoldTrigger trigger =
                sensorObject.GetComponent<
                    ProximityHoldTrigger>();

            if (trigger == null)
                trigger =
                    sensorObject.AddComponent<
                        ProximityHoldTrigger>();

            trigger.Configure(
                proximityHoldDuration,
                true,
                true);

            ProximityProgressRing ring =
                sensorObject.GetComponent<
                    ProximityProgressRing>();

            if (ring == null)
                ring =
                    sensorObject.AddComponent<
                        ProximityProgressRing>();

            ring.Configure(
                trigger,
                rig.InteractionAnchor);

            ChestProximityOpener opener =
                sensorObject.GetComponent<
                    ChestProximityOpener>();

            if (opener == null)
                opener =
                    sensorObject.AddComponent<
                        ChestProximityOpener>();

            opener.Configure(
                trigger,
                chest);

            EditorUtility.SetDirty(sphere);
            EditorUtility.SetDirty(trigger);
            EditorUtility.SetDirty(ring);
            EditorUtility.SetDirty(opener);
        }

        private static void RemoveMenuGameplay(
            GameObject root)
        {
            // Menu prefab intentionally has no physical/gameplay chest logic.
            DestroyComponentsInChildren<
                ChestProximityOpener>(root);

            DestroyComponentsInChildren<
                ProximityProgressRing>(root);

            DestroyComponentsInChildren<
                ProximityHoldTrigger>(root);

            DestroyComponentsInChildren<
                Collider>(root);

            LootChest chest =
                root.GetComponent<LootChest>();

            if (chest != null)
                DestroyImmediate(chest);

            Transform sensor =
                root.transform.Find(
                    "ProximityHoldSensor");

            if (sensor != null)
                DestroyImmediate(sensor.gameObject);
        }

        private static void DestroyComponentsInChildren<T>(
            GameObject root)
            where T : Component
        {
            T[] components =
                root.GetComponentsInChildren<T>(true);

            foreach (T component in components)
            {
                if (component != null)
                    DestroyImmediate(component);
            }
        }

        private void CreateOrUpdateWorldCollider(
            GameObject root)
        {
            BoxCollider box =
                root.GetComponent<BoxCollider>();

            if (box == null)
                box = root.AddComponent<BoxCollider>();

            Transform visual =
                root.transform.Find(
                    VisualChildName);

            if (visual == null)
            {
                Debug.LogWarning(
                    "Chest builder: Visual root not found, collider was not updated.");

                return;
            }

            // Fit the collider to the CLOSED CHEST'S real visible geometry.
            //
            // Do NOT use only the Blender 'Base' node; that made the collider
            // much too short vertically. Also ignore rarity glow meshes so
            // emissive decoration cannot inflate the physical bounds.
            Renderer[] solidRenderers =
                visual
                    .GetComponentsInChildren<Renderer>(true)
                    .Where(
                        renderer =>
                            renderer != null &&
                            (renderer is MeshRenderer ||
                             renderer is SkinnedMeshRenderer) &&
                            !IsRarityGlowRenderer(renderer))
                    .ToArray();

            if (!TryCalculateLocalBounds(
                    root.transform,
                    solidRenderers,
                    out Bounds localBounds))
            {
                Debug.LogWarning(
                    "Chest builder: could not calculate solid visual bounds for the world collider.");

                return;
            }

            Vector3 padding =
                Vector3.one *
                colliderPadding;

            box.center =
                localBounds.center;

            box.size =
                localBounds.size +
                padding * 2f;
        }


        private static void RemoveRootCollider(
            GameObject root)
        {
            BoxCollider box =
                root.GetComponent<BoxCollider>();

            if (box != null)
                DestroyImmediate(box);
        }

        private static bool TryCalculateLocalBounds(
            Transform root,
            IReadOnlyList<Renderer> renderers,
            out Bounds localBounds)
        {
            localBounds = default;

            if (renderers == null ||
                renderers.Count == 0)
            {
                return false;
            }

            bool initialized = false;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                Bounds worldBounds =
                    renderer.bounds;

                Vector3 center =
                    worldBounds.center;

                Vector3 extents =
                    worldBounds.extents;

                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 worldCorner =
                                center +
                                Vector3.Scale(
                                    extents,
                                    new Vector3(
                                        x,
                                        y,
                                        z));

                            Vector3 localCorner =
                                root.InverseTransformPoint(
                                    worldCorner);

                            if (!initialized)
                            {
                                localBounds =
                                    new Bounds(
                                        localCorner,
                                        Vector3.zero);

                                initialized = true;
                            }
                            else
                            {
                                localBounds.Encapsulate(
                                    localCorner);
                            }
                        }
                    }
                }
            }

            return initialized;
        }

        private bool ValidateSource()
        {
            if (sourceModel == null)
                return false;

            string path =
                AssetDatabase.GetAssetPath(
                    sourceModel);

            if (string.IsNullOrWhiteSpace(path))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Source",
                    "Choose the imported FBX/model asset from the Project window.",
                    "OK");

                return false;
            }

            if (!IsValidAssetsFolder(worldOutputFolder) ||
                !IsValidAssetsFolder(menuOutputFolder))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Output Folder",
                    "Both output folders must be inside Assets/.",
                    "OK");

                return false;
            }

            return true;
        }

        private static bool IsValidAssetsFolder(
            string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && path.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal);
        }

        private static void CleanupRoot(
            GameObject root,
            bool loadedPrefabContents)
        {
            if (root == null)
                return;

            if (loadedPrefabContents)
            {
                PrefabUtility.UnloadPrefabContents(
                    root);
            }
            else
            {
                DestroyImmediate(root);
            }
        }

        private static void WarnAboutMissingReferences(
            Transform lidPivot,
            Transform interactionAnchor,
            Transform lootSpawnAnchor,
            Renderer[] glowRenderers)
        {
            if (lidPivot == null)
                Debug.LogWarning(
                    "Chest builder: LidPivot was not found.");

            if (interactionAnchor == null)
                Debug.LogWarning(
                    "Chest builder: InteractionAnchor was not found.");

            if (lootSpawnAnchor == null)
                Debug.LogWarning(
                    "Chest builder: LootSpawnAnchor was not found.");

            if (glowRenderers == null ||
                glowRenderers.Length == 0)
            {
                Debug.LogWarning(
                    $"Chest builder: no Renderer under '{GlowPrefix}*' was found.");
            }
        }

        private static bool IsRarityGlowRenderer(
            Renderer renderer)
        {
            if (renderer == null)
                return false;

            Transform current =
                renderer.transform;

            while (current != null)
            {
                if (current.name.StartsWith(
                        GlowPrefix,
                        StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static Transform FindRecursive(
            Transform root,
            string targetName)
        {
            if (root == null)
                return null;

            if (root.name.Equals(
                    targetName,
                    StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform result =
                    FindRecursive(
                        root.GetChild(i),
                        targetName);

                if (result != null)
                    return result;
            }

            return null;
        }

        private static string BuildBaseName(
            string sourceName)
        {
            string result =
                NormalizeBaseName(sourceName);

            if (result.StartsWith(
                    "KVA_",
                    StringComparison.OrdinalIgnoreCase))
            {
                result =
                    result.Substring(4);
            }

            return result;
        }

        private static string NormalizeBaseName(
            string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "AlienChest";

            string result =
                SanitizeName(input);

            if (result.StartsWith(
                    "PF_",
                    StringComparison.OrdinalIgnoreCase))
            {
                result =
                    result.Substring(3);
            }

            if (result.EndsWith(
                    "_MenuPreview",
                    StringComparison.OrdinalIgnoreCase))
            {
                result =
                    result.Substring(
                        0,
                        result.Length -
                        "_MenuPreview".Length);
            }

            return result;
        }

        private static string SanitizeName(
            string input)
        {
            char[] invalid =
                Path.GetInvalidFileNameChars();

            string result =
                new string(
                    input
                        .Where(
                            c => !invalid.Contains(c))
                        .ToArray());

            return result
                .Replace(" ", "_")
                .Replace("-", "_")
                .Trim('_');
        }

        private static void EnsureFolder(
            string assetFolder)
        {
            string normalized =
                assetFolder
                    .Replace("\\", "/")
                    .TrimEnd('/');

            if (AssetDatabase.IsValidFolder(
                    normalized))
            {
                return;
            }

            string[] parts =
                normalized.Split('/');

            string current =
                parts[0];

            for (int i = 1;
                 i < parts.Length;
                 i++)
            {
                string next =
                    $"{current}/{parts[i]}";

                if (!AssetDatabase.IsValidFolder(
                        next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);
                }

                current = next;
            }
        }
    }
}
