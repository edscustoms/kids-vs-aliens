using UnityEngine;

[DisallowMultipleComponent]
public sealed class ElectricGrenadeBurstVFX : MonoBehaviour
{
    [Header("Particles")]
    [SerializeField]
    private ParticleSystem coreFlash;

    [SerializeField]
    private ParticleSystem shockRing;

    [SerializeField]
    private ParticleSystem sparks;

    [SerializeField]
    private ParticleSystem residualParticles;

    [Tooltip("Optional root scaled to the blast diameter for radius-dependent presentation.")]
    [SerializeField]
    private Transform radiusScaledRoot;

    [Header("Pre-created Arcs")]
    [SerializeField]
    private ElectricArcVFX[] targetArcs =
        new ElectricArcVFX[0];

    [SerializeField]
    private ElectricArcVFX[] groundArcs =
        new ElectricArcVFX[0];

    [SerializeField, Min(0.05f)]
    private float presentationDuration = 0.5f;

    [SerializeField]
    private Vector2 groundArcRadiusRange =
        new Vector2(0.3f, 0.9f);

    private bool playing;
    private float releaseTime;
    private Vector3 initialRadiusScale =
        Vector3.one;

    private void Awake()
    {
        if (radiusScaledRoot != null)
        {
            initialRadiusScale =
                radiusScaledRoot.localScale;
        }

        ResetPresentation();
    }

    public void Play(
        Vector3[] targetPositions,
        int targetCount,
        float radius,
        Color color)
    {
        ResetPresentation();

        if (radiusScaledRoot != null)
        {
            radiusScaledRoot.localScale =
                Vector3.Scale(
                    initialRadiusScale,
                    Vector3.one *
                    Mathf.Max(
                        0.01f,
                        radius * 2f));
        }

        PlayParticles(
            coreFlash,
            color);

        PlayParticles(
            shockRing,
            color);

        PlayParticles(
            sparks,
            color);

        PlayParticles(
            residualParticles,
            color);

        int visibleTargetCount =
            Mathf.Min(
                targetCount,
                targetArcs != null
                    ? targetArcs.Length
                    : 0);

        for (int i = 0;
             i < visibleTargetCount;
             i++)
        {
            targetArcs[i]?.Play(
                transform.position,
                targetPositions[i],
                color);
        }

        if (groundArcs != null)
        {
            float minimumRadius =
                Mathf.Clamp01(
                    Mathf.Min(
                        groundArcRadiusRange.x,
                        groundArcRadiusRange.y));

            float maximumRadius =
                Mathf.Clamp01(
                    Mathf.Max(
                        groundArcRadiusRange.x,
                        groundArcRadiusRange.y));

            for (int i = 0;
                 i < groundArcs.Length;
                 i++)
            {
                if (groundArcs[i] == null)
                    continue;

                Vector2 direction =
                    Random.insideUnitCircle;

                if (direction.sqrMagnitude < 0.001f)
                {
                    direction =
                        Vector2.right;
                }

                direction.Normalize();

                float arcRadius =
                    Random.Range(
                        minimumRadius,
                        maximumRadius) *
                    radius;

                Vector3 end =
                    transform.position +
                    new Vector3(
                        direction.x,
                        0f,
                        direction.y) *
                    arcRadius +
                    Vector3.up * 0.03f;

                groundArcs[i].Play(
                    transform.position,
                    end,
                    color);
            }
        }

        playing = true;
        releaseTime =
            Time.time +
            presentationDuration;
    }

    private void Update()
    {
        if (!playing ||
            Time.time < releaseTime)
        {
            return;
        }

        playing = false;

        VfxPool.Release(
            this);
    }

    private void OnDisable()
    {
        ResetPresentation();
    }

    private void ResetPresentation()
    {
        playing = false;

        StopParticles(coreFlash);
        StopParticles(shockRing);
        StopParticles(sparks);
        StopParticles(residualParticles);

        HideArcs(targetArcs);
        HideArcs(groundArcs);

        if (radiusScaledRoot != null)
        {
            radiusScaledRoot.localScale =
                initialRadiusScale;
        }
    }

    private static void PlayParticles(
        ParticleSystem particles,
        Color color)
    {
        if (particles == null)
            return;

        ParticleSystem.MainModule main =
            particles.main;

        main.startColor =
            color;

        particles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        particles.Play(
            true);
    }

    private static void StopParticles(
        ParticleSystem particles)
    {
        if (particles == null)
            return;

        particles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static void HideArcs(
        ElectricArcVFX[] arcs)
    {
        if (arcs == null)
            return;

        for (int i = 0;
             i < arcs.Length;
             i++)
        {
            arcs[i]?.Hide();
        }
    }
}
