using UnityEngine;

// Observes transport phases; never drives movement or VFX clocks.
[DisallowMultipleComponent, RequireComponent(typeof(BeamTransportController))]
public sealed class BeamAudioPresentation : MonoBehaviour
{
    [SerializeField] private SoundEvent startSound;
    [SerializeField] private SoundEvent loopSound;
    [SerializeField] private SoundEvent endSound;
    [SerializeField] private AudioEmitter emitter;
    private BeamTransportController transport;
    private void Awake() => transport = GetComponent<BeamTransportController>();
    private void OnEnable()
    {
        if (transport == null) transport = GetComponent<BeamTransportController>();
        transport.BeamShown += Begin;
        transport.MotionStarted += Moving;
        transport.DestinationReached += Landed;
        transport.TransportEnded += Stop;
    }
    private void OnDisable()
    {
        transport.BeamShown -= Begin;
        transport.MotionStarted -= Moving;
        transport.DestinationReached -= Landed;
        transport.TransportEnded -= Stop;
        Stop();
    }
    private void Begin(Vector3 position) { Stop(); AudioService.Play(startSound, position); }
    private void Moving() { if (emitter != null) emitter.Play(loopSound); }
    private void Landed() { Stop(); AudioService.Play(endSound, transform.position); }
    private void Stop() { if (emitter != null) emitter.Stop(); }
}
