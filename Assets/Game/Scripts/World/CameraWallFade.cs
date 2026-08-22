using System.Collections.Generic;
using UnityEngine;

public class CameraWallFade : MonoBehaviour
{
    [SerializeField]
    private Transform player;

    [SerializeField]
    private LayerMask wallLayer;

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

    private Renderer[] fadeWalls;
    private readonly HashSet<Renderer> obstructingWalls = new();

    private void Start()
    {
        fadeWalls = FindObjectsByType<Renderer>();
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
            wallLayer,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            Renderer wall = hit.collider.GetComponent<Renderer>();

            if (wall == null)
            {
                wall = hit.collider.GetComponentInParent<Renderer>();
            }

            if (wall != null)
            {
                obstructingWalls.Add(wall);
            }
        }

        foreach (Renderer wall in fadeWalls)
        {
            if (wall == null)
                continue;

            if (((1 << wall.gameObject.layer) & wallLayer.value) == 0)
                continue;

            float targetAlpha = obstructingWalls.Contains(wall) ? fadedAlpha : 1f;

            Color color = wall.material.GetColor("_BaseColor");

            color.a = Mathf.MoveTowards(color.a, targetAlpha, fadeSpeed * Time.deltaTime);

            wall.material.SetColor("_BaseColor", color);
        }
    }
}
