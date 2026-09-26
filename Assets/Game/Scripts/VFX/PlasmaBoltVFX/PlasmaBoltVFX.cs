using System;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PlasmaBoltVFX : MonoBehaviour
{
    [Header("Bolt")]
    [SerializeField]
    private float speed = DefaultSpeed;
    public const float DefaultSpeed = 35f;
    public float TravelSpeed => speed > 0 && float.IsFinite(speed) ? speed : DefaultSpeed;

    [SerializeField]
    private float boltLength = 0.35f;

    [Header("Fallback")]
    [SerializeField]
    private Color defaultColor = Color.magenta;

    private LineRenderer line;
    private MaterialPropertyBlock propertyBlock;

    private Vector3 direction;
    private Vector3 target;
    private float totalDistance;
    private float travelled;

    private Action onArrive;
    private bool activeBolt;
    private bool useUnscaledTime;
    private float? scheduledArrival;
    private float departureTime;
    private Vector3 startPoint;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;

        propertyBlock =
            new MaterialPropertyBlock();
    }

    public void Initialize(
        Vector3 start,
        Vector3 end,
        Color? auraColor = null,
        Action onArrive = null,
        bool useUnscaledTime = false,
        float? arrivalTime = null
    )
    {
        direction =
            (end - start).normalized;

        target =
            end;

        totalDistance =
            Vector3.Distance(
                start,
                end
            );

        travelled = 0f;
        scheduledArrival = arrivalTime;
        departureTime = Time.time;
        startPoint = start;

        transform.position =
            start;

        this.onArrive =
            onArrive;

        this.useUnscaledTime =
            useUnscaledTime;

        activeBolt = true;

        SetColor(
            auraColor ?? defaultColor
        );

        UpdateLine();

        // Very short / zero-length shots should still finish cleanly.
        if (totalDistance <= 0.001f)
        {
            Arrive();
        }
    }

    private void Update()
    {
        if (!activeBolt)
            return;

        float movement =
            speed * (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

        travelled += movement;
        if (scheduledArrival.HasValue)
        {
            float duration = scheduledArrival.Value - departureTime;
            travelled = duration <= 0 ? totalDistance : totalDistance * Mathf.Clamp01((Time.time - departureTime) / duration);
        }

        if (travelled >= totalDistance)
        {
            transform.position =
                target;

            UpdateLine();
            Arrive();

            return;
        }

        transform.position = startPoint + direction * travelled;

        UpdateLine();
    }

    private void Arrive()
    {
        if (!activeBolt)
            return;

        activeBolt = false;

        Action callback =
            onArrive;

        onArrive = null;

        callback?.Invoke();

        VfxPool.Release(
            this
        );
    }

    private void UpdateLine()
    {
        Vector3 head =
            transform.position;

        float currentLength =
            Mathf.Min(
                boltLength,
                travelled
            );

        Vector3 tail =
            head
            - direction
            * currentLength;

        line.SetPosition(
            0,
            tail
        );

        line.SetPosition(
            1,
            head
        );
    }

    private void SetColor(
        Color color
    )
    {
        line.GetPropertyBlock(
            propertyBlock
        );

        propertyBlock.SetColor(
            "_BaseColor",
            color
        );

        propertyBlock.SetColor(
            "_EmissionColor",
            color * 6f
        );

        line.SetPropertyBlock(
            propertyBlock
        );
    }

    private void OnDisable()
    {
        activeBolt = false;
        scheduledArrival = null;
        useUnscaledTime = false;
        onArrive = null;
    }
}
