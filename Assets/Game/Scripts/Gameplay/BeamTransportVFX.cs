using UnityEngine;

public enum BeamTransportDirection { Down, Up }

// Presentation only: authored sparks use Y velocity in an upright beam.
[DisallowMultipleComponent]
public sealed class BeamTransportVFX : MonoBehaviour
{
    [SerializeField] private GameObject beamVisual;
    [SerializeField] private GameObject groundRing;
    [SerializeField] private ParticleSystem beamSparks;
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
    }

    public void Hide()
    {
        if (beamSparks != null) beamSparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (beamVisual != null) beamVisual.SetActive(false);
        if (groundRing != null) groundRing.SetActive(false);
    }
}
