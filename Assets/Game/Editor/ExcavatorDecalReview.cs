using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Actual URP renders in an isolated preview scene. Never saves a gameplay scene.</summary>
public static class ExcavatorDecalReview
{
    public const string OutputFolder = "Logs/ExcavatorDecals";

    [MenuItem("Tools/Kids VS Aliens/Environment/Excavator Decals/Capture Camera Angle Review")]
    public static void Capture()
    {
        Directory.CreateDirectory(OutputFolder);
        var preview = new PreviewRenderUtility();
        try
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ExcavatorDecalSetup.PrefabPath));
            preview.AddSingleGO(root);
            var renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var camera = preview.camera;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.22f,.25f,.29f);
            camera.fieldOfView = 35;
            preview.ambientColor = new Color(.5f,.5f,.5f);
            preview.lights[0].enabled = preview.lights[1].enabled = true;
            preview.lights[0].intensity = 1.3f;
            preview.lights[1].intensity = .8f;
            preview.lights[0].transform.rotation = Quaternion.Euler(45,35,0);
            preview.lights[1].transform.rotation = Quaternion.Euler(30,215,0);
            foreach (int elevation in new[] { 12, 35, 60 })
            {
                for (int yaw = 0; yaw < 360; yaw += 45)
                {
                    float y = yaw * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
                    Vector3 direction = new Vector3(Mathf.Cos(y)*Mathf.Cos(e), Mathf.Sin(e), Mathf.Sin(y)*Mathf.Cos(e));
                    camera.transform.position = bounds.center + direction * 25;
                    camera.transform.LookAt(bounds.center);
                    Save(preview, $"orbit-e{elevation}-y{yaw}", 1200, 900);
                }
            }
            Detail(preview, "left-body", new Vector3(-33.85f,2.8f,-13.86f), Vector3.forward, 1.9f);
            Detail(preview, "right-body", new Vector3(-33.6f,2.8f,-17.53f), Vector3.back, 2.2f);
            Detail(preview, "left-boom", new Vector3(-28.1f,6f,-15.4252f), Vector3.forward, 1.7f);
            Detail(preview, "right-boom", new Vector3(-28.1f,6f,-16.0611f), Vector3.back, 1.7f);
            Detail(preview, "rear", new Vector3(-36.14f,2.98f,-15.7f), Vector3.left, .75f);
            Detail(preview, "top-service", new Vector3(-33.5f,3.55f,-16f), Vector3.up, 1.1f);
        }
        finally { preview.Cleanup(); }
        Debug.Log("Excavator: 24 perspective orbit views and 6 orthographic details captured to " + Path.GetFullPath(OutputFolder));
    }

    static void Detail(PreviewRenderUtility preview, string name, Vector3 target, Vector3 direction, float size)
    {
        preview.camera.orthographic = true;
        preview.camera.orthographicSize = size;
        preview.camera.transform.position = target + direction * 12;
        preview.camera.transform.LookAt(target, direction == Vector3.up ? Vector3.right : Vector3.up);
        Save(preview, name, 1400, 1000);
    }

    public static Texture2D ReadPreview(PreviewRenderUtility preview, int width, int height)
    {
        preview.BeginPreview(new Rect(0,0,width,height), GUIStyle.none);
        preview.Render(true);
        var rendered = (RenderTexture)preview.EndPreview();
        var previous = RenderTexture.active;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = rendered;
            texture.ReadPixels(new Rect(0,0,width,height),0,0,false);
            if (!rendered.sRGB && QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                var pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++) pixels[i] = pixels[i].gamma;
                texture.SetPixels(pixels);
            }
            texture.Apply();
            return texture;
        }
        catch { Object.DestroyImmediate(texture); throw; }
        finally { RenderTexture.active = previous; }
    }

    static void Save(PreviewRenderUtility preview, string name, int width, int height)
    {
        Texture2D texture = ReadPreview(preview, width, height);
        try { File.WriteAllBytes(Path.Combine(OutputFolder, name + ".png"), texture.EncodeToPNG()); }
        finally { Object.DestroyImmediate(texture); }
    }
}
