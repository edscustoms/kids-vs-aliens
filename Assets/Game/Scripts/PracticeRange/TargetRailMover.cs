using UnityEngine;

public class TargetRailMover : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField]
    private float travelDistance = 6f;

    [SerializeField]
    private float moveSpeed = 2f;

    private Vector3 startLocalPosition;
    private Vector3 leftPosition;
    private Vector3 rightPosition;

    private bool movingRight = true;

    private void Awake()
    {
        startLocalPosition = transform.localPosition;

        float halfDistance = travelDistance * 0.5f;

        leftPosition =
            startLocalPosition +
            Vector3.left * halfDistance;

        rightPosition =
            startLocalPosition +
            Vector3.right * halfDistance;
    }

    private void Update()
    {
        Vector3 targetPosition =
            movingRight
                ? rightPosition
                : leftPosition;

        transform.localPosition =
            Vector3.MoveTowards(
                transform.localPosition,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

        if (
            Vector3.Distance(
                transform.localPosition,
                targetPosition
            ) < 0.001f
        )
        {
            movingRight = !movingRight;
        }
    }
}