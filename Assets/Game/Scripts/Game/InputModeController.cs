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
#endif

    private void Awake()
    {
#if UNITY_EDITOR
        ApplyMode(editorMode);

#elif UNITY_ANDROID || UNITY_IOS
        ApplyMode(GameInputMode.Mobile);

#else
        ApplyMode(GameInputMode.Desktop);
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            return;

        ApplyMode(editorMode);
    }
#endif

    private void ApplyMode(GameInputMode mode)
    {
        CurrentMode = mode;

        if (mobileControlsCanvas != null)
        {
            mobileControlsCanvas.SetActive(mode == GameInputMode.Mobile);
        }

        Debug.Log($"Game input mode: {CurrentMode}");
    }
}
