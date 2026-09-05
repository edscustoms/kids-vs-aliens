using UnityEngine;

[DisallowMultipleComponent]
public sealed class ElectricGrenadeBurstVFX : MonoBehaviour
{
    // Kept only so an already-authored V1 prefab survives the script upgrade.
    // The V2 setup command disables these billboard-based burst layers.
    [SerializeField, HideInInspector]
    private ParticleSystem coreFlash;

    [SerializeField, HideInInspector]
    private ParticleSystem shockRing;

    [SerializeField, HideInInspector]
    private Transform radiusScaledRoot;

    [Header("Generated V2 Core")]
    [SerializeField]
    private Transform energyCore;

    [SerializeField]
    private Renderer energyCoreRenderer;

    [SerializeField]
    private Transform energyHalo;

    [SerializeField]
    private Renderer energyHaloRenderer;

    [SerializeField]
    private LineRenderer shockRingLine;

    [Header("Particles")]
    [SerializeField]
    private ParticleSystem sparks;

    [SerializeField]
    private ParticleSystem residualParticles;

    [Header("Pre-created Arcs")]
    [SerializeField]
    private ElectricArcVFX[] radialArcs =
        new ElectricArcVFX[0];

    [SerializeField]
    private ElectricArcVFX[] secondaryArcs =
        new ElectricArcVFX[0];

    [SerializeField]
    private ElectricArcVFX[] targetArcs =
        new ElectricArcVFX[0];

    [SerializeField]
    private ElectricArcVFX[] groundArcs =
        new ElectricArcVFX[0];

    [Header("Overall Timing")]
    [SerializeField, Min(0.1f)]
    private float presentationDuration = 0.85f;

    [Header("Core")]
    [SerializeField, Min(0.02f)]
    private float coreDuration = 0.24f;

    [SerializeField, Min(0.01f)]
    private float corePeakDiameterFactor = 0.085f;

    [SerializeField, Min(0f)]
    private float coreIntensity = 2.2f;

    [SerializeField, Min(0f)]
    private float haloIntensity = 0.85f;

    [Header("Shock Ring")]
    [SerializeField, Min(0.02f)]
    private float shockRingDuration = 0.62f;

    [SerializeField, Range(0.1f, 1.2f)]
    private float shockRingRadiusFactor = 1.08f;

    [SerializeField, Range(16, 64)]
    private int shockRingSegments = 40;

    [Header("Radial Burst")]
    [SerializeField, Min(0.02f)]
    private float radialDuration = 0.38f;

    [SerializeField, Min(0.01f)]
    private float radialRetargetInterval = 0.05f;

    [SerializeField]
    private Vector2 radialRadiusRange =
        new Vector2(0.38f, 0.98f);

    [SerializeField, Min(0.01f)]
    private float radialArcLifetime = 0.12f;

    [SerializeField, Min(0.05f)]
    private float radialWidthMultiplier = 0.88f;

    [SerializeField, Min(0f)]
    private float radialJitterMultiplier = 0.75f;

    [Header("Secondary Branches")]
    [SerializeField, Min(0.02f)]
    private float secondaryDuration = 0.48f;

    [SerializeField, Min(0.01f)]
    private float secondaryRetargetInterval = 0.07f;

    [SerializeField, Min(0.01f)]
    private float secondaryArcLifetime = 0.13f;

    [SerializeField, Min(0.05f)]
    private float secondaryWidthMultiplier = 0.62f;

    [SerializeField, Min(0f)]
    private float secondaryJitterMultiplier = 0.9f;

    [Header("Target Arcs")]
    [SerializeField, Min(0.01f)]
    private float targetRetargetInterval = 0.11f;

    [SerializeField, Min(0.01f)]
    private float targetArcLifetime = 0.18f;

    [SerializeField, Min(0.05f)]
    private float targetArcWidthMultiplier = 1.0f;

    [SerializeField, Min(0f)]
    private float targetArcJitterMultiplier = 0.55f;

    [Header("Ground Crawlers")]
    [SerializeField, Min(0.01f)]
    private float groundRetargetInterval = 0.085f;

    [SerializeField]
    private Vector2 groundArcRadiusRange =
        new Vector2(0.32f, 0.95f);

    [SerializeField, Min(0.01f)]
    private float groundArcLifetime = 0.16f;

