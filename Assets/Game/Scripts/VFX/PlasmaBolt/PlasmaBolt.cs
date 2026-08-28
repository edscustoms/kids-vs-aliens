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
    private MaterialPropertyBlock propertyBlock;

    private Vector3 direction;
    private Vector3 target;
    private float totalDistance;
    private float travelled;

    private Action onArrive;
    private bool activeBolt;

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
        Action onArrive = null
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

        transform.position =
            start;

        this.onArrive =
            onArrive;

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
            speed * Time.deltaTime;

        travelled +=
            movement;

        if (travelled >= totalDistance)
        {
            transform.position =
                target;

            UpdateLine();
            Arrive();

            return;
        }

        transform.position +=
            direction * movement;

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
        onArrive = null;
    }
}
