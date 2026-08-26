using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class FadeWhenBlockingPlayer : MonoBehaviour
{
    [SerializeField]
    private Transform player;

    [FormerlySerializedAs("wallLayer")]
    [SerializeField]
    private LayerMask fadeWhenBlockingPlayerLayer;

    [Header("Fade")]
    [Range(0f, 1f)]
    [SerializeField]
    private float fadedAlpha = 0.15f;

    [SerializeField]
    private float fadeSpeed = 8f;

    // =====================================================
    // V1 CAMERA OCCLUSION DESIGN
    //
    // The old implementation used one SphereCastAll with a
    // fairly large radius. That produced two false positives:
    //
    // 1. low walls faded even when most of Amy was visible;
    // 2. when Amy stood close to a wall IN FRONT of her, the
    //    sphere around the end of the cast overlapped that wall
    //    even though it was not between the camera and Amy.
    //
    // We now use three thin, exact camera -> Amy sight lines:
    // upper body, torso and lower torso.
    //
    // A wall fades only when it blocks at least TWO of those
    // three lines. So low cover that only hides Amy's legs does
    // not disappear, while a genuinely camera-blocking wall does.
    //
    // The rays stop exactly at Amy, so geometry in FRONT of Amy
    // cannot be accidentally classified as a camera blocker.
    // =====================================================

    private const int VisibilitySampleCount = 3;
    private const int RequiredBlockedSamples = 2;

    private static readonly int FadeId = Shader.PropertyToID("_Fade");

    private Renderer[] fadeWalls;
    private CharacterController playerController;

    private readonly Vector3[] playerVisibilitySamples = new Vector3[VisibilitySampleCount];

    private readonly RaycastHit[] occlusionHits = new RaycastHit[32];

    private readonly HashSet<Renderer> obstructingWalls = new HashSet<Renderer>();

    private readonly HashSet<Renderer> wallsHitByCurrentSample = new HashSet<Renderer>();

    private readonly Dictionary<Renderer, int> blockedSampleCounts =
        new Dictionary<Renderer, int>();

    private readonly Dictionary<Renderer, float> currentFade = new Dictionary<Renderer, float>();

    private MaterialPropertyBlock propertyBlock;

    // =====================================================
    // INITIALIZATION
    // =====================================================

    private void Start()
    {
        if (player != null)
        {
            playerController = player.GetComponent<CharacterController>();
        }

        fadeWalls = FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        propertyBlock = new MaterialPropertyBlock();

        foreach (Renderer wall in fadeWalls)
        {
            if (wall == null)
                continue;

            if (((1 << wall.gameObject.layer) & fadeWhenBlockingPlayerLayer.value) == 0)
            {
                continue;
            }

            currentFade[wall] = 1f;
            SetFade(wall, 1f);
        }
    }

    // =====================================================
    // UPDATE
    // =====================================================

    private void Update()
    {
        if (player == null)
            return;

        obstructingWalls.Clear();
        blockedSampleCounts.Clear();

        BuildPlayerVisibilitySamples();

        for (int sampleIndex = 0; sampleIndex < playerVisibilitySamples.Length; sampleIndex++)
        {
            CollectWallsBlockingSample(playerVisibilitySamples[sampleIndex]);
        }

        foreach (KeyValuePair<Renderer, int> pair in blockedSampleCounts)
        {
            if (pair.Value >= RequiredBlockedSamples)
            {
                obstructingWalls.Add(pair.Key);
            }
        }

        foreach (Renderer wall in fadeWalls)
        {
            if (wall == null)
                continue;

            if (!currentFade.ContainsKey(wall))
                continue;

            float targetFade = obstructingWalls.Contains(wall) ? fadedAlpha : 1f;

            float fade = Mathf.MoveTowards(
                currentFade[wall],
                targetFade,
                fadeSpeed * Time.deltaTime
            );

            currentFade[wall] = fade;

            SetFade(wall, fade);
        }
    }

    // =====================================================
    // PLAYER VISIBILITY SAMPLES
    // =====================================================

    private void BuildPlayerVisibilitySamples()
    {
        if (playerController != null)
        {
            Bounds bounds = playerController.bounds;

            Vector3 horizontalCenter = new Vector3(bounds.center.x, 0f, bounds.center.z);

            float bottom = bounds.min.y;
            float height = bounds.size.y;

            // Head / upper body.
            playerVisibilitySamples[0] = horizontalCenter + Vector3.up * (bottom + height * 0.82f);

            // Chest / torso.
            playerVisibilitySamples[1] = horizontalCenter + Vector3.up * (bottom + height * 0.62f);

            // Lower torso / hips.
            playerVisibilitySamples[2] = horizontalCenter + Vector3.up * (bottom + height * 0.42f);

            return;
        }

        // Defensive fallback for a player without a
        // CharacterController.
        playerVisibilitySamples[0] = player.position + Vector3.up * 1.4f;

        playerVisibilitySamples[1] = player.position + Vector3.up * 1.0f;

        playerVisibilitySamples[2] = player.position + Vector3.up * 0.7f;
    }

    // =====================================================
    // CAMERA -> PLAYER OCCLUSION
    // =====================================================

    private void CollectWallsBlockingSample(Vector3 playerSample)
    {
        Vector3 direction = playerSample - transform.position;

        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return;

        direction /= distance;

        int hitCount = Physics.RaycastNonAlloc(
            transform.position,
            direction,
            occlusionHits,
            distance,
            fadeWhenBlockingPlayerLayer,
            QueryTriggerInteraction.Ignore
        );

        wallsHitByCurrentSample.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = occlusionHits[i];

            if (hit.collider == null)
                continue;

            // Safety against a hit that numerically lands
            // at / beyond the player sample.
            if (hit.distance >= distance - 0.01f)
                continue;

            Renderer wall = hit.collider.GetComponent<Renderer>();

            if (wall == null)
            {
                wall = hit.collider.GetComponentInParent<Renderer>();
            }

            if (wall == null)
                continue;

            wallsHitByCurrentSample.Add(wall);
        }

        foreach (Renderer wall in wallsHitByCurrentSample)
        {
            if (blockedSampleCounts.TryGetValue(wall, out int count))
            {
                blockedSampleCounts[wall] = count + 1;
            }
            else
            {
                blockedSampleCounts[wall] = 1;
            }
        }
    }

    // =====================================================
    // PUBLIC QUERY
    // =====================================================

    public bool IsBlockingPlayer(Renderer renderer)
    {
        return renderer != null && obstructingWalls.Contains(renderer);
    }

    // =====================================================
    // SHADER PROPERTY
    // =====================================================

    private void SetFade(Renderer renderer, float fade)
    {
        propertyBlock.Clear();

        renderer.GetPropertyBlock(propertyBlock);

        propertyBlock.SetFloat(FadeId, fade);

        renderer.SetPropertyBlock(propertyBlock);
    }
}
