using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Packs existing Poly Haven maps and the baked asphalt footprint for ground shading only.</summary>
public static class BikeRouteGroundMaterialBake
{
    const string Folder = "Assets/Game/Scenes/BikeRoute/Environment/Surfaces/";
    const string MaterialPath = "Assets/Game/Scenes/BikeRoute/Environment/Foundation/Terrain Rock Projection.mat";
    const int DetailSize = 512, FieldSize = 2048;
    const float FieldRange = 32;

    [MenuItem("Tools/Level Authoring/Refresh BikeRoute Ground Materials")]
    public static void Refresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "BikeRoute")
            throw new InvalidOperationException("Open BikeRoute outside Play Mode first.");
        var terrain = Object.FindAnyObjectByType<Terrain>();
        var roads = GameObject.Find("LevelGeometry/BakedAsphalt");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (terrain == null || roads == null || material == null || terrain.materialTemplate != material)
            throw new InvalidOperationException("Missing the authored BikeRoute Terrain, material or baked asphalt.");
        foreach (Transform road in roads.transform)
            if (road.GetComponent<MeshFilter>()?.sharedMesh == null)
                throw new InvalidOperationException("Missing baked road mesh: " + road.name);

        // Two arrays retain matching albedo/normal/AO/roughness coordinates without
        // multiplying texture bindings per family. No source importer is modified.
        var colors = new Texture2DArray(DetailSize, DetailSize, 4, TextureFormat.RGBA32, true, false);
        var relief = new Texture2DArray(DetailSize, DetailSize, 4, TextureFormat.RGBA32, true, true);
        string[] families = { "gravel_ground_01", "dirt", "gravel_stones", "forrest_ground_01" };
        for (int layer = 0; layer < families.Length; layer++)
        {
            string family = families[layer], size = layer == 3 ? "2k" : "1k";
            string prefix = "Assets/Game/Art/Environment/Textures/Ground/" + family + "/" + family;
            var albedo = Read(prefix + "_diff_" + size + ".jpg");
            var normal = Read(prefix + "_nor_gl_" + size + ".png");
            var mask = Read(prefix + "_urp_mask_" + size + ".png");
            var c = new Color[DetailSize * DetailSize];
            var r = new Color[c.Length];
            for (int y = 0; y < DetailSize; y++) for (int x = 0; x < DetailSize; x++)
            {
                float u = (x + .5f) / DetailSize, v = (y + .5f) / DetailSize;
                var a = albedo.GetPixelBilinear(u, v);
                var n = normal.GetPixelBilinear(u, v);
                var m = mask.GetPixelBilinear(u, v);
                c[y * DetailSize + x] = new Color(a.r, a.g, a.b, m.g);
                r[y * DetailSize + x] = new Color(n.r, n.g, m.a, 1);
            }
            colors.SetPixels(c, layer); relief.SetPixels(r, layer);
            Object.DestroyImmediate(albedo); Object.DestroyImmediate(normal); Object.DestroyImmediate(mask);
        }
        colors.wrapMode = relief.wrapMode = TextureWrapMode.Repeat;
        colors.filterMode = relief.filterMode = FilterMode.Trilinear;
        colors.anisoLevel = relief.anisoLevel = 4;
        colors.Apply(true, true); relief.Apply(true, true);
        Save(colors, "Ground Color AO.asset"); Save(relief, "Ground Normal Smoothness.asset");

        var origin = terrain.transform.position; var sizeWS = terrain.terrainData.size;
        var distance = new float[FieldSize * FieldSize]; Array.Fill(distance, FieldRange);
        foreach (Transform road in roads.transform)
        {
            var vertices = road.GetComponent<MeshFilter>().sharedMesh.vertices;
            for (int i = 0; i + 3 < vertices.Length; i += 2)
            {
                var left = road.TransformPoint(vertices[i]); var right = road.TransformPoint(vertices[i + 1]);
                var nextLeft = road.TransformPoint(vertices[i + 2]); var nextRight = road.TransformPoint(vertices[i + 3]);
                var a = (left + right) * .5f; var b = (nextLeft + nextRight) * .5f;
                float wa = Vector3.Distance(left, right) * .5f, wb = Vector3.Distance(nextLeft, nextRight) * .5f;
                float radius = FieldRange + Mathf.Max(wa, wb);
                int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,b.x)-radius-origin.x)/sizeWS.x*FieldSize),0,FieldSize-1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x,b.x)+radius-origin.x)/sizeWS.x*FieldSize),0,FieldSize-1);
                int z0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z,b.z)-radius-origin.z)/sizeWS.z*FieldSize),0,FieldSize-1);
                int z1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z,b.z)+radius-origin.z)/sizeWS.z*FieldSize),0,FieldSize-1);
                var start = new Vector2(a.x,a.z); var delta = new Vector2(b.x-a.x,b.z-a.z);
                for(int z=z0;z<=z1;z++) for(int x=x0;x<=x1;x++)
                {
                    var p = new Vector2(origin.x+(x+.5f)/FieldSize*sizeWS.x,origin.z+(z+.5f)/FieldSize*sizeWS.z);
                    float t = Mathf.Clamp01(Vector2.Dot(p-start,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                    float d = Mathf.Max(0,Vector2.Distance(p,start+delta*t)-Mathf.Lerp(wa,wb,t));
                    if(d >= distance[z*FieldSize+x]) continue;
                    float roadY = Mathf.Lerp(a.y,b.y,t);
                    float groundY = terrain.SampleHeight(new Vector3(p.x,0,p.y))+origin.y;
                    // A bridge overhead must not paint a shoulder onto the underpass.
                    if(Mathf.Abs(roadY-groundY)>3.5f) continue;
                    distance[z*FieldSize+x]=d;
                }
            }
        }
        var field = new Texture2D(FieldSize,FieldSize,TextureFormat.R8,false,true);
        var bytes = new byte[distance.Length];
        for(int i=0;i<bytes.Length;i++) bytes[i]=(byte)Mathf.RoundToInt(distance[i]/FieldRange*255);
        field.LoadRawTextureData(bytes); field.wrapMode=TextureWrapMode.Clamp;
        field.filterMode=FilterMode.Bilinear; field.Apply(false,true);
        Save(field,"Ground Road Distance.asset");
        material.SetTexture("_GroundColors",AssetDatabase.LoadAssetAtPath<Texture>(Folder+"Ground Color AO.asset"));
        material.SetTexture("_GroundRelief",AssetDatabase.LoadAssetAtPath<Texture>(Folder+"Ground Normal Smoothness.asset"));
        material.SetTexture("_GroundRoadDistance",AssetDatabase.LoadAssetAtPath<Texture>(Folder+"Ground Road Distance.asset"));
        material.SetVector("_GroundField",new Vector4(origin.x,origin.z,sizeWS.x,sizeWS.z));
        material.SetFloat("_GroundStrength",1);
        material.SetFloat("_GroundNormalStrength",1.8f);
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
    }

    static Texture2D Read(string path)
    {
        var texture = new Texture2D(2,2,TextureFormat.RGBA32,false,true);
        if(!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot read " + path);
        return texture;
    }
    static void Save(Object generated,string name)
    {
        string path=Folder+name; generated.name=Path.GetFileNameWithoutExtension(name);
        var existing=AssetDatabase.LoadAssetAtPath<Object>(path);
        if(existing==null) AssetDatabase.CreateAsset(generated,path);
        else { EditorUtility.CopySerialized(generated,existing);Object.DestroyImmediate(generated);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing); }
    }
}
