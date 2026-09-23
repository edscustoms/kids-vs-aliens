using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class CameraOcclusionController : MonoBehaviour
{
    private const int ContextSampleCount = 10;
    private const int AmySampleCount = 5;
    private const int HitBufferSize = 64;

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

    [Tooltip("Silhouette width in gameplay-camera pixels.")]
    [SerializeField, Range(.75f, 4f)] private float silhouetteWidthPixels = 1.5f;

    private CharacterController playerController;

    // Collider -> logical occlusion object.
    private readonly Dictionary<Collider, OcclusionTarget> colliderToTarget =
        new Dictionary<Collider, OcclusionTarget>();

    // Root transform -> one logical occlusion object.
    private readonly Dictionary<Transform, OcclusionTarget> targetByRoot =
        new Dictionary<Transform, OcclusionTarget>();

    // Renderer state stays per-renderer because every child renderer
    // still needs its own materials/fade.
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

    // Read-only presentation view. Detection, grouping and material fade remain authoritative above.
    public readonly struct SilhouetteRenderer
    {
        public readonly Renderer renderer;
        public readonly Material[] materials;
        public readonly float strength;
        public SilhouetteRenderer(Renderer renderer, Material[] materials, float strength)
        { this.renderer = renderer; this.materials = materials; this.strength = strength; }
    }

    public Color SilhouetteColor => occlusionLineColor;
    public float SilhouetteWidth => silhouetteWidthPixels;
    public float SilhouetteDashLength => dashLengthPixels;
    public float SilhouetteDashFill => dashFill;

    public void CollectSilhouetteRenderers(Camera camera, List<SilhouetteRenderer> output)
    {
        output.Clear();
        if (!isActiveAndEnabled || !showOcclusionLines || camera != gameplayCamera) return;
        foreach (var renderer in activeFadeRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                || renderer.forceRenderingOff || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0
                || !rendererStates.TryGetValue(renderer, out var state) || state.currentFade >= lineStartFade) continue;
            float strength = Mathf.Clamp01((lineStartFade - state.currentFade)
                / Mathf.Max(lineStartFade - hiddenVisibility, .001f));
            if (strength > .001f) output.Add(new SilhouetteRenderer(renderer, state.originalMaterials, strength));
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
