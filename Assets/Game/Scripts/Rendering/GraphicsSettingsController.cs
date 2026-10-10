using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>One device-wide owner. Owns a private URP asset; scene content registers separately.</summary>
public sealed class GraphicsSettingsController : MonoBehaviour
{
    public static GraphicsSettingsController Instance { get; private set; }
    public GraphicsQualityConfiguration Configuration { get; private set; }
    public UniversalRenderPipelineAsset RuntimePipeline => runtimePipeline;
    private UniversalRenderPipelineAsset sourcePipeline, runtimePipeline;
    private RenderPipelineAsset previousQualityPipeline;
    private float originalLodBias;
    private readonly Dictionary<Camera, CameraState> renderingCameras = new();
    private sealed class CameraState
    {
        public float far;
        public bool post;
        public float[] distances;
        public UniversalAdditionalCameraData data;
        public bool rendering;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Instance = null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Ensure()
    {
        if (Instance != null) return;
        var config = Resources.Load<GraphicsQualityConfiguration>("GraphicsQuality");
        if (config == null) { Debug.LogError("Missing Resources/GraphicsQuality configuration."); return; }
        var owner = new GameObject("Graphics Settings");
        owner.AddComponent<GraphicsSettingsController>().Initialize(config);
    }
    public void Initialize(GraphicsQualityConfiguration configuration)
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        if (runtimePipeline != null) return;
        Instance = this; Configuration = configuration;
        DontDestroyOnLoad(gameObject);
        previousQualityPipeline = QualitySettings.renderPipeline;
        sourcePipeline = (previousQualityPipeline != null ? previousQualityPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
        originalLodBias = QualitySettings.lodBias;
        if (sourcePipeline != null)
        {
            runtimePipeline = Instantiate(sourcePipeline);
            runtimePipeline.name = sourcePipeline.name + " (Graphics Settings)";
            runtimePipeline.hideFlags = HideFlags.DontSave;
            QualitySettings.renderPipeline = runtimePipeline;
        }
        GraphicsPreferences.Changed += Apply;
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
        SceneManager.sceneUnloaded += RemoveDestroyedCameras;
        Apply();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        gameObject.AddComponent<DeveloperFpsHud>();
#endif
    }
    public void Apply()
    {
        if (Configuration == null) return;
        var preset = Configuration.Get(GraphicsPreferences.Quality);
        if (runtimePipeline != null)
        {
            runtimePipeline.renderScale = sourcePipeline.renderScale * preset.renderScaleMultiplier;
            // URP gates BOTH main/additional shadow caster passes on maxShadowDistance > 0.
            // Leave mixed lighting, baked masks, lights and shader support untouched.
            runtimePipeline.shadowDistance = GraphicsPreferences.Shadows ? sourcePipeline.shadowDistance * preset.shadowDistanceMultiplier : 0;
        }
        QualitySettings.lodBias = originalLodBias * preset.lodBiasMultiplier;
    }
    private void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        if (Configuration == null || camera.cameraType != CameraType.Game || camera.targetTexture != null) return;
        var preset = Configuration.Get(GraphicsPreferences.Quality);
        if (renderingCameras.TryGetValue(camera, out var state) && state.rendering) return;
        if (preset.farClipLimit <= 0 && preset.distanceLimitedLayers.value == 0 && preset.postProcessing != GraphicsPostQuality.Disabled) return;
        if (state == null) { state = new CameraState(); renderingCameras.Add(camera, state); }
        state.far = camera.farClipPlane;
        state.rendering = true;
        state.distances = null;
        if (preset.farClipLimit > 0) camera.farClipPlane = Mathf.Max(camera.nearClipPlane + .01f, Mathf.Min(state.far, preset.farClipLimit));
        if (preset.distanceLimitedLayers.value != 0)
        {
            state.distances = camera.layerCullDistances;
            var distances = (float[])state.distances.Clone();
            for (int i = 0; i < 32; i++)
                if ((preset.distanceLimitedLayers.value & (1 << i)) != 0)
                    distances[i] = Mathf.Min(distances[i] > 0 ? distances[i] : camera.farClipPlane, preset.objectDrawDistance);
            camera.layerCullDistances = distances;
        }
        if (camera.TryGetComponent(out state.data))
        {
            state.post = state.data.renderPostProcessing;
            if (preset.postProcessing == GraphicsPostQuality.Disabled) state.data.renderPostProcessing = false;
        }
    }
    private void EndCamera(ScriptableRenderContext context, Camera camera)
    {
        if (!renderingCameras.TryGetValue(camera, out var state)) return;
        if (state.rendering) RestoreCamera(camera, state);
        state.rendering = false;
    }
    private void RemoveDestroyedCameras(Scene scene)
    {
        var removed = new List<Camera>();
        foreach (var pair in renderingCameras) if (pair.Key == null) removed.Add(pair.Key);
        foreach (var camera in removed) renderingCameras.Remove(camera);
    }
    private static void RestoreCamera(Camera camera, CameraState state)
    {
        if (camera == null) return;
        camera.farClipPlane = state.far;
        if (state.distances != null) camera.layerCullDistances = state.distances;
        if (state.data != null) state.data.renderPostProcessing = state.post;
    }
    private void OnDestroy()
    {
        if (Instance != this) return;
        GraphicsPreferences.Changed -= Apply;
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        SceneManager.sceneUnloaded -= RemoveDestroyedCameras;
        foreach (var item in renderingCameras) if (item.Value.rendering) RestoreCamera(item.Key, item.Value);
        if (QualitySettings.renderPipeline == runtimePipeline) QualitySettings.renderPipeline = previousQualityPipeline;
        QualitySettings.lodBias = originalLodBias;
        if (runtimePipeline != null) Destroy(runtimePipeline);
        Instance = null;
    }
}
