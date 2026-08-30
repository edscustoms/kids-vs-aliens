using System;
using System.IO;
using System.Linq;
using KidsVsAliens.Environment;
using UnityEditor;
using UnityEngine;

namespace KidsVsAliens.EditorTools.Fence
{
    public sealed class FenceSectionPrefabBuilderWindow : EditorWindow
    {
        private const string DefaultOutputFolder =
            "Assets/Game/Prefabs/Environment/Fence";

        private const string DefaultGeneratedMeshFolder =
            "Assets/Game/Art/Environment/Fence/Generated";

        [SerializeField] private GameObject sourceFenceFbx;
        [SerializeField] private Material poleMaterial;
        [SerializeField] private Material chainLinkMaterial;

        [SerializeField] private string outputFolder =
            DefaultOutputFolder;

        [SerializeField] private string generatedMeshFolder =
            DefaultGeneratedMeshFolder;

        [MenuItem(
            "Tools/Kids VS Aliens/Level Tools/Fence Prefab Builder")]
        public static void Open()
        {
            GetWindow<FenceSectionPrefabBuilderWindow>(
                "Fence Prefab Builder");
        }

        private void OnEnable()
        {
            TryUseCurrentSelection();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(
                "Fence Section Prefab Builder V5",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "V5 keeps the clean baked meshes and also creates one lightweight BoxCollider barrier. " +
                "The configurable prefab then works only with true Unity X/Y/Z dimensions.",
                MessageType.Info);

            sourceFenceFbx =
                (GameObject)EditorGUILayout.ObjectField(
                    "Source Fence FBX",
                    sourceFenceFbx,
                    typeof(GameObject),
                    false);

            poleMaterial =
                (Material)EditorGUILayout.ObjectField(
                    "Pole Material",
                    poleMaterial,
                    typeof(Material),
                    false);

            chainLinkMaterial =
                (Material)EditorGUILayout.ObjectField(
                    "Chain Link Material",
                    chainLinkMaterial,
                    typeof(Material),
                    false);

            outputFolder =
                EditorGUILayout.TextField(
                    "Output Folder",
                    outputFolder);

            generatedMeshFolder =
                EditorGUILayout.TextField(
                    "Generated Mesh Folder",
                    generatedMeshFolder);

            EditorGUILayout.Space(8);

            using (new EditorGUI.DisabledScope(
                       sourceFenceFbx == null ||
                       poleMaterial == null ||
                       chainLinkMaterial == null))
            {
                if (GUILayout.Button(
                        "Create / Update PF_FenceSection",
                        GUILayout.Height(36)))
                {
                    CreateOrUpdatePrefab();
                }
            }

            if (GUILayout.Button("Use Selected FBX"))
                TryUseCurrentSelection();
        }

