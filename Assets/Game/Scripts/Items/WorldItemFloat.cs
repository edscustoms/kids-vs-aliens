using UnityEngine;

public class WorldItemFloat : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 80f;
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 2f;

    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.localPosition;
    }

    private void Update()
    {
        // Rotate
        transform.Rotate(
            Vector3.up,
            rotationSpeed * Time.deltaTime,
            Space.World
        );

        // Float up/down
        float yOffset =
            Mathf.Sin(Time.time * bobSpeed) * bobHeight;

        transform.localPosition =
            startPosition + Vector3.up * yOffset;
    }
}