using UnityEngine;

public enum BeamTransportDirection { Down, Up }

// Presentation only: authored sparks use Y velocity in an upright beam.
[DisallowMultipleComponent]
public sealed class BeamTransportVFX : MonoBehaviour
{
    [SerializeField] private GameObject beamVisual;
    [SerializeField] private GameObject groundRing;
    [SerializeField] private ParticleSystem beamSparks;
    [Header("Hoist visibility (seconds)")]
    [SerializeField, Min(0f)] private float hoistFadeInDuration = .35f;
    [SerializeField, Min(0f)] private float hoistFadeOutDuration = .55f;
    private static readonly int VisibilityId = Shader.PropertyToID("_BeamVisibility");
    private Renderer[] renderers;
    private MaterialPropertyBlock properties;
    private float fadeElapsed;
    private bool fadingOut;
    public float Visibility { get; private set; }
    public bool IsMaterializing => Visibility < 1f && !fadingOut;
    private BeamTransportDirection authoredDirection;
    private ParticleSystem.MinMaxCurve authoredY;
    private bool captured;
    public BeamTransportDirection Direction { get; private set; }

    public void SetDirection(BeamTransportDirection direction)
    {
        Direction = direction;
        if (beamSparks == null) return;
        var velocity = beamSparks.velocityOverLifetime;
        if (!captured)
        {
            authoredY = velocity.y;
            Vector3 axis = velocity.space == ParticleSystemSimulationSpace.World ? Vector3.up : beamSparks.transform.up;
            float worldY = Vector3.Dot(axis, Vector3.up) * authoredY.Evaluate(0.5f, 0.5f);
            authoredDirection = worldY >= 0f ? BeamTransportDirection.Up : BeamTransportDirection.Down;
            captured = true;
        }
        velocity.y = Scale(authoredY, direction == authoredDirection ? 1f : -1f);
    }

    private static ParticleSystem.MinMaxCurve Scale(ParticleSystem.MinMaxCurve curve, float sign)
    {
        if (curve.mode == ParticleSystemCurveMode.Constant) curve.constant *= sign;
        else if (curve.mode == ParticleSystemCurveMode.TwoConstants)
        {
            float min = curve.constantMin * sign, max = curve.constantMax * sign;
            curve.constantMin = Mathf.Min(min, max);
            curve.constantMax = Mathf.Max(min, max);
        }
        else curve.curveMultiplier *= sign;
        return curve;
    }

    public void Show(Vector3 position, BeamTransportDirection direction)
    {
        Hide(); // Clear old world-space particles before relocating/reusing.
        transform.position = position;
        SetDirection(direction);
        if (beamVisual != null) beamVisual.SetActive(true);
        if (groundRing != null) groundRing.SetActive(true);
        if (beamSparks != null) beamSparks.Play(true);
        // Activation creates the additional authored spirals. Cache AFTER activation.
        renderers = GetComponentsInChildren<Renderer>(true);
        SetVisibility(1f);
    }

    public void BeginHoistFadeIn()
    {
        fadeElapsed = 0f;
        fadingOut = false;
        SetVisibility(hoistFadeInDuration > 0f ? 0f : 1f);
    }

    // The transport owns the stationary prelude clock, never the path clock.
    public void AdvanceHoistFadeIn(float deltaTime)
    {
        fadeElapsed += Mathf.Max(0f, deltaTime);
        SetVisibility(hoistFadeInDuration > 0f
            ? Mathf.SmoothStep(0f, 1f, fadeElapsed / hoistFadeInDuration) : 1f);
    }

    public void FadeOutAfterLanding()
    {
        fadeElapsed = 0f;
        fadingOut = true;
        if (hoistFadeOutDuration <= 0f) Hide();
    }

    private void Update()
    {
        if (!fadingOut) return;
        fadeElapsed += Time.deltaTime;
        SetVisibility(1f - Mathf.SmoothStep(0f, 1f, fadeElapsed / hoistFadeOutDuration));
        if (Visibility <= 0f) Hide();
    }

    private void SetVisibility(float value)
    {
        Visibility = value;
        if (renderers == null) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(properties);
            properties.SetFloat(VisibilityId, value);
            renderer.SetPropertyBlock(properties);
        }
    }

    public void Hide()
    {
        fadingOut = false;
        fadeElapsed = 0f;
        SetVisibility(0f);
        if (beamSparks != null) beamSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (beamVisual != null) beamVisual.SetActive(false);
        if (groundRing != null) groundRing.SetActive(false);
    }

    private void OnDisable() => Hide();
}
