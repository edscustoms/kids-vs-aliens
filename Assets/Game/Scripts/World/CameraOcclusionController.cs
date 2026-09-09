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
        "When the main 9/10 rule triggers, a renderer must appear in at least this many context samples to join the fade set."
    )]
    [Range(1, ContextSampleCount)]
    [SerializeField]
    private int rendererJoinMinimumSamples = 2;

    [Tooltip(
        "When the Amy-body rule triggers, a renderer must cover at least this many Amy samples to join the fade set."
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
        "Subtle structural guide color. This does not modify the object's real material/color."
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

    private CharacterController playerController;

    private Mesh occlusionLineMesh;
    private Material occlusionLineMaterial;
    private MaterialPropertyBlock occlusionLineProperties;

    private readonly Dictionary<Collider, Renderer> colliderToRenderer =
        new Dictionary<Collider, Renderer>();

    private readonly Dictionary<Renderer, OcclusionState> rendererStates =
        new Dictionary<Renderer, OcclusionState>();

    private readonly Dictionary<Renderer, FrameMetrics> frameMetrics =
        new Dictionary<Renderer, FrameMetrics>();

    private readonly HashSet<Renderer> renderersHitThisRay = new HashSet<Renderer>();

    private readonly HashSet<Renderer> activeFadeRenderers = new HashSet<Renderer>();

    private readonly List<Renderer> activeFadeBuffer = new List<Renderer>();

    private readonly Vector3[] samplePositions = new Vector3[ContextSampleCount];

    private readonly Vector3[] amySamplePositions = new Vector3[AmySampleCount];

    private readonly RaycastHit[] hitBuffer = new RaycastHit[HitBufferSize];

    public static CameraOcclusionController Active { get; private set; }

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

    private void Awake()
    {
        Active = this;

        if (player != null)
            playerController = player.GetComponent<CharacterController>();

        InitializeOcclusionLines();
        BuildLevelCache();
    }

    private void OnDisable()
    {
        RestoreEverything();

        if (Active == this)
            Active = null;
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterials();
        DestroyOcclusionLineResources();
    }

    // =====================================================
    // CACHE — ONCE
    // =====================================================

    private void BuildLevelCache()
    {
        colliderToRenderer.Clear();
        rendererStates.Clear();

        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider == null)
                continue;

            Renderer renderer = collider.GetComponent<Renderer>();

            if (renderer == null)
                renderer = collider.GetComponentInParent<Renderer>();

            if (renderer == null)
                continue;

            colliderToRenderer[collider] = renderer;

            if (!rendererStates.ContainsKey(renderer))
            {
                rendererStates.Add(
                    renderer,
                    new OcclusionState { originalMaterials = renderer.sharedMaterials }
                );
            }
        }

        Debug.Log(
            $"Camera Occlusion: cached {colliderToRenderer.Count} colliders / "
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
            return;

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
            cameraForward = Vector3.forward;

        cameraForward.Normalize();

        Vector3 cameraRight = gameplayCamera.transform.right;

        cameraRight.y = 0f;

        if (cameraRight.sqrMagnitude < 0.001f)
            cameraRight = Vector3.right;

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

        renderersHitThisRay.Clear();

        float preserveHeight = player.position.y + preserveBelowHeight;

        bool blocked = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            Collider collider = hit.collider;

            if (collider == null)
                continue;

            if (hit.distance >= distance - 0.02f)
                continue;

            if (!colliderToRenderer.TryGetValue(collider, out Renderer renderer))
            {
                continue;
            }

            if (renderer == null)
                continue;

            if (renderer.bounds.max.y <= preserveHeight)
                continue;

            if (!renderersHitThisRay.Add(renderer))
                continue;

            blocked = true;

            if (frameMetrics.TryGetValue(renderer, out FrameMetrics metrics))
            {
                if (amySample)
                {
                    metrics.amyHits++;
                }
                else
                {
                    metrics.contextHits++;
                }

                frameMetrics[renderer] = metrics;
            }
            else
            {
                frameMetrics.Add(
                    renderer,
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

        foreach (KeyValuePair<Renderer, FrameMetrics> pair in frameMetrics)
        {
            Renderer renderer = pair.Key;

            if (renderer == null)
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

            if (!rendererStates.TryGetValue(renderer, out OcclusionState state))
            {
                continue;
            }

            state.lastRequestedTime = now;

            activeFadeRenderers.Add(renderer);
        }
    }

    // =====================================================
    // ANIMATED FADE
    // =====================================================

    private void UpdateAnimatedFade()
    {
        if (activeFadeRenderers.Count == 0)
            return;

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

                ConfigureMaterialForTransparentFade(clone);

                state.originalFadeColors[i] = GetMaterialColor(clone);

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

        // Works directly with URP/Lit and any compatible
        // shader exposing the standard URP surface controls.
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            // Alpha blend.
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
            return;

        for (int i = 0; i < state.fadeMaterials.Length; i++)
        {
            Material material = state.fadeMaterials[i];

            if (material == null)
                continue;

            Color color = state.originalFadeColors[i];

            color.a *= fade;

            SetMaterialColor(material, color);
        }
    }

    private static void RestoreOriginalMaterials(Renderer renderer, OcclusionState state)
    {
        if (!state.fadeMaterialsAssigned)
            return;

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

        occlusionLineMesh = BuildUnitLineBoxMesh();
    }

    private static Mesh BuildUnitLineBoxMesh()
    {
        Mesh mesh = new Mesh
        {
            name = "KVA Camera Occlusion Line Box",
            hideFlags = HideFlags.HideAndDontSave,
        };

        Vector3[] vertices =
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
        };

        int[] indices =
        {
            // Bottom
            0,
            1,
            1,
            2,
            2,
            3,
            3,
            0,
            // Top
            4,
            5,
            5,
            6,
            6,
            7,
            7,
            4,
            // Vertical
            0,
            4,
            1,
            5,
            2,
            6,
            3,
            7,
        };

        mesh.vertices = vertices;

        mesh.SetIndices(indices, MeshTopology.Lines, 0, true);

        mesh.RecalculateBounds();

        return mesh;
    }

    private void DrawOcclusionLines()
    {
        if (
            !showOcclusionLines
            || gameplayCamera == null
            || occlusionLineMaterial == null
            || occlusionLineMesh == null
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
                continue;

            float lineStrength = Mathf.Clamp01(
                (lineStartFade - state.currentFade)
                    / Mathf.Max(lineStartFade - hiddenVisibility, 0.001f)
            );

            if (lineStrength <= 0.001f)
                continue;

            Color lineColor = occlusionLineColor;

            lineColor.a *= lineStrength;

            occlusionLineProperties.Clear();

            occlusionLineProperties.SetColor(LineColorId, lineColor);

            occlusionLineProperties.SetFloat(DashLengthPixelsId, dashLengthPixels);

            occlusionLineProperties.SetFloat(DashFillId, dashFill);

            Bounds localBounds = renderer.localBounds;

            if (localBounds.size.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            Matrix4x4 boundsMatrix = Matrix4x4.TRS(
                localBounds.center,
                Quaternion.identity,
                localBounds.size
            );

            Matrix4x4 drawMatrix = renderer.localToWorldMatrix * boundsMatrix;

            Graphics.DrawMesh(
                occlusionLineMesh,
                drawMatrix,
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

        if (occlusionLineMesh != null)
        {
            if (Application.isPlaying)
            {
                Destroy(occlusionLineMesh);
            }
            else
            {
                DestroyImmediate(occlusionLineMesh);
            }

            occlusionLineMesh = null;
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

        // Aim should ignore the blocker for the ENTIRE time it is
        // visually faded, including the smooth restore transition.
        return state.isOccluding || state.currentFade < 0.9999f;
    }
}
