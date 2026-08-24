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

    [Header("Occlusion")]
    [SerializeField]
    private float sphereRadius = 0.5f;

    [SerializeField]
    private float playerHeightOffset = 0.8f;

    [Header("Fade")]
    [Range(0f, 1f)]
    [SerializeField]
    private float fadedAlpha = 0.15f;

    [SerializeField]
    private float fadeSpeed = 8f;

    private static readonly int FadeId = Shader.PropertyToID("_Fade");

    private Renderer[] fadeWalls;
    private readonly HashSet<Renderer> obstructingWalls = new();
    private readonly Dictionary<Renderer, float> currentFade = new();

    private MaterialPropertyBlock propertyBlock;

    private void Start()
    {
        fadeWalls = FindObjectsByType<Renderer>();

        propertyBlock = new MaterialPropertyBlock();

        foreach (Renderer wall in fadeWalls)
        {
            if (wall == null)
                continue;

            if (((1 << wall.gameObject.layer) & fadeWhenBlockingPlayerLayer.value) == 0)
                continue;

            currentFade[wall] = 1f;
            SetFade(wall, 1f);
        }
    }

    private void Update()
    {
        obstructingWalls.Clear();

        Vector3 target = player.position + Vector3.up * playerHeightOffset;

        Vector3 direction = target - transform.position;
        float distance = direction.magnitude;

        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            sphereRadius,
            direction.normalized,
            distance,
            fadeWhenBlockingPlayerLayer,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            Renderer wall = hit.collider.GetComponent<Renderer>();

            if (wall == null)
                wall = hit.collider.GetComponentInParent<Renderer>();

            if (wall != null)
                obstructingWalls.Add(wall);
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

    private void SetFade(Renderer renderer, float fade)
    {
        propertyBlock.Clear();

        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(FadeId, fade);
        renderer.SetPropertyBlock(propertyBlock);
    }
}
