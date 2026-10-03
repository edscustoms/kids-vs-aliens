using Cinemachine;
using UnityEngine;

/// <summary>Applies presets to the existing camera; retains the scene's authored Action baseline.</summary>
[DefaultExecutionOrder(-10), DisallowMultipleComponent, RequireComponent(typeof(CinemachineVirtualCamera))]
public sealed class GameplayCameraController : CinemachineExtension
{
    [SerializeField]
    private GameplayCameraProfile profile;
    private CinemachineVirtualCamera cameraRig;
    private CinemachineTransposer body;
    private CinemachineBasicMultiChannelPerlin noise;
    private CinemachineComponentBase aim;
    private Camera outputCamera;
    private bool actionOrthographic,
        actionPhysical,
        changedProjection;
    private LensSettings actionLens;
    private Quaternion actionRotation;
    private Vector3 actionOffset,
        actionDamping;
    private CinemachineTransposer.BindingMode actionBinding;
    private bool actionNoise,
        actionAim,
        captured,
        started;

    // The authored camera Follow relationship identifies its player. Rider owns phases;
    // this owner only remembers presentation progress, never gameplay or saved mode.
    private PlayerBikeRider bikeRider;
    private StarterAssets.ThirdPersonController playerLook;
    private BikeRidePhase observedPhase;
    private float bikeWeight, blendFrom, blendTo, blendElapsed, blendDuration;
    private Vector3 bikePosition, bikeLookPoint;
    private Quaternion bikeRotation;
    private Quaternion footRotation;
    private Vector2 entryLook;
    private LensSettings footLens;
    private bool customProjection;
    public float BikeBlend => bikeWeight;
    private BikeCameraFraming BikeFraming => profile != null ? profile.bike : null;

    public void Configure(GameplayCameraProfile value) => profile = value;

    protected override void OnEnable()
    {
        base.OnEnable();
        GameplayCameraSettings.Changed += Apply;
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        if (started)
            Apply(GameplayCameraSettings.Mode);
    }

    private void Start()
    {
        // All scene brains have registered by Start, before their first LateUpdate.
        started = true;
        CaptureAction();
        Apply(GameplayCameraSettings.Mode);
    }

