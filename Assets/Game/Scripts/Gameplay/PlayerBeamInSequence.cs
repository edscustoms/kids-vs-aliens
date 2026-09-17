using UnityEngine;

// Level-start adapter. The controller owns all movement and control leases.
[DefaultExecutionOrder(-150), DisallowMultipleComponent]
public sealed class PlayerBeamInSequence : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform beamInSpawn;
    [SerializeField] private BeamTransportVFX transportVfx;
    [SerializeField, Min(0.1f)] private float startHeight = 3.5f;
    [SerializeField, Min(0f)] private float initialDelay = 0.5f;
    [SerializeField, Min(0.1f)] private float descentDuration = 2f;
    [SerializeField, Min(0f)] private float landingHold = 0.35f;
    private bool started;

    private void Awake()
    {
        if (started) return;
        started = true;
        var transport = player != null ? player.GetComponent<BeamTransportController>() : null;
        if (transport == null || !transport.TryArrival(beamInSpawn, transportVfx, startHeight,
            descentDuration, initialDelay, landingHold))
        {
            string details = transport == null ? "No player transport controller." :
                $"active={transport.isActiveAndEnabled}, capsuleEnabled={transport.GetComponent<CharacterController>().enabled}, "
                + $"suspended={transport.GetComponent<GameplaySuspensionController>().IsSuspended}, vfx={transportVfx != null}, "
                + $"landingSafe={beamInSpawn != null && transport.IsLandingSafe(beamInSpawn.position)}, "
                + $"pathClear={beamInSpawn != null && transport.IsSegmentClear(beamInSpawn.position + Vector3.up * startHeight, beamInSpawn.position)}";
            Debug.LogError("Beam arrival could not start. Check scene wiring and capsule path/support. " + details, this);
        }
    }
}
