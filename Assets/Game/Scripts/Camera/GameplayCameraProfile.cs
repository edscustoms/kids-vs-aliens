using System;
using Cinemachine;
using UnityEngine;

[Serializable]
public sealed class GameplayCameraPreset
{
    public LensSettings.OverrideModes projection = LensSettings.OverrideModes.Perspective;
    [Range(1, 179)] public float fieldOfView = 40;
    [Min(0.1f)] public float orthographicSize = 8;
    public Vector3 rotation = new Vector3(30, 0, 0);
    public Vector3 followOffset = new Vector3(0, 3, -7);
    public Vector3 damping = new Vector3(0.1f, 0.1f, 0.1f);
    public CinemachineTransposer.BindingMode binding = CinemachineTransposer.BindingMode.WorldSpace;
    public Vector3 targetOffset;
    public bool useNoise = true;
}

[CreateAssetMenu(menuName = "Camera/Gameplay Camera Profile")]
public sealed class GameplayCameraProfile : ScriptableObject
{
    [Header("Temporary bike framing (does not change the selected preset)")]
    public BikeCameraFraming bike = new BikeCameraFraming();
    public GameplayCameraPreset tactical = new GameplayCameraPreset
    {
        fieldOfView = 45, rotation = new Vector3(35, 0, 0),
        followOffset = new Vector3(0, 5, -10)
    };
    public GameplayCameraPreset isometric = new GameplayCameraPreset
    {
        projection = LensSettings.OverrideModes.Orthographic,
        orthographicSize = 8, rotation = new Vector3(35.26439f, 45, 0),
        followOffset = new Vector3(-8, 8, -8), useNoise = false
    };
}

[Serializable]
public sealed class BikeCameraFraming
{
    [Min(.5f)] public float distance = 4.8f;
    [Tooltip("Camera height above the stable bike root, in metres.")]
    public float height = 2.5f;
    [Tooltip("Additional downward pitch after aiming at the seat/look-ahead point.")]
    [Range(-20, 30)] public float pitch = 4;
    [Range(20, 110)] public float fieldOfView = 70;
    [Tooltip("Look point height above SeatPoint, in metres.")]
    public float targetHeight = .7f;
    [Min(0)] public float forwardLookAhead = 2.8f;
    [Min(0)] public float followDamping = .12f;
    [Min(.01f)] public float mountBlendDuration = .8f;
    [Tooltip("Capped by the rider's dismount phase so on-foot framing returns with control.")]
    [Min(.01f)] public float dismountBlendDuration = .6f;
    [Tooltip("Maximum yaw speed during mount/dismount. Large turns can outlast the position/lens blend.")]
    [Min(1)] public float transitionYawSpeed = 110;
}
