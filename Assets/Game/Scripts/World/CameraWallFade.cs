using UnityEngine;

public class CameraWallFade : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private LayerMask wallLayer;

    [SerializeField] private float fadeDistance = 2.5f;

    [Range(0f, 1f)]
    [SerializeField] private float fadedAlpha = 0.2f;

    [SerializeField] private float fadeSpeed = 8f;

    private Renderer[] fadeWalls;

    void Start()
    {
        fadeWalls = FindObjectsByType<Renderer>();
    }

    void Update()
    {
        foreach (Renderer wall in fadeWalls)
        {
            if (wall == null)
                continue;
            if (((1 << wall.gameObject.layer) & wallLayer.value) == 0)
                continue;

            Collider wallCollider = wall.GetComponent<Collider>();

            if (wallCollider == null)
                continue;

            Vector3 closestPoint =
                wallCollider.ClosestPoint(player.position);

            float distance =
                Vector3.Distance(player.position, closestPoint);

            float targetAlpha =
                distance <= fadeDistance ? fadedAlpha : 1f;

            Color color = wall.material.GetColor("_BaseColor");

            color.a = Mathf.MoveTowards(
                color.a,
                targetAlpha,
                fadeSpeed * Time.deltaTime
            );

            wall.material.SetColor("_BaseColor", color);
        }
    }
}