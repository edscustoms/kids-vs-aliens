using UnityEngine;

public sealed class AlienFlybyBike : MonoBehaviour
{
    [SerializeField] private AlienBikeVisual visual;
    [SerializeField] private AudioEmitter engine;
    [SerializeField] private SoundEvent flybySound;
    private AlienFlybyPath path;
    private float speed, distance;
    private bool reverse;
    public bool IsFlying => path != null;
    public void Begin(AlienFlybyPath route, bool backwards, float metersPerSecond, float delay, int variant)
    {
        if (route == null || !route.IsValidated)
            return;
        path = route;
        reverse = backwards;
        speed = metersPerSecond;
        distance = -delay * speed;
        transform.position = path.PositionAtDistance(0, reverse, out var tangent);
        transform.rotation = Quaternion.LookRotation(tangent);
        gameObject.SetActive(true);
        visual.SetVariant(variant);
        visual.SetPower(.45f);
        visual.ClearTrails();
        engine.Play(flybySound);
        engine.SetResponse(.94f + .055f * variant);
    }
    private void Update()
    {
        if (path == null || Time.deltaTime <= 0)
            return;
        distance += speed * Time.deltaTime;
        if (distance < 0)
            return;
        if (distance >= path.Length)
        {
            path = null;
            gameObject.SetActive(false);
            return;
        }
        transform.position = path.PositionAtDistance(distance, reverse, out var tangent);
        transform.rotation = Quaternion.LookRotation(tangent);
    }
    private void OnDisable()
    {
        path = null;
        engine?.Stop();
    }
}
