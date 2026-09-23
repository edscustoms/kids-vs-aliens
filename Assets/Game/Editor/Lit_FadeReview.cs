using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Disposable Editor Play Mode rendering review. Never saves scene/material/pipeline assets.
[InitializeOnLoad]
public static class Lit_FadeReview
{
    const string Key = "Lit_FadeReview";
    const string Output = "Logs/LitFade";
    static double ready;
    static RenderPipelineAsset originalPipeline;
    static Camera camera;
    static Renderer subject;
    static RenderTexture target;
    static Texture2D pixels;
    static Material material;
    static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

    static Lit_FadeReview()
    {
        EditorApplication.playModeStateChanged += Mode;
        EditorApplication.update += Finish;
    }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + "Failed", false);
        SessionState.SetBool(Key + "Done", false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false) || state != PlayModeStateChange.EnteredPlayMode) return;
        Application.runInBackground = true;
        originalPipeline = QualitySettings.renderPipeline;
        ready = EditorApplication.timeSinceStartup + 3;
        EditorApplication.update += Review;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Debug.Log("LIT_FADE PASS: " + message);
    }

    static void Review()
    {
        if (EditorApplication.timeSinceStartup < ready) return;
        EditorApplication.update -= Review;
        try
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit_Fade");
            Require(shader != null && shader.isSupported, "Lit_Fade shader supported");
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Weapons/M_Weapon_Glass.mat");
            Require(source != null && source.GetFloat("_Surface") == 1,
                "Existing transparent M_Weapon_Glass source is available");
            camera = new GameObject("Lit Fade Review Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -4);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.045f, .075f, .12f, 0);
            camera.orthographic = true; camera.orthographicSize = 1.2f;
            camera.allowHDR = true; camera.allowMSAA = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None;
            var light = new GameObject("Review Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(30, -30, 0);
            light.shadows = LightShadows.None;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = Vector3.one * 1.8f;
            subject = sphere.GetComponent<Renderer>();
            target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create(); pixels = new Texture2D(512, 512, TextureFormat.RGBAFloat, false, true);
            var gui = new KidsVsAliens.Editor.Lit_FadeShaderGUI();
            foreach (string profile in new[] { "Mobile", "PC" })
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                    "Assets/Game/Settings/Rendering/" + profile + "_RPAsset.asset");
                QualitySettings.renderPipeline = pipeline;
                int clean = Array.FindIndex(pipeline.rendererDataList.ToArray(), r => r != null && r.rendererFeatures.Count == 0);
                Require(clean >= 0, profile + " existing feature-free review renderer");
                data.SetRenderer(clean);
                // Original glass settings first, then all stock blend modes, opaque and cutout.
                for (int mode = -1; mode < 6; mode++)
                {
                    // Compare to stock Lit even after the production source adopts Lit_Fade.
                    material = new Material(source) { shader = Shader.Find("Universal Render Pipeline/Lit") };
                    if (mode >= 0)
                    {
                        material.SetFloat("_Surface", mode < 4 ? 1 : 0);
                        material.SetFloat("_Blend", mode < 4 ? mode : 0);
                        material.SetFloat("_BlendModePreserveSpecular", mode == 0 ? 0 : 1);
                        material.SetFloat("_AlphaClip", mode == 5 ? 1 : 0);
                        material.SetFloat("_Cutoff", .2f);
                        // Exercise additional normal Lit terms, including emission fading.
                        material.SetFloat("_Metallic", .55f);
                        material.SetColor("_EmissionColor", new Color(.2f, .06f, .03f));
                    }
                    gui.ValidateMaterial(material);
                    subject.sharedMaterial = material;
                    subject.enabled = false; var background = Capture(); subject.enabled = true;
                    SetFade(1); var stock = Capture();
                    if (mode == -1)
                    {
                        SetFade(0); var ignored = Capture();
                        Require(Error(stock, ignored) < .00001f, profile + " stock Lit baseline ignores MPB _Fade=0");
                    }
                    material.shader = shader; gui.ValidateMaterial(material);
                    SetFade(1); var full = Capture();
                    Require(Error(stock, full) < .0001f, profile + " mode=" + mode + " fade=1 matches stock Lit");
                    SetFade(.5f); var half = Capture();
                    SetFade(0); var zero = Capture();
                    Require(Error(zero, background) < .0001f, profile + " mode=" + mode + " fade=0 invisible");
                    Require(Error(full, background) > .001f, profile + " mode=" + mode + " visible reference");
                    if (mode < 4)
                    {
                        var expected = full.Select((c, i) => Color.Lerp(background[i], c, .5f)).ToArray();
                        // RGB comparison: RT alpha depends on the chosen stock blend state.
                        Require(Error(half, expected) < .0003f, profile + " mode=" + mode + " half fade linearly blends to background");
                    }
                    else Require(Error(half, background) > .001f && Error(half, full) > .001f,
                        profile + " mode=" + mode + " opaque coverage fades");
                    if (mode == -1) SaveStrip(profile, stock, full, half, zero);
                    // Compile the retained auxiliary passes as well as the rendered forward pass.
                    SetFade(.5f);
                    for (int pass = 0; pass < material.passCount; pass++) material.SetPass(pass);
                    Object.DestroyImmediate(material); material = null;
                }
            }
            var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            Require(errors.Length == 0, "All exercised shader passes compile: " + string.Join("; ", errors.Select(e => e.message)));
            File.WriteAllText(Output + "/result.txt", "PASS: Mobile + PC. Stock Lit baseline ignores _Fade; Lit_Fade matches stock at 1, fades through MPB at 0.5, disappears at 0. Uses existing transparent glass settings. All four transparency blend modes plus opaque and cutout checked. Auxiliary passes compiled. No production assets saved.");
        }
        catch (Exception e) { SessionState.SetBool(Key + "Failed", true); Debug.LogException(e); }
        finally
        {
            QualitySettings.renderPipeline = originalPipeline;
            if (material != null) Object.DestroyImmediate(material);
            if (pixels != null) Object.DestroyImmediate(pixels);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            SessionState.SetBool(Key + "Done", true);
            EditorApplication.ExitPlaymode();
        }
    }

    static void SetFade(float fade) { Block.Clear(); Block.SetFloat("_Fade", fade); subject.SetPropertyBlock(Block); }
    static Color[] Capture()
    {
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active;
        RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply();
        RenderTexture.active = previous;
        return pixels.GetPixels();
    }
    static float Error(Color[] a, Color[] b)
    {
        float total = 0;
        for (int i = 0; i < a.Length; i++)
            total += Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b);
        return total / (a.Length * 3);
    }
    static void SaveStrip(string name, params Color[][] images)
    {
        var strip = new Texture2D(512 * images.Length, 512, TextureFormat.RGB24, false);
        for (int i = 0; i < images.Length; i++) strip.SetPixels(i * 512, 0, 512, 512, images[i].Select(c => c.gamma).ToArray());
        strip.Apply(); File.WriteAllBytes(Output + "/" + name + "-stock-full-half-zero.png", strip.EncodeToPNG());
        Object.DestroyImmediate(strip);
    }
    static void Finish()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !SessionState.GetBool(Key + "Done", false)) return;
        SessionState.SetBool(Key, false); SessionState.SetBool(Key + "Done", false);
        EditorApplication.Exit(SessionState.GetBool(Key + "Failed", false) ? 1 : 0);
    }
}

