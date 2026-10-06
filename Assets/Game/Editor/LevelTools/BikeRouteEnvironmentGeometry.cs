using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EasyRoads3Dv3;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class BikeRouteEnvironmentSetup
{
    const string Content = "Assets/Game/Scenes/BikeRoute/Environment";
    const string ScenePath = "Assets/Game/Scenes/BikeRoute.unity";
    const string Materials = "Assets/Game/Art/Environment/Materials/";
    const string Models = "Assets/Game/Art/Environment/Buildings/BikeRoute/";
    static BikeRouteGuide guide;
    static Terrain terrain;
    static Route[] routes;
    static Material[] stone;
    static PhysicsMaterial slide;

    sealed class Route
    {
        public ERModularRoad source;
        public Vector3[] points;
        public float[] distance;
        public int index;
        public float half;
        public float Length => distance[distance.Length - 1];
        public Route(ERModularRoad road, int i)
        {
            source = road; index = i; points = road.splinePoints.ToArray();
            half = road.roadWidth * .5f + (i == 4 ? 12 : i == 2 ? 2.75f : 3.5f);
            distance = new float[points.Length];
            for (int n = 1; n < points.Length; n++) distance[n] = distance[n - 1] + Vector3.Distance(points[n - 1], points[n]);
        }
        public Vector3 At(float s)
        {
            int n = Array.BinarySearch(distance, Mathf.Clamp(s, 0, Length));
            if (n >= 0) return points[n];
            n = Mathf.Clamp(~n, 1, points.Length - 1);
            return Vector3.Lerp(points[n - 1], points[n], Mathf.InverseLerp(distance[n - 1], distance[n], s));
        }
        public Vector3 Right(float s) => Vector3.Cross(Vector3.up, (At(s + 2) - At(s - 2)).normalized).normalized;
        public bool Bridge(float s) { var p = At(s); return index == 5 && p.x > 1180 && p.x < 1270 && p.z > 1545 && p.z < 1760; }
        public float Corridor(float s) => half + WorkArea(this, s) * 10;
        public float Progress(float s) => Mathf.Lerp(guide.paths[index].startProgress, guide.paths[index].endProgress, s / Length);
    }

    // Shared vertices and no overlapping end caps: collision normals remain horizontal even on hills.
    sealed class MeshBuilder
    {
        public readonly List<Vector3> vertices = new();
        public readonly List<Vector2> uv = new();
        public readonly List<Color> colors = new();
        public readonly List<int>[] triangles;
        public MeshBuilder(int materials = 1) { triangles = Enumerable.Range(0, materials).Select(_ => new List<int>()).ToArray(); }
        public int Vertex(Vector3 p, Vector2 texture) { int i = vertices.Count; vertices.Add(p); uv.Add(texture); colors.Add(Color.black); return i; }
        public void Quad(int a, int b, int c, int d, int mat = 0, bool reverse = false)
        {
            triangles[mat].AddRange(reverse ? new[] { a, c, b, b, c, d } : new[] { a, b, c, b, d, c });
        }
        public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int mat = 0)
        {
            int i = vertices.Count;
            foreach (var p in new[] { a, b, c, d }) Vertex(p, new Vector2(p.x + p.z, p.y) * .25f);
            Quad(i, i + 1, i + 2, i + 3, mat, true);
        }
    }

    static Material LoadMaterial(string group, string name) => AssetDatabase.LoadAssetAtPath<Material>(Materials + group + "/M_" + name + ".mat")
        ?? throw new InvalidOperationException("Import the requested Poly Haven material first: " + name);
    static Transform Group(string name, Transform parent) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
    static float Blend(float a, float b, float p) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, p));
    // Broad, overlapping transition bands also drive cliff forms, ground paint and vegetation density.
    static Vector2 Climate(float p)
    {
        float basalt = Blend(530, 1250, p) * (1 - Blend(1580, 2220, p));
        float forest = Mathf.Max(Blend(1690, 2390, p) * (1 - Blend(2680, 3370, p)),
            Blend(3710, 4440, p) * (1 - Blend(4620, 5220, p)));
        return new Vector2(basalt, forest);
    }
    static float WorkArea(Route r, float s)
    {
        if (r.index == 0) return 1 - Blend(280, 350, s);
        if (r.index == 7) return Blend(10, 65, s);
        if (r.index == 4) return Blend(60, 160, s) * (1 - Blend(460, 560, s));
        if (r.index == 5) return Blend(620, 670, s) * (1 - Blend(765, 825, s));
        return 0;
    }
    static float Height(Route r, float s)
    {
        var climate = Climate(r.Progress(s));
        float h = 10 + 9 * Mathf.PerlinNoise(s * .009f, r.index + 2.4f)
            + 4 * Mathf.Sin(s * .043f + r.index) + 2 * Mathf.Sin(s * .119f);
        h += climate.x * 7 - climate.y * 3;
        float opening = r.index == 0 ? 1 - Blend(150, 290, s) : r.index == 7 ? Blend(40, 150, s) : r.index == 4 ? .8f : 0;
        return Mathf.Lerp(Mathf.Lerp(h, 4.5f, opening), 1.8f, WorkArea(r, s));
    }
    // One footprint owns the visible wall foot, collision and opening tests.
    static float WallOffset(Route r, float s, int side) => Mathf.Max(r.source.roadWidth * .5f + 1.4f,
        r.Corridor(s) - .5f + (1 - WorkArea(r, s)) * (1.5f * Mathf.Sin(s * .026f + side * 1.7f + r.index)
        + 1.1f * Mathf.Sin(s * .063f + side * 2.1f)));
    static bool Opening(Route r, Vector3 p, float clearance = 0)
    {
        foreach (var other in routes)
        {
            if (other == r) continue;
            for (int i = 0; i < other.points.Length - 1; i++)
            {
                Vector3 delta = other.points[i + 1] - other.points[i];
                Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
                float t = Mathf.Clamp01(Vector3.Dot(p - other.points[i], flat) / Mathf.Max(.001f, flat.sqrMagnitude));
                Vector3 q = other.points[i] + delta * t;
                if (Mathf.Abs(q.y - p.y) > 3) continue;
                float station = Mathf.Lerp(other.distance[i], other.distance[i + 1], t);
                int side = Vector3.Dot(p - q, other.Right(station)) < 0 ? -1 : 1;
                float radius = WallOffset(other, station, side) + clearance;
                if (new Vector2(q.x - p.x, q.z - p.z).sqrMagnitude < radius * radius) return true;
            }
        }
        return false;
    }
    static float WallHeight(Route r, float s, Vector3 foot)
    {
        float top = r.Bridge(s) ? 1.3f : Height(r, s);
        foreach (var other in routes)
            if (other != r) foreach (var q in other.points)
                if (q.y > foot.y + 5 && new Vector2(q.x - foot.x, q.z - foot.z).sqrMagnitude < Mathf.Pow(other.half + 3, 2))
                    top = Mathf.Min(top, q.y - foot.y - 2);
        return top;
    }
    static GameObject MeshObject(string name, MeshBuilder b, Transform parent, Material[] materials, bool collision)
    {
        if (b.vertices.Count == 0 || b.triangles.All(t => t.Count == 0)) return null;
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Content + "/" + name + ".asset");
        bool create = mesh == null;
        if (create) mesh = new Mesh { name = name }; else mesh.Clear();
        mesh.indexFormat = b.vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(b.vertices); mesh.SetUVs(0, b.uv); mesh.SetColors(b.colors); mesh.subMeshCount = b.triangles.Length;
        for (int i = 0; i < b.triangles.Length; i++) mesh.SetTriangles(b.triangles[i], i);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        if (create) AssetDatabase.CreateAsset(mesh, Content + "/" + name + ".asset");
        else { EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }
        var go = Group(name, parent).gameObject; go.isStatic = true;
        if (materials != null) { go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = materials; }
        if (collision) { var c = go.AddComponent<MeshCollider>(); c.sharedMesh = mesh; c.sharedMaterial = slide; }
        return go;
    }

    public static void InspectBatch()
    {
        Initialize(); Physics.SyncTransforms();
        using var report = new StreamWriter("Logs/BikeRouteEnvironment/collision-before.txt");
        foreach (var r in routes)
            for (float s = 2; s < r.Length - 4; s += 3)
                foreach (float height in new[] { 1.2f, 4f, 7f, 10f })
                {
                    var p = r.At(s) + Vector3.up * height; var delta = r.At(s + 3) - r.At(s);
                    foreach (var hit in Physics.SphereCastAll(p, .75f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                        if (hit.collider.transform.parent != null && hit.collider.transform.parent.name == "Corridor Boundaries")
                            report.WriteLine($"{r.source.name} s={s:F1} height={height} blocker={hit.collider.name} point={hit.point} normal={hit.normal}");
                }
        foreach (var ramp in guide.jumps) report.WriteLine($"JUMP path={ramp.path} release={ramp.releaseDistance} point={guide.At(ramp.path, ramp.releaseDistance).position}");
    }
    static void Initialize()
    {
        EditorSceneManager.OpenScene(ScenePath);
        guide = Object.FindAnyObjectByType<BikeRouteGuide>(); terrain = Object.FindAnyObjectByType<Terrain>();
        routes = Object.FindObjectsByType<ERModularRoad>(FindObjectsInactive.Include).OrderBy(r => r.name, StringComparer.Ordinal).Select((r, i) => new Route(r, i)).ToArray();
        if (guide == null || terrain == null || routes.Length != guide.paths.Length) throw new InvalidOperationException("BikeRoute authoring sources are incomplete.");
    }
    [MenuItem("Tools/Level Authoring/Author BikeRoute Environment Art and Collision")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Initialize();
        foreach (string name in BuildingNames)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx") == null)
                throw new InvalidOperationException("Missing original Blender building: " + name);
        stone = new[] { LoadMaterial("Wall", "Quarry_Wall_02"), LoadMaterial("Wall", "Dark_Rock"), LoadMaterial("Ground", "Forest_Floor") };
        var asphalt = LoadMaterial("Ground", "Asphalt_01");
        LoadMaterial("Wall", "Sandstone_Blocks_05"); LoadMaterial("Wall", "Sandstone_Blocks_08"); LoadMaterial("Ground", "Forrest_Ground_01");
        Directory.CreateDirectory(Content); AssetDatabase.Refresh();
        slide = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Content + "/RoadsideSlide.physicMaterial");
        if (slide == null) { slide = new PhysicsMaterial("RoadsideSlide") { dynamicFriction = .08f, staticFriction = .08f, bounciness = 0, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum }; AssetDatabase.CreateAsset(slide, Content + "/RoadsideSlide.physicMaterial"); }
        terrain.GetComponent<TerrainCollider>().sharedMaterial = slide;
        var geometry = GameObject.Find("LevelGeometry").transform;
        foreach (var name in new[] { "Environment Art", "Smooth Corridor Collision" })
            if (geometry.Find(name) != null) Object.DestroyImmediate(geometry.Find(name).gameObject);
        var art = Group("Environment Art", geometry); var collision = Group("Smooth Corridor Collision", geometry);
        foreach (Transform old in geometry.Find("Corridor Boundaries"))
            if (old.name.Contains("Cut") || old.name.Contains("BridgeSafety")) { old.gameObject.SetActive(false); old.gameObject.tag = "EditorOnly"; }
        foreach (Transform road in geometry.Find("BakedAsphalt")) road.GetComponent<Renderer>().sharedMaterial = asphalt;
        foreach (var renderer in geometry.Find("JumpTests").GetComponentsInChildren<Renderer>())
            renderer.sharedMaterial = renderer.name.StartsWith("03_") ? asphalt : LoadMaterial("Ground", "Forrest_Ground_01");
        foreach (var mat in stone.Concat(new[] { asphalt, LoadMaterial("Wall", "Sandstone_Blocks_05"), LoadMaterial("Wall", "Sandstone_Blocks_08"), LoadMaterial("Ground", "Forrest_Ground_01") }))
        {
            mat.EnableKeyword("_OCCLUSIONMAP"); mat.enableInstancing = true;
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
        }
        PaintGround();
        SharpenLocalTerrainToes();
        // Terrain and its TerrainCollider own the canyon. Never rebuild facade strips over it.
        BuildDressing(art); BuildBuildings(geometry, art); AuthorContainmentAndPressure(geometry, art);
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene); EditorSceneManager.SaveScene(terrain.gameObject.scene);
        Debug.Log("BikeRoute environment authored; road, bike, chase and finish owners retained.");
    }

    static void Column(MeshBuilder mesh, Vector3 p, float radius, float height, int material)
    {
        for (int n = 0; n < 6; n++)
        {
            float a = n * Mathf.PI / 3, b = (n + 1) * Mathf.PI / 3;
            var left = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
            var right = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * radius;
            mesh.Face(p + left, p + right, p + left + Vector3.up * height, p + right + Vector3.up * height, material);
            mesh.Face(p + left + Vector3.up * height, p + right + Vector3.up * height, p + Vector3.up * height, p + Vector3.up * height, material);
        }
    }
}
