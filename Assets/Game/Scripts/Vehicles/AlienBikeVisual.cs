using UnityEngine;

/// <summary>Shared cosmetic mesh/trails only. Neither riding nor path state belongs here.</summary>
[DisallowMultipleComponent]
public sealed class AlienBikeVisual : MonoBehaviour
{
    [SerializeField] private Renderer hull;
    [SerializeField] private int plasmaMaterialIndex = 3;
    [SerializeField] private TrailRenderer[] trails;
    [SerializeField, ColorUsage(false, true)] private Color[] colors = { new(0, 3, 6), new(2.5f, .25f, 6), new(5, .1f, 2.5f) };
    private MaterialPropertyBlock properties;
    private int variant;
    private float lastPower = -1;
    public void SetVariant(int value)
    {
        variant = Mathf.Abs(value) % colors.Length;
        lastPower = -1;
        SetPower(0);
    }
    public void SetPower(float power)
    {
        power = Mathf.Clamp01(power);
        if (Mathf.Abs(lastPower - power) < .015f)
            return;
        lastPower = power;
        properties ??= new MaterialPropertyBlock();
        if (hull != null)
        {
            properties.SetColor("_EmissionColor", colors[variant] * Mathf.Lerp(1, 2.4f, power));
            hull.SetPropertyBlock(properties, plasmaMaterialIndex);
        }
        if (trails == null)
            return;
        foreach (var trail in trails)
        {
            if (trail == null)
                continue;
            trail.widthMultiplier = Mathf.Lerp(.12f, .3f, power);
            trail.startColor = colors[variant] * .65f;
            trail.endColor = Color.clear;
        }
    }
    public void ClearTrails()
    {
        if (trails != null)
        foreach (var trail in trails)
        if (trail != null)
            trail.Clear();
    }
    private void OnEnable()
    {
        lastPower = -1;
        SetPower(0);
        ClearTrails();
    }
}
