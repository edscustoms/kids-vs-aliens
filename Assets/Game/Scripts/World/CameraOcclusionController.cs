using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class CameraOcclusionController : MonoBehaviour
{
    private const int ContextSampleCount = 10;
    private const int AmySampleCount = 5;
    private const int HitBufferSize = 64;

    private static readonly int LineColorId = Shader.PropertyToID("_LineColor");

    private static readonly int DashLengthPixelsId = Shader.PropertyToID("_DashLengthPixels");

    private static readonly int DashFillId = Shader.PropertyToID("_DashFill");

    [Header("References")]
    [SerializeField]
    private Transform player;

    [SerializeField]
    private Camera gameplayCamera;

    [Header("Visibility Area")]
    [SerializeField]
    private float footprintRadius = 1.5f;

    [SerializeField]
    private float visibilityHeight = 1.0f;

    [Tooltip("Geometry completely below this height relative to Amy is preserved.")]
    [SerializeField]
    private float preserveBelowHeight = 0.45f;

    [Header("Occlusion Detection")]
    [Tooltip("Main rule: how many of the 10 context samples must be blocked.")]
    [Range(1, ContextSampleCount)]
    [SerializeField]
    private int requiredBlockedSamples = 9;

    [Tooltip("Emergency Amy rule: how many of the 5 Amy-body samples must be blocked.")]
    [Range(1, AmySampleCount)]
    [SerializeField]
    private int requiredAmyBlockedSamples = 4;

    [Tooltip(
        "When the main 9/10 rule triggers, a logical occluder must appear "
            + "in at least this many context samples to join the fade set."
    )]
    [Range(1, ContextSampleCount)]
    [SerializeField]
    private int rendererJoinMinimumSamples = 2;

    [Tooltip(
        "When the Amy-body rule triggers, a logical occluder must cover "
            + "at least this many Amy samples to join the fade set."
    )]
    [Range(1, AmySampleCount)]
    [SerializeField]
    private int rendererJoinMinimumAmySamples = 2;

    [SerializeField]
    private LayerMask occlusionMask = ~0;

    [Header("Animated Fade")]
    [Tooltip("Final visibility of an occluding object. 0 = fully invisible.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float hiddenVisibility = 0.0f;

    [Tooltip("Seconds to fade an obstruction away.")]
    [Min(0.01f)]
    [SerializeField]
    private float fadeOutDuration = 0.16f;

    [Tooltip("Seconds to restore an obstruction.")]
    [Min(0.01f)]
    [SerializeField]
    private float fadeInDuration = 0.22f;

    [Tooltip("Keeps blockers faded briefly after detection clears.")]
    [Min(0f)]
    [SerializeField]
    private float occlusionHold = 0.16f;

    [Header("Occlusion Lines")]
    [SerializeField]
    private bool showOcclusionLines = true;

    [Tooltip(
        "Subtle structural guide color. " + "This does not modify the object's real material/color."
    )]
    [SerializeField]
    private Color occlusionLineColor = new Color(0.72f, 0.80f, 0.95f, 0.30f);

    [Tooltip("Lines start appearing once the object has faded below this visibility.")]
    [Range(0.05f, 1f)]
    [SerializeField]
    private float lineStartFade = 0.75f;

    [Tooltip("Dash length in screen pixels.")]
    [Range(2f, 64f)]
    [SerializeField]
    private float dashLengthPixels = 14f;

    [Tooltip("How much of each dash segment remains visible.")]
    [Range(0.05f, 0.95f)]
    [SerializeField]
    private float dashFill = 0.55f;

    [Tooltip(
        "Minimum angle between adjacent mesh faces that counts as a "
            + "structural edge. Internal triangle diagonals on flat "
            + "ProBuilder faces are ignored."
    )]
    [Range(1f, 89f)]
    [SerializeField]
    private float structuralEdgeAngle = 12f;

    private CharacterController playerController;

    private Material occlusionLineMaterial;
    private MaterialPropertyBlock occlusionLineProperties;

    // Collider -> logical occlusion object.
    private readonly Dictionary<Collider, OcclusionTarget> colliderToTarget =
        new Dictionary<Collider, OcclusionTarget>();

    // Root transform -> one logical occlusion object.
    private readonly Dictionary<Transform, OcclusionTarget> targetByRoot =
        new Dictionary<Transform, OcclusionTarget>();

    // Renderer state stays per-renderer because every child renderer
    // still needs its own materials/fade/structural line mesh.
    private readonly Dictionary<Renderer, OcclusionState> rendererStates =
        new Dictionary<Renderer, OcclusionState>();

    // Metrics are now tracked per LOGICAL OCCLUDER instead of
    // per individual mesh renderer.
    private readonly Dictionary<OcclusionTarget, FrameMetrics> frameMetrics =
        new Dictionary<OcclusionTarget, FrameMetrics>();

    // Prevent one logical object from counting more than once
    // during a single ray.
    private readonly HashSet<OcclusionTarget> targetsHitThisRay = new HashSet<OcclusionTarget>();

    // Individual renderers currently being visually faded.
    private readonly HashSet<Renderer> activeFadeRenderers = new HashSet<Renderer>();

    private readonly List<Renderer> activeFadeBuffer = new List<Renderer>();

    private readonly Vector3[] samplePositions = new Vector3[ContextSampleCount];

    private readonly Vector3[] amySamplePositions = new Vector3[AmySampleCount];

    private readonly RaycastHit[] hitBuffer = new RaycastHit[HitBufferSize];

    public static CameraOcclusionController Active { get; private set; }

    // =====================================================
    // LOGICAL OCCLUSION TARGET
    // =====================================================

    private sealed class OcclusionTarget
    {
        public Renderer[] renderers;

        // Cached highest point across every renderer in the group.
        public float maxWorldY;
    }

    private sealed class OcclusionState
    {
        public float currentFade = 1f;

        public float lastRequestedTime = float.NegativeInfinity;

        public bool isOccluding;

        public Material[] originalMaterials;
        public Material[] fadeMaterials;
        public Color[] originalFadeColors;
        public bool fadeMaterialsAssigned;

        // Cached once from the renderer's real mesh geometry.
        public Mesh structuralLineMesh;
    }

    private struct FrameMetrics
    {
        public int contextHits;
        public int amyHits;
    }

    // =====================================================
    // UNITY
    // =====================================================

    private void Awake()
    {
        Active = this;

        if (player != null)
        {
            playerController = player.GetComponent<CharacterController>();
        }

        InitializeOcclusionLines();
        BuildLevelCache();
    }

    private void OnDisable()
    {
        RestoreEverything();

        if (Active == this)
        {
            Active = null;
        }
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterials();
        DestroyOcclusionLineResources();
    }

    // =====================================================
    // CACHE
    // =====================================================

    private void BuildLevelCache()
    {
        colliderToTarget.Clear();
        targetByRoot.Clear();
        rendererStates.Clear();

        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider == null)
                continue;

            // If this collider belongs to an authored logical group,
            // the ENTIRE group becomes one occlusion target.
            CameraOcclusionGroup group = collider.GetComponentInParent<CameraOcclusionGroup>();

            Transform logicalRoot = null;

            Renderer[] targetRenderers = null;

            // -------------------------------------------------
            // GROUPED OBJECT
            // -------------------------------------------------

            if (group != null)
            {
                logicalRoot = group.transform;

                targetRenderers = group.GetComponentsInChildren<Renderer>(true);
            }
            // -------------------------------------------------
            // NORMAL OBJECT / OLD BEHAVIOUR
            // -------------------------------------------------

            else
            {
                Renderer renderer = collider.GetComponent<Renderer>();

                if (renderer == null)
                {
                    renderer = collider.GetComponentInParent<Renderer>();
                }

                if (renderer == null)
                    continue;

                logicalRoot = renderer.transform;

                targetRenderers = new Renderer[] { renderer };
            }

            if (logicalRoot == null)
                continue;

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                continue;
            }

            // Reuse the same logical target when several colliders
            // belong to the same container/group/renderer.
            if (!targetByRoot.TryGetValue(logicalRoot, out OcclusionTarget target))
            {
                float maxWorldY = float.NegativeInfinity;

                List<Renderer> validRenderers = new List<Renderer>(targetRenderers.Length);

                for (int rendererIndex = 0; rendererIndex < targetRenderers.Length; rendererIndex++)
                {
                    Renderer renderer = targetRenderers[rendererIndex];

                    if (renderer == null)
                        continue;

                    validRenderers.Add(renderer);

                    maxWorldY = Mathf.Max(maxWorldY, renderer.bounds.max.y);

                    if (!rendererStates.ContainsKey(renderer))
                    {
                        rendererStates.Add(
                            renderer,
                            new OcclusionState
                            {
                                originalMaterials = renderer.sharedMaterials,

                                structuralLineMesh = BuildStructuralLineMesh(renderer),
                            }
                        );
                    }
                }

                if (validRenderers.Count == 0)
                    continue;

                target = new OcclusionTarget
                {
                    renderers = validRenderers.ToArray(),

                    maxWorldY = maxWorldY,
                };

                targetByRoot.Add(logicalRoot, target);
            }

            colliderToTarget[collider] = target;
        }

        Debug.Log(
            $"Camera Occlusion: cached "
                + $"{colliderToTarget.Count} colliders / "
                + $"{targetByRoot.Count} logical occluders / "
                + $"{rendererStates.Count} renderers.",
            this
        );
    }

    // =====================================================
    // UPDATE
    // =====================================================

    private void LateUpdate()
    {
        if (player == null || gameplayCamera == null)
        {
            return;
        }

        frameMetrics.Clear();

        BuildSamplePositions();
        BuildAmySamplePositions();

        int blockedContextSamples = 0;
        int blockedAmySamples = 0;

        for (int i = 0; i < ContextSampleCount; i++)
        {
            if (CollectBlockersForSample(samplePositions[i], false))
            {
                blockedContextSamples++;
            }
        }

        for (int i = 0; i < AmySampleCount; i++)
        {
            if (CollectBlockersForSample(amySamplePositions[i], true))
            {
                blockedAmySamples++;
            }
        }

        bool contextRuleTriggered = blockedContextSamples >= requiredBlockedSamples;

        bool amyRuleTriggered = blockedAmySamples >= requiredAmyBlockedSamples;

        if (contextRuleTriggered || amyRuleTriggered)
        {
            RequestFrameBlockers(contextRuleTriggered, amyRuleTriggered);
        }

        UpdateAnimatedFade();
        DrawOcclusionLines();
    }

    // =====================================================
    // VISIBILITY SAMPLES
    // =====================================================

    private void BuildSamplePositions()
    {
        Vector3 playerRoot = player.position;

        Vector3 center =
            playerController != null
                ? playerController.bounds.center
                : playerRoot + Vector3.up * visibilityHeight;

        Vector3 cameraForward = gameplayCamera.transform.forward;

        cameraForward.y = 0f;

        if (cameraForward.sqrMagnitude < 0.001f)
        {
            cameraForward = Vector3.forward;
        }

        cameraForward.Normalize();

        Vector3 cameraRight = gameplayCamera.transform.right;

        cameraRight.y = 0f;

        if (cameraRight.sqrMagnitude < 0.001f)
        {
            cameraRight = Vector3.right;
        }

        cameraRight.Normalize();

        Vector3 ringCenter = playerRoot + Vector3.up * visibilityHeight;

        float diagonal = footprintRadius * 0.70710678f;

        samplePositions[0] = center + Vector3.up * 0.35f;

        samplePositions[1] = center;

        samplePositions[2] = ringCenter + cameraForward * footprintRadius;

        samplePositions[3] = ringCenter - cameraForward * footprintRadius;

        samplePositions[4] = ringCenter + cameraRight * footprintRadius;

        samplePositions[5] = ringCenter - cameraRight * footprintRadius;

        samplePositions[6] = ringCenter + cameraForward * diagonal + cameraRight * diagonal;

        samplePositions[7] = ringCenter + cameraForward * diagonal - cameraRight * diagonal;

        samplePositions[8] = ringCenter - cameraForward * diagonal + cameraRight * diagonal;

        samplePositions[9] = ringCenter - cameraForward * diagonal - cameraRight * diagonal;
    }

    private void BuildAmySamplePositions()
    {
        Bounds bounds;

        if (playerController != null)
        {
            bounds = playerController.bounds;
        }
        else
        {
            Vector3 fallbackCenter = player.position + Vector3.up * 1.0f;

            bounds = new Bounds(fallbackCenter, new Vector3(0.6f, 1.8f, 0.6f));
        }

        float bottom = bounds.min.y;

        float height = bounds.size.y;

        Vector3 center = new Vector3(bounds.center.x, 0f, bounds.center.z);

        Vector3 cameraRight = gameplayCamera.transform.right;

        cameraRight.y = 0f;

        if (cameraRight.sqrMagnitude < 0.001f)
        {
            cameraRight = Vector3.right;
        }

        cameraRight.Normalize();

        float shoulderOffset = Mathf.Max(
            0.12f,
            Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.65f
        );

        // Head / upper body
        amySamplePositions[0] = center + Vector3.up * (bottom + height * 0.84f);

        // Left shoulder
        amySamplePositions[1] =
            center + Vector3.up * (bottom + height * 0.68f) - cameraRight * shoulderOffset;

        // Torso
        amySamplePositions[2] = center + Vector3.up * (bottom + height * 0.60f);

        // Right shoulder
        amySamplePositions[3] =
            center + Vector3.up * (bottom + height * 0.68f) + cameraRight * shoulderOffset;

        // Hips / lower torso
        amySamplePositions[4] = center + Vector3.up * (bottom + height * 0.42f);
    }

    // =====================================================
    // DETECTION
    // =====================================================

    private bool CollectBlockersForSample(Vector3 targetPosition, bool amySample)
    {
        Vector3 cameraPosition = gameplayCamera.transform.position;

        Vector3 direction = targetPosition - cameraPosition;

        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return false;

        direction /= distance;

        int hitCount = Physics.RaycastNonAlloc(
            cameraPosition,
            direction,
            hitBuffer,
            distance,
            occlusionMask,
            QueryTriggerInteraction.Ignore
        );

        targetsHitThisRay.Clear();

        float preserveHeight = player.position.y + preserveBelowHeight;

        bool blocked = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];

            Collider collider = hit.collider;

            if (collider == null)
                continue;

            if (hit.distance >= distance - 0.02f)
            {
                continue;
            }

            if (!colliderToTarget.TryGetValue(collider, out OcclusionTarget target))
            {
                continue;
            }

            if (target == null)
                continue;

            // Preserve logical occluders that are entirely below Amy.
            if (target.maxWorldY <= preserveHeight)
            {
                continue;
            }

            // A logical group can contain many child colliders.
            // It must only count once per ray.
            if (!targetsHitThisRay.Add(target))
            {
                continue;
            }

            blocked = true;

            if (frameMetrics.TryGetValue(target, out FrameMetrics metrics))
            {
                if (amySample)
                {
                    metrics.amyHits++;
                }
                else
                {
                    metrics.contextHits++;
                }

                frameMetrics[target] = metrics;
            }
            else
            {
                frameMetrics.Add(
                    target,
                    new FrameMetrics
                    {
                        contextHits = amySample ? 0 : 1,

                        amyHits = amySample ? 1 : 0,
                    }
                );
            }
        }

        return blocked;
    }

    // =====================================================
    // OCCLUSION SET
    // =====================================================

    private void RequestFrameBlockers(bool contextRuleTriggered, bool amyRuleTriggered)
    {
        float now = Time.time;

        foreach (KeyValuePair<OcclusionTarget, FrameMetrics> pair in frameMetrics)
        {
            OcclusionTarget target = pair.Key;

            if (target == null)
                continue;

            FrameMetrics metrics = pair.Value;

            bool joinsFromContext =
                contextRuleTriggered && metrics.contextHits >= rendererJoinMinimumSamples;

            bool joinsFromAmy =
                amyRuleTriggered && metrics.amyHits >= rendererJoinMinimumAmySamples;

            if (!joinsFromContext && !joinsFromAmy)
            {
                continue;
            }

            Renderer[] renderers = target.renderers;

            if (renderers == null)
                continue;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null)
                    continue;

                if (!rendererStates.TryGetValue(renderer, out OcclusionState state))
                {
                    continue;
                }

                state.lastRequestedTime = now;

                activeFadeRenderers.Add(renderer);
            }
        }
    }

    // =====================================================
    // ANIMATED FADE
    // =====================================================

    private void UpdateAnimatedFade()
    {
        if (activeFadeRenderers.Count == 0)
        {
            return;
        }

        activeFadeBuffer.Clear();

        activeFadeBuffer.AddRange(activeFadeRenderers);

        float now = Time.time;

        float dt = Time.deltaTime;

        float fadeOutSpeed = (1f - hiddenVisibility) / Mathf.Max(fadeOutDuration, 0.01f);

        float fadeInSpeed = (1f - hiddenVisibility) / Mathf.Max(fadeInDuration, 0.01f);

        for (int i = 0; i < activeFadeBuffer.Count; i++)
        {
            Renderer renderer = activeFadeBuffer[i];

            if (renderer == null)
            {
                activeFadeRenderers.Remove(renderer);

                continue;
            }

            if (!rendererStates.TryGetValue(renderer, out OcclusionState state))
            {
                activeFadeRenderers.Remove(renderer);

                continue;
            }

            bool shouldOcclude = now - state.lastRequestedTime <= occlusionHold;

            state.isOccluding = shouldOcclude;

            float targetFade = shouldOcclude ? hiddenVisibility : 1f;

            float speed = targetFade < state.currentFade ? fadeOutSpeed : fadeInSpeed;

            float newFade = Mathf.MoveTowards(state.currentFade, targetFade, speed * dt);

            if (!Mathf.Approximately(newFade, state.currentFade))
            {
                EnsureFadeMaterials(renderer, state);

                state.currentFade = newFade;

                ApplyFadeToMaterials(state, newFade);
            }

            if (!shouldOcclude && state.currentFade >= 0.9999f)
            {
                state.currentFade = 1f;

                state.isOccluding = false;

                RestoreOriginalMaterials(renderer, state);

                activeFadeRenderers.Remove(renderer);
            }
        }
    }

    // =====================================================
    // TEMPORARY TRANSPARENT MATERIALS
    // =====================================================

    private void EnsureFadeMaterials(Renderer renderer, OcclusionState state)
    {
        if (state.fadeMaterials == null)
        {
            Material[] originals = state.originalMaterials;

            state.fadeMaterials = new Material[originals.Length];

            state.originalFadeColors = new Color[originals.Length];

            for (int i = 0; i < originals.Length; i++)
            {
                Material original = originals[i];

                if (original == null)
                    continue;

                Material clone = new Material(original)
                {
                    name = original.name + " [CameraFadeRuntime]",

                    hideFlags = HideFlags.DontSave,
                };

                // Preferred production path:
                // SG_EnvironmentSurface remains Opaque
                // and uses _Fade for dither/alpha clipping.
                if (clone.HasProperty("_Fade"))
                {
                    clone.SetFloat("_Fade", 1f);
                }
                else
                {
                    // Legacy/fallback path for normal URP/Lit.
                    ConfigureMaterialForTransparentFade(clone);

                    state.originalFadeColors[i] = GetMaterialColor(clone);
                }

                state.fadeMaterials[i] = clone;
            }
        }

        if (!state.fadeMaterialsAssigned)
        {
            renderer.sharedMaterials = state.fadeMaterials;

            state.fadeMaterialsAssigned = true;
        }
    }

    private static void ConfigureMaterialForTransparentFade(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_SrcBlendAlpha"))
        {
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        }

        if (material.HasProperty("_DstBlendAlpha"))
        {
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        material.SetOverrideTag("RenderType", "Transparent");

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        material.DisableKeyword("_ALPHATEST_ON");

        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static Color GetMaterialColor(Material material)
    {
        if (material == null)
            return Color.white;

        if (material.HasProperty("_BaseColor"))
        {
            return material.GetColor("_BaseColor");
        }

        if (material.HasProperty("_Color"))
        {
            return material.GetColor("_Color");
        }

        if (material.HasProperty("_BaseColorTint"))
        {
            return material.GetColor("_BaseColorTint");
        }

        return Color.white;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);

            return;
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);

            return;
        }

        if (material.HasProperty("_BaseColorTint"))
        {
            material.SetColor("_BaseColorTint", color);
        }
    }

    private static void ApplyFadeToMaterials(OcclusionState state, float fade)
    {
        if (state.fadeMaterials == null)
        {
            return;
        }

        for (int i = 0; i < state.fadeMaterials.Length; i++)
        {
            Material material = state.fadeMaterials[i];

            if (material == null)
                continue;

            if (material.HasProperty("_Fade"))
            {
                material.SetFloat("_Fade", fade);

                continue;
            }

            Color color = state.originalFadeColors[i];

            color.a *= fade;

            SetMaterialColor(material, color);
        }
    }

    private static void RestoreOriginalMaterials(Renderer renderer, OcclusionState state)
    {
        if (!state.fadeMaterialsAssigned)
        {
            return;
        }

        renderer.sharedMaterials = state.originalMaterials;

        state.fadeMaterialsAssigned = false;
    }

    private void RestoreEverything()
    {
        foreach (KeyValuePair<Renderer, OcclusionState> pair in rendererStates)
        {
            Renderer renderer = pair.Key;

            OcclusionState state = pair.Value;

            if (renderer != null)
            {
                RestoreOriginalMaterials(renderer, state);
            }

            state.currentFade = 1f;

            state.isOccluding = false;
        }

        activeFadeRenderers.Clear();
    }

    private void DestroyRuntimeMaterials()
    {
        foreach (KeyValuePair<Renderer, OcclusionState> pair in rendererStates)
        {
            Material[] materials = pair.Value.fadeMaterials;

            if (materials == null)
                continue;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];

                if (material == null)
                    continue;

                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }
        }
    }

    // =====================================================
    // OCCLUSION LINES
    // =====================================================

    private void InitializeOcclusionLines()
    {
        Shader lineShader = Resources.Load<Shader>("SH_CameraOcclusionLines");

        if (lineShader == null)
        {
            lineShader = Shader.Find("KVA/CameraOcclusionLines");
        }

        if (lineShader == null)
        {
            Debug.LogError(
                "Camera Occlusion: could not find " + "SH_CameraOcclusionLines.shader.",
                this
            );

            return;
        }

        occlusionLineMaterial = new Material(lineShader)
        {
            name = "M_CameraOcclusionLines_Runtime",

            hideFlags = HideFlags.HideAndDontSave,
        };

        occlusionLineProperties = new MaterialPropertyBlock();
    }

    private readonly struct QuantizedVertex
        : System.IEquatable<QuantizedVertex>,
            System.IComparable<QuantizedVertex>
    {
        private const float Precision = 10000f;

        public readonly int x;
        public readonly int y;
        public readonly int z;

        public QuantizedVertex(Vector3 value)
        {
            x = Mathf.RoundToInt(value.x * Precision);

            y = Mathf.RoundToInt(value.y * Precision);

            z = Mathf.RoundToInt(value.z * Precision);
        }

        public bool Equals(QuantizedVertex other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is QuantizedVertex other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;

                hash = hash * 31 + x;

                hash = hash * 31 + y;

                hash = hash * 31 + z;

                return hash;
            }
        }

        public int CompareTo(QuantizedVertex other)
        {
            int xCompare = x.CompareTo(other.x);

            if (xCompare != 0)
                return xCompare;

            int yCompare = y.CompareTo(other.y);

            if (yCompare != 0)
                return yCompare;

            return z.CompareTo(other.z);
        }
    }

    private readonly struct EdgeKey : System.IEquatable<EdgeKey>
    {
        public readonly QuantizedVertex a;
        public readonly QuantizedVertex b;

        public EdgeKey(Vector3 first, Vector3 second)
        {
            QuantizedVertex qa = new QuantizedVertex(first);

            QuantizedVertex qb = new QuantizedVertex(second);

            if (qa.CompareTo(qb) <= 0)
            {
                a = qa;
                b = qb;
            }
            else
            {
                a = qb;
                b = qa;
            }
        }

        public bool Equals(EdgeKey other)
        {
            return a.Equals(other.a) && b.Equals(other.b);
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return a.GetHashCode() * 397 ^ b.GetHashCode();
            }
        }
    }

    private struct EdgeInfo
    {
        public Vector3 a;
        public Vector3 b;

        public Vector3 firstNormal;

        public int faceCount;
        public bool isSharp;
    }

    private Mesh BuildStructuralLineMesh(Renderer renderer)
    {
        if (renderer == null)
            return null;

        // Surface decoration fades with its group but is not structural geometry.
        // Opt-out is authored on the shader; no new scene component or registration is required.
        Material[] lineMaterials = renderer.sharedMaterials;
        if (lineMaterials.Length > 0)
        {
            bool suppressLines = true;
            for (int i = 0; i < lineMaterials.Length; i++)
            {
                if (lineMaterials[i] == null || lineMaterials[i].GetTag("CameraOcclusionLines", false, "") != "Off")
                {
                    suppressLines = false;
                    break;
                }
            }
            if (suppressLines) return null;
        }

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return BuildBoundsFallbackLineMesh(renderer.localBounds, renderer.name);
        }

        Mesh sourceMesh = meshFilter.sharedMesh;

        if (!sourceMesh.isReadable)
        {
            return BuildBoundsFallbackLineMesh(renderer.localBounds, renderer.name);
        }

        Vector3[] sourceVertices = sourceMesh.vertices;

        if (sourceVertices == null || sourceVertices.Length == 0)
        {
            return BuildBoundsFallbackLineMesh(renderer.localBounds, renderer.name);
        }

        Dictionary<EdgeKey, EdgeInfo> edges = new Dictionary<EdgeKey, EdgeInfo>();

        float sharpDotThreshold = Mathf.Cos(structuralEdgeAngle * Mathf.Deg2Rad);

        for (int subMesh = 0; subMesh < sourceMesh.subMeshCount; subMesh++)
        {
            int[] triangles = sourceMesh.GetTriangles(subMesh);

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = sourceVertices[triangles[i]];

                Vector3 b = sourceVertices[triangles[i + 1]];

                Vector3 c = sourceVertices[triangles[i + 2]];

                Vector3 normal = Vector3.Cross(b - a, c - a);

                if (normal.sqrMagnitude <= 0.0000001f)
                {
                    continue;
                }

                normal.Normalize();

                RegisterStructuralEdge(edges, a, b, normal, sharpDotThreshold);

                RegisterStructuralEdge(edges, b, c, normal, sharpDotThreshold);

                RegisterStructuralEdge(edges, c, a, normal, sharpDotThreshold);
            }
        }

        List<Vector3> lineVertices = new List<Vector3>(edges.Count * 2);

        foreach (KeyValuePair<EdgeKey, EdgeInfo> pair in edges)
        {
            EdgeInfo edge = pair.Value;

            bool shouldDraw = edge.faceCount == 1 || edge.isSharp;

            if (!shouldDraw)
                continue;

            lineVertices.Add(edge.a);

            lineVertices.Add(edge.b);
        }

        if (lineVertices.Count == 0)
        {
            return BuildBoundsFallbackLineMesh(renderer.localBounds, renderer.name);
        }

        int[] indices = new int[lineVertices.Count];

        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        Mesh lineMesh = new Mesh
        {
            name = "KVA Occlusion Structural Lines - " + renderer.name,

            hideFlags = HideFlags.HideAndDontSave,
        };

        if (lineVertices.Count > 65535)
        {
            lineMesh.indexFormat = IndexFormat.UInt32;
        }

        lineMesh.SetVertices(lineVertices);

        lineMesh.SetIndices(indices, MeshTopology.Lines, 0, true);

        lineMesh.bounds = sourceMesh.bounds;

        return lineMesh;
    }

    private static void RegisterStructuralEdge(
        Dictionary<EdgeKey, EdgeInfo> edges,
        Vector3 a,
        Vector3 b,
        Vector3 faceNormal,
        float sharpDotThreshold
    )
    {
        EdgeKey key = new EdgeKey(a, b);

        if (edges.TryGetValue(key, out EdgeInfo edge))
        {
            edge.faceCount++;

            float dot = Vector3.Dot(edge.firstNormal, faceNormal);

            if (dot < sharpDotThreshold)
            {
                edge.isSharp = true;
            }

            edges[key] = edge;

            return;
        }

        edges.Add(
            key,
            new EdgeInfo
            {
                a = a,
                b = b,
                firstNormal = faceNormal,
                faceCount = 1,
                isSharp = false,
            }
        );
    }

    private static Mesh BuildBoundsFallbackLineMesh(Bounds bounds, string rendererName)
    {
        Vector3 min = bounds.min;

        Vector3 max = bounds.max;

        Vector3[] vertices =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, max.z),
        };

        int[] indices = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };

        Mesh mesh = new Mesh
        {
            name = "KVA Occlusion Bounds Fallback - " + rendererName,

            hideFlags = HideFlags.HideAndDontSave,
        };

        mesh.vertices = vertices;

        mesh.SetIndices(indices, MeshTopology.Lines, 0, true);

        mesh.bounds = bounds;

        return mesh;
    }

    private void DrawOcclusionLines()
    {
        if (
            !showOcclusionLines
            || gameplayCamera == null
            || occlusionLineMaterial == null
            || activeFadeRenderers.Count == 0
        )
        {
            return;
        }

        foreach (Renderer renderer in activeFadeRenderers)
        {
            if (renderer == null)
                continue;

            if (!rendererStates.TryGetValue(renderer, out OcclusionState state))
            {
                continue;
            }

            if (state.currentFade >= lineStartFade)
            {
                continue;
            }

            float lineStrength = Mathf.Clamp01(
                (lineStartFade - state.currentFade)
                    / Mathf.Max(lineStartFade - hiddenVisibility, 0.001f)
            );

            if (lineStrength <= 0.001f)
            {
                continue;
            }

            Color lineColor = occlusionLineColor;

            lineColor.a *= lineStrength;

            occlusionLineProperties.Clear();

            occlusionLineProperties.SetColor(LineColorId, lineColor);

            occlusionLineProperties.SetFloat(DashLengthPixelsId, dashLengthPixels);

            occlusionLineProperties.SetFloat(DashFillId, dashFill);

            Mesh structuralLineMesh = state.structuralLineMesh;

            if (structuralLineMesh == null)
            {
                continue;
            }

            Graphics.DrawMesh(
                structuralLineMesh,
                renderer.localToWorldMatrix,
                occlusionLineMaterial,
                renderer.gameObject.layer,
                gameplayCamera,
                0,
                occlusionLineProperties,
                ShadowCastingMode.Off,
                false,
                null,
                LightProbeUsage.Off
            );
        }
    }

    private void DestroyOcclusionLineResources()
    {
        if (occlusionLineMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(occlusionLineMaterial);
            }
            else
            {
                DestroyImmediate(occlusionLineMaterial);
            }

            occlusionLineMaterial = null;
        }

        foreach (KeyValuePair<Renderer, OcclusionState> pair in rendererStates)
        {
            Mesh structuralLineMesh = pair.Value.structuralLineMesh;

            if (structuralLineMesh == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(structuralLineMesh);
            }
            else
            {
                DestroyImmediate(structuralLineMesh);
            }

            pair.Value.structuralLineMesh = null;
        }
    }

    // =====================================================
    // AIM / GAMEPLAY QUERY
    // =====================================================

    public bool IsOccluded(Renderer renderer)
    {
        if (renderer == null)
            return false;

        if (!rendererStates.TryGetValue(renderer, out OcclusionState state))
        {
            return false;
        }

        // Aim ignores the blocker during the entire visual fade,
        // including the smooth restore transition.
        return state.isOccluding || state.currentFade < 0.9999f;
    }
}