        private void TryUseCurrentSelection()
        {
            GameObject selected =
                Selection.activeObject as GameObject;

            if (selected == null)
                return;

            string path =
                AssetDatabase.GetAssetPath(selected);

            if (!string.Equals(
                    Path.GetExtension(path),
                    ".fbx",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            sourceFenceFbx = selected;
            Repaint();
        }

        private void CreateOrUpdatePrefab()
        {
            GameObject sourceInstance =
                (GameObject)PrefabUtility.InstantiatePrefab(
                    sourceFenceFbx);

            if (sourceInstance == null)
            {
                EditorUtility.DisplayDialog(
                    "Fence Builder",
                    "Could not instantiate Source Fence FBX.",
                    "OK");
                return;
            }

            // The source model root must be identity so child world rotation/scale
            // describes only the imported FBX conversion + authored child transform.
            sourceInstance.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);

            sourceInstance.transform.localScale =
                Vector3.one;

            sourceInstance.hideFlags =
                HideFlags.HideAndDontSave;

            try
            {
                Transform roundSource =
                    FindChild(
                        sourceInstance.transform,
                        "FencePole_Round_1m40");

                Transform squareSource =
                    FindChild(
                        sourceInstance.transform,
                        "FencePole_Square_1m40");

                Transform railSource =
                    FindChild(
                        sourceInstance.transform,
                        "Rail_Top");

                Transform chainSource =
                    FindChild(
                        sourceInstance.transform,
                        "ChainLinkPlane");

                if (roundSource == null ||
                    squareSource == null ||
                    railSource == null ||
                    chainSource == null)
                {
                    EditorUtility.DisplayDialog(
                        "Fence Builder",
                        "Could not find all required source children.\n\n" +
                        "Required:\n" +
                        "- FencePole_Round_1m40\n" +
                        "- FencePole_Square_1m40\n" +
                        "- Rail_Top\n" +
                        "- ChainLinkPlane",
                        "OK");
                    return;
                }

                EnsureFolder(generatedMeshFolder);
                EnsureFolder(outputFolder);

                Mesh roundMesh =
                    BakeSourceMesh(
                        roundSource,
                        $"{generatedMeshFolder}/FencePole_Round_Baked.asset");

                Mesh squareMesh =
                    BakeSourceMesh(
                        squareSource,
                        $"{generatedMeshFolder}/FencePole_Square_Baked.asset");

                Mesh railMesh =
                    BakeSourceMesh(
                        railSource,
                        $"{generatedMeshFolder}/FenceRail_Baked.asset");

                Mesh chainMesh =
                    BakeSourceMesh(
                        chainSource,
                        $"{generatedMeshFolder}/ChainLinkPlane_Baked.asset");

                if (roundMesh == null ||
                    squareMesh == null ||
                    railMesh == null ||
                    chainMesh == null)
                {
                    EditorUtility.DisplayDialog(
                        "Fence Builder",
                        "One or more source meshes could not be baked.",
                        "OK");
                    return;
                }

                GameObject root =
                    new GameObject("PF_FenceSection");

                try
                {
                    MeshFilter leftPole =
                        CreateMeshChild(
                            root.transform,
                            "Pole_Left",
                            roundMesh,
                            poleMaterial);

                    MeshFilter rightPole =
                        CreateMeshChild(
                            root.transform,
                            "Pole_Right",
                            roundMesh,
                            poleMaterial);

                    MeshFilter topRail =
                        CreateMeshChild(
                            root.transform,
                            "Rail_Top",
                            railMesh,
                            poleMaterial);

                    MeshFilter bottomRail =
                        CreateMeshChild(
                            root.transform,
                            "Rail_Bottom",
                            railMesh,
                            poleMaterial);

                    MeshFilter chainLink =
                        CreateMeshChild(
                            root.transform,
                            "ChainLink",
                            chainMesh,
                            chainLinkMaterial);

                    GameObject collisionObject =
                        new GameObject("Collision");

                    collisionObject.transform.SetParent(
                        root.transform,
                        false);

                    collisionObject.transform.localPosition =
                        Vector3.zero;

                    collisionObject.transform.localRotation =
                        Quaternion.identity;

                    collisionObject.transform.localScale =
                        Vector3.one;

                    BoxCollider fenceCollider =
                        collisionObject.AddComponent<BoxCollider>();

                    ConfigurableFenceSection configurable =
                        root.AddComponent<ConfigurableFenceSection>();

                    configurable.ConfigureAuthoringReferences(
                        roundMesh,
                        squareMesh,
                        railMesh,
                        chainMesh,
                        leftPole,
                        rightPole,
                        topRail,
                        bottomRail,
                        chainLink,
                        poleMaterial,
                        chainLinkMaterial,
                        fenceCollider);

                    configurable.Rebuild();

                    string prefabPath =
                        $"{outputFolder}/PF_FenceSection.prefab";

                    GameObject prefab =
                        PrefabUtility.SaveAsPrefabAsset(
                            root,
                            prefabPath);

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    Selection.activeObject = prefab;
                    EditorGUIUtility.PingObject(prefab);

                    ShowNotification(
                        new GUIContent(
                            "PF_FenceSection V5 created."));
                }
                finally
                {
                    DestroyImmediate(root);
                }
            }
            finally
            {
                DestroyImmediate(sourceInstance);
            }
        }

        private static Mesh BakeSourceMesh(
            Transform source,
            string assetPath)
        {
            MeshFilter filter =
                source.GetComponent<MeshFilter>();

            if (filter == null ||
                filter.sharedMesh == null)
            {
                return null;
            }

            Mesh sourceMesh =
                filter.sharedMesh;

            Mesh baked =
                UnityEngine.Object.Instantiate(
                    sourceMesh);

            baked.name =
                Path.GetFileNameWithoutExtension(
                    assetPath);

            // Ignore the source object's position completely.
            // Blender deliberately lays the kit pieces out for inspection.
            //
            // Bake ONLY final imported orientation + scale into the mesh vertices.
            Matrix4x4 bakeMatrix =
                Matrix4x4.TRS(
                    Vector3.zero,
                    source.rotation,
                    source.lossyScale);

            Vector3[] vertices =
                baked.vertices;

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] =
                    bakeMatrix.MultiplyPoint3x4(
                        vertices[i]);
            }

