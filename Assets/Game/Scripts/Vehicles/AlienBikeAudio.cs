using UnityEngine;

/// <summary>One semantic emitter set for a bike. AudioService retains playback/pause ownership.</summary>
public sealed class AlienBikeAudio : MonoBehaviour
{
    [SerializeField] private AudioEmitter engine, charge, boost;
    [SerializeField] private SoundEvent engineSound, chargeSound, jumpSound, turboSound;
    private bool running, charging, boosting;
    public void SetEngine(bool active, float throttle = 0)
    {
        if (active != running)
        {
            running = active && engine != null && engine.Play(engineSound);
            if (!active)
                engine?.Stop();
        }
        engine?.SetResponse(Mathf.Lerp(.82f, 1.35f, Mathf.Clamp01(throttle)), Mathf.Lerp(.65f, 1, Mathf.Clamp01(throttle)));
    }
    public void SetCharge(bool active, float amount)
    {
        if (active != charging)
        {
            charging = active && charge != null && charge.Play(chargeSound);
            if (!active)
                charge?.Stop();
        }
        charge?.SetResponse(Mathf.Lerp(.7f, 1.65f, amount), Mathf.Lerp(.4f, 1, amount));
    }
    public void SetTurbo(bool active)
    {
        if (active == boosting)
            return;
        boosting = active && boost != null && boost.Play(turboSound);
        if (!active)
            boost?.Stop();
    }
    public void Jump() => AudioService.Play(jumpSound, transform.position);
    public void Stop()
    {
        SetEngine(false);
        SetCharge(false, 0);
        SetTurbo(false);
    }
    private void OnDisable() => Stop();
}
