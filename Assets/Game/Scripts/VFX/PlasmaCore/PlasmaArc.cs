using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PlasmaArc : MonoBehaviour
{
    [SerializeField]
    private int segments = 12;

    [SerializeField]
    private float maxLength = 0.18f;

    [SerializeField]
    private float jitter = 0.025f;

    [SerializeField]
    private float refreshRate = 0.05f;

    private LineRenderer line;
    private float timer;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = segments;
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            GenerateArc();
            timer = refreshRate;
        }
    }

    public void Configure(
        int newSegments,
        float newMaxLength,
        float newJitter,
        float newRefreshRate,
        float newWidth,
        Color auraColor
    )
    {
        segments = newSegments;
        maxLength = newMaxLength;
        jitter = newJitter;
        refreshRate = newRefreshRate;

        if (line == null)
            line = GetComponent<LineRenderer>();

        line.positionCount = segments;
        line.startWidth = newWidth;
        line.endWidth = newWidth;

        MaterialPropertyBlock block = new();
        line.GetPropertyBlock(block);

        block.SetColor("_BaseColor", auraColor);
        block.SetColor("_EmissionColor", auraColor * 8f);

        line.SetPropertyBlock(block);
    }

    private void GenerateArc()
    {
        Vector3 start = Vector3.zero;

        Vector3 direction = Random.onUnitSphere;
        Vector3 end = direction * maxLength;

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);

            Vector3 point = Vector3.Lerp(start, end, t);

            if (i > 0 && i < segments - 1)
            {
                point += Random.insideUnitSphere * jitter;
            }

            line.SetPosition(i, point);
        }
    }
}
