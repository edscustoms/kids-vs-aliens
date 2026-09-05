using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class ElectricArcVFX : MonoBehaviour
{
    private const int MaxBranches = 2;
    private const int BranchPointCount = 7;

    [SerializeField]
    private LineRenderer lineRenderer;

    [Header("Shape")]
    [SerializeField, Range(4, 28)]
    private int pointCount = 18;

    [SerializeField, Min(0f)]
    private float jitter = 0.035f;

    [SerializeField, Range(0.05f, 1f)]
    private float endWidthFactor = 0.24f;

    [Header("Lightning Shape")]
    [SerializeField, Min(0f)]
    private float pathWanderMultiplier = 5.25f;

    [SerializeField, Range(0f, 1f)]
    private float directionPersistence = 0.58f;

    [SerializeField, Range(0, MaxBranches)]
    private int maxBranches = MaxBranches;

    [SerializeField, Range(0f, 1f)]
    private float branchChance = 0.82f;

    [SerializeField]
    private Vector2 branchLengthRange = new Vector2(0.18f, 0.38f);

    [SerializeField, Range(0.05f, 1f)]
    private float branchWidthFactor = 0.52f;

    [SerializeField, Min(0f)]
    private float branchWanderMultiplier = 0.75f;

    [Header("Timing")]
    [SerializeField, Min(0.005f)]
    private float refreshInterval = 0.026f;

    [SerializeField, Min(0.01f)]
    private float defaultLifetime = 0.12f;

    [Header("Width")]
    [SerializeField, Min(0.001f)]
    private float baseWidth = 0.018f;

    [SerializeField]
    private bool fadeOut = true;

    private Vector3[] points = new Vector3[0];
    private readonly LineRenderer[] branchRenderers = new LineRenderer[MaxBranches];
    private readonly Vector3[][] branchPoints = new Vector3[MaxBranches][];

    private bool isPlaying;
    private Vector3 currentStart;
    private Vector3 currentEnd;
    private Color currentColor;
    private float currentLifetime;
    private float currentWidthMultiplier = 1f;
    private float currentJitterMultiplier = 1f;
    private float currentRefreshInterval;
    private float nextRefreshTime;
    private float endTime;

    private void Awake()
    {
        CacheReferences();
        EnsurePointBuffer();
        EnsureBranchRenderers();
        Hide();
    }

    private void Update()
    {
        if (!isPlaying || lineRenderer == null)
            return;

        float remaining = endTime - Time.time;

        if (remaining <= 0f)
        {
            Hide();
            return;
        }

        if (Time.time >= nextRefreshTime)
        {
            GenerateArc();
            nextRefreshTime = Time.time + currentRefreshInterval;
        }

        ApplyVisualState(
            remaining /
            Mathf.Max(0.001f, currentLifetime));
    }

    public void Play(
        Vector3 start,
        Vector3 end,
        Color color)
    {
        Play(
            start,
            end,
            color,
            defaultLifetime,
            1f,
            1f,
            refreshInterval);
    }

    public void Play(
        Vector3 start,
        Vector3 end,
        Color color,
        float lifetime,
        float widthMultiplier,
        float jitterMultiplier,
        float refreshEvery)
    {
        CacheReferences();

        if (lineRenderer == null)
            return;

        EnsurePointBuffer();
        EnsureBranchRenderers();

        currentStart = start;
        currentEnd = end;
        currentColor = color;
        currentLifetime = Mathf.Max(0.01f, lifetime);
        currentWidthMultiplier = Mathf.Max(0.01f, widthMultiplier);
        currentJitterMultiplier = Mathf.Max(0f, jitterMultiplier);
        currentRefreshInterval = Mathf.Max(
            0.005f,
            refreshEvery > 0f
                ? refreshEvery
                : refreshInterval);

        isPlaying = true;
        endTime = Time.time + currentLifetime;
        nextRefreshTime = Time.time;

        lineRenderer.enabled = true;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = points.Length;

        GenerateArc();
        ApplyVisualState(1f);
    }

    public void Hide()
    {
        isPlaying = false;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }

        for (int i = 0; i < branchRenderers.Length; i++)
        {
            if (branchRenderers[i] != null)
            {
                branchRenderers[i].enabled = false;
            }
        }
    }

    private void GenerateArc()
    {
        Vector3 displacement = currentEnd - currentStart;
        float length = displacement.magnitude;

        Vector3 forward =
            length > 0.0001f
                ? displacement / length
                : Vector3.forward;

        BuildPerpendicularBasis(
            forward,
            out Vector3 basisA,
            out Vector3 basisB);

        float baseOffsetScale =
            jitter *
            currentJitterMultiplier *
            Mathf.Max(0.25f, length);

        // V3 jitter was deliberately tiny to remove the chunky zig-zags.
        // Here it becomes the seed for a persistent random walk instead of
        // independent offsets around a straight line. This makes the bolt
        // genuinely wander through 3D while still landing exactly on its end.
        float wanderStep =
            baseOffsetScale *
            Mathf.Max(0f, pathWanderMultiplier);

        Vector2 walk = Vector2.zero;
        Vector2 walkDirection = Random.insideUnitCircle.normalized;

        points[0] = currentStart;

        for (int i = 1; i < points.Length - 1; i++)
        {
            float t = i / (float)(points.Length - 1);

            Vector2 randomDirection = Random.insideUnitCircle;

            if (randomDirection.sqrMagnitude < 0.0001f)
            {
                randomDirection = Vector2.right;
            }

            randomDirection.Normalize();

            walkDirection = Vector2.Lerp(
                randomDirection,
                walkDirection,
                Mathf.Clamp01(directionPersistence));

            if (walkDirection.sqrMagnitude < 0.0001f)
            {
                walkDirection = randomDirection;
            }

            walkDirection.Normalize();

            walk +=
                walkDirection *
                wanderStep *
                Random.Range(0.72f, 1.28f);

            // Pull the path back toward the destination as it approaches the
            // end. The resulting path can travel in strong random directions,
            // but never misses the authored endpoint.
            float restore = Mathf.Lerp(0.02f, 0.42f, t * t);
            walk = Vector2.Lerp(walk, Vector2.zero, restore);

            float envelope = Mathf.Sin(t * Mathf.PI);

            Vector3 point = Vector3.Lerp(
                currentStart,
                currentEnd,
                t);

            point +=
                (basisA * walk.x + basisB * walk.y) *
                envelope;

            // Small high-frequency kink on top of the larger wandering path.
            Vector2 micro = Random.insideUnitCircle * baseOffsetScale * 0.42f;

            point +=
                (basisA * micro.x + basisB * micro.y) *
                envelope;

            points[i] = point;
        }

        points[points.Length - 1] = currentEnd;
        lineRenderer.SetPositions(points);

        GenerateBranches(
            forward,
            basisA,
            basisB,
            length,
            baseOffsetScale);
    }

    private void GenerateBranches(
        Vector3 mainForward,
        Vector3 basisA,
        Vector3 basisB,
        float mainLength,
        float baseOffsetScale)
    {
        int allowedBranches = Mathf.Clamp(maxBranches, 0, MaxBranches);

        for (int branchIndex = 0; branchIndex < MaxBranches; branchIndex++)
        {
            LineRenderer branch = branchRenderers[branchIndex];

            if (branch == null)
                continue;

            bool shouldShow =
                branchIndex < allowedBranches &&
                Random.value <= branchChance &&
                points.Length >= 6 &&
                mainLength > 0.15f;

            if (!shouldShow)
            {
                branch.enabled = false;
                continue;
            }

            int minOrigin = Mathf.Max(2, Mathf.RoundToInt(points.Length * 0.22f));
            int maxOrigin = Mathf.Min(points.Length - 3, Mathf.RoundToInt(points.Length * 0.82f));
            int originIndex = Random.Range(minOrigin, maxOrigin + 1);

            Vector3 origin = points[originIndex];

            Vector3 tangent =
                (points[Mathf.Min(originIndex + 1, points.Length - 1)] -
                 points[Mathf.Max(originIndex - 1, 0)]).normalized;

            if (tangent.sqrMagnitude < 0.0001f)
            {
                tangent = mainForward;
            }

            float sideA = Random.Range(-1f, 1f);
            float sideB = Random.Range(-1f, 1f);

            Vector3 branchDirection =
                tangent * Random.Range(0.12f, 0.55f) +
                basisA * sideA * Random.Range(0.75f, 1.35f) +
                basisB * sideB * Random.Range(0.75f, 1.35f);

            if (branchDirection.sqrMagnitude < 0.0001f)
            {
                branchDirection = basisA;
            }

            branchDirection.Normalize();

            float minLength = Mathf.Max(0.05f, Mathf.Min(branchLengthRange.x, branchLengthRange.y));
            float maxLength = Mathf.Max(minLength, Mathf.Max(branchLengthRange.x, branchLengthRange.y));
            float branchLength =
                mainLength *
                Random.Range(minLength, maxLength);

            Vector3 branchEnd =
                origin +
                branchDirection * branchLength;

            GenerateBranchPath(
                branchIndex,
                origin,
                branchEnd,
                baseOffsetScale);

            branch.enabled = true;
        }
    }

    private void GenerateBranchPath(
        int branchIndex,
        Vector3 start,
        Vector3 end,
        float mainOffsetScale)
    {
        LineRenderer branch = branchRenderers[branchIndex];
        Vector3[] buffer = branchPoints[branchIndex];

        Vector3 displacement = end - start;
        float length = displacement.magnitude;

        Vector3 forward =
            length > 0.0001f
                ? displacement / length
                : Vector3.forward;

        BuildPerpendicularBasis(
            forward,
            out Vector3 basisA,
            out Vector3 basisB);

        float localWander =
            Mathf.Max(
                mainOffsetScale * 0.9f,
                length * 0.055f) *
            Mathf.Max(0f, branchWanderMultiplier);

        Vector2 walk = Vector2.zero;
        Vector2 direction = Random.insideUnitCircle.normalized;

        buffer[0] = start;

        for (int i = 1; i < buffer.Length - 1; i++)
        {
            float t = i / (float)(buffer.Length - 1);

            Vector2 randomDirection = Random.insideUnitCircle;

            if (randomDirection.sqrMagnitude < 0.0001f)
            {
                randomDirection = Vector2.up;
            }

            randomDirection.Normalize();

            direction = Vector2.Lerp(
                randomDirection,
                direction,
                0.42f).normalized;

            walk +=
                direction *
                localWander *
                Random.Range(0.7f, 1.3f);

            walk = Vector2.Lerp(
                walk,
                Vector2.zero,
                Mathf.Lerp(0.02f, 0.34f, t * t));

            float envelope = Mathf.Sin(t * Mathf.PI);

            Vector3 point = Vector3.Lerp(start, end, t);

            point +=
                (basisA * walk.x + basisB * walk.y) *
                envelope;

            buffer[i] = point;
        }

        buffer[buffer.Length - 1] = end;

        branch.positionCount = buffer.Length;
        branch.SetPositions(buffer);
    }

    private void ApplyVisualState(
        float life01)
    {
        float fade =
            fadeOut
                ? Smooth01(Mathf.Clamp01(life01))
                : 1f;

        float width =
            baseWidth *
            currentWidthMultiplier *
            Mathf.Lerp(0.22f, 1f, fade);

        lineRenderer.startWidth = width;
        lineRenderer.endWidth =
            width *
            Mathf.Clamp(
                endWidthFactor,
                0.05f,
                1f);

        Color startColor = currentColor;
        Color endColor = currentColor;

        startColor.a *= fade;
        endColor.a *= fade * 0.55f;

        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;

        for (int i = 0; i < branchRenderers.Length; i++)
        {
            LineRenderer branch = branchRenderers[i];

            if (branch == null || !branch.enabled)
                continue;

            float branchWidth =
                width *
                Mathf.Clamp(branchWidthFactor, 0.05f, 1f);

            branch.startWidth = branchWidth;
            branch.endWidth = branchWidth * 0.12f;

            Color branchStart = currentColor;
            Color branchEnd = currentColor;

            branchStart.a *= fade * 0.8f;
            branchEnd.a *= fade * 0.22f;

            branch.startColor = branchStart;
            branch.endColor = branchEnd;
        }
    }

    private void EnsureBranchRenderers()
    {
        if (lineRenderer == null)
            return;

        for (int i = 0; i < MaxBranches; i++)
        {
            if (branchPoints[i] == null || branchPoints[i].Length != BranchPointCount)
            {
                branchPoints[i] = new Vector3[BranchPointCount];
            }

            if (branchRenderers[i] != null)
                continue;

            Transform existing = transform.Find($"RuntimeBranch_{i + 1:00}");
            GameObject branchObject;

            if (existing != null)
            {
                branchObject = existing.gameObject;
            }
            else
            {
                branchObject = new GameObject($"RuntimeBranch_{i + 1:00}");
                branchObject.transform.SetParent(transform, false);
            }

            LineRenderer branch = branchObject.GetComponent<LineRenderer>();

            if (branch == null)
            {
                branch = branchObject.AddComponent<LineRenderer>();
            }

            CopyLineRendererPresentation(lineRenderer, branch);
            branch.positionCount = BranchPointCount;
            branch.enabled = false;

            branchRenderers[i] = branch;
        }
    }

    private static void CopyLineRendererPresentation(
        LineRenderer source,
        LineRenderer target)
    {
        target.sharedMaterial = source.sharedMaterial;
        target.useWorldSpace = true;
        target.alignment = source.alignment;
        target.textureMode = source.textureMode;
        target.numCornerVertices = 0;
        target.numCapVertices = 0;
        target.shadowCastingMode = source.shadowCastingMode;
        target.receiveShadows = source.receiveShadows;
        target.lightProbeUsage = source.lightProbeUsage;
        target.reflectionProbeUsage = source.reflectionProbeUsage;
        target.motionVectorGenerationMode = source.motionVectorGenerationMode;
    }

    private static void BuildPerpendicularBasis(
        Vector3 forward,
        out Vector3 basisA,
        out Vector3 basisB)
    {
        basisA = Vector3.Cross(forward, Vector3.up);

        if (basisA.sqrMagnitude < 0.0001f)
        {
            basisA = Vector3.Cross(forward, Vector3.right);
        }

        basisA.Normalize();
        basisB = Vector3.Cross(forward, basisA).normalized;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void CacheReferences()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }
    }

    private void EnsurePointBuffer()
    {
        pointCount =
            Mathf.Clamp(
                pointCount,
                4,
                28);

        if (points == null ||
            points.Length != pointCount)
        {
            points =
                new Vector3[pointCount];
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
        EnsurePointBuffer();

        refreshInterval =
            Mathf.Max(
                0.005f,
                refreshInterval);

        defaultLifetime =
            Mathf.Max(
                0.01f,
                defaultLifetime);

        baseWidth =
            Mathf.Max(
                0.001f,
                baseWidth);

        maxBranches =
            Mathf.Clamp(
                maxBranches,
                0,
                MaxBranches);

        branchChance = Mathf.Clamp01(branchChance);
        branchWidthFactor = Mathf.Clamp(branchWidthFactor, 0.05f, 1f);
    }
#endif
}