    private void OnDisable()
    {
        GameplayCameraSettings.Changed -= Apply;
        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);
        bikeWeight = blendFrom = blendTo = 0;
        observedPhase = BikeRidePhase.OnFoot;
        ResetProjection();
        Apply(GameplayCameraSettings.Mode);
    }

    private void CaptureAction()
    {
        if (captured)
            return;
        cameraRig = GetComponent<CinemachineVirtualCamera>();
        body = cameraRig.GetCinemachineComponent<CinemachineTransposer>();
        if (body == null)
            return;
        noise = cameraRig.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        aim = cameraRig.GetCinemachineComponent(CinemachineCore.Stage.Aim);
        actionLens = cameraRig.m_Lens;
        var brain = CinemachineCore.Instance.FindPotentialTargetBrain(cameraRig);
        outputCamera = brain != null ? brain.OutputCamera : null;
        actionOrthographic =
            outputCamera != null ? outputCamera.orthographic : actionLens.Orthographic;
        actionPhysical =
            outputCamera != null ? outputCamera.usePhysicalProperties : actionLens.IsPhysicalCamera;
        actionRotation = transform.localRotation;
        actionOffset = body.m_FollowOffset;
        actionDamping = new Vector3(body.m_XDamping, body.m_YDamping, body.m_ZDamping);
        actionBinding = body.m_BindingMode;
        actionNoise = noise != null && noise.enabled;
        actionAim = aim != null && aim.enabled;
        if (cameraRig.Follow != null)
        {
            bikeRider = cameraRig.Follow.GetComponentInParent<PlayerBikeRider>();
            playerLook = cameraRig.Follow.GetComponentInParent<StarterAssets.ThirdPersonController>();
        }
        captured = true;
    }

    private void Update()
    {
        var tuning = BikeFraming;
        if (!captured || tuning == null)
            return;
        var phase = bikeRider != null && bikeRider.isActiveAndEnabled
            ? bikeRider.Phase : BikeRidePhase.OnFoot;
        var bike = bikeRider != null ? bikeRider.Bike : null;
        if (bike == null || !bike.isActiveAndEnabled)
            phase = BikeRidePhase.OnFoot;
        if (phase != observedPhase)
        {
            if (observedPhase == BikeRidePhase.OnFoot && phase != BikeRidePhase.OnFoot)
            {
                entryLook = playerLook != null ? playerLook.CameraLookAngles : Vector2.zero;
                UpdateBikePose(bike, 0);
            }
            switch (phase)
            {
                case BikeRidePhase.Approaching:
                    BlendBikeTo(1, tuning.mountBlendDuration);
                    break;
                case BikeRidePhase.Mounting:
                    // A very short approach must still finish its blend before seating.
                    if (blendTo != 1 || blendDuration - blendElapsed > bikeRider.TransitionDuration)
                        BlendBikeTo(1, Mathf.Min(tuning.mountBlendDuration, bikeRider.TransitionDuration));
                    break;
                case BikeRidePhase.Riding:
                    bikeWeight = blendFrom = blendTo = 1;
                    break;
                case BikeRidePhase.Dismounting:
                    BlendBikeTo(0, Mathf.Min(tuning.dismountBlendDuration, bikeRider.TransitionDuration));
                    break;
                case BikeRidePhase.OnFoot:
                    if (observedPhase == BikeRidePhase.Dismounting)
                        bikeWeight = blendFrom = blendTo = 0;
                    else
                        BlendBikeTo(0, tuning.dismountBlendDuration);
                    break;
            }
            observedPhase = phase;
        }
        if (phase != BikeRidePhase.OnFoot)
            UpdateBikePose(bike, Time.deltaTime);
        // Interrupted rides also recover while a death/disable lease has paused the world.
        float dt = phase == BikeRidePhase.OnFoot ? Time.unscaledDeltaTime : Time.deltaTime;
        blendElapsed += dt;
        bikeWeight = Mathf.Lerp(blendFrom, blendTo, Mathf.SmoothStep(0, 1,
            Mathf.Clamp01(blendElapsed / Mathf.Max(.01f, blendDuration))));
        if (bikeWeight == 0)
            ResetProjection();
    }

    private void BlendBikeTo(float target, float duration)
    {
        blendFrom = bikeWeight;
        blendTo = target;
        blendElapsed = 0;
        blendDuration = Mathf.Max(.01f, duration);
    }

    private void UpdateBikePose(AlienBikeController bike, float dt)
    {
        var tuning = BikeFraming;
        // Read existing look input independently of inherited player/mount rotations.
        // No new input or recenter rule: bike steering supplies the stable heading.
        var lookAngles = playerLook != null ? playerLook.CameraLookAngles : entryLook;
        float lookYaw = Mathf.DeltaAngle(entryLook.x, lookAngles.x);
        float lookPitch = Mathf.DeltaAngle(entryLook.y, lookAngles.y);
        var yaw = Quaternion.Euler(0, bike.transform.eulerAngles.y + lookYaw, 0);
        Vector3 position = bike.transform.position + Vector3.up * tuning.height
            + yaw * (Vector3.back * Mathf.Max(.5f, tuning.distance));
        Vector3 look = bike.seatPoint.position + Vector3.up * tuning.targetHeight
            + yaw * (Vector3.forward * tuning.forwardLookAhead);
        Quaternion rotation = Quaternion.LookRotation(look - position, Vector3.up)
            * Quaternion.Euler(tuning.pitch + lookPitch, 0, 0);
        float t = dt <= 0 || tuning.followDamping <= 0 ? 1 : 1 - Mathf.Exp(-dt / tuning.followDamping);
        bikePosition = Vector3.Lerp(bikePosition, position, t);
        bikeRotation = Quaternion.Slerp(bikeRotation, rotation, t);
        bikeLookPoint = look;
    }

    public override void PrePipelineMutateCameraStateCallback(CinemachineVirtualCameraBase vcam,
        ref CameraState state, float deltaTime)
    {
        // Cinemachine writes its result back to the rig Transform. Keep that bike result
        // from becoming the next frame's on-foot orientation (and the return blend).
        if (isActiveAndEnabled && captured)
            state.RawOrientation = footRotation;
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (!isActiveAndEnabled || bikeWeight <= 0 || BikeFraming == null)
            return;
        if (stage == CinemachineCore.Stage.Body)
        {
            footLens = state.Lens;
            state.RawPosition = Vector3.Lerp(state.RawPosition, bikePosition, bikeWeight);
            state.ReferenceLookAt = state.HasLookAt
                ? Vector3.Lerp(state.ReferenceLookAt, bikeLookPoint, bikeWeight) : bikeLookPoint;
            var lens = state.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, BikeFraming.fieldOfView, bikeWeight);
            lens.NearClipPlane = Mathf.Lerp(lens.NearClipPlane, .1f, bikeWeight);
            lens.ModeOverride = LensSettings.OverrideModes.Perspective;
            state.Lens = lens;
        }
        if (stage == CinemachineCore.Stage.Aim)
            state.RawOrientation = Quaternion.Slerp(state.RawOrientation, bikeRotation, bikeWeight);
        // Noise, render feedback and the existing occlusion owner retain their stages.
    }

    private void OnCameraUpdated(CinemachineBrain brain)
    {
        if (brain.OutputCamera != outputCamera || !brain.IsLive(cameraRig))
            return;
        if (bikeWeight <= 0 || bikeWeight >= 1 || !footLens.Orthographic)
        {
            ResetProjection();
            return;
        }
        // Normalize perspective at the follow plane before blending with orthographic.
        // This keeps screen scale continuous; simply toggling orthographic would cut.
        float distance = Mathf.Max(.1f, Vector3.Dot(cameraRig.Follow.position - outputCamera.transform.position,
            outputCamera.transform.forward));
        float size = footLens.OrthographicSize, aspect = outputCamera.aspect;
        var ortho = Matrix4x4.Ortho(-size * aspect, size * aspect, -size, size,
            outputCamera.nearClipPlane, outputCamera.farClipPlane);
        var perspective = Matrix4x4.Perspective(outputCamera.fieldOfView, aspect,
            outputCamera.nearClipPlane, outputCamera.farClipPlane);
        var projection = new Matrix4x4();
        for (int i = 0; i < 16; i++)
            projection[i] = Mathf.Lerp(ortho[i], perspective[i] / distance, bikeWeight);
        outputCamera.projectionMatrix = projection;
        customProjection = true;
    }

    private void ResetProjection()
    {
        if (customProjection && outputCamera != null)
            outputCamera.ResetProjectionMatrix();
        customProjection = false;
    }

    public void Apply(GameplayCameraMode mode)
    {
        CaptureAction();
        if (!captured)
            return;
        RestoreAction();
        if (mode == GameplayCameraMode.Action || profile == null)
        {
            RefreshWhilePaused();
            return;
        }
        GameplayCameraPreset preset =
            mode == GameplayCameraMode.Isometric ? profile.isometric : profile.tactical;
        if (preset == null)
        {
            RefreshWhilePaused();
            return;
        }
        LensSettings lens = actionLens;
        lens.ModeOverride = preset.projection;
        lens.FieldOfView = preset.fieldOfView;
        lens.OrthographicSize = preset.orthographicSize;
        cameraRig.m_Lens = lens;
        changedProjection = true;
        transform.rotation = Quaternion.Euler(preset.rotation);
        footRotation = transform.rotation;
        body.m_BindingMode = preset.binding;
        body.m_FollowOffset = preset.followOffset + preset.targetOffset;
        SetDamping(preset.damping);
        // The preset defines fixed orientation; do not let a scene's optional Aim stage override it.
        if (aim != null)
            aim.enabled = false;
        if (noise != null)
            noise.enabled = preset.useNoise && actionNoise;
        cameraRig.PreviousStateIsValid = false;
        RefreshWhilePaused();
    }

    private void RefreshWhilePaused()
    {
        if (!Application.isPlaying || Time.timeScale != 0f)
            return;
        var brain = CinemachineCore.Instance.FindPotentialTargetBrain(cameraRig);
        if (brain == null)
            return;
        // SmartUpdate can wait for a physics tick that never occurs during suspension.
        // Evaluate this invalidated rig once, then let the existing brain publish its state.
        // Negative delta time resets damping; gameplay time and brain settings stay intact.
        cameraRig.InternalUpdateCameraState(brain.DefaultWorldUp, -1f);
        brain.ManualUpdate();
    }

    private void RestoreAction()
    {
        if (!captured || body == null)
            return;
        cameraRig.m_Lens = actionLens;
        // Cinemachine's None override inherits the output camera's current projection.
        // Undo a previous preset's output change before restoring that inherited baseline.
        if (
            changedProjection
            && outputCamera != null
            && actionLens.ModeOverride == LensSettings.OverrideModes.None
        )
        {
            outputCamera.orthographic = actionOrthographic;
            outputCamera.usePhysicalProperties = actionPhysical;
        }
        changedProjection = false;
        transform.localRotation = actionRotation;
        footRotation = transform.rotation;
        body.m_FollowOffset = actionOffset;
        body.m_BindingMode = actionBinding;
        SetDamping(actionDamping);
        if (noise != null)
            noise.enabled = actionNoise;
        if (aim != null)
            aim.enabled = actionAim;
        cameraRig.PreviousStateIsValid = false;
    }

    private void SetDamping(Vector3 damping)
    {
        body.m_XDamping = damping.x;
        body.m_YDamping = damping.y;
        body.m_ZDamping = damping.z;
    }
}
