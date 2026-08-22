using UnityEngine;

public class PickupGlowPulse : MonoBehaviour
{
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float minScale = 0.7f;
    [SerializeField] private float maxScale = 1f;

    private Vector3 baseScale;

    private void Start()
    {
        baseScale = transform.localScale;
    }

    private void Update()
    {
        float t =
            (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;

        float scale =
            Mathf.Lerp(minScale, maxScale, t);

        transform.localScale =
            baseScale * scale;
    }
}