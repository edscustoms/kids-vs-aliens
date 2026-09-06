using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class ElectricGrenadeCoreVFX : MonoBehaviour
{
    [Header("Core")]
    [SerializeField]
    private Renderer coreRenderer;

    [SerializeField, Min(0f)]
    private float pulseSpeed = 5f;

    [SerializeField, Range(0f, 0.25f)]
    private float scalePulse = 0.035f;

    [SerializeField, Range(0f, 1f)]
    private float emissionPulse = 0.10f;

    [Header("Arc Material")]
    [Tooltip("Optional. Leave empty to reuse the CoreEnergy material.")]
    [SerializeField]
    private Material arcMaterial;

    [Header("Pool")]
    [SerializeField, Min(1)]
    private int poolSize = 10;

    [SerializeField, Min(0)]
    private int minActiveArcs = 4;

    [SerializeField, Min(1)]
    private int maxActiveArcs = 7;

    [Header("Chamber Volume")]
    [Tooltip("Hard radial boundary. No generated lightning point can exceed this.")]
    [SerializeField, Min(0.001f)]
    private float chamberRadius = 0.018f;

    [Tooltip("Hard vertical boundary measured from CoreEnergy.")]
    [SerializeField, Min(0.001f)]
    private float chamberHalfHeight = 0.045f;

    [Tooltip("Shrinks the usable volume slightly so bloom/line width also stays visually inside.")]
    [SerializeField, Range(0.5f, 1f)]
    private float containment = 0.82f;

    [Tooltip("Optional local offset if CoreEnergy is not exactly centered in the chamber.")]
    [SerializeField]
    private Vector3 chamberCenterOffset = Vector3.zero;

    [Header("Arc Shape")]
    [SerializeField, Range(3, 12)]
    private int segments = 6;

    [SerializeField, Min(0.0001f)]
    private float arcWidth = 0.00075f;

    [SerializeField, Range(0f, 1f)]
    private float jitterStrength = 0.35f;

    [Tooltip("Shortest arc as a fraction of chamber radius.")]
    [SerializeField, Range(0.05f, 2f)]
    private float minArcLength = 0.30f;

    [Tooltip("Longest arc as a fraction of chamber radius.")]
    [SerializeField, Range(0.05f, 3f)]
    private float maxArcLength = 0.85f;

    [Header("Chaos")]
    [Tooltip("Chance that an arc begins close to the glowing core.")]
    [SerializeField, Range(0f, 1f)]
    private float coreArcChance = 0.55f;

    [Tooltip("How close core-origin arcs begin to the central core.")]
    [SerializeField, Range(0f, 1f)]
    private float coreStartRadius = 0.15f;

    [SerializeField, Min(0.01f)]
    private float arcRefreshInterval = 0.045f;

    [SerializeField, Min(0.01f)]
    private float arcCountRefreshInterval = 0.12f;

    [ColorUsage(true, true)]
    [SerializeField]
    private Color arcColor = new Color(0.3f, 0.85f, 1f, 1f);

    private Transform arcRoot;

    private LineRenderer[] arcPool;

    private Vector3[][] pointBuffers;

    private Vector3 baseScale;

    private MaterialPropertyBlock propertyBlock;

    private Color baseEmissionColor = Color.white;

    private bool hasEmissionProperty;

    private float arcTimer;
    private float countTimer;

    private int activeArcCount;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        if (coreRenderer == null)
        {
            coreRenderer = GetComponent<Renderer>();
        }

        baseScale = transform.localScale;

        CacheEmission();

        BuildPool();

        RandomizeActiveArcCount();

        RefreshArcs();
    }

    private void OnEnable()
    {
        arcTimer = 0f;
        countTimer = 0f;
    }

    private void OnDisable()
    {
        transform.localScale = baseScale;

        SetAllArcsActive(false);

        RestoreEmission();
    }

    private void Update()
    {
        UpdateCorePulse();

        arcTimer += Time.deltaTime;

        countTimer += Time.deltaTime;

        if (countTimer >= arcCountRefreshInterval)
        {
            countTimer = 0f;

            RandomizeActiveArcCount();
        }

        if (arcTimer >= arcRefreshInterval)
        {
            arcTimer = 0f;

            RefreshArcs();
        }
    }

    // =====================================================
    // CORE
    // =====================================================

    private void UpdateCorePulse()
    {
        float wave = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;

        float scaleMultiplier = Mathf.Lerp(1f - scalePulse, 1f + scalePulse, wave);

        transform.localScale = baseScale * scaleMultiplier;

        if (coreRenderer == null || !hasEmissionProperty)
        {
            return;
        }

        float emissionMultiplier = Mathf.Lerp(1f - emissionPulse, 1f + emissionPulse, wave);

        coreRenderer.GetPropertyBlock(propertyBlock);

        propertyBlock.SetColor(EmissionColorId, baseEmissionColor * emissionMultiplier);

        coreRenderer.SetPropertyBlock(propertyBlock);
    }

    // =====================================================
    // POOL
    // =====================================================

    private void BuildPool()
    {
        GameObject rootObject = new GameObject("RuntimeElectricArcPool");

        arcRoot = rootObject.transform;

        // IMPORTANT:
        //
        // Put the VFX coordinate system exactly at CoreEnergy,
        // but do NOT parent it to CoreEnergy itself.
        //
        // That means pulsing CoreEnergy scale does not distort
        // the lightning volume.

        Transform parent = transform.parent;

        arcRoot.SetParent(parent, false);

        arcRoot.localPosition = transform.localPosition;

        arcRoot.localRotation = transform.localRotation;

        arcRoot.localScale = Vector3.one;

        int safePoolSize = Mathf.Max(1, poolSize);

        int pointCount = Mathf.Max(4, segments + 1);

        arcPool = new LineRenderer[safePoolSize];

        pointBuffers = new Vector3[safePoolSize][];

        Material materialToUse = arcMaterial;

        if (materialToUse == null && coreRenderer != null)
        {
            materialToUse = coreRenderer.sharedMaterial;
        }

        for (int i = 0; i < safePoolSize; i++)
        {
            GameObject arcObject = new GameObject($"CoreArc_{i + 1:00}");

            arcObject.transform.SetParent(arcRoot, false);

            LineRenderer line = arcObject.AddComponent<LineRenderer>();

            line.useWorldSpace = false;

            line.loop = false;

            line.alignment = LineAlignment.View;

            line.textureMode = LineTextureMode.Stretch;

            line.generateLightingData = false;

            line.shadowCastingMode = ShadowCastingMode.Off;

            line.receiveShadows = false;

            line.numCornerVertices = 1;

            line.numCapVertices = 1;

            line.positionCount = pointCount;

            line.startWidth = arcWidth;

            line.endWidth = arcWidth * 0.35f;

            line.startColor = arcColor;

            line.endColor = arcColor;

            if (materialToUse != null)
            {
                line.sharedMaterial = materialToUse;
            }

            pointBuffers[i] = new Vector3[pointCount];

            arcPool[i] = line;

            arcObject.SetActive(false);
        }
    }

    // =====================================================
    // ARC COUNT
    // =====================================================

    private void RandomizeActiveArcCount()
    {
        if (arcPool == null || arcPool.Length == 0)
        {
            return;
        }

        int minimum = Mathf.Clamp(minActiveArcs, 0, arcPool.Length);

        int maximum = Mathf.Clamp(maxActiveArcs, minimum, arcPool.Length);

        activeArcCount = Random.Range(minimum, maximum + 1);

        for (int i = 0; i < arcPool.Length; i++)
        {
            arcPool[i].gameObject.SetActive(i < activeArcCount);
        }
    }

    // =====================================================
    // ELECTRIC CHAOS
    // =====================================================

    private void RefreshArcs()
    {
        if (arcPool == null || arcRoot == null)
        {
            return;
        }

        for (int i = 0; i < activeArcCount; i++)
        {
            Vector3 start;

            if (Random.value < coreArcChance)
            {
                start = RandomPointNearCore();
            }
            else
            {
                start = RandomPointInsideChamber();
            }

            float length = Random.Range(minArcLength, maxArcLength) * chamberRadius;

            Vector3 direction = Random.onUnitSphere;

            // Keep arcs visually interesting in a tall chamber.
            direction.y *= 0.65f;

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.right;
            }

            direction.Normalize();

            Vector3 end = start + direction * length;

            end = ClampInsideChamber(end);

            BuildArc(arcPool[i], pointBuffers[i], start, end);
        }
    }

    private Vector3 RandomPointNearCore()
    {
        float usableRadius = GetUsableRadius();

        float radius = usableRadius * coreStartRadius;

        Vector3 point = Random.insideUnitSphere * radius;

        // Slightly taller distribution than spherical.
        point.y *= 1.5f;

        point += chamberCenterOffset;

        return ClampInsideChamber(point);
    }

    private Vector3 RandomPointInsideChamber()
    {
        float usableRadius = GetUsableRadius();

        float usableHalfHeight = GetUsableHalfHeight();

        float angle = Random.Range(0f, Mathf.PI * 2f);

        // sqrt gives even area distribution
        // instead of bunching everything in the center.

        float radius = Mathf.Sqrt(Random.value) * usableRadius;

        float y = Random.Range(-usableHalfHeight, usableHalfHeight);

        return chamberCenterOffset
            + new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
    }

    private void BuildArc(LineRenderer line, Vector3[] points, Vector3 start, Vector3 end)
    {
        int lastIndex = points.Length - 1;

        Vector3 axis = end - start;

        float arcDistance = axis.magnitude;

        Vector3 direction = arcDistance > 0.00001f ? axis / arcDistance : Vector3.right;

        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)lastIndex;

            Vector3 point = Vector3.Lerp(start, end, t);

            if (i != 0 && i != lastIndex)
            {
                float middleStrength = Mathf.Sin(Mathf.PI * t);

                Vector3 random = Random.insideUnitSphere;

                // Remove most jitter along the arc direction.
                // This makes actual lightning kinks instead of
                // simply stretching/shrinking the line.

                random -= direction * Vector3.Dot(random, direction);

                if (random.sqrMagnitude > 0.0001f)
                {
                    random.Normalize();
                }

                float jitterDistance = arcDistance * jitterStrength * 0.35f;

                point += random * jitterDistance * middleStrength;
            }

            // HARD CONTAINMENT.
            //
            // This happens AFTER jitter.
            // Every visible point is forced inside the chamber.

            points[i] = ClampInsideChamber(point);
        }

        line.SetPositions(points);
    }

    // =====================================================
    // HARD VOLUME CONTAINMENT
    // =====================================================

    private Vector3 ClampInsideChamber(Vector3 point)
    {
        Vector3 local = point - chamberCenterOffset;

        float usableRadius = GetUsableRadius();

        float usableHalfHeight = GetUsableHalfHeight();

        // Vertical clamp.

        local.y = Mathf.Clamp(local.y, -usableHalfHeight, usableHalfHeight);

        // Radial cylinder clamp around LOCAL zero.

        Vector2 radial = new Vector2(local.x, local.z);

        float sqrRadius = usableRadius * usableRadius;

        if (radial.sqrMagnitude > sqrRadius)
        {
            radial.Normalize();

            radial *= usableRadius;

            local.x = radial.x;

            local.z = radial.y;
        }

        return local + chamberCenterOffset;
    }

    private float GetUsableRadius()
    {
        return chamberRadius * containment;
    }

    private float GetUsableHalfHeight()
    {
        return chamberHalfHeight * containment;
    }

    // =====================================================
    // EMISSION
    // =====================================================

    private void CacheEmission()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (coreRenderer == null || coreRenderer.sharedMaterial == null)
        {
            return;
        }

        Material material = coreRenderer.sharedMaterial;

        hasEmissionProperty = material.HasProperty(EmissionColorId);

        if (hasEmissionProperty)
        {
            baseEmissionColor = material.GetColor(EmissionColorId);
        }
    }

    private void RestoreEmission()
    {
        if (coreRenderer == null || !hasEmissionProperty)
        {
            return;
        }

        coreRenderer.GetPropertyBlock(propertyBlock);

        propertyBlock.SetColor(EmissionColorId, baseEmissionColor);

        coreRenderer.SetPropertyBlock(propertyBlock);
    }

    private void SetAllArcsActive(bool active)
    {
        if (arcPool == null)
            return;

        for (int i = 0; i < arcPool.Length; i++)
        {
            if (arcPool[i] != null)
            {
                arcPool[i].gameObject.SetActive(active);
            }
        }
    }

