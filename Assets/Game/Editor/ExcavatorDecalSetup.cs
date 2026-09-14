using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class ExcavatorDecalSetup
{
    public const string PrefabPath = "Assets/Game/Prefabs/Environment/Machinery/PF_Excavator_A.prefab";
    public const string LayoutPath = "Assets/Game/Editor/ExcavatorDecalLayout.asset";
    public const string MeshFolder = "Assets/Game/Prefabs/Environment/Machinery/ExcavatorDecalMeshes";
    public const string ShaderPath = "Assets/Game/Shaders/ExcavatorMeshDecal.shader";
    const string TextureFolder = "Assets/Game/Art/Environment/Machinery/Textures/";
    const string MaterialFolder = "Assets/Game/Art/Environment/Materials/Machinery/";
    const string Menu = "Tools/Kids VS Aliens/Environment/Excavator Decals/";
    public const int AtlasWidth = 1448, AtlasHeight = 1086;

    [MenuItem(Menu + "Create Missing Decals (Preserve Existing)")]
    public static void CreateMissing() => SavePrefab(false);

    [MenuItem(Menu + "Rebuild Decals from Authored Layout")]
    public static void Rebuild() => SavePrefab(true);

    [MenuItem(Menu + "Select Authored Layout")]
    public static void SelectLayout() => Selection.activeObject = GetOrCreateLayout();

    static void SavePrefab(bool rebuild)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before authoring the excavator prefab.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == PrefabPath)
        {
            // Operate on the visible contents; let the normal Prefab Mode save/Undo workflow apply.
            Undo.RegisterFullObjectHierarchyUndo(stage.prefabContentsRoot, "Author excavator decals");
            Configure(stage.prefabContentsRoot, GetOrCreateLayout(), rebuild);
            EditorSceneManager.MarkSceneDirty(stage.scene);
            Debug.Log("Excavator decals updated in Prefab Mode. Save the prefab to keep these edits.");
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Configure(root, GetOrCreateLayout(), rebuild);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log(rebuild ? "Excavator decals rebuilt from the authored layout." : "Missing excavator decals created; existing transforms preserved.");
    }

    public static ExcavatorDecalLayout GetOrCreateLayout()
    {
        var layout = AssetDatabase.LoadAssetAtPath<ExcavatorDecalLayout>(LayoutPath);
        if (layout != null) return layout;
        layout = ScriptableObject.CreateInstance<ExcavatorDecalLayout>();
        layout.labels = DefaultLabels();
        AssetDatabase.CreateAsset(layout, LayoutPath);
        return layout;
    }

    public static void Configure(GameObject root, ExcavatorDecalLayout layout, bool rebuild)
    {
        Transform model = root.transform.Find("Excavator_A_10");
        if (model == null || root.GetComponent<CameraOcclusionGroup>() == null)
            throw new InvalidOperationException("Expected Excavator_A_10 and the existing root CameraOcclusionGroup.");
        ValidateLayout(layout);
        if (!AssetDatabase.IsValidFolder(MeshFolder))
            AssetDatabase.CreateFolder("Assets/Game/Prefabs/Environment/Machinery", "ExcavatorDecalMeshes");
        Material brand = GetMaterial(false, rebuild), safety = GetMaterial(true, rebuild);
        bool newRoot = root.transform.Find("Decals") == null;
        Transform decals = Child(root.transform, "Decals");
        if (newRoot || rebuild)
        {
            decals.localPosition = model.localPosition;
            // The source FBX is Z-up; its prefab override turns it upright by -90 degrees X.
            decals.localRotation = model.localRotation * Quaternion.Euler(90, 0, 0);
            decals.localScale = model.localScale;
        }
        foreach (var label in layout.labels)
        {
            Transform existing = decals.Find(label.path);
            if (existing != null && !rebuild) continue;
            string[] segments = label.path.Split('/');
            Transform parent = decals;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                bool created = parent.Find(segments[i]) == null;
                parent = Child(parent, segments[i]);
                if (created || rebuild) { parent.localPosition = Vector3.zero; parent.localRotation = Quaternion.identity; parent.localScale = Vector3.one; }
            }
            Transform node = existing != null ? existing : Child(parent, segments[segments.Length - 1]);
            node.localPosition = label.surfacePosition + label.outwardNormal.normalized * label.surfaceOffset;
            node.localRotation = Quaternion.LookRotation(-label.outwardNormal.normalized, label.textUp.normalized);
            node.localScale = new Vector3(label.width, label.width * label.atlasPixels.height / label.atlasPixels.width, 1);
            var filter = node.GetComponent<MeshFilter>();
            if (filter == null) filter = node.gameObject.AddComponent<MeshFilter>();
            var renderer = node.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = node.gameObject.AddComponent<MeshRenderer>();
            filter.sharedMesh = GetMesh(label, rebuild);
            renderer.sharedMaterial = label.safetyAtlas ? safety : brand;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        // Save only resources owned by this tool. Never save unrelated open scenes or assets.
        AssetDatabase.SaveAssetIfDirty(brand);
        AssetDatabase.SaveAssetIfDirty(safety);
    }

    static Transform Child(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null) return child;
        var go = new GameObject(name) { layer = parent.gameObject.layer };
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Material GetMaterial(bool safety, bool rebuild)
    {
        string path = MaterialFolder + (safety ? "M_Excavator_Decals_Safety.mat" : "M_Excavator_Decals_Brand.mat");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + (safety ? "T_Excavator_Safety_Decals.png" : "T_Excavator_Rivetark_Decals.png"));
        if (shader == null || texture == null) throw new InvalidOperationException("Missing decal shader or supplied atlas.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool create = material == null;
        if (create) material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
        if (create || rebuild || material.shader != shader)
        {
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Cutoff", 0.3f);
            material.SetFloat("_Fade", 1);
            material.renderQueue = -1;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }
        if (create) AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static Mesh GetMesh(ExcavatorDecalLayout.Label label, bool rebuild)
    {
        string path = MeshFolder + "/" + label.path.Replace('/', '_') + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create = mesh == null;
        if (!create && !rebuild) return mesh;
        if (create) mesh = new Mesh { name = label.path.Replace('/', '_') };
        mesh.Clear();
        mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        Rect p = label.atlasPixels;
        // Half-texel inset keeps bilinear sampling inside the explicitly selected atlas label.
        float left = (p.xMin + .5f) / AtlasWidth, right = (p.xMax - .5f) / AtlasWidth;
        float bottom = 1 - (p.yMax - .5f) / AtlasHeight, top = 1 - (p.yMin + .5f) / AtlasHeight;
        mesh.uv = new[] { new Vector2(left,bottom), new Vector2(right,bottom), new Vector2(right,top), new Vector2(left,top) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
        if (create) AssetDatabase.CreateAsset(mesh, path);
        else { EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }
        return mesh;
    }

    public static void ValidateLayout(ExcavatorDecalLayout layout)
    {
        if (layout == null || layout.labels == null) throw new ArgumentException("Missing authored layout.");
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in layout.labels)
        {
            if (p == null || string.IsNullOrEmpty(p.path) || !paths.Add(p.path)
                || !(p.path.StartsWith("Branding/", StringComparison.Ordinal) || p.path.StartsWith("Safety/", StringComparison.Ordinal))
                || p.path.Contains("..") || p.path.Contains("\\") || p.path.EndsWith("/", StringComparison.Ordinal)
                || p.width <= 0 || p.surfaceOffset < 0 || p.outwardNormal.sqrMagnitude < .99f
                || Vector3.Cross(p.outwardNormal, p.textUp).sqrMagnitude < .01f
                || p.atlasPixels.width <= 1 || p.atlasPixels.height <= 1
                || p.atlasPixels.xMin < 0 || p.atlasPixels.yMin < 0
                || p.atlasPixels.xMax > AtlasWidth || p.atlasPixels.yMax > AtlasHeight)
                throw new ArgumentException("Invalid or duplicate decal layout entry: " + p?.path);
        }
    }

    public static List<ExcavatorDecalLayout.Label> DefaultLabels()
    {
        // Explicit measured surface positions, not guesses from bounds or automatic projection.
        // Measurements below are in the prefab inspection frame; subtract its authored FBX offset.
        var origin = new Vector3(-29.99f, .72f, -15.71f);
        var labels = new List<ExcavatorDecalLayout.Label>();
        void Add(string path, bool safety, Rect pixels, Vector3 position, Vector3 normal, float width, Vector3? up = null, float offset = .002f)
        {
            labels.Add(new ExcavatorDecalLayout.Label { path = path, safetyAtlas = safety, atlasPixels = pixels,
                surfacePosition = position - origin, outwardNormal = normal, textUp = up ?? Vector3.up, width = width, surfaceOffset = offset });
        }
        Add("Branding/BoomBrand_L", false, new Rect(60,395,638,130), new Vector3(-27.65f,6.03f,-15.4252f), Vector3.forward, 2.4f, new Vector3(-.09f,1,0), .003f);
        Add("Branding/BoomBrand_R", false, new Rect(752,395,642,130), new Vector3(-27.65f,6.03f,-16.0611f), Vector3.back, 2.4f, new Vector3(-.09f,1,0));
        Add("Branding/BodyBrand_L", false, new Rect(20,48,680,333), new Vector3(-33.39f,3.02f,-13.8589f), Vector3.forward, .72f);
        Add("Branding/BodyBrand_R", false, new Rect(747,48,680,333), new Vector3(-34.75f,2.97f,-17.53165f), Vector3.back, 1.5f);
        Add("Branding/RearBrand", false, new Rect(30,699,307,100), new Vector3(-36.141f,3.20f,-15.70f), Vector3.left, .50f, offset: .003f);
        Add("Branding/EX27_L", false, new Rect(29,536,375,85), new Vector3(-33.8f,2.36f,-13.8589f), Vector3.forward, .65f);
        Add("Branding/EX27_R", false, new Rect(1043,536,382,85), new Vector3(-32.9f,2.32f,-17.53165f), Vector3.back, .65f);
        Add("Safety/MovingBoom_L", true, new Rect(15,35,615,289), new Vector3(-29.1f,5.97f,-15.4252f), Vector3.forward, .40f);
        Add("Safety/MovingBoom_R", true, new Rect(15,35,615,289), new Vector3(-29.1f,5.97f,-16.0611f), Vector3.back, .40f);
        Add("Safety/KeepClear_Rear", true, new Rect(635,35,515,295), new Vector3(-36.140f,2.83f,-15.70f), Vector3.left, .50f, offset: .003f);
        Add("Safety/HighPressure", true, new Rect(575,334,578,226), new Vector3(-32.2f,2.70f,-17.53165f), Vector3.back, .46f);
        Add("Safety/AuthorizedPersonnel", true, new Rect(569,565,563,184), new Vector3(-33.0714f,2.95f,-14.45f), Vector3.left, .48f);
        Add("Safety/NoStep_Top", true, new Rect(19,565,545,184), new Vector3(-33.50f,3.5473f,-16.65f), Vector3.up, .60f, Vector3.right);
        Add("Safety/LiftPoint_L", true, new Rect(998,766,136,151), new Vector3(-33.15f,2.32f,-13.8589f), Vector3.forward, .16f);
        Add("Safety/LiftPoint_R", true, new Rect(998,766,136,151), new Vector3(-31.75f,2.34f,-17.53165f), Vector3.back, .16f);
        Add("Safety/ServiceLabels/EngineOil", true, new Rect(357,989,181,58), new Vector3(-33.50f,3.5473f,-15.92f), Vector3.up, .34f, Vector3.right);
        Add("Safety/ServiceLabels/Coolant", true, new Rect(899,989,175,58), new Vector3(-33.50f,3.5473f,-15.48f), Vector3.up, .34f, Vector3.right);
        return labels;
    }
}
