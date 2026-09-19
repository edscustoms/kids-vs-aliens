using UnityEngine;

/// <summary>Presentation inside the authored cone. Never scales or drives the transport.</summary>
[DisallowMultipleComponent]
public sealed class BeamEnergyField : MonoBehaviour
{
    [SerializeField] private ParticleSystem motes;
    [SerializeField] private LineRenderer spiral;
    // Sampled by explicit authoring from the existing cone mesh, in this component's space.
    [SerializeField, HideInInspector] private Vector3 bottomCenter, topCenter;
    [SerializeField, HideInInspector] private Vector2 bottomRadii, topRadii;
    [SerializeField, Range(1f, 4f)] private float spiralTurns = 2.1f;
    [SerializeField] private float rotationDegreesPerSecond = 45f;
    [SerializeField, Range(.01f, .12f)] private float spiralWidth = .045f;
    [SerializeField, Range(.5f, .98f)] private float spiralRadiusFraction = .91f;
    [SerializeField, Range(.3f, .95f)] private float particleRadiusFraction = .86f;
    private readonly Vector3[] ribbon = new Vector3[161];
    private ParticleSystem.Particle[] particles;
    private float phase;

    private void OnEnable()
    {
        phase = 0;
        if (motes != null && (particles == null || particles.Length != motes.main.maxParticles))
            particles = new ParticleSystem.Particle[motes.main.maxParticles];
        if (spiral != null) spiral.positionCount = ribbon.Length;
        DrawSpiral();
    }

    private void LateUpdate()
    {
        phase = Mathf.Repeat(phase + rotationDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime, Mathf.PI * 2f);
        DrawSpiral();
        ConfineParticles();
    }

    private void DrawSpiral()
    {
        if (spiral == null) return;
        spiral.widthMultiplier = spiralWidth;
        for (int i = 0; i < ribbon.Length; i++)
        {
            float t = Mathf.Lerp(.015f, .96f, i / (float)(ribbon.Length - 1));
            float angle = phase + t * spiralTurns * Mathf.PI * 2f;
            ribbon[i] = Point(t, angle, spiralRadiusFraction);
        }
        spiral.SetPositions(ribbon);
    }

    private Vector3 Point(float t, float angle, float radius)
    {
        Vector2 radii = Vector2.Lerp(bottomRadii, topRadii, t) * radius;
        return Vector3.Lerp(bottomCenter, topCenter, t) + new Vector3(Mathf.Cos(angle) * radii.x, 0, Mathf.Sin(angle) * radii.y);
    }

    private void ConfineParticles()
    {
        if (motes == null || particles == null || !motes.isPlaying) return;
        int count = motes.GetParticles(particles);
        for (int i = 0; i < count; i++)
        {
            // Native ParticleSystem Y velocity owns travel direction. Stable per-particle
            // radial coordinates follow the taper instead of escaping as it narrows.
            Vector3 local = transform.InverseTransformPoint(motes.transform.TransformPoint(particles[i].position));
            float t = (local.y - bottomCenter.y) / Mathf.Max(.001f, topCenter.y - bottomCenter.y);
            if (t <= 0 || t >= 1) { particles[i].remainingLifetime = 0; continue; }
            uint seed = particles[i].randomSeed;
            float angle = (seed % 4093) / 4093f * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(((seed / 4093) % 1021) / 1021f) * particleRadiusFraction;
            particles[i].position = motes.transform.InverseTransformPoint(transform.TransformPoint(Point(t, angle, radius)));
        }
        motes.SetParticles(particles, count);
    }
}
