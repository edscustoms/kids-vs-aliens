using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAim : MonoBehaviour
{
    [Header("General")]
    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private float rotationSpeed = 20f;

    [Header("Mobile Auto Aim")]
    [SerializeField]
    private LayerMask enemyLayer;

    [SerializeField]
    private float autoAimRange = 20f;

    // Handy later for testing mobile behaviour inside Unity Editor
    [SerializeField]
    private bool forceMobileAimInEditor = false;

    [SerializeField]
    private FadeWhenBlockingPlayer fadeWhenBlockingPlayer;

    private readonly RaycastHit[] aimHits = new RaycastHit[32];

    public Vector3 AimPoint { get; private set; }
    public bool HasAimPoint { get; private set; }

    private Plane groundPlane;
    private int aimMask;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (fadeWhenBlockingPlayer == null && mainCamera != null)
        {
            fadeWhenBlockingPlayer = mainCamera.GetComponent<FadeWhenBlockingPlayer>();
        }

        // Our current floor is at Y = 0
        groundPlane = new Plane(Vector3.up, Vector3.zero);
        aimMask = ~LayerMask.GetMask("Player");
    }

    private void LateUpdate()
    {
        bool useMobileAim = Application.isMobilePlatform || forceMobileAimInEditor;

        if (useMobileAim)
            AutoAim();
        else
            MouseAim();
    }

    private void MouseAim()
    {
        if (Mouse.current == null || mainCamera == null)
            return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);

        int hitCount = Physics.RaycastNonAlloc(
            ray,
            aimHits,
            1000f,
            aimMask,
            QueryTriggerInteraction.Collide
        );

        bool foundHit = false;
        RaycastHit closestHit = default;
        float closestDistance = Mathf.Infinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = aimHits[i];

            Renderer renderer = hit.collider.GetComponent<Renderer>();

            if (renderer == null)
                renderer = hit.collider.GetComponentInParent<Renderer>();

            // Skip walls currently faded because they block the player's view
            if (
                renderer != null
                && fadeWhenBlockingPlayer != null
                && fadeWhenBlockingPlayer.IsBlockingPlayer(renderer)
            )
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                foundHit = true;
            }
        }

        if (foundHit)
        {
            AimPoint = closestHit.point;
            HasAimPoint = true;

            RotateTowards(AimPoint);
            return;
        }

        // Fallback in case the cursor ray hits nothing
        if (groundPlane.Raycast(ray, out float distance))
        {
            AimPoint = ray.GetPoint(distance);
            HasAimPoint = true;

            RotateTowards(AimPoint);
            return;
        }

        HasAimPoint = false;
    }

    private void AutoAim()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, autoAimRange, enemyLayer);

        if (enemies.Length == 0)
        {
            HasAimPoint = false;
            return;
        }

        Transform closestEnemy = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider enemy in enemies)
        {
            float distance = (enemy.transform.position - transform.position).sqrMagnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy.transform;
            }
        }

        if (closestEnemy != null)
        {
            AimPoint = closestEnemy.position;
            HasAimPoint = true;

            RotateTowards(AimPoint);
        }
    }

    private void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}
