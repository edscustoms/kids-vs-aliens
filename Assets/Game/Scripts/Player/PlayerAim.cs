using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAim : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float rotationSpeed = 20f;

    [Header("Mobile Auto Aim")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float autoAimRange = 20f;

    // Handy later for testing mobile behaviour inside Unity Editor
    [SerializeField] private bool forceMobileAimInEditor = false;

    private Plane groundPlane;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Our current floor is at Y = 0
        groundPlane = new Plane(Vector3.up, Vector3.zero);
    }

    private void LateUpdate()
    {
        bool useMobileAim =
            Application.isMobilePlatform || forceMobileAimInEditor;

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

        if (!groundPlane.Raycast(ray, out float distance))
            return;

        Vector3 targetPosition = ray.GetPoint(distance);

        RotateTowards(targetPosition);
    }

    private void AutoAim()
    {
        Collider[] enemies = Physics.OverlapSphere(
            transform.position,
            autoAimRange,
            enemyLayer
        );

        if (enemies.Length == 0)
            return;

        Transform closestEnemy = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider enemy in enemies)
        {
            float distance =
                (enemy.transform.position - transform.position).sqrMagnitude;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy.transform;
            }
        }

        if (closestEnemy != null)
            RotateTowards(closestEnemy.position);
    }

    private void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}