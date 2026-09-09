using System.Collections.Generic;
using UnityEngine;

public sealed class FadeWhenBlockingPlayer : MonoBehaviour
{
    private const int VisibilitySampleCount = 5;

    [Header("References")]
    [SerializeField]
    private Transform player;

    [SerializeField]
    private CameraOcclusionAuthoring occlusionAuthoring;

    [Header("Physics")]
    [SerializeField]
    private LayerMask occlusionMask = ~0;

    [Header("Detection")]
    [Range(1, VisibilitySampleCount)]
    [SerializeField]
    private int requiredBlockedSamples = 3;

    [Header("Fade")]
    [Tooltip("1 = fully visible, 0 = fully faded. Start around 0.10.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float fadedAmount = 0.10f;

    [SerializeField]
    private float fadeOutSpeed = 10f;

    [SerializeField]
    private float restoreSpeed = 6f;

    [Tooltip("Prevents rapid flickering when moving past wall edges.")]
    [SerializeField]
    private float restoreDelay = 0.15f;

    private CharacterController playerController;

    private readonly Vector3[] visibilitySamples = new Vector3[VisibilitySampleCount];

    private readonly RaycastHit[] rayHits = new RaycastHit[64];

    // Prevent one Renderer receiving multiple votes
    // from the same sightline.
    private readonly HashSet<Renderer> renderersHitThisSample = new HashSet<Renderer>();

    private readonly Dictionary<Renderer, RuntimeState> states =
        new Dictionary<Renderer, RuntimeState>();

    private readonly List<Renderer> trackedRenderers = new List<Renderer>();

    private sealed class RuntimeState
    {
        public int blockedSamples;
        public bool isBlocking;
        public float clearTimer;
    }

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private void Awake()
    {
        if (player != null)
        {
            playerController = player.GetComponent<CharacterController>();
        }
    }

    // =====================================================
    // UPDATE
    // =====================================================

    private void LateUpdate()
    {
        if (player == null || occlusionAuthoring == null)
        {
            return;
        }

        ResetBlockedSampleCounts();

        BuildVisibilitySamples();

        for (int i = 0; i < VisibilitySampleCount; i++)
        {
            CollectBlockingRenderers(visibilitySamples[i]);
        }

        UpdateFadeStates();
    }

    // =====================================================
    // PLAYER VISIBILITY SAMPLES
    // =====================================================

    private void BuildVisibilitySamples()
    {
        if (playerController != null)
        {
            Bounds bounds = playerController.bounds;

            float bottom = bounds.min.y;

            float height = bounds.size.y;

            Vector3 center = new Vector3(bounds.center.x, 0f, bounds.center.z);

            Vector3 cameraRight = transform.right;

            cameraRight.y = 0f;

            if (cameraRight.sqrMagnitude > 0.001f)
            {
                cameraRight.Normalize();
            }
            else
            {
                cameraRight = Vector3.right;
            }

            float shoulderOffset = Mathf.Max(
                0.12f,
                Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.65f
            );

            // Head / upper body
            visibilitySamples[0] = center + Vector3.up * (bottom + height * 0.84f);

            // Left shoulder
            visibilitySamples[1] =
                center + Vector3.up * (bottom + height * 0.68f) - cameraRight * shoulderOffset;

            // Torso
            visibilitySamples[2] = center + Vector3.up * (bottom + height * 0.60f);

            // Right shoulder
            visibilitySamples[3] =
                center + Vector3.up * (bottom + height * 0.68f) + cameraRight * shoulderOffset;

            // Hips
            visibilitySamples[4] = center + Vector3.up * (bottom + height * 0.42f);

            return;
        }

        // Fallback if no CharacterController exists.
        Vector3 p = player.position;

        visibilitySamples[0] = p + Vector3.up * 1.55f;

        visibilitySamples[1] = p + Vector3.up * 1.25f - transform.right * 0.15f;

        visibilitySamples[2] = p + Vector3.up * 1.10f;

        visibilitySamples[3] = p + Vector3.up * 1.25f + transform.right * 0.15f;

        visibilitySamples[4] = p + Vector3.up * 0.75f;
    }

    // =====================================================
    // OCCLUSION DETECTION
    // =====================================================

    private void CollectBlockingRenderers(Vector3 samplePosition)
    {
        Vector3 direction = samplePosition - transform.position;

        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return;

        direction /= distance;

        int hitCount = Physics.RaycastNonAlloc(
            transform.position,
            direction,
            rayHits,
            distance,
            occlusionMask,
            QueryTriggerInteraction.Ignore
        );

        renderersHitThisSample.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = rayHits[i];

            Collider collider = hit.collider;

            if (collider == null)
                continue;

            if (hit.distance >= distance - 0.01f)
                continue;

            if (!occlusionAuthoring.TryGetRenderer(collider, out Renderer renderer))
            {
                continue;
            }

            if (renderer == null)
                continue;

            // Same wall may contain multiple colliders.
            // It still gets only ONE vote from this ray.
            if (!renderersHitThisSample.Add(renderer))
                continue;

            RuntimeState state = GetOrCreateState(renderer);

            state.blockedSamples++;
        }
    }

    // =====================================================
    // RUNTIME STATE
    // =====================================================

    private RuntimeState GetOrCreateState(Renderer renderer)
    {
        if (states.TryGetValue(renderer, out RuntimeState state))
        {
            return state;
        }

        state = new RuntimeState();

        states.Add(renderer, state);
        trackedRenderers.Add(renderer);

        return state;
    }

    private void ResetBlockedSampleCounts()
    {
        for (int i = trackedRenderers.Count - 1; i >= 0; i--)
        {
            Renderer renderer = trackedRenderers[i];

            if (renderer == null)
            {
                trackedRenderers.RemoveAt(i);
                continue;
            }

            if (states.TryGetValue(renderer, out RuntimeState state))
            {
                state.blockedSamples = 0;
            }
        }
    }

    private void UpdateFadeStates()
    {
        float dt = Time.deltaTime;

        for (int i = 0; i < trackedRenderers.Count; i++)
        {
            Renderer renderer = trackedRenderers[i];

            if (renderer == null)
                continue;

            if (!states.TryGetValue(renderer, out RuntimeState state))
            {
                continue;
            }

            bool blockedNow = state.blockedSamples >= requiredBlockedSamples;

            if (blockedNow)
            {
                state.isBlocking = true;
                state.clearTimer = 0f;
            }
            else if (state.isBlocking)
            {
                state.clearTimer += dt;

                if (state.clearTimer >= restoreDelay)
                {
                    state.isBlocking = false;
                    state.clearTimer = 0f;
                }
            }

            float targetFade = state.isBlocking ? fadedAmount : 1f;

            float currentFade = occlusionAuthoring.GetCurrentFade(renderer);

            float speed = targetFade < currentFade ? fadeOutSpeed : restoreSpeed;

            float newFade = Mathf.MoveTowards(currentFade, targetFade, speed * dt);

            occlusionAuthoring.ApplyFade(renderer, newFade);
        }
    }

    // =====================================================
    // GAMEPLAY / AIM QUERY
    // =====================================================

    public bool IsBlockingPlayer(Renderer renderer)
    {
        if (renderer == null)
            return false;

        return states.TryGetValue(renderer, out RuntimeState state) && state.isBlocking;
    }
}
