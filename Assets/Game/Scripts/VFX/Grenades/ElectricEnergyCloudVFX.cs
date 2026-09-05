using UnityEngine;

[DisallowMultipleComponent]
public sealed class ElectricEnergyCloudVFX : MonoBehaviour
{
    [Header("Layers")]
    [SerializeField]
    private ParticleSystem cloudBody;

    [SerializeField]
    private ParticleSystem brightWisps;

    [Header("Cloud Body")]
    [SerializeField, Min(0.01f)]
    private float bodyMinSizeRadiusFactor = 0.20f;

    [SerializeField, Min(0.01f)]
    private float bodyMaxSizeRadiusFactor = 0.38f;

    [SerializeField, Min(0f)]
    private float bodyMinSpeedRadiusFactor = 0.08f;

    [SerializeField, Min(0f)]
    private float bodyMaxSpeedRadiusFactor = 0.21f;

    [SerializeField, Min(0f)]
    private float bodyShapeRadiusFactor = 0.065f;

    [SerializeField, Range(1, 5)]
    private int bodyPocketCount = 3;

    [SerializeField, Min(0f)]
    private float bodyPocketSpreadRadiusFactor = 0.12f;

    [SerializeField, Min(1)]
    private int bodyMinParticleCount = 22;

    [SerializeField, Min(1)]
    private int bodyMaxParticleCount = 28;

    [Header("Bright Wisps")]
    [SerializeField, Min(0.01f)]
    private float wispMinSizeRadiusFactor = 0.09f;

    [SerializeField, Min(0.01f)]
    private float wispMaxSizeRadiusFactor = 0.21f;

    [SerializeField, Min(0f)]
    private float wispMinSpeedRadiusFactor = 0.14f;

    [SerializeField, Min(0f)]
    private float wispMaxSpeedRadiusFactor = 0.33f;

    [SerializeField, Min(0f)]
    private float wispShapeRadiusFactor = 0.05f;

    [SerializeField, Range(1, 6)]
    private int wispPocketCount = 4;

    [SerializeField, Min(0f)]
    private float wispPocketSpreadRadiusFactor = 0.18f;

    [SerializeField, Min(1)]
    private int wispMinParticleCount = 22;

    [SerializeField, Min(1)]
    private int wispMaxParticleCount = 30;

    [Header("Color")]
    [SerializeField, Range(0f, 1f)]
    private float bodySecondaryMix = 0.18f;

    [SerializeField, Range(0f, 1f)]
    private float wispSecondaryMix = 0.34f;

    [SerializeField, Range(0f, 1f)]
    private float bodyAlpha = 0.52f;

    [SerializeField, Range(0f, 1f)]
    private float wispAlpha = 0.68f;

    public void Play(float radius, Color primaryColor, Color secondaryColor)
    {
        float safeRadius = Mathf.Max(0.25f, radius);

        ConfigureLayer(
            cloudBody,
            safeRadius,
            bodyMinSizeRadiusFactor,
            bodyMaxSizeRadiusFactor,
            bodyMinSpeedRadiusFactor,
            bodyMaxSpeedRadiusFactor,
            bodyShapeRadiusFactor,
            Color.Lerp(primaryColor, secondaryColor, bodySecondaryMix),
            Color.Lerp(primaryColor, new Color(0.18f, 0.56f, 1f, 1f), 0.42f),
            bodyAlpha);

        ConfigureLayer(
            brightWisps,
            safeRadius,
            wispMinSizeRadiusFactor,
            wispMaxSizeRadiusFactor,
            wispMinSpeedRadiusFactor,
            wispMaxSpeedRadiusFactor,
            wispShapeRadiusFactor,
            Color.Lerp(new Color(0.72f, 0.96f, 1f, 1f), primaryColor, 0.35f),
            Color.Lerp(primaryColor, secondaryColor, wispSecondaryMix),
            wispAlpha);

        RestartClustered(
            cloudBody,
            safeRadius,
            bodyPocketCount,
            bodyPocketSpreadRadiusFactor,
            bodyMinParticleCount,
            bodyMaxParticleCount,
            0.28f);

        RestartClustered(
            brightWisps,
            safeRadius,
            wispPocketCount,
            wispPocketSpreadRadiusFactor,
            wispMinParticleCount,
            wispMaxParticleCount,
            0.46f);
    }

    public void Stop()
    {
        StopLayer(cloudBody);
        StopLayer(brightWisps);
    }

    private static void ConfigureLayer(
        ParticleSystem system,
        float radius,
        float minSizeFactor,
        float maxSizeFactor,
        float minSpeedFactor,
        float maxSpeedFactor,
        float shapeRadiusFactor,
        Color minColor,
        Color maxColor,
        float alpha)
    {
        if (system == null)
            return;

        float minSize = Mathf.Max(0.06f, radius * Mathf.Min(minSizeFactor, maxSizeFactor));
        float maxSize = Mathf.Max(minSize, radius * Mathf.Max(minSizeFactor, maxSizeFactor));
        float minSpeed = Mathf.Max(0f, radius * Mathf.Min(minSpeedFactor, maxSpeedFactor));
        float maxSpeed = Mathf.Max(minSpeed, radius * Mathf.Max(minSpeedFactor, maxSpeedFactor));

        ParticleSystem.MainModule main = system.main;
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);

