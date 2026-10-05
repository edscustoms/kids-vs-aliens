using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Applies a temporary render pose AFTER Cinemachine, restoring it before gameplay runs again.
/// Never feeds offsets into the virtual camera, aim, movement, occlusion queries or saved transforms.
/// </summary>
[DefaultExecutionOrder(-10000), DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class CameraFeedbackController : MonoBehaviour
{
    [SerializeField] private ThirdPersonController player;
    [SerializeField] private GameplaySuspensionController suspension;
    [SerializeField] private PlayerBikeRider mountedPlayer;
    public void ConfigureMountedPlayer(PlayerBikeRider rider) => mountedPlayer = rider;
    private Camera output;
    private bool backgrounded, unfocused, poseApplied;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private struct Impulse
    {
        public Vector3 rotation, position;
        public float start, duration;
    }
    private readonly Impulse[] impulses = new Impulse[16];
    private int count;
    // BikeRiding is a suspension lease for the foot motor, not a world pause.
    // Only explicitly wired scenes may accept render feedback during that lease.
    private bool MountedDriving => mountedPlayer != null && mountedPlayer.isActiveAndEnabled && mountedPlayer.IsDriving;
    private bool CanPlay => isActiveAndEnabled && output != null && output.isActiveAndEnabled
        && CameraFeedbackSettings.Enabled && Time.timeScale > 0f && !backgrounded && !unfocused
        && player != null && (player.isActiveAndEnabled || MountedDriving)
        && (suspension == null || !suspension.IsSuspended || MountedDriving && !suspension.BlocksControls);

    public void Configure(ThirdPersonController localPlayer, GameplaySuspensionController owner)
    {
        UnbindPlayer(); Clear();
        player = localPlayer; suspension = owner;
        if (isActiveAndEnabled) BindPlayer();
    }
    private void Awake() => output = GetComponent<Camera>();
    private void OnEnable()
    {
        output = GetComponent<Camera>();
        CameraFeedbackService.Requested += Request;
        CameraFeedbackSettings.Changed += OnSetting;
        GameplayCameraSettings.Changed += OnCameraMode;
        RenderPipelineManager.beginCameraRendering += BeginRender;
        RenderPipelineManager.endCameraRendering += EndRender;
        RenderPipelineManager.endContextRendering += EndContext;
        BindPlayer();
    }
    private void OnDisable()
    {
        Clear(); UnbindPlayer();
        CameraFeedbackService.Requested -= Request;
        CameraFeedbackSettings.Changed -= OnSetting;
        GameplayCameraSettings.Changed -= OnCameraMode;
        RenderPipelineManager.beginCameraRendering -= BeginRender;
        RenderPipelineManager.endCameraRendering -= EndRender;
        RenderPipelineManager.endContextRendering -= EndContext;
    }
    private void BindPlayer()
    {
        if (player != null) player.Landed += OnLanded;
        if (suspension != null) suspension.SuspensionChanged += OnSuspension;
    }
    private void UnbindPlayer()
    {
        if (player != null) player.Landed -= OnLanded;
        if (suspension != null) suspension.SuspensionChanged -= OnSuspension;
    }
    private void OnLanded(float speed) { if (CanPlay) CameraFeedbackService.Landed(speed); }
    private void OnSetting(bool enabled) { if (!enabled) Clear(); }
    private void OnCameraMode(GameplayCameraMode mode) => Clear();
    private void OnSuspension(bool paused) { if (paused) Clear(); }
    private void OnApplicationPause(bool paused) { backgrounded = paused; Clear(); }
    private void OnApplicationFocus(bool focused) { unfocused = !focused; Clear(); }
    private void Update()
    {
        // Recovery guard if a renderer aborted before its end callback.
        RestorePose();
        if (!CanPlay) Clear();
    }
    private void Request(CameraFeedbackProfile profile, float strength, Vector3 worldDirection)
    {
        if (!CanPlay) return;
        Vector3 rotation = profile.rotation;
        float directionMagnitude = worldDirection.sqrMagnitude;
        if (profile.directionalInfluence > 0 && directionMagnitude > .0001f && !float.IsInfinity(directionMagnitude))
        {
            var direction = transform.InverseTransformDirection(worldDirection.normalized);
            rotation += new Vector3(0, direction.x, -direction.x) * profile.directionalInfluence;
        }
        Evaluate(Time.unscaledTime, out _, out _); // Reclaim expired slots before accepting a new bullet.
        if (count == impulses.Length)
        {
            for (int i = 1; i < count; i++) impulses[i - 1] = impulses[i];
            count--;
        }
        impulses[count++] = new Impulse { rotation = rotation * strength, position = profile.position * strength,
            duration = Mathf.Max(.01f, profile.duration), start = Time.unscaledTime };
    }
    private void Evaluate(float now, out Vector3 position, out Vector3 rotation)
    {
        position = rotation = Vector3.zero;
        int live = 0;
        for (int i = 0; i < count; i++)
        {
            var pulse = impulses[i];
            float age = (now - pulse.start) / pulse.duration;
            if (age >= 1f) continue;
            impulses[live++] = pulse;
            float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age));
            position += pulse.position * weight;
            rotation += pulse.rotation * weight;
        }
        count = live;
        var config = CameraFeedbackService.Config;
        if (config == null) { position = rotation = Vector3.zero; return; }
        position = Vector3.ClampMagnitude(position, config.maximumPosition);
        rotation = Vector3.ClampMagnitude(rotation, config.maximumRotation);
    }
    private void BeginRender(ScriptableRenderContext context, Camera camera)
    {
        if (camera != output) return;
        RestorePose();
        if (!CanPlay) { Clear(); return; }
        Evaluate(Time.unscaledTime, out var position, out var rotation);
        if (count == 0) return;
        savedPosition = transform.localPosition; savedRotation = transform.localRotation;
        poseApplied = true;
        transform.position += transform.rotation * position;
        transform.rotation *= Quaternion.Euler(rotation);
    }
    private void EndRender(ScriptableRenderContext context, Camera camera) { if (camera == output) RestorePose(); }
    private void EndContext(ScriptableRenderContext context, List<Camera> cameras) => RestorePose();
    private void RestorePose()
    {
        if (!poseApplied) return;
        transform.SetLocalPositionAndRotation(savedPosition, savedRotation);
        poseApplied = false;
    }
    private void Clear() { RestorePose(); count = 0; }
}
