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
