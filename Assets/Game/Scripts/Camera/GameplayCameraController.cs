using Cinemachine;
using UnityEngine;

/// <summary>Applies presets to the existing camera; retains the scene's authored Action baseline.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CinemachineVirtualCamera))]
public sealed class GameplayCameraController : MonoBehaviour
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

    public void Configure(GameplayCameraProfile value) => profile = value;

    private void OnEnable()
    {
        GameplayCameraSettings.Changed += Apply;
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
        RestoreAction();
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
        captured = true;
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
