using UnityEditor;
using UnityEngine;

public static partial class BikeRouteEnvironmentSetup
{
    // Three rounded bank toes exposed by removing the duplicate cliff skins acted
    // as side ramps. Reshape only their lower seven metres; keep floor and crest.
    static void SharpenLocalTerrainToes()
    {
        var data = terrain.terrainData;
        int resolution = data.heightmapResolution;
        var heights = data.GetHeights(0, 0, resolution, resolution);
        foreach (var patch in new[] { (0, 400f, 480f), (1, 200f, 280f), (2, 180f, 260f) })
        {
            var route = routes[patch.Item1];
            var bounds = new Bounds(route.At(patch.Item2), Vector3.zero);
            for (float s = patch.Item2; s <= patch.Item3; s += 2)
                bounds.Encapsulate(route.At(s) + route.Right(s) * (route.Corridor(s) + 5));
            bounds.Expand(new Vector3(12, 0, 12));
            int x0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.x-terrain.transform.position.x)/data.size.x*(resolution-1)),0,resolution-1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.x-terrain.transform.position.x)/data.size.x*(resolution-1)),0,resolution-1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.z-terrain.transform.position.z)/data.size.z*(resolution-1)),0,resolution-1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.z-terrain.transform.position.z)/data.size.z*(resolution-1)),0,resolution-1);
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
            {
                var p = terrain.transform.position + new Vector3(x*data.size.x/(resolution-1), 0, z*data.size.z/(resolution-1));
                float best = float.PositiveInfinity, station = 0; Vector3 center = default;
                for (int i = 1; i < route.points.Length; i++)
                {
                    if (route.distance[i] < patch.Item2 || route.distance[i-1] > patch.Item3) continue;
                    var delta = route.points[i]-route.points[i-1]; var flat = Vector3.ProjectOnPlane(delta,Vector3.up);
                    float t = Mathf.Clamp01(Vector3.Dot(p-route.points[i-1],flat)/flat.sqrMagnitude);
                    var q = route.points[i-1] + delta*t;
                    float distance = Vector3.ProjectOnPlane(p-q,Vector3.up).sqrMagnitude;
                    if (distance >= best) continue;
                    best=distance; center=q; station=Mathf.Lerp(route.distance[i-1],route.distance[i],t);
                }
                float side = Vector3.Dot(p-center,route.Right(station));
                if (side < route.Corridor(station)+3 || side > route.Corridor(station)+9) continue;
                float h = heights[z,x]*data.size.y + terrain.transform.position.y - center.y;
                if (h <= .05f || h >= 7) continue;
                float blend = Blend(patch.Item2,patch.Item2+10,station)*(1-Blend(patch.Item3-10,patch.Item3,station));
                float sharpened = 7*Mathf.Pow(h/7,.12f);
                heights[z,x] += (sharpened-h)*blend/data.size.y;
            }
        }
        data.SetHeights(0,0,heights); terrain.Flush();
        EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
    }
}
