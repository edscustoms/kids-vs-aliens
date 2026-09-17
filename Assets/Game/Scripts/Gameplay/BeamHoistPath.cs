using UnityEngine;

/// <summary>Snapshot of one validated hoist. Targets moving afterward cannot change the route.</summary>
public struct BeamHoistPath
{
    public Vector3 start, release, control1, control2, landing;
    public float liftDuration, transferDuration;
    public Vector3 Evaluate(float t)
    {
        float u = 1f - t;
        return u * u * u * release + 3f * u * u * t * control1
            + 3f * u * t * t * control2 + t * t * t * landing;
    }
    public float ControlPolygonLength => Vector3.Distance(release, control1)
        + Vector3.Distance(control1, control2) + Vector3.Distance(control2, landing);
    public float SecondDerivativeBound => 6f * Mathf.Max(
        (control2 - 2f * control1 + release).magnitude,
        (landing - 2f * control2 + control1).magnitude);

    public static BeamHoistPath Create(Vector3 start, Vector3 landing, float releaseHeight,
        float liftDuration, float transferDuration)
    {
        Vector3 release = new Vector3(start.x, Mathf.Max(releaseHeight, landing.y + 0.15f), start.z);
        float rise = Mathf.Clamp(Vector3.Distance(new Vector3(start.x, landing.y, start.z), landing) * 0.15f, 0.2f, 0.8f);
        return new BeamHoistPath
        {
            start = start, release = release, landing = landing,
            // Vertical tangent at release and a soft downward tangent at the supported landing.
            control1 = release + Vector3.up * rise,
            control2 = landing + Vector3.up * (release.y - landing.y + rise),
            liftDuration = liftDuration, transferDuration = transferDuration
        };
    }
}
