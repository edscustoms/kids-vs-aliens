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

    public Vector3 AimPoint { get; private set; }
    public bool HasAimPoint { get; private set; }

    private Plane groundPlane;
    private int aimMask;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

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

        // First try actual world geometry / enemies
        if (
            Physics.Raycast(ray, out RaycastHit hit, 1000f, aimMask, QueryTriggerInteraction.Collide)
        )
        {
            AimPoint = hit.point;
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
