using UnityEngine;

/// <summary>One shared mesh/material, with per-event shader properties. No independent clock or gameplay.</summary>
[DisallowMultipleComponent]
public sealed class AlienGroundAuroraPatch : MonoBehaviour
{
    [SerializeField] private MeshRenderer ribbon;
    private MaterialPropertyBlock properties;
    private static readonly int Tint = Shader.PropertyToID("_Tint");
    private static readonly int Motion = Shader.PropertyToID("_Motion");
    private static readonly int Footprint = Shader.PropertyToID("_Footprint");
    public float Opacity { get; private set; }
    public MeshRenderer Ribbon => ribbon;

    public void Initialize()
    {
        if (properties == null) properties = new MaterialPropertyBlock();
        Hide();
    }

    public void Begin(in AlienGroundAuroraSchedule.Event value)
    {
        transform.SetLocalPositionAndRotation(value.position, value.rotation);
        transform.localScale = new Vector3(value.dimensions.x, 1, value.dimensions.y);
        properties.SetVector(Footprint, value.footprint);
        gameObject.SetActive(true);
    }

    public void Present(in AlienGroundAuroraSchedule.Event value, float age)
    {
        Opacity = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / value.fadeIn))
            * Mathf.SmoothStep(0, 1, Mathf.Clamp01((value.lifetime - age) / value.fadeOut));
        Color color = value.tint * value.intensity;
        color.a = value.tint.a * Opacity;
        properties.SetColor(Tint, color);
        properties.SetVector(Motion, new Vector4(age, value.motionSpeed, value.phase, 0));
        ribbon.SetPropertyBlock(properties);
    }

    public void Hide()
    {
        Opacity = 0;
        gameObject.SetActive(false);
    }
}
