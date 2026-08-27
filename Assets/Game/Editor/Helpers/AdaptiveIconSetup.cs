#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

// make sure the 2 paths do contain the new images.

public static class KidsVsAliensAdaptiveIconSetup
{
    private const string BackgroundPath = "Assets/Game/UI/AppIcon/IconBackground.png";

    private const string ForegroundPath = "Assets/Game/UI/AppIcon/IconForeground.png";

    private const string GeneratedFolder = "Assets/Game/UI/AppIcon/Generated";

    [MenuItem("Tools/Kids VS Aliens/Helpers/Setup Android Adaptive Icons")]
    public static void Setup()
    {
        Texture2D background = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);

        Texture2D foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath);

        if (background == null)
        {
            Debug.LogError($"Missing adaptive icon background:\n{BackgroundPath}");
            return;
        }

        if (foreground == null)
        {
            Debug.LogError($"Missing adaptive icon foreground:\n{ForegroundPath}");
            return;
        }

        EnsureFolder(GeneratedFolder);

        NamedBuildTarget platform = NamedBuildTarget.Android;
        PlatformIconKind kind = AndroidPlatformIconKind.Adaptive;

        PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(platform, kind);

        if (icons == null || icons.Length == 0)
        {
            Debug.LogError(
                "Unity returned no Android Adaptive icon slots. "
                    + "Make sure Android Build Support is installed."
            );
            return;
        }

        // Generate exactly the sizes Unity currently asks for.
        for (int i = 0; i < icons.Length; i++)
        {
            PlatformIcon icon = icons[i];

            int width = icon.width;
            int height = icon.height;

            string backgroundOutput = $"{GeneratedFolder}/IconBackground_{width}x{height}.png";

            string foregroundOutput = $"{GeneratedFolder}/IconForeground_{width}x{height}.png";

            ResizeAndWrite(background, width, height, backgroundOutput);

            ResizeAndWrite(foreground, width, height, foregroundOutput);
        }

        AssetDatabase.Refresh();

        // Import generated PNGs correctly and assign them.
        for (int i = 0; i < icons.Length; i++)
        {
            PlatformIcon icon = icons[i];

            int width = icon.width;
            int height = icon.height;

            string backgroundOutput = $"{GeneratedFolder}/IconBackground_{width}x{height}.png";

            string foregroundOutput = $"{GeneratedFolder}/IconForeground_{width}x{height}.png";

            ConfigureImporter(backgroundOutput, hasAlpha: false);

            ConfigureImporter(foregroundOutput, hasAlpha: true);

            Texture2D generatedBackground = AssetDatabase.LoadAssetAtPath<Texture2D>(
                backgroundOutput
            );

            Texture2D generatedForeground = AssetDatabase.LoadAssetAtPath<Texture2D>(
                foregroundOutput
            );

            if (generatedBackground == null || generatedForeground == null)
            {
                Debug.LogError($"Failed loading generated adaptive icon " + $"{width}x{height}.");
                return;
            }

            // Unity Android Adaptive icon UI shows:
            // Layer 0 = Background
            // Layer 1 = Foreground
            icon.SetTexture(generatedBackground, 0);
            icon.SetTexture(generatedForeground, 1);
        }

        PlayerSettings.SetPlatformIcons(platform, kind, icons);

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Android Adaptive icons configured successfully "
                + $"for {icons.Length} density slots."
        );
    }

    private static void ResizeAndWrite(Texture2D source, int width, int height, string assetPath)
    {
        RenderTexture previous = RenderTexture.active;

        RenderTexture rt = RenderTexture.GetTemporary(
            width,
            height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Default
        );

        rt.filterMode = FilterMode.Bilinear;

        Graphics.Blit(source, rt);

        RenderTexture.active = rt;

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);

        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);

        result.Apply();

        byte[] png = result.EncodeToPNG();

        string absolutePath = ToAbsoluteProjectPath(assetPath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));

        File.WriteAllBytes(absolutePath, png);

        Object.DestroyImmediate(result);

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
    }

    private static void ConfigureImporter(string assetPath, bool hasAlpha)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Default;

        importer.textureShape = TextureImporterShape.Texture2D;

        importer.sRGBTexture = true;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = hasAlpha;

        importer.textureCompression = TextureImporterCompression.Uncompressed;

        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;

        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Split('/');

        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string ToAbsoluteProjectPath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;

        return Path.Combine(projectRoot, assetPath);
    }
}
#endif