            baked.vertices = vertices;

            Vector3[] normals =
                baked.normals;

            if (normals != null &&
                normals.Length == vertices.Length)
            {
                Matrix4x4 normalMatrix =
                    bakeMatrix.inverse.transpose;

                for (int i = 0; i < normals.Length; i++)
                {
                    normals[i] =
                        normalMatrix
                            .MultiplyVector(normals[i])
                            .normalized;
                }

                baked.normals = normals;
            }

            Vector4[] tangents =
                baked.tangents;

            if (tangents != null &&
                tangents.Length == vertices.Length)
            {
                for (int i = 0; i < tangents.Length; i++)
                {
                    Vector3 tangent =
                        bakeMatrix
                            .MultiplyVector(
                                new Vector3(
                                    tangents[i].x,
                                    tangents[i].y,
                                    tangents[i].z))
                            .normalized;

                    tangents[i] =
                        new Vector4(
                            tangent.x,
                            tangent.y,
                            tangent.z,
                            tangents[i].w);
                }

                baked.tangents = tangents;
            }

            baked.RecalculateBounds();

            // Normalize expected authored orientation:
            // - poles must be vertical along Y
            // - rails must be horizontal along X
            // - chain plane must be X wide / Y high
            //
            // Because the source FBX already looks correct in Unity, baking its
            // final imported rotation above produces exactly those axes.

            if (AssetDatabase.LoadAssetAtPath<Mesh>(
                    assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            AssetDatabase.CreateAsset(
                baked,
                assetPath);

            return AssetDatabase.LoadAssetAtPath<Mesh>(
                assetPath);
        }

        private static MeshFilter CreateMeshChild(
            Transform parent,
            string name,
            Mesh mesh,
            Material material)
        {
            GameObject child =
                new GameObject(name);

            child.transform.SetParent(
                parent,
                false);

            child.transform.localPosition =
                Vector3.zero;

            child.transform.localRotation =
                Quaternion.identity;

            child.transform.localScale =
                Vector3.one;

            MeshFilter filter =
                child.AddComponent<MeshFilter>();

            filter.sharedMesh = mesh;

            MeshRenderer renderer =
                child.AddComponent<MeshRenderer>();

            renderer.sharedMaterial = material;

            return filter;
        }

        private static Transform FindChild(
            Transform root,
            string exactName)
        {
            return root
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(
                    t => string.Equals(
                        t.name,
                        exactName,
                        StringComparison.OrdinalIgnoreCase));
        }

        private static void EnsureFolder(
            string folder)
        {
            folder =
                folder.Replace("\\", "/");

            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts =
                folder.Split('/');

            if (parts.Length == 0 ||
                parts[0] != "Assets")
            {
                throw new InvalidOperationException(
                    "Folder must be inside Assets.");
            }

            string current =
                "Assets";

            for (int i = 1; i < parts.Length; i++)
            {
                string next =
                    $"{current}/{parts[i]}";

                if (!AssetDatabase.IsValidFolder(next))
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
