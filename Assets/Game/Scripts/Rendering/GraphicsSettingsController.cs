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
    private int pipelineQualityLevel;
    private bool initialized;
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
    private static void Reset()
    {
        if (Instance != null) Instance.RestoreOriginalPipeline();
        Instance = null;
    }
    private void OnEnable()
    {
        // OnEnable also runs after a script reload while playing.
        if (Application.isPlaying) Initialize(Resources.Load<GraphicsQualityConfiguration>("GraphicsQuality"));
    }
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
        if (initialized || configuration == null) return;
        initialized = true;
        Instance = this; Configuration = configuration;
        DontDestroyOnLoad(gameObject);
        pipelineQualityLevel = QualitySettings.GetQualityLevel();
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
        if (GetComponent<DeveloperFpsHud>() == null) gameObject.AddComponent<DeveloperFpsHud>();
#endif
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += RestoreOriginalPipeline;
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
    }
    public void Apply()
    {
        if (!initialized || Configuration == null) return;
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
    private void OnDisable() => RestoreOriginalPipeline();
    private void OnDestroy() => RestoreOriginalPipeline();
#if UNITY_EDITOR
    private void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) RestoreOriginalPipeline();
    }
#endif
    /// <summary>Release owned rendering state before play exit, reload or build preprocessing.</summary>
    public void RestoreOriginalPipeline()
    {
        // Instance is reset on domain reload; cleanup must depend on this owner's state.
        if (!initialized) return;
        initialized = false;
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= RestoreOriginalPipeline;
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
        GraphicsPreferences.Changed -= Apply;
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        SceneManager.sceneUnloaded -= RemoveDestroyedCameras;
        foreach (var item in renderingCameras) if (item.Value.rendering) RestoreCamera(item.Key, item.Value);
        renderingCameras.Clear();
        int activeQuality = QualitySettings.GetQualityLevel();
        try
        {
            // The Editor can change the active quality before teardown. Restore the
            // slot we actually changed, without overwriting another owner's pipeline.
            if (pipelineQualityLevel != activeQuality) QualitySettings.SetQualityLevel(pipelineQualityLevel, false);
            if (QualitySettings.renderPipeline == runtimePipeline) QualitySettings.renderPipeline = previousQualityPipeline;
            QualitySettings.lodBias = originalLodBias;
        }
        finally
        {
            if (pipelineQualityLevel != activeQuality) QualitySettings.SetQualityLevel(activeQuality, false);
        }
#if UNITY_EDITOR
        // SRP build-data collection can run while this clone is assigned. Restoring
        // QualitySettings alone does not invalidate its cached asset references.
        UnityEditor.Rendering.CoreBuildData.instance.Dispose();
#endif
        if (runtimePipeline != null)
        {
            if (Application.isPlaying) Destroy(runtimePipeline);
            else DestroyImmediate(runtimePipeline);
        }
        runtimePipeline = null;
        sourcePipeline = null;
        previousQualityPipeline = null;
        if (Instance == this) Instance = null;
    }
}