#if UNITY_EDITOR

    // =====================================================
    // DEBUG VOLUME
    //
    // Select CoreEnergy in Scene view and Unity will show
    // exactly where lightning is allowed to exist.
    // =====================================================

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 oldMatrix = Gizmos.matrix;

        Transform parent = transform.parent;

        if (parent != null)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(
                parent.TransformPoint(transform.localPosition),
                parent.rotation * transform.localRotation,
                parent.lossyScale
            );

            Gizmos.matrix = matrix;
        }
        else
        {
            Gizmos.matrix = transform.localToWorldMatrix;
        }

        Gizmos.color = new Color(0f, 1f, 1f, 0.65f);

        float radius = chamberRadius * containment;

        float halfHeight = chamberHalfHeight * containment;

        const int steps = 32;

        Vector3 previousTop = Vector3.zero;

        Vector3 previousBottom = Vector3.zero;

        for (int i = 0; i <= steps; i++)
        {
            float angle = i / (float)steps * Mathf.PI * 2f;

            Vector3 top =
                chamberCenterOffset
                + new Vector3(Mathf.Cos(angle) * radius, halfHeight, Mathf.Sin(angle) * radius);

            Vector3 bottom =
                chamberCenterOffset
                + new Vector3(Mathf.Cos(angle) * radius, -halfHeight, Mathf.Sin(angle) * radius);

            if (i > 0)
            {
                Gizmos.DrawLine(previousTop, top);

                Gizmos.DrawLine(previousBottom, bottom);
            }

            if (i == 0 || i == steps / 4 || i == steps / 2 || i == steps * 3 / 4)
            {
                Gizmos.DrawLine(top, bottom);
            }

            previousTop = top;

            previousBottom = bottom;
        }

        Gizmos.matrix = oldMatrix;
    }

#endif
}
