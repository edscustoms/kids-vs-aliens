using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class BikeRouteEnvironmentSetup
{
    struct GroundSample { public Route route; public float distance; public Vector3 point; }
    static void PaintGround()
    {
        var data = terrain.terrainData;
        var materials = new[] { stone[0], stone[1], LoadMaterial("Ground", "Forrest_Ground_01"), stone[2] };
        var layers = new TerrainLayer[materials.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            string path = Content + "/Ground_" + i + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            bool create = layer == null; if (create) layer = new TerrainLayer();
            layer.diffuseTexture = (Texture2D)materials[i].GetTexture("_BaseMap");
            layer.normalMapTexture = (Texture2D)materials[i].GetTexture("_BumpMap");
            layer.maskMapTexture = (Texture2D)materials[i].GetTexture("_MetallicGlossMap");
            layer.tileSize = Vector2.one * (i < 2 ? 5 : 3.5f); layer.normalScale = .7f;
            if (create) AssetDatabase.CreateAsset(layer, path); else { EditorUtility.SetDirty(layer); AssetDatabase.SaveAssetIfDirty(layer); }
            layers[i] = layer;
        }
        data.terrainLayers = layers;
        var bins = new Dictionary<Vector2Int, List<GroundSample>>();
        foreach (var route in routes)
            for (float s = 0; s < route.Length; s += 5)
            {
                var sample = new GroundSample { route = route, distance = s, point = route.At(s) };
                var cell = new Vector2Int(Mathf.FloorToInt(sample.point.x / 32), Mathf.FloorToInt(sample.point.z / 32));
                for (int z = -4; z <= 4; z++) for (int x = -4; x <= 4; x++)
                {
                    var key = cell + new Vector2Int(x, z);
                    if (!bins.TryGetValue(key, out var list)) bins[key] = list = new List<GroundSample>();
                    list.Add(sample);
                }
            }
        GroundSample Nearest(float x, float z, out float distance)
        {
            var result = default(GroundSample); float best = float.PositiveInfinity;
            if (bins.TryGetValue(new Vector2Int(Mathf.FloorToInt(x / 32), Mathf.FloorToInt(z / 32)), out var list))
                foreach (var sample in list)
                {
                    float d = Mathf.Pow(sample.point.x - x, 2) + Mathf.Pow(sample.point.z - z, 2);
                    if (d < best) { best = d; result = sample; }
                }
            distance = Mathf.Sqrt(best); return result;
        }
        int resolution = data.alphamapResolution; var paint = new float[resolution, resolution, 4];
        for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++)
        {
            float wx = x * data.size.x / (resolution - 1), wz = z * data.size.z / (resolution - 1);
            var sample = Nearest(wx, wz, out float distance);
            var climate = sample.route == null ? Vector2.zero : Climate(sample.route.Progress(sample.distance));
            float forest = climate.y * (.75f + .25f * Mathf.PerlinNoise(wx * .027f, wz * .027f));
            float dark = climate.x * (1 - forest);
            float trail = sample.route != null ? 1 - Blend(sample.route.source.roadWidth * .4f, sample.route.half + 6, distance) : 0;
            paint[z, x, 0] = 1 - forest - dark; paint[z, x, 1] = dark;
            paint[z, x, 2] = forest * trail; paint[z, x, 3] = forest * (1 - trail);
        }
        data.SetAlphamaps(0, 0, paint);
        int hres = data.heightmapResolution; var heights = data.GetHeights(0, 0, hres, hres);
        for (int z = 0; z < hres; z++) for (int x = 0; x < hres; x++)
        {
            float wx = x * data.size.x / (hres - 1), wz = z * data.size.z / (hres - 1);
            var sample = Nearest(wx, wz, out float distance);
            if (sample.route == null || distance < sample.route.half - .4f || distance > sample.route.Corridor(sample.distance) + 35) continue;
            // Preserve the already-carved lower trail and deck air space at the over/under.
            if (wx > 1160 && wx < 1290 && wz > 1525 && wz < 1780) continue;
            if (distance < sample.route.Corridor(sample.distance) - .4f)
            { heights[z, x] = (sample.point.y - .2f) / data.size.y; continue; }
            float rim = sample.point.y + Height(sample.route, sample.distance);
            float outer = 1 - Blend(sample.route.Corridor(sample.distance) + 9, sample.route.Corridor(sample.distance) + 35, distance);
            float inner = Blend(sample.route.Corridor(sample.distance) + 4, sample.route.Corridor(sample.distance) + 8, distance);
            float desired = Mathf.Lerp(sample.point.y - .2f, rim, inner);
            heights[z, x] = Mathf.Lerp(heights[z, x], desired / data.size.y, outer);
        }
        // Smooth only the outer terrain; the road/offroad floor, authored ramps and crossover stay exact.
        for (int pass = 0; pass < 3; pass++)
        {
            var smoothed = (float[,])heights.Clone();
            for (int z = 1; z < hres - 1; z++) for (int x = 1; x < hres - 1; x++)
            {
                float wx = x * data.size.x / (hres - 1), wz = z * data.size.z / (hres - 1);
                var sample = Nearest(wx, wz, out float distance);
                if (sample.route == null || distance < sample.route.Corridor(sample.distance) + 5 || distance > sample.route.half + 140) continue;
                if (wx > 1160 && wx < 1290 && wz > 1525 && wz < 1780) continue;
                smoothed[z, x] = (heights[z, x] * 4 + heights[z-1, x] + heights[z+1, x] + heights[z, x-1] + heights[z, x+1]) / 8;
            }
            heights = smoothed;
        }
        data.SetHeights(0, 0, heights); terrain.Flush(); EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
    }

    static GameObject Place(string path, Vector3 p, Quaternion rotation, Transform parent, float scale = 1)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing existing dressing asset: " + path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(p, rotation); go.transform.localScale *= scale;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform); return go;
    }
    static void BuildDressing(Transform art)
    {
        var existing = GameObject.Find("LevelGeometry").transform.Find("Forest Clusters");
        if (existing != null) { existing.gameObject.SetActive(false); existing.gameObject.tag = "EditorOnly"; }
        var forest = Group("Transitional forest groves", art); var random = new System.Random(431);
        const string trees = "Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer ";
        foreach (var r in routes)
            for (float s = 24; s < r.Length - 24; s += 24)
            {
                var climate = Climate(r.Progress(s));
                if (r.Bridge(s) || random.NextDouble() > climate.y * .85f + .06f) continue;
                int side = random.Next(2) == 0 ? -1 : 1;
                var cluster = Group(r.source.name + " Grove " + Mathf.RoundToInt(s), forest);
                for (int n = 0; n < 4; n++)
                {
                    float station = s + (n - 1.5f) * 3.5f;
                    var p = r.At(station) + r.Right(station) * (side * (WallOffset(r, station, side) + 4 + n % 2 * 3));
                    if (Opening(r, p, 4)) continue;
                    p.y = terrain.SampleHeight(p) + terrain.transform.position.y - .12f;
                    Place(trees + (n == 0 ? "Medium" : "Small") + " BOTD URP.prefab", p, Quaternion.Euler(0, random.Next(360), 0), cluster, n == 3 ? .35f : .65f + (float)random.NextDouble() * .4f);
                }
                if (climate.y > .35f) for (int n = 0; n < 3; n++)
                {
                    float station = s + n * 4; var p = r.At(station) + r.Right(station) * (side * (WallOffset(r, station, side) + 1));
                    if (Opening(r, p, 3)) continue;
                    p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                    Place(trees + "Small BOTD URP.prefab", p, Quaternion.Euler(0, random.Next(360), 0), cluster, .18f + n * .04f);
                }
            }
        var yards = Group("Service and construction dressing", art);
        const string prefabs = "Assets/Game/Prefabs/Environment/";
        var items = new[] { "Construction/PF_ConcretePipe_Big.prefab", "Construction/PF_ConcretePipe_Small.prefab",
            "Construction/PF_Portable_SiteToilet.prefab", "ConstructionSite/Cable/PF_CableReel_A.prefab",
            "ConstructionSite/Pallets/PF_CementPallet_Gritmix_A.prefab", "ConstructionSite/Pallets/PF_TImber_Bundle_On_Pallet.prefab" };
        foreach (var location in new[] { (0, 110f, 1), (0, 240f, -1), (4, 250f, 1), (5, 700f, -1), (7, 115f, 1), (7, 235f, -1) })
        {
            var r = routes[location.Item1]; float s = location.Item2; int side = location.Item3;
            var yard = Group("Work pocket " + r.source.name + " " + s, yards);
            for (int n = 0; n < items.Length; n++)
            {
                var p = r.At(s + n * 5) + r.Right(s + n * 5) * (side * (r.source.roadWidth * .5f + 2.2f));
                if (Opening(r, p, 2)) continue;
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                var prop = Place(prefabs + items[n], p, Quaternion.LookRotation(r.Right(s) * side), yard, n == 0 ? .75f : 1);
                KeepOutsideDrivingLine(prop, r, s + n * 5, side);
            }
            for (int n = 0; n < 2; n++)
            {
                var p = r.At(s - 9 + n * 5) + r.Right(s) * (side * (r.source.roadWidth * .5f + 1.2f));
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                var barrier = Place("Assets/Game/ThirdParty/Concrete Barriers/Prefabs/PRE_Concrete Barrier_1.prefab", p,
                    Quaternion.LookRotation(r.Right(s) * side), yard);
                KeepOutsideDrivingLine(barrier, r, s - 9 + n * 5, side);
            }
        }
        // Reuse the existing excavator prefab; no mission/controller is added to the scene.
        var service = routes[4]; var machine = service.At(410) + service.Right(410) * 11;
        machine.y = terrain.SampleHeight(machine) + terrain.transform.position.y;
        Place(prefabs + "Machinery/PF_Excavator_A.prefab", machine, Quaternion.LookRotation(-service.Right(410)), yards);
        BuildForestEdges(art);
    }

    public static void RefreshForestEdgesBatch()
    {
        Initialize(); stone = new[] { LoadMaterial("Wall", "Quarry_Wall_02") };
        var art = GameObject.Find("LevelGeometry").transform.Find("Environment Art");
        if (art == null) throw new InvalidOperationException("Author BikeRoute environment first.");
        BuildForestEdges(art);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(terrain.gameObject.scene);
    }

    static void BuildForestEdges(Transform art)
    {
        var previous = art.Find("Forest ledge dressing");
        if (previous != null) Object.DestroyImmediate(previous.gameObject);
        var root = Group("Forest ledge dressing", art);
        string meshPath = Content + "/ForestBoulder.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            var b = new MeshBuilder(); const int sides = 9, rings = 6;
            for (int row = 0; row <= rings; row++) for (int side = 0; side <= sides; side++)
            {
                float latitude = row * Mathf.PI / rings, angle = side * Mathf.PI * 2 / sides;
                float radius = 1 + .16f * Mathf.Sin(angle * 3 + row * 2);
                b.Vertex(new Vector3(Mathf.Cos(angle) * Mathf.Sin(latitude) * radius, Mathf.Cos(latitude), Mathf.Sin(angle) * Mathf.Sin(latitude) * radius), new Vector2(side / (float)sides, row / (float)rings));
                if (row > 0 && side > 0) { int n = row * (sides + 1) + side; b.Quad(n - sides - 2, n - sides - 1, n - 1, n); }
            }
            mesh = new Mesh { name = "ForestBoulder" }; mesh.SetVertices(b.vertices); mesh.SetUVs(0, b.uv); mesh.SetTriangles(b.triangles[0], 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, meshPath);
        }
        string materialPath = Content + "/ForestRock.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(stone[0]); material.SetColor("_BaseColor", new Color(.66f, .74f, .57f)); material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var random = new System.Random(577);
        foreach (var r in routes) for (float s = 12; s < r.Length - 12; s += 13)
        {
            float forest = Climate(r.Progress(s)).y;
            if (r.Bridge(s) || random.NextDouble() > forest * .9f) continue;
            var cluster = Group(r.source.name + " Ledge " + Mathf.RoundToInt(s), root);
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 p = r.At(s) + r.Right(s) * (side * (WallOffset(r, s, side) + 1.3f));
                if (Opening(r, p, 3)) continue;
                p.y = r.At(s).y + 1.1f;
                var rock = Group("Mossy rock", cluster); rock.position = p;
                rock.rotation = Quaternion.Euler(0, random.Next(360), 0); rock.localScale = new Vector3(.9f, 1.8f + (float)random.NextDouble() * 2.3f, 1.6f);
                float ground = terrain.SampleHeight(p) + terrain.transform.position.y;
                if (rock.position.y - rock.localScale.y > ground)
                    rock.position = new Vector3(p.x, ground + rock.localScale.y - .15f, p.z);
                rock.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; rock.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                rock.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
                rock.gameObject.isStatic = true;
                p += r.Right(s) * (side * -.6f);
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y - .1f;
                Place("Assets/Forst/Conifers [BOTD]/Render Pipeline Support/URP/Prefabs/PF Conifer Medium BOTD URP.prefab",
                    p, Quaternion.Euler(0, random.Next(360), 0), cluster, .22f + (float)random.NextDouble() * .09f);
            }
        }
    }

    static void KeepOutsideDrivingLine(GameObject prop, Route route, float station, int side, bool allowRotation = true)
    {
        Vector3 outward = route.Right(station) * side;
        Bounds BoundsOf() { var b = new Bounds(prop.transform.position, Vector3.zero); foreach (var renderer in prop.GetComponentsInChildren<Renderer>()) b.Encapsulate(renderer.bounds); return b; }
        float Width(Bounds b) => Mathf.Abs(outward.x) * b.extents.x + Mathf.Abs(outward.z) * b.extents.z;
        var first = BoundsOf(); var rotation = prop.transform.rotation;
        prop.transform.rotation *= Quaternion.Euler(0, 90, 0); var second = BoundsOf();
        if (!allowRotation || Width(first) < Width(second)) prop.transform.rotation = rotation;
        var bounds = BoundsOf(); float nearest = Vector3.Dot(bounds.center - route.At(station), outward) - Width(bounds);
        prop.transform.position += outward * Mathf.Max(0, route.source.roadWidth * .5f + .8f - nearest);
        PrefabUtility.RecordPrefabInstancePropertyModifications(prop.transform);
    }

}