        minColor.a *= alpha;
        maxColor.a *= alpha;
        main.startColor = new ParticleSystem.MinMaxGradient(minColor, maxColor);

        ParticleSystem.ShapeModule shape = system.shape;
        if (shape.enabled)
        {
            shape.radius = Mathf.Max(0.025f, radius * shapeRadiusFactor);
        }
    }

    private static void RestartClustered(
        ParticleSystem system,
        float radius,
        int pocketCount,
        float pocketSpreadRadiusFactor,
        int minParticleCount,
        int maxParticleCount,
        float verticalSpreadMultiplier)
    {
        if (system == null)
            return;

        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        int pockets = Mathf.Max(1, pocketCount);
        int minCount = Mathf.Max(1, Mathf.Min(minParticleCount, maxParticleCount));
        int maxCount = Mathf.Max(minCount, Mathf.Max(minParticleCount, maxParticleCount));
        int totalCount = Random.Range(minCount, maxCount + 1);

        Transform systemTransform = system.transform;
        Vector3 originalPosition = systemTransform.position;
        int remaining = totalCount;

        system.Play(true);

        for (int pocketIndex = 0; pocketIndex < pockets; pocketIndex++)
        {
            int pocketsLeft = pockets - pocketIndex;
            int emitCount =
                pocketIndex == pockets - 1
                    ? remaining
                    : Mathf.Max(1, remaining / pocketsLeft);

            remaining -= emitCount;

            Vector3 offset;

            if (pocketIndex == 0)
            {
                // Keep one dense pocket close to the ignition point so the
                // core still feels embedded inside the cloud.
                offset = Random.insideUnitSphere * radius * pocketSpreadRadiusFactor * 0.22f;
            }
            else
            {
                Vector2 planar = Random.insideUnitCircle;
                if (planar.sqrMagnitude < 0.0001f)
                {
                    planar = Vector2.right;
                }

                float distance =
                    radius *
                    pocketSpreadRadiusFactor *
                    Random.Range(0.42f, 1f);

                offset = new Vector3(
                    planar.x * distance,
                    Random.Range(-distance, distance) * verticalSpreadMultiplier,
                    planar.y * distance);
            }

            systemTransform.position = originalPosition + offset;
            system.Emit(emitCount);
        }

        systemTransform.position = originalPosition;
    }

    private static void StopLayer(ParticleSystem system)
    {
        if (system == null)
            return;

        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable()
    {
        Stop();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        bodyMinSizeRadiusFactor = Mathf.Max(0.01f, bodyMinSizeRadiusFactor);
        bodyMaxSizeRadiusFactor = Mathf.Max(bodyMinSizeRadiusFactor, bodyMaxSizeRadiusFactor);
        bodyMinSpeedRadiusFactor = Mathf.Max(0f, bodyMinSpeedRadiusFactor);
        bodyMaxSpeedRadiusFactor = Mathf.Max(bodyMinSpeedRadiusFactor, bodyMaxSpeedRadiusFactor);
        bodyShapeRadiusFactor = Mathf.Max(0f, bodyShapeRadiusFactor);
        bodyPocketCount = Mathf.Clamp(bodyPocketCount, 1, 5);
        bodyPocketSpreadRadiusFactor = Mathf.Max(0f, bodyPocketSpreadRadiusFactor);
        bodyMinParticleCount = Mathf.Max(1, bodyMinParticleCount);
        bodyMaxParticleCount = Mathf.Max(bodyMinParticleCount, bodyMaxParticleCount);

        wispMinSizeRadiusFactor = Mathf.Max(0.01f, wispMinSizeRadiusFactor);
        wispMaxSizeRadiusFactor = Mathf.Max(wispMinSizeRadiusFactor, wispMaxSizeRadiusFactor);
        wispMinSpeedRadiusFactor = Mathf.Max(0f, wispMinSpeedRadiusFactor);
        wispMaxSpeedRadiusFactor = Mathf.Max(wispMinSpeedRadiusFactor, wispMaxSpeedRadiusFactor);
        wispShapeRadiusFactor = Mathf.Max(0f, wispShapeRadiusFactor);
        wispPocketCount = Mathf.Clamp(wispPocketCount, 1, 6);
        wispPocketSpreadRadiusFactor = Mathf.Max(0f, wispPocketSpreadRadiusFactor);
        wispMinParticleCount = Mathf.Max(1, wispMinParticleCount);
        wispMaxParticleCount = Mathf.Max(wispMinParticleCount, wispMaxParticleCount);

        bodySecondaryMix = Mathf.Clamp01(bodySecondaryMix);
        wispSecondaryMix = Mathf.Clamp01(wispSecondaryMix);
        bodyAlpha = Mathf.Clamp01(bodyAlpha);
        wispAlpha = Mathf.Clamp01(wispAlpha);
    }
#endif
}