    [SerializeField, Min(0.05f)]
    private float groundArcWidthMultiplier = 0.85f;

    [SerializeField, Min(0f)]
    private float groundArcJitterMultiplier = 0.55f;

    [Header("Placement + Color")]
    [SerializeField, Min(0f)]
    private float originLift = 0.12f;

    [SerializeField, Min(0f)]
    private float targetLift = 0.08f;

    [SerializeField, Min(0f)]
    private float groundHeightJitter = 0.035f;

    [SerializeField]
    private Color secondaryColor =
        new Color(0.58f, 0.18f, 1f, 1f);

    private readonly Vector3[] cachedTargetPositions =
        new Vector3[16];

    private Vector3[] ringPoints =
        new Vector3[0];

    private MaterialPropertyBlock coreBlock;
    private MaterialPropertyBlock haloBlock;

    private bool playing;
    private int currentTargetCount;
    private float activeRadius;
    private float startTime;
    private float endTime;
    private float nextRadialRefreshTime;
    private float nextSecondaryRefreshTime;
    private float nextTargetRefreshTime;
    private float nextGroundRefreshTime;
    private Color activeColor;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        coreBlock =
            new MaterialPropertyBlock();

        haloBlock =
            new MaterialPropertyBlock();

        EnsureRingBuffer();
        ResetPresentation();
    }

    public void Play(
        Vector3[] targetPositions,
        int targetCount,
        float radius,
        Color color)
    {
        ResetPresentation();

        activeRadius =
            Mathf.Max(0.01f, radius);

        activeColor = color;

        currentTargetCount =
            Mathf.Clamp(
                targetCount,
                0,
                Mathf.Min(
                    cachedTargetPositions.Length,
                    targetPositions != null
                        ? targetPositions.Length
                        : 0));

        for (int i = 0;
             i < currentTargetCount;
             i++)
        {
            cachedTargetPositions[i] =
                targetPositions[i];
        }

        PlayParticles(
            sparks,
            color);

        PlayParticles(
            residualParticles,
            Color.Lerp(
                color,
                secondaryColor,
                0.45f));

        startTime = Time.time;
        endTime =
            startTime +
            presentationDuration;

        nextRadialRefreshTime =
            startTime;

        nextSecondaryRefreshTime =
            startTime + 0.02f;

        nextTargetRefreshTime =
            startTime + 0.035f;

        nextGroundRefreshTime =
            startTime + 0.045f;

        playing = true;

        SetCoreVisible(true);
        SetShockRingVisible(true);

        UpdateCore(startTime);
        UpdateShockRing(startTime);
        RefreshRadialArcs(forceAll: true);
    }

    private void Update()
    {
        if (!playing)
            return;

        float now = Time.time;

        if (now >= endTime)
        {
            playing = false;
            VfxPool.Release(this);
            return;
        }

        UpdateCore(now);
        UpdateShockRing(now);

        float elapsed =
            now - startTime;

        if (elapsed <= radialDuration &&
            now >= nextRadialRefreshTime)
        {
            RefreshRadialArcs(forceAll: false);

            nextRadialRefreshTime =
                now +
                radialRetargetInterval;
        }

        if (elapsed <= secondaryDuration &&
            now >= nextSecondaryRefreshTime)
        {
            RefreshSecondaryArcs();

            nextSecondaryRefreshTime =
                now +
                secondaryRetargetInterval;
        }

        if (now >= nextTargetRefreshTime)
        {
            RefreshTargetArcs();

            nextTargetRefreshTime =
                now +
                targetRetargetInterval;
        }

        if (now >= nextGroundRefreshTime)
        {
            RefreshGroundArcs();

            nextGroundRefreshTime =
                now +
                groundRetargetInterval;
        }
    }

    private void UpdateCore(
        float now)
    {
        if (energyCore == null ||
            energyCoreRenderer == null)
        {
            return;
        }

        float elapsed =
            now - startTime;

        if (elapsed >= coreDuration)
        {
            SetCoreVisible(false);
            return;
        }

        float t =
            Mathf.Clamp01(
                elapsed /
                Mathf.Max(0.01f, coreDuration));

        // Keep the center as a compact white/cyan ignition point. V2 let the
        // sphere swell too far, so it read as a balloon instead of energy.
        float attack =
            Smooth01(
                Mathf.Clamp01(t / 0.16f));

        float decay =
            1f -
            Smooth01(
                Mathf.Clamp01(
                    (t - 0.16f) / 0.84f));

        float pulse =
            Mathf.Min(
                attack,
                decay);

        float peakDiameter =
            Mathf.Clamp(
                activeRadius *
                corePeakDiameterFactor,
                0.28f,
                0.48f);

        float coreDiameter =
            Mathf.Lerp(
                0.05f,
                peakDiameter,
                Mathf.Pow(pulse, 0.65f));

        energyCore.localScale =
            Vector3.one *
            coreDiameter;

        Color coreColor =
            Color.Lerp(
                new Color(0.72f, 0.96f, 1f, 1f),
                activeColor,
                0.22f);

        coreColor *=
            Mathf.Lerp(
                coreIntensity * 0.65f,
                coreIntensity,
                pulse);

        coreColor.a =
            Mathf.Pow(
                Mathf.Clamp01(decay),
                1.1f);

        ApplyRendererColor(
            energyCoreRenderer,
            coreBlock,
            coreColor);

        if (energyHalo == null ||
            energyHaloRenderer == null)
        {
            return;
        }

        // Small translucent corona only. It should fade around the ignition
        // point, not expand into a giant glowing sphere.
        float haloDiameter =
            Mathf.Lerp(
                peakDiameter * 0.7f,
                peakDiameter * 1.65f,
                EaseOutCubic(t));

        energyHalo.localScale =
            Vector3.one *
            haloDiameter;

        Color haloColor =
            Color.Lerp(
                activeColor,
                secondaryColor,
                0.18f) *
            haloIntensity;

        haloColor.a =
            Mathf.Pow(
                1f - t,
                1.8f) *
            0.28f;

        ApplyRendererColor(
            energyHaloRenderer,
            haloBlock,
            haloColor);
    }

    private void UpdateShockRing(
        float now)
    {
        if (shockRingLine == null)
            return;

        float elapsed =
            now - startTime;

        if (elapsed >= shockRingDuration)
        {
            SetShockRingVisible(false);
            return;
        }

        EnsureRingBuffer();

        float t =
            Mathf.Clamp01(
                elapsed /
                Mathf.Max(
                    0.01f,
                    shockRingDuration));

        // The ring now travels for its full lifetime while fading. With the
        // SrcAlpha/One material in V3, this alpha is actually respected.
        float radius =
            Mathf.Lerp(
                0.12f,
                activeRadius *
                shockRingRadiusFactor,
                EaseOutCubic(t));

        Vector3 center =
            transform.position +
            Vector3.up * 0.045f;

        for (int i = 0;
             i < ringPoints.Length;
             i++)
        {
            float angle =
                i /
                (float)ringPoints.Length *
                Mathf.PI *
                2f;

            float ripple =
                1f +
                Mathf.Sin(
                    angle * 6f +
                    startTime * 19f) *
                0.018f *
                (1f - t);

            ringPoints[i] =
                center +
                new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle)) *
                radius *
                ripple;
        }

        shockRingLine.positionCount =
            ringPoints.Length;

        shockRingLine.SetPositions(
            ringPoints);

        float alpha =
            Mathf.Pow(
                1f - t,
                1.15f) *
            0.9f;

        Color ringColor =
            Color.Lerp(
                activeColor,
                secondaryColor,
                0.12f);

        ringColor.a = alpha;

        shockRingLine.startColor =
            ringColor;

        shockRingLine.endColor =
            ringColor;

        float width =
            Mathf.Lerp(
                0.065f,
                0.006f,
                Smooth01(t));

        shockRingLine.startWidth = width;
        shockRingLine.endWidth = width;
    }

    private void RefreshRadialArcs(
        bool forceAll)
    {
        if (radialArcs == null ||
            radialArcs.Length == 0)
        {
            return;
        }

        float elapsed =
            Time.time - startTime;

        float life01 =
            Mathf.Clamp01(
                1f -
                elapsed /
                Mathf.Max(
                    0.01f,
                    radialDuration));

        int budget =
            forceAll
                ? radialArcs.Length
                : Mathf.Clamp(
                    Mathf.CeilToInt(
                        Mathf.Lerp(
                            2f,
                            radialArcs.Length,
                            life01)),
                    1,
                    radialArcs.Length);

        Vector3 origin =
            transform.position +
            Vector3.up * originLift;

        float minimumRadius =
            Mathf.Clamp01(
                Mathf.Min(
                    radialRadiusRange.x,
                    radialRadiusRange.y));

        float maximumRadius =
            Mathf.Clamp01(
                Mathf.Max(
                    radialRadiusRange.x,
                    radialRadiusRange.y));

        for (int i = 0;
             i < radialArcs.Length;
             i++)
        {
            ElectricArcVFX arc =
                radialArcs[i];

            if (arc == null)
                continue;

            if (i >= budget)
            {
                arc.Hide();
                continue;
            }

            Vector3 direction =
                Random.onUnitSphere;

            direction.y =
                Random.Range(
                    0.04f,
                    0.72f);

            direction.Normalize();

            float distance =
                Random.Range(
                    minimumRadius,
                    maximumRadius) *
                activeRadius;

            Color arcColor =
                Random.value < 0.24f
                    ? Color.Lerp(
                        activeColor,
                        secondaryColor,
                        Random.Range(0.22f, 0.52f))
                    : activeColor;

            arc.Play(
                origin,
                origin +
                direction *
                distance,
                arcColor,
                radialArcLifetime,
                radialWidthMultiplier,
                radialJitterMultiplier,
                0.018f);
        }
    }

    private void RefreshSecondaryArcs()
    {
        if (secondaryArcs == null ||
            secondaryArcs.Length == 0)
        {
            return;
        }

        float elapsed =
            Time.time - startTime;

        float life01 =
            Mathf.Clamp01(
                1f -
                elapsed /
                Mathf.Max(
                    0.01f,
                    secondaryDuration));

        int budget =
            Mathf.Clamp(
                Mathf.CeilToInt(
                    Mathf.Lerp(
                        1f,
                        secondaryArcs.Length,
                        life01)),
                1,
                secondaryArcs.Length);

        Vector3 origin =
            transform.position +
            Vector3.up * originLift;

        for (int i = 0;
             i < secondaryArcs.Length;
             i++)
        {
            ElectricArcVFX arc =
                secondaryArcs[i];

            if (arc == null)
                continue;

            if (i >= budget)
            {
                arc.Hide();
                continue;
            }

            Vector3 direction =
                Random.onUnitSphere;

            direction.y =
                Random.Range(
                    0.02f,
                    0.65f);

            direction.Normalize();

            Vector3 start =
                origin +
                direction *
                Random.Range(
                    activeRadius * 0.18f,
                    activeRadius * 0.42f);

            Vector3 branchDirection =
                (direction +
                 Random.insideUnitSphere *
                 0.48f).normalized;

            branchDirection.y =
                Mathf.Max(
                    -0.12f,
                    branchDirection.y);

            Vector3 end =
                origin +
                branchDirection *
                Random.Range(
                    activeRadius * 0.5f,
                    activeRadius * 0.92f);

            Color arcColor =
                Color.Lerp(
                    activeColor,
                    secondaryColor,
                    Random.Range(
                        0.5f,
                        0.88f));

            arc.Play(
                start,
                end,
                arcColor,
                secondaryArcLifetime,
                secondaryWidthMultiplier,
                secondaryJitterMultiplier,
                0.02f);
        }
    }

    private void RefreshTargetArcs()
    {
        if (targetArcs == null ||
            targetArcs.Length == 0)
        {
            return;
        }

        if (currentTargetCount <= 0)
        {
            HideArcs(targetArcs);
            return;
        }

        Vector3 origin =
            transform.position +
            Vector3.up * originLift;

        int budget =
            Mathf.Min(
                targetArcs.Length,
                Mathf.Max(
                    currentTargetCount,
                    Mathf.Min(
                        targetArcs.Length,
                        currentTargetCount * 2)));

        for (int i = 0;
             i < targetArcs.Length;
             i++)
        {
            ElectricArcVFX arc =
                targetArcs[i];

            if (arc == null)
                continue;

            if (i >= budget)
            {
                arc.Hide();
                continue;
            }

            Vector3 target =
                cachedTargetPositions[
                    i % currentTargetCount] +
                Vector3.up * targetLift;

            Color arcColor =
                Color.Lerp(
                    activeColor,
                    new Color(0.72f, 0.96f, 1f, 1f),
                    0.12f);

            arc.Play(
                origin,
                target,
                arcColor,
                targetArcLifetime,
                targetArcWidthMultiplier,
                targetArcJitterMultiplier,
                0.018f);
        }
    }

    private void RefreshGroundArcs()
    {
        if (groundArcs == null ||
            groundArcs.Length == 0)
        {
            return;
        }

        float elapsed =
            Time.time - startTime;

        float life01 =
            Mathf.Clamp01(
                1f -
                elapsed /
                Mathf.Max(
                    0.01f,
                    presentationDuration));

        int budget =
            Mathf.Clamp(
                Mathf.CeilToInt(
                    Mathf.Lerp(
                        2f,
                        groundArcs.Length,
                        life01)),
                1,
                groundArcs.Length);

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

        Vector3 origin =
            transform.position +
            Vector3.up * 0.055f;

        for (int i = 0;
             i < groundArcs.Length;
             i++)
        {
            ElectricArcVFX arc =
                groundArcs[i];

            if (arc == null)
                continue;

            if (i >= budget)
            {
                arc.Hide();
                continue;
            }

            Vector2 direction =
                Random.insideUnitCircle;

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction =
                    Vector2.right;
            }

            direction.Normalize();

            float distance =
                Random.Range(
                    minimumRadius,
                    maximumRadius) *
                activeRadius;

            float y =
                Random.Range(
                    -groundHeightJitter,
                    groundHeightJitter);

            Vector3 end =
                transform.position +
                new Vector3(
                    direction.x * distance,
                    0.04f + y,
                    direction.y * distance);

            Color arcColor =
                Color.Lerp(
                    activeColor,
                    secondaryColor,
                    Random.Range(
                        0.05f,
                        0.42f));

            arc.Play(
                origin,
                end,
                arcColor,
                groundArcLifetime,
                groundArcWidthMultiplier,
                groundArcJitterMultiplier,
                0.022f);
        }
    }

    private void SetCoreVisible(
        bool visible)
    {
        if (energyCoreRenderer != null)
        {
            energyCoreRenderer.enabled =
                visible;
        }

        if (energyHaloRenderer != null)
        {
            energyHaloRenderer.enabled =
                visible;
        }
    }

    private void SetShockRingVisible(
        bool visible)
    {
        if (shockRingLine != null)
        {
            shockRingLine.enabled =
                visible;
        }
    }

    private void EnsureRingBuffer()
    {
        shockRingSegments =
            Mathf.Clamp(
                shockRingSegments,
                16,
                64);

        if (ringPoints == null ||
            ringPoints.Length != shockRingSegments)
        {
            ringPoints =
                new Vector3[
                    shockRingSegments];
        }
    }

    private void OnDisable()
    {
        ResetPresentation();
    }

    private void ResetPresentation()
    {
        playing = false;
        currentTargetCount = 0;
        activeRadius = 0f;

        StopParticles(coreFlash);
        StopParticles(shockRing);
        StopParticles(sparks);
        StopParticles(residualParticles);

        HideArcs(radialArcs);
        HideArcs(secondaryArcs);
        HideArcs(targetArcs);
        HideArcs(groundArcs);

        SetCoreVisible(false);
        SetShockRingVisible(false);
    }

    private static void ApplyRendererColor(
        Renderer target,
        MaterialPropertyBlock block,
        Color color)
    {
        if (target == null ||
            block == null)
        {
            return;
        }

        target.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(EmissionColorId, color);
        target.SetPropertyBlock(block);
    }

    private static void PlayParticles(
        ParticleSystem particles,
        Color color)
    {
        if (particles == null)
            return;

        ParticleSystem.MainModule main =
            particles.main;

        main.startColor = color;

        particles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);

        particles.Play(true);
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

    private static float Smooth01(
        float value)
    {
        value =
            Mathf.Clamp01(value);

        return value *
               value *
               (3f - 2f * value);
    }

    private static float EaseOutCubic(
        float value)
    {
        value =
            Mathf.Clamp01(value);

        float inverse =
            1f - value;

        return 1f -
               inverse *
               inverse *
               inverse;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureRingBuffer();

        presentationDuration =
            Mathf.Max(
                0.1f,
                presentationDuration);

        coreDuration =
            Mathf.Max(
                0.02f,
                coreDuration);

        shockRingDuration =
            Mathf.Max(
                0.02f,
                shockRingDuration);
    }
#endif
}
