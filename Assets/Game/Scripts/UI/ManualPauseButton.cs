using UnityEngine;
using UnityEngine.UI;

// Existing top-right button and desktop pause intent open the menu; neither toggles resume.
public sealed class ManualPauseButton : MonoBehaviour
{
    [SerializeField]
    private InGameMenuController menu;

    [SerializeField]
    private StarterAssets.StarterAssetsInputs input;

    [SerializeField]
    private Button button;

    private void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(OpenMenu);
        if (input != null)
            input.PauseRequested += OpenMenu;
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OpenMenu);
        if (input != null)
            input.PauseRequested -= OpenMenu;
    }

    public void OpenMenu()
    {
        if (menu != null)
            menu.OpenMenu();
    }
}
