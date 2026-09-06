using UnityEngine;

public enum GameInputMode
{
    Desktop,
    Mobile,
}

public class InputModeController : MonoBehaviour
{
    public static GameInputMode CurrentMode { get; private set; }

    public static bool IsDesktop => CurrentMode == GameInputMode.Desktop;
    public static bool IsMobile => CurrentMode == GameInputMode.Mobile;

    [Header("Mobile UI")]
    [SerializeField]
    private GameObject mobileControlsCanvas;

#if UNITY_EDITOR
    [Header("Editor Testing")]
    [SerializeField]
    private GameInputMode editorMode = GameInputMode.Desktop;

    private bool refreshRequested;
#endif

    private void Awake()
    {
        // Set the logical mode immediately so other Awake() methods
        // can safely query IsDesktop / IsMobile.
        CurrentMode = GetDesiredMode();
    }

    private void Start()
    {
        // UI hierarchy changes are safe here.
        ApplyMode(GetDesiredMode());
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        // Don't touch Canvas hierarchy during OnValidate.
        refreshRequested = true;
    }

    private void Update()
    {
        if (!refreshRequested)
            return;

        refreshRequested = false;
        ApplyMode(editorMode);
    }
#endif

    private GameInputMode GetDesiredMode()
    {
#if UNITY_EDITOR
        return editorMode;
#elif UNITY_ANDROID || UNITY_IOS
        return GameInputMode.Mobile;
#else
        return GameInputMode.Desktop;
#endif
    }

    private void ApplyMode(GameInputMode mode)
    {
        CurrentMode = mode;

        if (mobileControlsCanvas != null)
            mobileControlsCanvas.SetActive(mode == GameInputMode.Mobile);
    }
}
