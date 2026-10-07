using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Authors the crossing parapets and breakable shortcut delineators, not the corridor.</summary>
public static class BikeRouteBoundaryAuthoring
{
    const string Folder = "Assets/Game/Scenes/BikeRoute/Environment/Boundaries";
    const string RootName = "Readable boundaries";
    public const float CrossingStart = 278, CrossingEnd = 514, ParapetOffset = 7.5f;
    public const float ParapetHeight = 1.3f, ParapetThickness = .4f;

    [MenuItem("Tools/Level Authoring/Refresh BikeRoute Boundary Readability")]
    public static void Refresh()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "BikeRoute")
            throw new InvalidOperationException("Open BikeRoute outside Play Mode first.");
        var geometry = scene.GetRootGameObjects().Single(r => r.name == "LevelGeometry").transform;
        var guide = Object.FindAnyObjectByType<BikeRouteGuide>();
        var terrain = geometry.GetComponentInChildren<Terrain>();
        var deck = geometry.Find("Crossing Structure/Crossing shoulder deck").GetComponent<MeshCollider>();
        var concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Materials/CrossingRail.mat");
        var paint = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Materials/RoadCenter.mat");
        var slide = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Game/Scenes/BikeRoute/Environment/RoadsideSlide.physicMaterial");
        if (guide == null || terrain == null || deck == null || concrete == null || paint == null || slide == null)
            throw new InvalidOperationException("Missing authored crossing or boundary materials.");

        // Preflight both sides before any scene/asset writes. Use only the existing
        // support surfaces, so a prop or an old parapet cannot become a false floor.
        var director = Object.FindAnyObjectByType<BikeRouteChaseDirector>();
        if (director == null || director.PlayerBike == null) throw new InvalidOperationException("Missing player bike.");
        var markerStarts = new[] { ShortcutMarkerStart(guide, 8), ShortcutMarkerStart(guide, 9) };
        var banks = new List<Vector3[]>();
        foreach (int side in new[] { -1, 1 })
        {
            var points = new List<Vector3>();
            for (float station = CrossingStart; station <= CrossingEnd; station += 2)
            {
                var at = guide.At(5, station);
                // Stop at the exposed deck: extending through the accepted abutment
                // banks would put their rock tips inside the new parapet faces.
                Vector3 p = at.position + at.Right * (side * ParapetOffset);
                var ray = new Ray(p + Vector3.up * 5, Vector3.down);
                float floor = terrain.SampleHeight(p) + terrain.transform.position.y;
                if (deck.Raycast(ray, out var hit, 10)) floor = Mathf.Max(floor, hit.point.y);
                if (floor < at.position.y - .75f)
                    throw new InvalidOperationException($"Unsupported parapet: station {station}, side {side}");
                // Ends can terminate into a bank; the face stays at deck height.
                p.y = Mathf.Min(floor, at.position.y - .15f) - .12f;
                points.Add(p);
            }
            banks.Add(points.ToArray());
        }
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game/Scenes/BikeRoute/Environment", "Boundaries");
        var white = ParapetMaterial("Parapet White", concrete, new Color(.88f, .87f, .82f));
        var yellow = ParapetMaterial("Parapet Yellow", concrete, new Color(.95f, .68f, .035f));
        var existing = geometry.Find(RootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
        var root = new GameObject(RootName).transform; root.SetParent(geometry, false);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Refresh BikeRoute boundaries");
        for (int side = 0; side < banks.Count; side++)
        {
            var mesh = BuildParapet(banks[side]);
            CreateMesh(side == 0 ? "Crossing left parapet" : "Crossing right parapet", mesh, root, white, slide, yellow);
        }
        var reflectors = new Surface();
        foreach (var bank in banks)
            for (int i = 10; i < bank.Length - 10; i += 10)
            {
                Vector3 forward = (bank[i + 1] - bank[i - 1]).normalized;
                Vector3 inward = (guide.At(5, CrossingStart + i * 2).position - bank[i]).normalized;
                inward.y = 0; inward.Normalize();
                var center = bank[i] + inward * (ParapetThickness * .5f + .012f) + Vector3.up * .98f;
                reflectors.Face(center-forward*.18f-Vector3.up*.07f, center+forward*.18f-Vector3.up*.07f,
                    center+forward*.18f+Vector3.up*.07f, center-forward*.18f+Vector3.up*.07f, inward);
            }
        CreateMesh("Crossing reflectors", reflectors.ToMesh(), root, paint, null);

        // One shared local mesh, but an independent existing sign owner per pole.
        var marker = new Surface();
        marker.Box(Vector3.zero, Vector3.right * .1f, Vector3.forward * .035f, 1.45f);
        marker.material = 1;
        foreach (int face in new[] { -1, 1 })
        {
            var center = Vector3.up * 1.12f + Vector3.forward * (face * .037f);
            marker.Face(center-Vector3.right*.1f-Vector3.up*.12f, center+Vector3.right*.1f-Vector3.up*.12f,
                center+Vector3.right*.1f+Vector3.up*.12f, center-Vector3.right*.1f+Vector3.up*.12f, Vector3.forward*face);
        }
        var markerMesh = SaveMesh("Shortcut delineator", marker.ToMesh());
        foreach (int path in new[] { 8, 9 })
        for (int slot = 0; slot < 3; slot++)
        foreach (int side in new[] { -1, 1 })
        {
            var at = guide.At(path, markerStarts[path-8] + slot * 16);
            Vector3 p = at.position + at.Right * (side * (at.halfWidth + 1.5f));
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y - .08f;
            var pole = new GameObject($"Shortcut {path} marker {slot} {side}");
            pole.transform.SetParent(root, false);
            pole.transform.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.ProjectOnPlane(at.forward, Vector3.up)));
            var visual = new GameObject("Delineator", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(pole.transform, false);
            visual.GetComponent<MeshFilter>().sharedMesh = markerMesh;
            visual.GetComponent<MeshRenderer>().sharedMaterials = new[] { concrete, paint };
            var solid = visual.AddComponent<BoxCollider>();
            solid.center = Vector3.up * .725f;
            solid.size = new Vector3(.2f, 1.45f, .07f);
            solid.sharedMaterial = slide;
            var body = visual.AddComponent<Rigidbody>();
            body.mass = 8;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var trigger = pole.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = solid.center;
            trigger.size = new Vector3(4, 1.65f, 4);
            pole.AddComponent<RunWorldObject>().ConfigureIdentity($"bikeroute-shortcut-marker-{path}-{slot}-{side}");
            var sign = pole.AddComponent<BikeRouteBreakableSign>();
            sign.signBody = body;
            sign.approach = trigger;
            sign.playerBike = director.PlayerBike;
            sign.speedFraction = .6f;
            sign.speedLoss = .08f;
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Tools/Level Authoring/Refresh BikeRoute Parapet Color Spacing")]
    public static void RefreshParapetColorSpacing()
    {
        int segments = Mathf.RoundToInt((CrossingEnd - CrossingStart) / 2);
        foreach (string side in new[] { "left", "right" })
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + $"/Crossing {side} parapet.asset");
            // This authored mesh has 24 vertices per span and 96 per rounded cap.
            // Reassign existing triangles only; collision topology and vertices stay exact.
            if (mesh == null || mesh.vertexCount != segments * 24 + 192)
                throw new InvalidOperationException("Unexpected parapet topology; inspect before changing colors.");
            var colors = new[] { new List<int>(), new List<int>() };
            var indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int first = Mathf.Min(indices[i], Mathf.Min(indices[i+1], indices[i+2]));
                int section = first < segments * 24 ? (first / 24) / 6
                    : first < segments * 24 + 96 ? 0 : (segments - 1) / 6;
                colors[section % 2].AddRange(new[] { indices[i], indices[i+1], indices[i+2] });
            }
            mesh.SetTriangles(colors[0], 0);
            mesh.SetTriangles(colors[1], 1);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
        }
    }

    public static float ShortcutMarkerStart(BikeRouteGuide guide, int path)
    {
        // Shortcuts initially share their main-road approach. Start the markers only
        // once the whole opening has separated, never inside the asphalt carriageway.
        for (float station = 8; station < guide.paths[path].Length * .5f; station += 4)
        {
            var at = guide.At(path, station); bool separated = true;
            foreach (var main in guide.paths.Where(p => !p.shortcut))
                for (int i = 0; i < main.points.Length - 1; i++)
                {
                    Vector3 a = main.points[i], delta = main.points[i + 1] - a;
                    float t = Mathf.Clamp01(Vector3.Dot(at.position-a,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                    Vector3 nearest = a + delta*t;
                    if (Mathf.Abs(nearest.y-at.position.y) > 4) continue;
                    if (Vector3.ProjectOnPlane(nearest-at.position,Vector3.up).magnitude < main.halfWidth+at.halfWidth+2)
                        separated = false;
                }
            if (separated) return station;
        }
        throw new InvalidOperationException("No clear shortcut mouth: " + path);
    }

    static Mesh BuildParapet(Vector3[] points)
    {
        var surface = new Surface(); var rights = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
            rights[i] = Vector3.Cross(Vector3.up, points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(i-1,0)]).normalized;
        for (int i = 0; i < points.Length - 1; i++)
        {
            surface.material = (i / 6) % 2; // 12 m sections, synchronized on both sides.
            var a = points[i]; var b = points[i+1]; var ra = rights[i]*ParapetThickness*.5f; var rb = rights[i+1]*ParapetThickness*.5f;
            var up = Vector3.up*ParapetHeight;
            surface.Face(a+ra,b+rb,b+rb+up,a+ra+up,rights[i]);
            surface.Face(a-ra,b-rb,b-rb+up,a-ra+up,-rights[i]);
            surface.Face(a-ra,a+ra,b+rb,b-rb,Vector3.down);
            surface.Face(a-ra+up,a+ra+up,b+rb+up,b-rb+up,Vector3.up);
        }
        // Round the terminal in plan rather than lowering it into a launch ramp.
        foreach (int end in new[] { 0, points.Length - 1 })
        {
            surface.material = end == 0 ? 0 : ((points.Length - 2) / 6) % 2;
            var center = points[end]; var right = rights[end];
            var outward = Vector3.Cross(right,Vector3.up)*(end==0?-1:1);
            for (int i = 0; i < 8; i++)
            {
                float a = i*Mathf.PI/8, b = (i+1)*Mathf.PI/8;
                Vector3 pa = center+(right*Mathf.Cos(a)+outward*Mathf.Sin(a))*ParapetThickness*.5f;
                Vector3 pb = center+(right*Mathf.Cos(b)+outward*Mathf.Sin(b))*ParapetThickness*.5f;
                var up = Vector3.up*ParapetHeight;
                surface.Face(pa,pb,pb+up,pa+up,(pa+pb)*.5f-center);
                surface.Triangle(center,pa,pb,Vector3.down);
                surface.Triangle(center+up,pa+up,pb+up,Vector3.up);
            }
        }
        return surface.ToMesh();
    }
    static Material ParapetMaterial(string name, Material source, Color color)
    {
        string path = Folder + "/" + name + ".mat";
        var asset = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (asset == null)
        {
            asset = new Material(source) { name = name };
            asset.SetColor("_BaseColor", color);
            asset.SetColor("_Color", color);
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    static Mesh SaveMesh(string name, Mesh mesh)
    {
        string path = Folder + "/" + name + ".asset";
        mesh.name = name;
        var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (asset == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, asset);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    static void CreateMesh(string name, Mesh mesh, Transform parent, Material material, PhysicsMaterial slide, Material alternate = null)
    {
        var asset = SaveMesh(name, mesh);
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = asset;
        go.GetComponent<MeshRenderer>().sharedMaterials = alternate == null ? new[] { material } : new[] { material, alternate };
        if (slide != null)
        {
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = asset;
            collider.sharedMaterial = slide;
        }
    }
    sealed class Surface
    {
        readonly List<Vector3> vertices = new(); readonly List<Vector2> uv = new(); readonly List<int>[] triangles = { new(), new() };
        public int material;
        public void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 normal)
        {
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0) (b,c)=(c,b);
            int first=vertices.Count;vertices.AddRange(new[]{a,b,c});
            uv.AddRange(new[]{new Vector2(a.x+a.z,a.y),new Vector2(b.x+b.z,b.y),new Vector2(c.x+c.z,c.y)});
            triangles[material].AddRange(new[]{first,first+1,first+2});
        }
        public void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal)
        { Triangle(a,b,c,normal);Triangle(a,c,d,normal); }
        public void Box(Vector3 p,Vector3 right,Vector3 forward,float height)
        {
            var up=Vector3.up*height;
            Face(p-right-forward,p+right-forward,p+right-forward+up,p-right-forward+up,-forward);
            Face(p-right+forward,p+right+forward,p+right+forward+up,p-right+forward+up,forward);
            Face(p-right-forward,p-right+forward,p-right+forward+up,p-right-forward+up,-right);
            Face(p+right-forward,p+right+forward,p+right+forward+up,p+right-forward+up,right);
            Face(p-right-forward+up,p+right-forward+up,p+right+forward+up,p-right+forward+up,Vector3.up);
        }
        public Mesh ToMesh() { var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.subMeshCount = triangles[1].Count > 0 ? 2 : 1;
            for (int i = 0; i < mesh.subMeshCount; i++) mesh.SetTriangles(triangles[i],i);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh; }
    }
}
