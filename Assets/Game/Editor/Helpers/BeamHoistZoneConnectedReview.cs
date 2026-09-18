using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Opt-in batch-only fixtures; never replaces or saves an interactive author's open scene.
public static class BeamHoistZoneConnectedReview
{
    public static void Capture()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run these fixtures in a separate batch editor.");
        Directory.CreateDirectory("Logs/HoistConnected");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Connected pad review camera").AddComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.012f, .014f, .023f); camera.nearClipPlane = .1f; camera.farClipPlane = 30;
        camera.transform.rotation = Quaternion.Euler(90, 0, 0);
        var report = new StringBuilder();
        foreach (var fixture in new[] { ("rectangle", "XXXX/XXXX"), ("three-joined", "XXXX../XXXXXX/XXXXXX"),
            ("l-shape", "XX../XX../XXXX/XXXX"), ("hole", "XXXX/X..X/X..X/XXXX"), ("disconnected", "XX..XX/XX..XX") })
        {
            var effect = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BeamHoistZoneVFX>(BeamHoistZoneSetup.PrefabPath));
            try
            {
                var cells = BeamHoistZoneTopologyTests.Shape(fixture.Item2); effect.SetFootprint(cells);
                var mesh = effect.GetComponentInChildren<MeshFilter>().sharedMesh;
                camera.transform.position = mesh.bounds.center + Vector3.up * 10;
                camera.orthographicSize = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z) * .6f;
                effect.Present(BeamHoistZoneState.Available, 1, 0, 0);
                var pixels = Render(camera, fixture.Item1);
                report.AppendLine($"{fixture.Item1}: {cells.Count} cells, {effect.PatchCount} connected pads, {mesh.vertexCount} vertices");
                if (fixture.Item1 == "rectangle" && File.Exists("Logs/HoistConnected/approved-shader.txt"))
                {
                    var oldShader = ShaderUtil.CreateShaderAsset(File.ReadAllText("Logs/HoistConnected/approved-shader.txt")
                        .Replace("KVA/Beam Hoist Zone", "KVA/Review Original Hoist Zone").Replace("_Time.y", "0.0"));
                    var currentShader = ShaderUtil.CreateShaderAsset(File.ReadAllText("Assets/Game/Shaders/VFX/BeamHoistZone.shader")
                        .Replace("KVA/Beam Hoist Zone", "KVA/Review Current Hoist Zone").Replace("_Time.y", "0.0"));
                    var renderer = effect.GetComponentInChildren<MeshRenderer>();
                    var material = new Material(renderer.sharedMaterial) { shader = oldShader };
                    var original = renderer.sharedMaterial;
                    try
                    {
                        renderer.sharedMaterial = material;
                        var baseline = Render(camera, "rectangle-approved");
                        material.shader = currentShader;
                        pixels = Render(camera, "rectangle");
                        int differing = pixels.Where((p, i) => Math.Abs(p.r - baseline[i].r) > 1 || Math.Abs(p.g - baseline[i].g) > 1
                            || Math.Abs(p.b - baseline[i].b) > 1).Count();
                        report.AppendLine($"Rectangle pixel comparison against approved shader: {differing} pixels differ by >1/255.");
                        if (differing != 0) { File.WriteAllText("Logs/HoistConnected/render-review.txt", report.ToString()); throw new Exception("Rectangular appearance changed: inspect captures."); }
                    }
                    finally { renderer.sharedMaterial = original; Object.DestroyImmediate(material); Object.DestroyImmediate(oldShader); Object.DestroyImmediate(currentShader); }
                }
                effect.Present(BeamHoistZoneState.Active, 1, 0, 0); Render(camera, fixture.Item1 + "-active");
            }
            finally { Object.DestroyImmediate(effect.gameObject); }
        }
        Object.DestroyImmediate(camera.gameObject);
        File.WriteAllText("Logs/HoistConnected/render-review.txt", report.ToString());
    }
    private static Color32[] Render(Camera camera, string name)
    {
        var target = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(768, 768, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); pixels.Apply();
            File.WriteAllBytes("Logs/HoistConnected/" + name + ".png", pixels.EncodeToPNG());
            return pixels.GetPixels32();
        }
        finally { RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); }
    }
}
