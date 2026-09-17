using UnityEngine;

/// <summary>
/// Snapshot of one validated hoist.
///
/// Movement is exactly ONE cubic Bezier:
///
/// START (A) -> LANDING (B)
///
/// release is metadata used to determine the safe height of the curve.
/// It is NOT a movement destination.
/// </summary>
public struct BeamHoistPath
{
    public Vector3 start,
        release,
        control1,
        control2,
        landing;

    public float liftDuration,
        transferDuration;

    public float Duration => Mathf.Max(0.01f, liftDuration + transferDuration);

    public Vector3 Evaluate(float t)
    {
        t = Mathf.Clamp01(t);

        float u = 1f - t;

        // ONE curve:
        //
        // P0 = start
        // P1 = control1
        // P2 = control2
        // P3 = landing
        return u * u * u * start
            + 3f * u * u * t * control1
            + 3f * u * t * t * control2
            + t * t * t * landing;
    }

    public float ControlPolygonLength =>
        Vector3.Distance(start, control1)
        + Vector3.Distance(control1, control2)
        + Vector3.Distance(control2, landing);

    public float SecondDerivativeBound =>
        6f
        * Mathf.Max(
            (control2 - 2f * control1 + start).magnitude,
            (landing - 2f * control2 + control1).magnitude
        );

    public static BeamHoistPath Create(
        Vector3 start,
        Vector3 landing,
        float releaseHeight,
        float liftDuration,
        float transferDuration
    )
    {
        // Safe height supplied by the hoist surface.
        // Metadata only — Amy NEVER moves here separately.
        float safeHeight = Mathf.Max(releaseHeight, landing.y + 0.15f);

        Vector3 release = new Vector3(start.x, safeHeight, start.z);

        /*
         * First handle directly above START.
         *
         * This gives Amy a mostly vertical initial tangent,
         * while remaining part of ONE continuous curve.
         */
        Vector3 control1 = new Vector3(start.x, safeHeight, start.z);

        /*
         * Second handle above LANDING.
         *
         * This creates the rounded top and smooth approach
         * to the destination.
         */
        Vector3 control2 = new Vector3(landing.x, safeHeight, landing.z);

        return new BeamHoistPath
        {
            start = start,
            release = release,
            control1 = control1,
            control2 = control2,
            landing = landing,
            liftDuration = liftDuration,
            transferDuration = transferDuration,
        };
    }
}
