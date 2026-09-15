using UnityEngine;

/// <summary>Screen entry points remain stable when presentation transitions are added later.</summary>
public sealed class UIScreenRouter : MonoBehaviour
{
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject options;

    public void Configure(GameObject main, GameObject settings) { mainMenu = main; options = settings; }
    private void Start() => ShowMainMenu();
    public void ShowMainMenu() => Show(mainMenu);
    public void ShowOptions() => Show(options);
    private void Show(GameObject screen)
    {
        if (mainMenu != null) mainMenu.SetActive(screen == mainMenu);
        if (options != null) options.SetActive(screen == options);
    }
}
