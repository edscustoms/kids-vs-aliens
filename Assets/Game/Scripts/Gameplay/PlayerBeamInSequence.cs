using UnityEngine;
using UnityEngine.SceneManagement;

// Level-start adapter. The controller owns all movement and control leases.
[DefaultExecutionOrder(-150), DisallowMultipleComponent]
public sealed class PlayerBeamInSequence : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private BeamTransportVFX transportVfx;
    [SerializeField, Min(0.1f)] private float startHeight = 3.5f;
    [SerializeField, Min(0f)] private float initialDelay = 0.5f;
    [SerializeField, Min(0.1f)] private float descentDuration = 2f;
    [SerializeField, Min(0f)] private float landingHold = 0.35f;
    [Tooltip("Optional fresh-arrival CC. Continue skips LevelStart and never replays this line.")]
    [SerializeField] private DialogueMessage arrivalDialogue;
    private bool started;
    private BeamTransportController arrivalTransport;
    private bool destinationReached;

    // This component belongs on LevelStart itself. There is deliberately no assignable
    // destination reference: root position is the final player root pose, rotation is yaw.
    public Transform ArrivalTransform => transform;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneValidation()
    {
        SceneManager.sceneLoaded -= ValidateScene;
        SceneManager.sceneLoaded += ValidateScene;
    }

    private static void ValidateScene(Scene scene, LoadSceneMode mode)
    {
        if (RunSaveService.IsRestoringScene(scene.name)) return;
        int owners = 0;
        bool hasBeamPlayer = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var owner in root.GetComponentsInChildren<PlayerBeamInSequence>(true))
                if (owner.isActiveAndEnabled) owners++;
            hasBeamPlayer |= root.GetComponentInChildren<BeamTransportController>(true) != null;
        }
        if (hasBeamPlayer && owners != 1)
            Debug.LogError($"Fresh start in '{scene.name}' requires exactly one active LevelStart with PlayerBeamInSequence; found {owners}. Run Tools > Setup > Setup or Repair Active Gameplay Scene. No fallback spawn is intended.");
    }

    private void Awake()
    {
        if (RunSaveService.IsRestoringScene(gameObject.scene.name)) { started = true; if (transportVfx != null) transportVfx.Hide(); return; }
        if (started) return;
        started = true;
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var other in root.GetComponentsInChildren<PlayerBeamInSequence>(true))
                if (other != this && other.isActiveAndEnabled)
                {
                    Debug.LogError("Multiple LevelStart arrival authorities. Remove the unintended duplicate before starting a fresh run.", this);
                    return;
                }
        if (player == null)
        {
            // Prefab assets cannot reference a scene player. Resolve once, in this scene only.
            foreach (var candidate in FindObjectsByType<BeamTransportController>(FindObjectsInactive.Exclude))
                if (candidate.gameObject.scene == gameObject.scene)
                {
                    if (player != null) { Debug.LogError("Multiple beam players in the arrival scene.", this); return; }
                    player = candidate.transform;
                }
        }
        var transport = player != null ? player.GetComponent<BeamTransportController>() : null;
        if (transport == null || !transport.TryArrival(ArrivalTransform, transportVfx, startHeight,
            descentDuration, initialDelay, landingHold))
        {
            string details = transport == null ? "No player transport controller." :
                $"active={transport.isActiveAndEnabled}, capsuleEnabled={transport.GetComponent<CharacterController>().enabled}, "
                + $"suspended={transport.GetComponent<GameplaySuspensionController>().IsSuspended}, vfx={transportVfx != null}, "
                + $"LevelStart={transform.position}, endpointClear={transport.IsSegmentClear(transform.position, transform.position)}, "
                + $"pathClear={transport.IsSegmentClear(transform.position + Vector3.up * startHeight, transform.position)}";
            Debug.LogError("LevelStart arrival could not start. Place the LevelStart root at an unobstructed player-root position with a clear descent path and check scene wiring. " + details, this);
        }
        else if (arrivalDialogue != null)
        {
            arrivalTransport = transport;
            arrivalTransport.DestinationReached += OnDestinationReached;
            arrivalTransport.TransportEnded += OnArrivalEnded;
        }
    }

    private void OnDestinationReached() => destinationReached = true;
    private void OnArrivalEnded()
    {
        UnsubscribeArrival();
        // TransportEnded also reports cancellation. Only an arrival which reached
        // its authored endpoint can speak, after the transport has released control.
        if (!destinationReached || player == null || player.GetComponent<PlayerHealth>().IsDead) return;
        DialoguePlayer.Instance?.Play(arrivalDialogue, player.GetComponent<PlayerCharacter>().ActiveVisual?.DialogueIdentity);
    }
    private void UnsubscribeArrival()
    {
        if (arrivalTransport == null) return;
        arrivalTransport.DestinationReached -= OnDestinationReached;
        arrivalTransport.TransportEnded -= OnArrivalEnded;
        arrivalTransport = null;
    }
    private void OnDisable() => UnsubscribeArrival();
}
