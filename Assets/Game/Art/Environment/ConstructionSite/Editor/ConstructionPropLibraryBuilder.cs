#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ConstructionPropLibraryBuilder
{
    private const string MenuPath = "Tools/Kids VS Aliens/Construction Site/Build Prop Library";

    private const float BagWidth = 0.60f;
    private const float BagDepth = 0.38f;
    private const float BagHeight = 0.15f;
    private const float PalletWidth = 1.08f;
    private const float PalletDepth = 0.82f;
    private const float PalletHeight = 0.13f;

    // Slight stylized scale bump so these props read better next to Amy.
    private const float BagVisualScale = 1.10f;
    private const float PalletVisualScale = 1.15f;
    private const float TimberVisualScale = 1.50f;
    private const float CableVisualScale = 1.12f;

    private const float TimberLength = 2.20f;
    private const float TimberWidth = 0.14f;
    private const float TimberHeight = 0.055f;
    private const float TimberGapZ = 0.008f;
    private const float TimberGapY = 0.006f;

    private sealed class Paths
    {
        public string EnvironmentRoot;
        public string GameRoot;
        public string ConstructionRoot;
        public string FbxRoot;
        public string TextureRoot;
        public string GeneratedRoot;
        public string MaterialRoot;
        public string PropMaterialRoot;
        public string BagMaterialRoot;
        public string LegacyPrefabRoot;
        public string PrefabRoot;
        public string BagPrefabRoot;
        public string PalletPrefabRoot;
        public string TimberPrefabRoot;
        public string CablePrefabRoot;
        public string ClusterPrefabRoot;
    }

    private sealed class Library
    {
        public Material Wood;
        public Material WoodDirty;
        public Material TimberWood;
        public Material Metal;
        public Material MetalRusty;
        public Material Cable;
        public Material BagGritmix;
        public Material BagBrixora;
        public Material BagKavrix;
        public Mesh BagMesh;
    }

    [MenuItem(MenuPath)]
    public static void BuildLibrary()
    {
        try
        {
            Paths p = ResolvePaths();
            MigrateLegacyPrefabFolderIfNeeded(p);
            EnsureOutputFolders(p);

            EditorUtility.DisplayProgressBar("KVA Construction Props", "Creating shared materials...", 0.08f);
            Library lib = BuildMaterials(p);

            EditorUtility.DisplayProgressBar("KVA Construction Props", "Creating cement bags...", 0.20f);
            BuildCementBagPrefabs(p, lib);

            EditorUtility.DisplayProgressBar("KVA Construction Props", "Creating pallets...", 0.40f);
            BuildPalletPrefabs(p, lib);

            EditorUtility.DisplayProgressBar("KVA Construction Props", "Creating timber props...", 0.62f);
            BuildTimberPrefabs(p, lib);

            EditorUtility.DisplayProgressBar("KVA Construction Props", "Creating cable reel props...", 0.78f);
            BuildCablePrefabs(p, lib);

            EditorUtility.DisplayProgressBar("KVA Construction Props", "Creating storage clusters...", 0.90f);
            BuildStorageClusters(p);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.PrefabRoot);

            Debug.Log(
                "[KVA] Construction prop library built successfully.\n" +
                "Created 3 cement bag variants, 3 loaded cement pallets, timber kits, cable reels and storage clusters.\n" +
                $"Output: {p.PrefabRoot}"
            );

            EditorUtility.DisplayDialog(
                "KVA Construction Props",
                "Done.\n\nCreated:\n" +
                "• 3 individual cement bags\n" +
                "• 3 different loaded cement pallets\n" +
                "• empty pallet\n" +
                "• timber board / strap / bundles / loose pile\n" +
                "• cable reel variants\n" +
                "• 2 optional storage clusters\n\n" +
                "Cement-bag print is now projected directly onto the bag mesh on both broad faces and the side faces (no floating quads).\n" +
                "Construction props also receive a small stylized scale bump.\n\n" +
                "Shared materials were created under Environment/Materials/ConstructionSite.\n" +
                "Prefabs were created under Prefabs/Environment/ConstructionSite.",
                "Nice"
            );
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogException(ex);
            EditorUtility.DisplayDialog(
                "KVA Construction Props - Failed",
                ex.Message + "\n\nSee Console for the full stack trace.",
                "OK"
            );
        }
    }

    [MenuItem("Tools/Kids VS Aliens/Construction Site/Update Cement Pallets + Timber Scale")]
    public static void UpdateCementPalletsAndTimberOnly()
    {
        try
        {
            Paths p = ResolvePaths();
            EnsureOutputFolders(p);

            EditorUtility.DisplayProgressBar("KVA Construction Props", "Rebuilding cement pallets only...", 0.20f);
            BuildDenseCementPalletsOnly(p);

            EditorUtility.DisplayProgressBar("KVA Construction Props", "Scaling timber + straps to 1.5x...", 0.65f);
            Library timberLib = new Library
            {
                TimberWood = LoadRequired<Material>(p.EnvironmentRoot + "/Materials/Wood/M_Plywood.mat"),
                Metal = LoadRequired<Material>(p.PropMaterialRoot + "/M_CS_Metal_Strap.mat"),
                MetalRusty = LoadRequired<Material>(p.PropMaterialRoot + "/M_CS_Metal_Rusty.mat")
            };
            BuildTimberPrefabs(p, timberLib);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog(
                "KVA Construction Props",
                "Done.\n\nOnly these assets were rebuilt:\n" +
                "• 3 loaded cement pallets\n" +
                "• timber board / strap / bundles / loose pile\n\n" +
                "Individual cement bags, cement-bag materials/shader, cable reels, empty pallet and other props were left alone.\n\n" +
                "Timber and its strap are now 1.5x the original authored size.",
                "Nice"
            );
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogException(ex);
            EditorUtility.DisplayDialog(
                "KVA Construction Props - Failed",
                ex.Message + "\n\nSee Console for the full stack trace.",
                "OK"
            );
        }
    }

    private static Paths ResolvePaths()
    {
        string[] guids = AssetDatabase.FindAssets("ConstructionPropLibraryBuilder t:Script");
        string scriptPath = null;

        foreach (string guid in guids)
        {
            string candidate = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (candidate.EndsWith("/ConstructionPropLibraryBuilder.cs", StringComparison.OrdinalIgnoreCase) &&
                candidate.Contains("/ConstructionSite/Editor/"))
            {
                scriptPath = candidate;
                break;
            }
        }

        if (string.IsNullOrEmpty(scriptPath))
            throw new InvalidOperationException("Could not resolve ConstructionPropLibraryBuilder.cs inside Environment/ConstructionSite/Editor.");

        string editorRoot = NormalizeAssetPath(Path.GetDirectoryName(scriptPath));
        string constructionRoot = NormalizeAssetPath(Path.GetDirectoryName(editorRoot));
        string environmentRoot = NormalizeAssetPath(Path.GetDirectoryName(constructionRoot));
        string artRoot = NormalizeAssetPath(Path.GetDirectoryName(environmentRoot));
        string gameRoot = NormalizeAssetPath(Path.GetDirectoryName(artRoot));
        string prefabRoot = gameRoot + "/Prefabs/Environment/ConstructionSite";

        return new Paths
        {
            EnvironmentRoot = environmentRoot,
            GameRoot = gameRoot,
            ConstructionRoot = constructionRoot,
            FbxRoot = constructionRoot + "/FBX",
            TextureRoot = constructionRoot + "/Textures",
            GeneratedRoot = constructionRoot + "/Generated",
            MaterialRoot = environmentRoot + "/Materials/ConstructionSite",
            PropMaterialRoot = environmentRoot + "/Materials/ConstructionSite/Props",
            BagMaterialRoot = environmentRoot + "/Materials/ConstructionSite/CementBags",
            LegacyPrefabRoot = constructionRoot + "/Prefabs",
            PrefabRoot = prefabRoot,
            BagPrefabRoot = prefabRoot + "/CementBags",
            PalletPrefabRoot = prefabRoot + "/Pallets",
            TimberPrefabRoot = prefabRoot + "/Timber",
            CablePrefabRoot = prefabRoot + "/Cable",
            ClusterPrefabRoot = prefabRoot + "/Clusters",
        };
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
    }

    private static void MigrateLegacyPrefabFolderIfNeeded(Paths p)
    {
        if (!AssetDatabase.IsValidFolder(p.LegacyPrefabRoot))
            return;

        if (AssetDatabase.IsValidFolder(p.PrefabRoot))
        {
            Debug.LogWarning(
                "[KVA] Legacy construction prefabs still exist at " + p.LegacyPrefabRoot +
                ". The new builder writes to " + p.PrefabRoot + "."
            );
            return;
        }

        string destinationParent = NormalizeAssetPath(Path.GetDirectoryName(p.PrefabRoot));
        EnsureFolder(destinationParent);

        string error = AssetDatabase.MoveAsset(p.LegacyPrefabRoot, p.PrefabRoot);
        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogWarning(
                "[KVA] Could not automatically move the legacy prefab folder. " + error +
                " New prefabs will still be created at " + p.PrefabRoot + "."
            );
        }
        else
        {
            Debug.Log("[KVA] Moved legacy construction prefabs to " + p.PrefabRoot);
        }
    }

    private static void EnsureOutputFolders(Paths p)
    {
        EnsureFolder(p.GeneratedRoot);
        EnsureFolder(p.MaterialRoot);
        EnsureFolder(p.PropMaterialRoot);
        EnsureFolder(p.BagMaterialRoot);
        EnsureFolder(p.PrefabRoot);
        EnsureFolder(p.BagPrefabRoot);
        EnsureFolder(p.PalletPrefabRoot);
        EnsureFolder(p.TimberPrefabRoot);
        EnsureFolder(p.CablePrefabRoot);
        EnsureFolder(p.ClusterPrefabRoot);
    }

    private static void EnsureFolder(string assetPath)
    {
        assetPath = NormalizeAssetPath(assetPath);
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string parent = NormalizeAssetPath(Path.GetDirectoryName(assetPath));
        string name = Path.GetFileName(assetPath);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }

    private static Library BuildMaterials(Paths p)
    {
        string sourceWood = p.EnvironmentRoot + "/Materials/Wood/M_Fine_Grained_Wood.mat";
        string sourceWoodDirty = p.EnvironmentRoot + "/Materials/Wood/M_Wood_Planks.mat";
        string sourceMetal = p.EnvironmentRoot + "/Materials/Metal/M_Metal_Plate_02.mat";
        string sourceMetalRusty = p.EnvironmentRoot + "/Materials/Metal/M_Rusty_Metal_Sheet.mat";
        string sourcePlywood = p.EnvironmentRoot + "/Materials/Wood/M_Plywood.mat";

        Material wood = CopyMaterial(sourceWood, p.PropMaterialRoot + "/M_CS_Wood_Construction.mat", "M_CS_Wood_Construction");
        Material woodDirty = CopyMaterial(sourceWoodDirty, p.PropMaterialRoot + "/M_CS_Wood_Dirty.mat", "M_CS_Wood_Dirty");
        Material metal = CopyMaterial(sourceMetal, p.PropMaterialRoot + "/M_CS_Metal_Strap.mat", "M_CS_Metal_Strap");
        Material metalRusty = CopyMaterial(sourceMetalRusty, p.PropMaterialRoot + "/M_CS_Metal_Rusty.mat", "M_CS_Metal_Rusty");

        // Reuse the existing project plywood material directly for all timber.
        Material timberWood = LoadRequired<Material>(sourcePlywood);

        Material cable = GetOrCreateLitMaterial(
            p.PropMaterialRoot + "/M_CS_Cable_Dark.mat",
            "M_CS_Cable_Dark",
            new Color(0.035f, 0.04f, 0.045f, 1f),
            0f,
            0.24f
        );

        Texture2D yellow = PrepareOverlayTexture(p.TextureRoot + "/cement bag overlay yellow.png");
        Texture2D blue = PrepareOverlayTexture(p.TextureRoot + "/cement bag overlay blue.png");
        Texture2D orange = PrepareOverlayTexture(p.TextureRoot + "/cement bag overlay orange.png");

        // Bake the FBX conversion/orientation once into a clean mesh whose local axes are:
        // X = long side, Y = height, Z = short side. This makes object-space projection
        // deterministic even when the prefab is rotated or stacked later.
        Mesh bagMesh = GetOrCreateNormalizedMeshAsset(
            p.FbxRoot + "/CementBag_A.fbx",
            p.GeneratedRoot + "/SM_CementBag_A_Normalized.asset",
            new Vector3(BagWidth, BagHeight, BagDepth),
            BagVisualScale
        );

        Material bagGritmix = GetOrCreateProjectedBagMaterial(
            p.BagMaterialRoot + "/M_CS_CementBag_Gritmix.mat",
            "M_CS_CementBag_Gritmix",
            yellow,
            bagMesh.bounds,
            new Color(0.95f, 0.72f, 0.05f, 1f)
        );

        Material bagBrixora = GetOrCreateProjectedBagMaterial(
            p.BagMaterialRoot + "/M_CS_CementBag_Brixora.mat",
            "M_CS_CementBag_Brixora",
            blue,
            bagMesh.bounds,
            new Color(0.08f, 0.35f, 0.95f, 1f)
        );

        Material bagKavrix = GetOrCreateProjectedBagMaterial(
            p.BagMaterialRoot + "/M_CS_CementBag_Kavrix.mat",
            "M_CS_CementBag_Kavrix",
            orange,
            bagMesh.bounds,
            new Color(1.00f, 0.28f, 0.035f, 1f)
        );

        return new Library
        {
            Wood = wood,
            WoodDirty = woodDirty,
            TimberWood = timberWood,
            Metal = metal,
            MetalRusty = metalRusty,
            Cable = cable,
            BagGritmix = bagGritmix,
            BagBrixora = bagBrixora,
            BagKavrix = bagKavrix,
            BagMesh = bagMesh,
        };
    }

    private static Material CopyMaterial(string sourcePath, string destinationPath, string name)
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
        if (source == null)
            throw new FileNotFoundException($"Required source material not found: {sourcePath}");

        Material destination = AssetDatabase.LoadAssetAtPath<Material>(destinationPath);
        if (destination == null)
        {
            destination = new Material(source) { name = name };
            AssetDatabase.CreateAsset(destination, destinationPath);
        }
        else
        {
            EditorUtility.CopySerialized(source, destination);
            destination.name = name;
            EditorUtility.SetDirty(destination);
        }

        return destination;
    }

    private static Material GetOrCreateLitMaterial(string path, string name, Color color, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("Universal Render Pipeline/Lit shader was not found.");

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
            mat.name = name;
        }

        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.renderQueue = -1;
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.SetOverrideTag("RenderType", "Opaque");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Texture2D PrepareOverlayTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new FileNotFoundException($"Overlay texture not found: {path}");

        bool needsReimport = false;
        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            needsReimport = true;
        }
        if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
        {
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            needsReimport = true;
        }
        if (!importer.sRGBTexture)
        {
            importer.sRGBTexture = true;
            needsReimport = true;
        }
        if (!importer.mipmapEnabled)
        {
            importer.mipmapEnabled = true;
            needsReimport = true;
        }
        if (importer.maxTextureSize != 1024)
        {
            importer.maxTextureSize = 1024;
            needsReimport = true;
        }
        if (importer.anisoLevel != 2)
        {
            importer.anisoLevel = 2;
            needsReimport = true;
        }
        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            needsReimport = true;
        }
        if (needsReimport)
            importer.SaveAndReimport();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
            throw new FileNotFoundException($"Could not load overlay texture after import: {path}");

        return texture;
    }

    private static Material GetOrCreateProjectedBagMaterial(
        string path,
        string name,
        Texture2D overlay,
        Bounds bagBounds,
        Color accentColor)
    {
        Shader shader = Shader.Find("Kids VS Aliens/Environment/Cement Bag Projected Print");
        if (shader == null)
            throw new InvalidOperationException(
                "Projected cement-bag shader was not found. Expected shader: " +
                "Kids VS Aliens/Environment/Cement Bag Projected Print"
            );

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
            mat.name = name;
        }

        // Kraft/cardboard base. Print is blended directly into the surface, not a floating card.
        mat.SetColor("_BaseColor", new Color(0.46f, 0.30f, 0.19f, 1f));
        mat.SetColor("_AccentColor", accentColor);
        mat.SetTexture("_PrintMap", overlay);
        mat.SetFloat("_Smoothness", 0.10f);
        mat.SetFloat("_PrintOpacity", 1.0f);
        mat.SetFloat("_PrintCutoff", 0.10f);
        mat.SetFloat("_ProjectionSharpness", 8.0f);
        mat.SetFloat("_SidePrintStrength", 0.82f);
        mat.SetFloat("_SideBandCenter", 0.50f);
        mat.SetFloat("_SideBandScale", 0.22f);
        mat.SetFloat("_SideAccentStrength", 0.14f);
        mat.SetVector("_BagBoundsMin", bagBounds.min);
        mat.SetVector("_BagBoundsSize", bagBounds.size);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Mesh GetOrCreateNormalizedMeshAsset(
        string fbxPath,
        string assetPath,
        Vector3 expectedSize,
        float uniformScale)
    {
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("Could not resolve ModelImporter for: " + fbxPath);

        bool restoreReadable = !importer.isReadable;
        if (restoreReadable)
        {
            // Mesh.CombineMeshes needs CPU access. Temporarily enable Read/Write only
            // while generating the normalized editor asset, then restore the FBX
            // to the production-friendly Read/Write OFF state.
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        GameObject tempRoot = new GameObject("__KVA_NormalizeMesh");
        Mesh combined = null;

        try
        {
            GameObject model = InstantiateModel(fbxPath, tempRoot.transform, "Model");
            OrientModelToExpectedBounds(model, expectedSize);

            MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
                throw new InvalidOperationException("No mesh geometry found in: " + fbxPath);

            List<CombineInstance> combines = new List<CombineInstance>();
            int totalVertices = 0;

            foreach (MeshFilter filter in filters)
            {
                if (filter.sharedMesh == null)
                    continue;

                CombineInstance ci = new CombineInstance
                {
                    mesh = filter.sharedMesh,
                    transform = tempRoot.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix
                };
                combines.Add(ci);
                totalVertices += filter.sharedMesh.vertexCount;
            }

            combined = new Mesh
            {
                name = "SM_CementBag_A_Normalized",
                indexFormat = totalVertices > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            combined.CombineMeshes(combines.ToArray(), true, true, false);
            combined.RecalculateBounds();

            // Put the reusable mesh origin at bottom-center and apply the stylized scale bump.
            Bounds b = combined.bounds;
            Vector3 pivot = new Vector3(b.center.x, b.min.y, b.center.z);
            Vector3[] vertices = combined.vertices;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = (vertices[i] - pivot) * uniformScale;
            combined.vertices = vertices;
            combined.RecalculateBounds();

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(combined, assetPath);
                combined = null; // AssetDatabase owns it now.
                return LoadRequired<Mesh>(assetPath);
            }

            EditorUtility.CopySerialized(combined, existing);
            existing.name = "SM_CementBag_A_Normalized";
            EditorUtility.SetDirty(existing);
            return existing;
        }
        finally
        {
            if (combined != null)
                UnityEngine.Object.DestroyImmediate(combined);
            UnityEngine.Object.DestroyImmediate(tempRoot);

            if (restoreReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }
    }

    private static void BuildCementBagPrefabs(Paths p, Library lib)
    {
        CreateCementBagPrefab(p, lib.BagMesh, "PF_CementBag_Gritmix", lib.BagGritmix);
        CreateCementBagPrefab(p, lib.BagMesh, "PF_CementBag_Brixora", lib.BagBrixora);
        CreateCementBagPrefab(p, lib.BagMesh, "PF_CementBag_Kavrix", lib.BagKavrix);
    }

    private static void CreateCementBagPrefab(
        Paths p,
        Mesh bagMesh,
        string prefabName,
        Material bagMaterial)
    {
        GameObject root = new GameObject(prefabName);
        try
        {
            GameObject surface = new GameObject("BagSurface");
            surface.transform.SetParent(root.transform, false);

            MeshFilter filter = surface.AddComponent<MeshFilter>();
            filter.sharedMesh = bagMesh;

            MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = bagMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;

            // The normalized mesh is bottom-centered, so the collider and placement are stable.
            Bounds b = bagMesh.bounds;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = b.center;
            collider.size = b.size;

            SavePrefab(root, p.BagPrefabRoot + "/" + prefabName + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void BuildPalletPrefabs(Paths p, Library lib)
    {
        CreateEmptyPalletPrefab(p, lib);
        BuildDenseCementPalletsOnly(p);
    }

    private static void BuildDenseCementPalletsOnly(Paths p)
    {
        // Keep the now-good individual cement bag prefabs untouched.
        // Only rebuild the loaded pallet assemblies from those existing prefabs.
        CreateDenseLoadedPallet(
            p,
            "PF_CementPallet_Gritmix_A",
            p.BagPrefabRoot + "/PF_CementBag_Gritmix.prefab",
            fullLayers: 4,
            topBagCount: 2,
            seed: 1103,
            messy: 0.24f
        );

        CreateDenseLoadedPallet(
            p,
            "PF_CementPallet_Brixora_A",
            p.BagPrefabRoot + "/PF_CementBag_Brixora.prefab",
            fullLayers: 4,
            topBagCount: 3,
            seed: 2207,
            messy: 0.34f
        );

        CreateDenseLoadedPallet(
            p,
            "PF_CementPallet_Kavrix_A",
            p.BagPrefabRoot + "/PF_CementBag_Kavrix.prefab",
            fullLayers: 5,
            topBagCount: 0,
            seed: 3301,
            messy: 0.20f
        );
    }

    private static void CreateDenseLoadedPallet(
        Paths p,
        string prefabName,
        string bagPrefabPath,
        int fullLayers,
        int topBagCount,
        int seed,
        float messy)
    {
        GameObject root = new GameObject(prefabName);
        try
        {
            GameObject palletPrefab = LoadRequired<GameObject>(p.PalletPrefabRoot + "/PF_Pallet_Empty_A.prefab");
            GameObject pallet = InstantiatePrefabAsset(palletPrefab, root.transform);
            pallet.name = "Pallet";
            pallet.transform.localPosition = Vector3.zero;
            pallet.transform.localRotation = Quaternion.identity;
            pallet.transform.localScale = Vector3.one;

            BoxCollider palletCollider = pallet.GetComponent<BoxCollider>();
            if (palletCollider == null)
                throw new InvalidOperationException("PF_Pallet_Empty_A is missing its root BoxCollider.");

            Bounds palletBounds = new Bounds(palletCollider.center, palletCollider.size);
            foreach (Collider c in pallet.GetComponentsInChildren<Collider>(true))
                c.enabled = false;

            GameObject bagPrefab = LoadRequired<GameObject>(bagPrefabPath);
            BoxCollider bagCollider = bagPrefab.GetComponent<BoxCollider>();
            if (bagCollider == null)
                throw new InvalidOperationException("Cement bag prefab is missing its root BoxCollider: " + bagPrefabPath);

            Vector3 bagSize = bagCollider.size;
            float bagBottom = bagCollider.center.y - bagSize.y * 0.5f;
            float firstLayerY = palletBounds.max.y - bagBottom;

            // Dense 2x2 construction-pallet stack. The tiny overhang is intentional:
            // it makes the bags read like a real loaded pallet rather than four boxes
            // floating neatly inside the pallet footprint.
            float xOffset = bagSize.x * 0.515f;
            float zOffset = bagSize.z * 0.515f;
            float layerPitch = bagSize.y * 0.975f;

            System.Random random = new System.Random(seed);
            int bagIndex = 0;

            for (int layer = 0; layer < fullLayers; layer++)
            {
                float y = firstLayerY + layer * layerPitch;
                float layerYaw = (layer & 1) == 0 ? 0f : 180f;

                // Very small whole-layer shift breaks the perfect vertical seams.
                float shiftX = Mathf.Sin((layer + 1) * 1.73f) * 0.010f;
                float shiftZ = Mathf.Cos((layer + 1) * 1.31f) * 0.008f;

                Vector3[] positions =
                {
                    new Vector3(palletBounds.center.x - xOffset + shiftX, y, palletBounds.center.z - zOffset + shiftZ),
                    new Vector3(palletBounds.center.x + xOffset + shiftX, y, palletBounds.center.z - zOffset + shiftZ),
                    new Vector3(palletBounds.center.x - xOffset + shiftX, y, palletBounds.center.z + zOffset + shiftZ),
                    new Vector3(palletBounds.center.x + xOffset + shiftX, y, palletBounds.center.z + zOffset + shiftZ),
                };

                for (int i = 0; i < positions.Length; i++)
                {
                    // Alternate 180 degrees within the layer as well so front/back
                    // printing varies naturally without changing the footprint.
                    float yaw = layerYaw + (((i + layer) & 1) == 0 ? 0f : 180f);
                    CreateBagOnPallet(root.transform, bagPrefab, ++bagIndex, positions[i], yaw, random, messy);
                }
            }

            if (topBagCount > 0)
            {
                float y = firstLayerY + fullLayers * layerPitch;
                float spacing = bagSize.z * 1.06f;

                if (topBagCount == 2)
                {
                    CreateBagOnPallet(root.transform, bagPrefab, ++bagIndex,
                        new Vector3(palletBounds.center.x - spacing * 0.52f, y, palletBounds.center.z + 0.006f),
                        90f, random, messy * 0.65f);
                    CreateBagOnPallet(root.transform, bagPrefab, ++bagIndex,
                        new Vector3(palletBounds.center.x + spacing * 0.52f, y, palletBounds.center.z - 0.004f),
                        90f, random, messy * 0.65f);
                }
                else
                {
                    // Three bags across the top fits the pallet width nicely once
                    // the bags are turned 90 degrees. This is the Brixora silhouette.
                    for (int i = -1; i <= 1; i++)
                    {
                        CreateBagOnPallet(root.transform, bagPrefab, ++bagIndex,
                            new Vector3(palletBounds.center.x + i * spacing, y, palletBounds.center.z + 0.006f),
                            90f, random, messy * 0.65f);
                    }
                }
            }

            Bounds assembledBounds = CalculateLocalMeshBounds(root.transform, root);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = assembledBounds.center;
            collider.size = assembledBounds.size;

            SavePrefab(root, p.PalletPrefabRoot + "/" + prefabName + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateEmptyPalletPrefab(Paths p, Library lib)
    {
        string prefabName = "PF_Pallet_Empty_A";
        GameObject root = new GameObject(prefabName);
        try
        {
            GameObject model = InstantiateModel(p.FbxRoot + "/WoodenPallet.fbx", root.transform, "Model");
            AssignSingleMaterial(model, lib.Wood);
            OrientModelToExpectedBounds(model, new Vector3(PalletWidth, PalletHeight, PalletDepth));
            ScaleModelUniform(model, PalletVisualScale);
            MoveModelToBottomCenter(root.transform, model);

            Bounds modelBounds = CalculateLocalMeshBounds(root.transform, model);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = modelBounds.center;
            collider.size = modelBounds.size;

            SavePrefab(root, p.PalletPrefabRoot + "/" + prefabName + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateLoadedPallet(
        Paths p,
        string prefabName,
        string bagPrefabPath,
        Library lib,
        int layers,
        int seed,
        float messy,
        bool topSingle)
    {
        GameObject root = new GameObject(prefabName);
        try
        {
            GameObject palletModel = InstantiateModel(p.FbxRoot + "/WoodenPallet.fbx", root.transform, "Pallet");
            AssignSingleMaterial(palletModel, lib.Wood);
            OrientModelToExpectedBounds(palletModel, new Vector3(PalletWidth, PalletHeight, PalletDepth));
            ScaleModelUniform(palletModel, PalletVisualScale);
            MoveModelToBottomCenter(root.transform, palletModel);
            Bounds palletBounds = CalculateLocalMeshBounds(root.transform, palletModel);

            GameObject bagPrefab = LoadRequired<GameObject>(bagPrefabPath);
            BoxCollider bagCollider = bagPrefab.GetComponent<BoxCollider>();
            if (bagCollider == null)
                throw new InvalidOperationException("Cement bag prefab is missing its root BoxCollider: " + bagPrefabPath);

            Vector3 bagSize = bagCollider.size;
            float bagBottom = bagCollider.center.y - bagSize.y * 0.5f;
            float firstLayerY = palletBounds.max.y - bagBottom;

            // Bags compress a little when stacked. This intentional overlap keeps
            // the stack looking loaded instead of floating layer-by-layer.
            float layerPitch = bagSize.y * 0.84f;
            System.Random random = new System.Random(seed);
            int bagIndex = 0;

            for (int layer = 0; layer < layers; layer++)
            {
                float y = firstLayerY + layer * layerPitch;
                bool cross = (layer & 1) == 1;

                if (!cross)
                {
                    // Long side runs along pallet X. Two bags fill the pallet depth.
                    float zOffset = bagSize.z * 0.50f;
                    CreateBagOnPallet(root.transform, bagPrefab, ++bagIndex,
                        new Vector3(palletBounds.center.x, y, palletBounds.center.z - zOffset),
                        0f, random, messy);
                    CreateBagOnPallet(root.transform, bagPrefab, ++bagIndex,
                        new Vector3(palletBounds.center.x, y, palletBounds.center.z + zOffset),
                        0f, random, messy);
                }
                else
                {
                    // Rotate 90 degrees. Two bags now fill the pallet width.
                    float xOffset = bagSize.z * 0.50f;
                    CreateBagOnPallet(root.transform, bagPrefab, ++bagIndex,
                        new Vector3(palletBounds.center.x - xOffset, y, palletBounds.center.z),
                        90f, random, messy);
                    CreateBagOnPallet(root.transform, bagPrefab, ++bagIndex,
                        new Vector3(palletBounds.center.x + xOffset, y, palletBounds.center.z),
                        90f, random, messy);
                }
            }

            // Brixora gets an intentionally different seven-bag silhouette.
            if (topSingle)
            {
                float y = firstLayerY + layers * layerPitch;
                CreateBagOnPallet(root.transform, bagPrefab, ++bagIndex,
                    new Vector3(palletBounds.center.x, y, palletBounds.center.z),
                    3.5f, random, messy * 0.65f);
            }

            Bounds assembledBounds = CalculateLocalMeshBounds(root.transform, root);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = assembledBounds.center;
            collider.size = assembledBounds.size;

            SavePrefab(root, p.PalletPrefabRoot + "/" + prefabName + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateBagOnPallet(Transform parent, GameObject bagPrefab, int index, Vector3 basePosition, float baseYaw, System.Random random, float messy)
    {
        GameObject bag = InstantiatePrefabAsset(bagPrefab, parent);
        bag.name = "Bag_" + index.ToString("00");

        float jitterXZ = 0.012f * messy;
        float jitterY = 0.004f * messy;
        float yaw = ((float)random.NextDouble() * 2f - 1f) * 2.0f * messy;
        float scale = Mathf.Lerp(0.985f, 1.015f, (float)random.NextDouble());

        bag.transform.localPosition = basePosition + new Vector3(
            RandRange(random, -jitterXZ, jitterXZ),
            RandRange(random, -jitterY, jitterY),
            RandRange(random, -jitterXZ, jitterXZ)
        );
        bag.transform.localRotation = Quaternion.Euler(0f, baseYaw + yaw, 0f);
        bag.transform.localScale = new Vector3(scale, Mathf.Lerp(0.97f, 1.01f, (float)random.NextDouble()), scale);

        foreach (Collider c in bag.GetComponentsInChildren<Collider>(true))
            c.enabled = false;
    }

    private static float RandRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private static void BuildTimberPrefabs(Paths p, Library lib)
    {
        CreateSimpleModelPrefab(
            p.FbxRoot + "/TimberWood_A.fbx",
            p.TimberPrefabRoot + "/PF_TimberWood_A.prefab",
            "PF_TimberWood_A",
            new[] { lib.TimberWood },
            new Vector3(TimberLength, TimberHeight, TimberWidth),
            new Vector3(TimberLength, TimberHeight, TimberWidth),
            new Vector3(0f, TimberHeight * 0.5f, 0f),
            TimberVisualScale
        );

        CreateSimpleModelPrefab(
            p.FbxRoot + "/TimberStrap_A.fbx",
            p.TimberPrefabRoot + "/PF_TimberStrap_A.prefab",
            "PF_TimberStrap_A",
            new[] { lib.Metal },
            new Vector3(0.035f, 0.238f, 0.584f),
            null,
            Vector3.zero,
            TimberVisualScale
        );

        CreateTimberBundle(p, "PF_TimberBundle_A", lib.TimberWood, lib.Metal, false);
        CreateTimberBundle(p, "PF_TimberBundle_Rusty_A", lib.TimberWood, lib.MetalRusty, true);
        CreateLooseTimberPile(p, lib);
    }

    private static void CreateTimberBundle(Paths p, string prefabName, Material woodMaterial, Material strapMaterial, bool rough)
    {
        GameObject root = new GameObject(prefabName);
        try
        {
            // Use wrapper prefabs rather than raw FBX roots. The wrapper preserves
            // Unity's FBX axis-conversion transform on the Model child while this
            // outer object remains safe to position/rotate for assembly.
            GameObject timberModel = LoadRequired<GameObject>(p.TimberPrefabRoot + "/PF_TimberWood_A.prefab");
            GameObject strapModel = LoadRequired<GameObject>(p.TimberPrefabRoot + "/PF_TimberStrap_A.prefab");

            float scaledLength = TimberLength * TimberVisualScale;
            float scaledWidth = TimberWidth * TimberVisualScale;
            float scaledHeight = TimberHeight * TimberVisualScale;
            float scaledGapY = TimberGapY * TimberVisualScale;
            float scaledGapZ = TimberGapZ * TimberVisualScale;

            float bundleWidth = 4f * scaledWidth + 3f * scaledGapZ;
            float startZ = -bundleWidth * 0.5f + scaledWidth * 0.5f;
            float[] zPositions =
            {
                startZ,
                startZ + (scaledWidth + scaledGapZ),
                startZ + 2f * (scaledWidth + scaledGapZ),
                startZ + 3f * (scaledWidth + scaledGapZ)
            };
            float rowPitch = scaledHeight + scaledGapY;
            int i = 0;

            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    GameObject board = InstantiatePrefabAsset(timberModel, root.transform);
                    board.name = "Timber_" + (++i).ToString("00");
                    AssignSingleMaterial(board, woodMaterial);
                    board.transform.localPosition = new Vector3(
                        rough ? Mathf.Sin(i * 1.7f) * 0.007f * TimberVisualScale : 0f,
                        row * rowPitch + (rough ? Mathf.Abs(Mathf.Sin(i * 2.1f)) * 0.002f * TimberVisualScale : 0f),
                        zPositions[col] + (rough ? Mathf.Sin(i * 2.6f) * 0.003f * TimberVisualScale : 0f)
                    );
                    board.transform.localRotation = Quaternion.Euler(0f, rough ? Mathf.Sin(i * 0.9f) * 0.3f : 0f, 0f);
                }
            }

            float strapX = scaledLength * 0.28f;
            for (int s = 0; s < 2; s++)
            {
                GameObject strap = InstantiatePrefabAsset(strapModel, root.transform);
                strap.name = "Strap_" + (s + 1).ToString("00");
                AssignSingleMaterial(strap, strapMaterial);
                strap.transform.localPosition = new Vector3(s == 0 ? -strapX : strapX, 0f, 0f);
            }

            float bundleHeight = 4f * scaledHeight + 3f * scaledGapY;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, bundleHeight * 0.5f, 0f);
            collider.size = new Vector3(scaledLength, bundleHeight, bundleWidth);

            SavePrefab(root, p.TimberPrefabRoot + "/" + prefabName + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateLooseTimberPile(Paths p, Library lib)
    {
        string prefabName = "PF_LooseTimberPile_A";
        GameObject root = new GameObject(prefabName);
        try
        {
            GameObject timberModel = LoadRequired<GameObject>(p.TimberPrefabRoot + "/PF_TimberWood_A.prefab");
            Vector3[] positions =
            {
                new Vector3( 0.00f, 0.00f,  0.00f),
                new Vector3( 0.06f, 0.055f, 0.04f),
                new Vector3(-0.09f, 0.110f,-0.02f),
                new Vector3( 0.18f, 0.00f,  0.22f),
                new Vector3(-0.20f, 0.055f, 0.27f),
            };
            float[] yaws = { -5f, 3f, -2f, 11f, -8f };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject board = InstantiatePrefabAsset(timberModel, root.transform);
                board.name = "LooseTimber_" + (i + 1).ToString("00");
                AssignSingleMaterial(board, lib.TimberWood);
                board.transform.localPosition = positions[i] * TimberVisualScale;
                board.transform.localRotation = Quaternion.Euler(0f, yaws[i], i == 2 ? 1.2f : 0f);
            }

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.09f, 0.12f) * TimberVisualScale;
            collider.size = new Vector3(2.35f, 0.18f, 0.75f) * TimberVisualScale;

            SavePrefab(root, p.TimberPrefabRoot + "/" + prefabName + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void BuildCablePrefabs(Paths p, Library lib)
    {
        CreateCableReelPrefab(p, "PF_CableReel_A", lib.Wood, lib.Cable);
        CreateCableReelPrefab(p, "PF_CableReel_Dirty_A", lib.WoodDirty, lib.Cable);
        CreateCableReelPair(p);
    }

    private static void CreateCableReelPrefab(Paths p, string prefabName, Material woodMaterial, Material cableMaterial)
    {
        GameObject root = new GameObject(prefabName);
        try
        {
            GameObject model = InstantiateModel(p.FbxRoot + "/CableReel_A.fbx", root.transform, "Model");
            AssignMaterialsBySlot(model, new[] { woodMaterial, cableMaterial });
            OrientModelToExpectedBounds(model, new Vector3(0.55f, 0.84f, 0.84f));
            ScaleModelUniform(model, CableVisualScale);
            MoveModelToBottomCenter(root.transform, model);

            Bounds modelBounds = CalculateLocalMeshBounds(root.transform, model);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = modelBounds.center;
            collider.size = modelBounds.size;

            SavePrefab(root, p.CablePrefabRoot + "/" + prefabName + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateCableReelPair(Paths p)
    {
        string prefabName = "PF_CableReelPair_A";
        GameObject root = new GameObject(prefabName);
        try
        {
            GameObject reelA = LoadRequired<GameObject>(p.CablePrefabRoot + "/PF_CableReel_A.prefab");
            GameObject reelB = LoadRequired<GameObject>(p.CablePrefabRoot + "/PF_CableReel_Dirty_A.prefab");

            GameObject a = InstantiatePrefabAsset(reelA, root.transform);
            a.name = "CableReel_A";
            a.transform.localPosition = new Vector3(-0.48f, 0f, 0.02f);
            a.transform.localRotation = Quaternion.Euler(0f, -12f, 0f);

            GameObject b = InstantiatePrefabAsset(reelB, root.transform);
            b.name = "CableReel_B";
            b.transform.localPosition = new Vector3(0.52f, 0f, 0.25f);
            b.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);

            SavePrefab(root, p.CablePrefabRoot + "/" + prefabName + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void BuildStorageClusters(Paths p)
    {
        CreateStorageClusterA(p);
        CreateStorageClusterB(p);
    }

    private static void CreateStorageClusterA(Paths p)
    {
        string prefabName = "PF_StorageCluster_A";
        GameObject root = new GameObject(prefabName);
        try
        {
            AddNested(p.TimberPrefabRoot + "/PF_TimberBundle_A.prefab", root.transform,
                "TimberBundle", new Vector3(-0.15f, 0f, 0.05f), 8f);
            AddNested(p.CablePrefabRoot + "/PF_CableReel_Dirty_A.prefab", root.transform,
                "CableReel", new Vector3(1.45f, 0f, 0.18f), -14f);
            AddNested(p.PalletPrefabRoot + "/PF_Pallet_Empty_A.prefab", root.transform,
                "EmptyPallet", new Vector3(-0.15f, 0f, -1.05f), 17f);
            AddNested(p.TimberPrefabRoot + "/PF_LooseTimberPile_A.prefab", root.transform,
                "LooseTimber", new Vector3(1.00f, 0f, -0.95f), -21f);

            SavePrefab(root, p.ClusterPrefabRoot + "/" + prefabName + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CreateStorageClusterB(Paths p)
    {
        string prefabName = "PF_StorageCluster_B";
        GameObject root = new GameObject(prefabName);
        try
        {
            AddNested(p.PalletPrefabRoot + "/PF_CementPallet_Brixora_A.prefab", root.transform,
                "CementPallet", new Vector3(-0.45f, 0f, 0.20f), -5f);
            AddNested(p.CablePrefabRoot + "/PF_CableReel_A.prefab", root.transform,
                "CableReel", new Vector3(1.05f, 0f, 0.35f), 24f);
            AddNested(p.TimberPrefabRoot + "/PF_TimberBundle_Rusty_A.prefab", root.transform,
                "TimberBundle", new Vector3(0.15f, 0f, -1.25f), 94f);

            SavePrefab(root, p.ClusterPrefabRoot + "/" + prefabName + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void AddNested(string prefabPath, Transform parent, string name, Vector3 position, float yaw)
    {
        GameObject prefab = LoadRequired<GameObject>(prefabPath);
        GameObject instance = InstantiatePrefabAsset(prefab, parent);
        instance.name = name;
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private static void CreateSimpleModelPrefab(
        string fbxPath,
        string prefabPath,
        string prefabName,
        Material[] materials,
        Vector3 expectedSize,
        Vector3? colliderSize,
        Vector3 colliderCenter,
        float visualScale = 1f)
    {
        GameObject root = new GameObject(prefabName);
        try
        {
            GameObject model = InstantiateModel(fbxPath, root.transform, "Model");
            AssignMaterialsBySlot(model, materials);
            OrientModelToExpectedBounds(model, expectedSize);
            ScaleModelUniform(model, visualScale);
            MoveModelToBottomCenter(root.transform, model);

            if (colliderSize.HasValue)
            {
                // Use the real normalized mesh bounds instead of a guessed center.
                Bounds modelBounds = CalculateLocalMeshBounds(root.transform, model);
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = modelBounds.center;
                collider.size = modelBounds.size;
            }

            SavePrefab(root, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ScaleModelUniform(GameObject model, float factor)
    {
        if (Mathf.Approximately(factor, 1f))
            return;

        model.transform.localScale *= factor;
    }

    private static void MoveModelToBottomCenter(Transform space, GameObject model)
    {
        Bounds b = CalculateLocalMeshBounds(space, model);
        model.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
    }

    private static Bounds CalculateLocalMeshBounds(Transform space, GameObject modelRoot)
    {
        MeshFilter[] filters = modelRoot.GetComponentsInChildren<MeshFilter>(true);
        bool hasBounds = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);

        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null)
                continue;

            Bounds b = filter.sharedMesh.bounds;
            Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;

            for (int xi = 0; xi < 2; xi++)
            for (int yi = 0; yi < 2; yi++)
            for (int zi = 0; zi < 2; zi++)
            {
                Vector3 localCorner = new Vector3(
                    xi == 0 ? b.min.x : b.max.x,
                    yi == 0 ? b.min.y : b.max.y,
                    zi == 0 ? b.min.z : b.max.z
                );

                Vector3 point = toSpace.MultiplyPoint3x4(localCorner);
                if (!hasBounds)
                {
                    result = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    result.Encapsulate(point);
                }
            }
        }

        if (!hasBounds)
            throw new InvalidOperationException("No MeshFilter geometry found under: " + modelRoot.name);

        return result;
    }

    private static void OrientModelToExpectedBounds(GameObject model, Vector3 expectedSize)
    {
        Transform parent = model.transform.parent;
        if (parent == null)
            throw new InvalidOperationException("Model must have a parent before axis normalization: " + model.name);

        Quaternion baseRotation = model.transform.localRotation;
        Quaternion bestRotation = baseRotation;
        float bestScore = float.PositiveInfinity;

        // FBX axis conversion can land on any 90-degree orientation depending on
        // Blender export + Unity ModelImporter settings. Try all right-angle
        // orientations and keep the one whose bounds best match the authored size.
        int[] angles = { 0, 90, 180, 270 };
        foreach (int x in angles)
        foreach (int y in angles)
        foreach (int z in angles)
        {
            Quaternion candidate = baseRotation * Quaternion.Euler(x, y, z);
            model.transform.localRotation = candidate;

            Bounds bounds = CalculateLocalMeshBounds(parent, model);
            Vector3 size = bounds.size;

            float score = AxisSizeScore(size.x, expectedSize.x) +
                          AxisSizeScore(size.y, expectedSize.y) +
                          AxisSizeScore(size.z, expectedSize.z);

            if (score < bestScore - 0.00001f)
            {
                bestScore = score;
                bestRotation = candidate;
            }
        }

        model.transform.localRotation = bestRotation;
        model.transform.localPosition = Vector3.zero;
    }

    private static float AxisSizeScore(float actual, float expected)
    {
        actual = Mathf.Max(actual, 0.000001f);
        expected = Mathf.Max(expected, 0.000001f);
        return Mathf.Abs(Mathf.Log(actual / expected));
    }

    private static GameObject InstantiateModel(string assetPath, Transform parent, string name)
    {
        GameObject source = LoadRequired<GameObject>(assetPath);
        GameObject instance = InstantiatePrefabAsset(source, parent);
        instance.name = name;

        // Preserve the FBX importer's local rotation and scale. Blender->Unity FBX
        // axis conversion can live on the imported root. Forcing identity here is
        // what made pallets/bags/timber stand on the wrong axis in the V1 builder.
        instance.transform.localPosition = Vector3.zero;
        return instance;
    }

    private static GameObject InstantiatePrefabAsset(GameObject asset, Transform parent)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(asset, parent) as GameObject;
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(asset, parent);
            instance.name = asset.name;
        }
        return instance;
    }

    private static T LoadRequired<T>(string assetPath) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset == null)
            throw new FileNotFoundException($"Required asset not found: {assetPath}");
        return asset;
    }

    private static void AssignSingleMaterial(GameObject root, Material material)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] current = renderer.sharedMaterials;
            if (current == null || current.Length == 0)
                renderer.sharedMaterial = material;
            else
            {
                Material[] replacement = new Material[current.Length];
                for (int i = 0; i < replacement.Length; i++)
                    replacement[i] = material;
                renderer.sharedMaterials = replacement;
            }
        }
    }

    private static void AssignMaterialsBySlot(GameObject root, Material[] materials)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            int slotCount = Mathf.Max(renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 0, materials.Length);
            if (slotCount == 0)
                continue;

            Material[] replacement = new Material[slotCount];
            for (int i = 0; i < slotCount; i++)
                replacement[i] = materials[Mathf.Min(i, materials.Length - 1)];
            renderer.sharedMaterials = replacement;
        }
    }

    private static void SavePrefab(GameObject root, string prefabPath)
    {
        bool success;
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out success);
        if (!success)
            throw new InvalidOperationException("Failed to save prefab: " + prefabPath);
    }
}
#endif
