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

        ApplyVisualState(remaining / Mathf.Max(0.001f, currentLifetime));
    }

    public void Play(
        Vector3 start,
        Vector3 end,
        Color color)
    {
        Play(start, end, color, defaultLifetime, 1f, 1f, refreshInterval);
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
        currentRefreshInterval = Mathf.Max(0.005f, refreshEvery > 0f ? refreshEvery : refreshInterval);

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

        BuildPerpendicularBasis(forward, out Vector3 basisA, out Vector3 basisB);

        points[0] = currentStart;
        points[points.Length - 1] = currentEnd;

        float baseAmplitude =
            jitter *
            currentJitterMultiplier *
            Mathf.Max(0.12f, length) *
            Mathf.Max(0.75f, pathWanderMultiplier);

        float roughness = Mathf.Lerp(0.38f, 0.68f, directionPersistence);
        float asymmetry = Random.Range(-1f, 1f);

        GenerateFractalPath(
            points,
            0,
            points.Length - 1,
            currentStart,
            currentEnd,
            basisA,
            basisB,
            baseAmplitude,
            roughness,
            asymmetry);

        AddMicroKinks(points, basisA, basisB, baseAmplitude * 0.12f);

        lineRenderer.SetPositions(points);

        GenerateBranches(forward, basisA, basisB, length, baseAmplitude);
    }

    private void GenerateBranches(
        Vector3 mainForward,
        Vector3 basisA,
        Vector3 basisB,
        float mainLength,
        float baseAmplitude)
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

            int minOrigin = Mathf.Max(2, Mathf.RoundToInt(points.Length * 0.18f));
            int maxOrigin = Mathf.Min(points.Length - 3, Mathf.RoundToInt(points.Length * 0.84f));
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
                tangent * Random.Range(0.05f, 0.28f) +
                basisA * sideA * Random.Range(0.9f, 1.4f) +
                basisB * sideB * Random.Range(0.9f, 1.4f);

            if (branchDirection.sqrMagnitude < 0.0001f)
            {
                branchDirection = basisA;
            }

            branchDirection.Normalize();

            float minLength = Mathf.Max(0.05f, Mathf.Min(branchLengthRange.x, branchLengthRange.y));
            float maxLength = Mathf.Max(minLength, Mathf.Max(branchLengthRange.x, branchLengthRange.y));
            float branchLength = mainLength * Random.Range(minLength, maxLength);

            Vector3 branchEnd = origin + branchDirection * branchLength;

            GenerateBranchPath(branchIndex, origin, branchEnd, baseAmplitude * Mathf.Max(0.25f, branchWanderMultiplier));
            branch.enabled = true;
        }
    }

    private void GenerateBranchPath(
        int branchIndex,
        Vector3 start,
        Vector3 end,
        float mainAmplitude)
    {
        LineRenderer branch = branchRenderers[branchIndex];
        Vector3[] buffer = branchPoints[branchIndex];

        if (branch == null || buffer == null || buffer.Length < 2)
            return;

        Vector3 displacement = end - start;
        float length = displacement.magnitude;

        Vector3 forward =
            length > 0.0001f
                ? displacement / length
                : Vector3.forward;

        BuildPerpendicularBasis(forward, out Vector3 basisA, out Vector3 basisB);

        buffer[0] = start;
        buffer[buffer.Length - 1] = end;

        float roughness = Mathf.Lerp(0.32f, 0.6f, directionPersistence * 0.8f + 0.1f);
        float amplitude = Mathf.Max(mainAmplitude * 0.55f, length * 0.08f);

        GenerateFractalPath(
            buffer,
            0,
            buffer.Length - 1,
            start,
            end,
            basisA,
            basisB,
            amplitude,
            roughness,
            Random.Range(-1f, 1f));

        AddMicroKinks(buffer, basisA, basisB, amplitude * 0.1f);

        branch.positionCount = buffer.Length;
        branch.SetPositions(buffer);
    }

    private static void GenerateFractalPath(
        Vector3[] buffer,
        int startIndex,
        int endIndex,
        Vector3 start,
        Vector3 end,
        Vector3 basisA,
        Vector3 basisB,
        float amplitude,
        float roughness,
        float asymmetry)
    {
        if (endIndex - startIndex <= 1)
        {
            return;
        }

        int midIndex = (startIndex + endIndex) / 2;
        float t = midIndex / (float)(buffer.Length - 1);

        Vector3 midpoint = Vector3.Lerp(start, end, 0.5f);

        Vector2 offset2 = Random.insideUnitCircle;
        if (offset2.sqrMagnitude < 0.0001f)
        {
            offset2 = Vector2.right;
        }

        offset2.Normalize();
        offset2.x += asymmetry * 0.45f;
        if (offset2.sqrMagnitude < 0.0001f)
        {
            offset2 = Vector2.right;
        }
        offset2.Normalize();

        float envelope = Mathf.Sin(t * Mathf.PI);
        float shapedAmplitude = amplitude * Mathf.Lerp(0.85f, 1.15f, Random.value) * envelope;

        midpoint += (basisA * offset2.x + basisB * offset2.y) * shapedAmplitude;
        buffer[midIndex] = midpoint;

        float childAsymmetryA = Mathf.Lerp(asymmetry, Random.Range(-1f, 1f), 0.45f);
        float childAsymmetryB = Mathf.Lerp(asymmetry, Random.Range(-1f, 1f), 0.45f);
        float nextAmplitude = amplitude * Mathf.Clamp01(roughness);

        GenerateFractalPath(buffer, startIndex, midIndex, start, midpoint, basisA, basisB, nextAmplitude, roughness, childAsymmetryA);
        GenerateFractalPath(buffer, midIndex, endIndex, midpoint, end, basisA, basisB, nextAmplitude, roughness, childAsymmetryB);
    }

    private static void AddMicroKinks(
        Vector3[] buffer,
        Vector3 basisA,
        Vector3 basisB,
        float microAmplitude)
    {
        if (buffer == null || buffer.Length < 3 || microAmplitude <= 0f)
            return;

        for (int i = 1; i < buffer.Length - 1; i++)
        {
            float t = i / (float)(buffer.Length - 1);
            float envelope = Mathf.Sin(t * Mathf.PI);
            Vector2 micro = Random.insideUnitCircle * microAmplitude * Random.Range(0.45f, 1f);
            buffer[i] += (basisA * micro.x + basisB * micro.y) * envelope;
        }
    }

    private void ApplyVisualState(float life01)
    {
        float fade = fadeOut ? Smooth01(Mathf.Clamp01(life01)) : 1f;

        float width =
            baseWidth *
            currentWidthMultiplier *
            Mathf.Lerp(0.22f, 1f, fade);

        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width * Mathf.Clamp(endWidthFactor, 0.05f, 1f);

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

            float branchWidth = width * Mathf.Clamp(branchWidthFactor, 0.05f, 1f);

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

    private static void CopyLineRendererPresentation(LineRenderer source, LineRenderer target)
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

    private static void BuildPerpendicularBasis(Vector3 forward, out Vector3 basisA, out Vector3 basisB)
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
        pointCount = Mathf.Clamp(pointCount, 4, 28);

        if (points == null || points.Length != pointCount)
        {
            points = new Vector3[pointCount];
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

        refreshInterval = Mathf.Max(0.005f, refreshInterval);
        defaultLifetime = Mathf.Max(0.01f, defaultLifetime);
        baseWidth = Mathf.Max(0.001f, baseWidth);
        maxBranches = Mathf.Clamp(maxBranches, 0, MaxBranches);
        branchChance = Mathf.Clamp01(branchChance);
        branchWidthFactor = Mathf.Clamp(branchWidthFactor, 0.05f, 1f);
    }
#endif
}
