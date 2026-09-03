using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class ElectricArcVFX : MonoBehaviour
{
    [SerializeField]
    private LineRenderer lineRenderer;

    [SerializeField, Range(2, 24)]
    private int pointCount = 9;

    [SerializeField, Min(0f)]
    private float jitter = 0.16f;

    private Vector3[] points =
        new Vector3[0];

    private void Awake()
    {
        CacheReferences();
        EnsurePointBuffer();
        Hide();
    }

    public void Play(
        Vector3 start,
        Vector3 end,
        Color color)
    {
        if (lineRenderer == null)
            return;

        EnsurePointBuffer();

        Vector3 direction =
            end - start;

        float length =
            direction.magnitude;

        Vector3 normalizedDirection =
            length > 0.0001f
                ? direction / length
                : Vector3.forward;

        for (int i = 0;
             i < points.Length;
             i++)
        {
            float progress =
                points.Length > 1
                    ? i / (float)(points.Length - 1)
                    : 0f;

            Vector3 point =
                Vector3.Lerp(
                    start,
                    end,
                    progress);

            if (i > 0 &&
                i < points.Length - 1)
            {
                Vector3 offset =
                    Random.insideUnitSphere;

                offset -=
                    normalizedDirection *
                    Vector3.Dot(
                        offset,
                        normalizedDirection);

                point +=
                    offset *
                    jitter *
                    Mathf.Max(
                        0.25f,
                        length);
            }

            points[i] = point;
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount =
            points.Length;

        lineRenderer.SetPositions(
            points);

        lineRenderer.startColor =
            color;

        lineRenderer.endColor =
            color;

        lineRenderer.enabled = true;
    }

    public void Hide()
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    private void CacheReferences()
    {
        if (lineRenderer == null)
        {
            lineRenderer =
                GetComponent<LineRenderer>();
        }
    }

    private void EnsurePointBuffer()
    {
        pointCount =
            Mathf.Clamp(
                pointCount,
                2,
                24);

        if (points == null ||
            points.Length != pointCount)
        {
            points =
                new Vector3[pointCount];
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
        EnsurePointBuffer();
    }
#endif
}
