using UnityEngine;
using UnityEngine.UI;

/// <summary>One shared menu sound policy, independent of button labels and save state.</summary>
[DefaultExecutionOrder(900), DisallowMultipleComponent, RequireComponent(typeof(AudioService))]
public sealed class UIAudioFeedback : MonoBehaviour
{
    [SerializeField] private SoundEvent clickSound;
    [SerializeField] private SoundEvent playConfirmSound;
    private static UIAudioFeedback instance;
    private static int confirmedFrame = -1;
    private bool clickPending;
    private bool retainClick;
    private AudioService service;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; confirmedFrame = -1; }
    private void Awake() { service = GetComponent<AudioService>(); instance = this; }
    private void Start()
    {
        // One scene-local pass includes inactive authored screens. Dynamic buttons
        // register through InterfaceFactory/UIButton; no per-frame hierarchy searches.
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var button in root.GetComponentsInChildren<Button>(true)) UIAudioButton.Ensure(button);
    }

    public static void Click(bool changingScene = false)
    {
        if (instance == null || confirmedFrame == Time.frameCount) return;
        instance.clickPending = true;
        instance.retainClick |= changingScene;
    }

    // Called only after the existing flow accepts a launch, or releases a pause.
    // Defer the ordinary click to LateUpdate so listener order never doubles it.
    public static void ConfirmGameplay(bool changingScene = false)
    {
        if (instance == null || confirmedFrame == Time.frameCount) return;
        confirmedFrame = Time.frameCount;
        instance.clickPending = false;
        instance.retainClick = false;
        AudioService.Play2D(instance.playConfirmSound);
        if (changingScene) instance.service.PreserveUiTailForSceneChange();
    }
    private void LateUpdate()
    {
        if (!clickPending) return;
        clickPending = false;
        if (confirmedFrame != Time.frameCount) AudioService.Play2D(clickSound);
        if (retainClick) service.PreserveUiTailForSceneChange();
        retainClick = false;
    }
    private void OnDestroy() { if (instance == this) instance = null; }
}
