using System;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PlasmaBoltVFX : MonoBehaviour
{
    [Header("Bolt")]
    [SerializeField]
    private float speed = 35f;

    [SerializeField]
    private float boltLength = 0.35f;

    [Header("Fallback")]
    [SerializeField]
    private Color defaultColor = Color.magenta;

    private LineRenderer line;

    private Vector3 direction;
    private Vector3 target;
    private float totalDistance;
    private float travelled;

    private Action onArrive;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
    }

    public void Initialize(
        Vector3 start,
        Vector3 end,
        Color? auraColor = null,
        Action onArrive = null
    )
    {
        direction = (end - start).normalized;
        target = end;

        totalDistance = Vector3.Distance(start, end);
        travelled = 0f;

        transform.position = start;

        this.onArrive = onArrive;

        SetColor(auraColor ?? defaultColor);
        UpdateLine();
    }

    private void Update()
    {
        float movement = speed * Time.deltaTime;

        travelled += movement;

        if (travelled >= totalDistance)
        {
            transform.position = target;
            UpdateLine();

            onArrive?.Invoke();

            Destroy(gameObject);
            return;
        }

        transform.position += direction * movement;

        UpdateLine();
    }

    private void UpdateLine()
    {
        Vector3 head = transform.position;

        float currentLength = Mathf.Min(boltLength, travelled);

        Vector3 tail = head - direction * currentLength;

        line.SetPosition(0, tail);
        line.SetPosition(1, head);
    }

    private void SetColor(Color color)
    {
        MaterialPropertyBlock block = new();

        line.GetPropertyBlock(block);

        block.SetColor("_BaseColor", color);
        block.SetColor("_EmissionColor", color * 6f);

        line.SetPropertyBlock(block);
    }
}
