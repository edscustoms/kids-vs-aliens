using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MenuController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField]
    private MenuPreviewCatalog catalog;

    [Header("Preview")]
    [SerializeField]
    private MenuPreviewStage previewStage;

    [Header("Type Carousel")]
    [SerializeField]
    private Button previousTypeButton;

    [SerializeField]
    private Button nextTypeButton;

    [SerializeField]
    private TMP_Text typeNameText;

    [Header("Item Carousel")]
    [SerializeField]
    private Button previousItemButton;

    [SerializeField]
    private Button nextItemButton;

    [SerializeField]
    private TMP_Text itemNameText;

    [Header("Game")]
    [SerializeField]
    private string gameSceneName = "GamePoc";

    private readonly List<MenuPreviewItem> currentItems = new();

    private MenuPreviewType currentType;
    private int currentItemIndex;

    private void Awake()
    {
        previousTypeButton.onClick.AddListener(PreviousType);
        nextTypeButton.onClick.AddListener(NextType);

        previousItemButton.onClick.AddListener(PreviousItem);
        nextItemButton.onClick.AddListener(NextItem);
    }

    private void Start()
    {
        currentType = MenuPreviewType.Character;
        currentItemIndex = 0;

        RefreshType();
    }

    private void OnDestroy()
    {
        previousTypeButton.onClick.RemoveListener(PreviousType);
        nextTypeButton.onClick.RemoveListener(NextType);

        previousItemButton.onClick.RemoveListener(PreviousItem);
        nextItemButton.onClick.RemoveListener(NextItem);
    }

    private void PreviousType()
    {
        ChangeType(-1);
    }

    private void NextType()
    {
        ChangeType(1);
    }

    private void ChangeType(int direction)
    {
        int typeCount = Enum.GetValues(typeof(MenuPreviewType)).Length;

        int newIndex = (int)currentType + direction;

        if (newIndex < 0)
            newIndex = typeCount - 1;

        if (newIndex >= typeCount)
            newIndex = 0;

        currentType = (MenuPreviewType)newIndex;

        currentItemIndex = 0;

        RefreshType();
    }

    private void PreviousItem()
    {
        ChangeItem(-1);
    }

    private void NextItem()
    {
        ChangeItem(1);
    }

    private void ChangeItem(int direction)
    {
        if (currentItems.Count == 0)
            return;

        currentItemIndex += direction;

        if (currentItemIndex < 0)
            currentItemIndex = currentItems.Count - 1;

        if (currentItemIndex >= currentItems.Count)
            currentItemIndex = 0;

        RefreshItem();
    }

    private void RefreshType()
    {
        typeNameText.text = currentType.ToString().ToUpperInvariant();

        catalog.GetItems(currentType, currentItems);

        currentItemIndex = 0;

        RefreshItem();
    }

    private void RefreshItem()
    {
        if (currentItems.Count == 0)
        {
            itemNameText.text = "NONE";

            previewStage.Clear();

            previousItemButton.interactable = false;
            nextItemButton.interactable = false;

            return;
        }

        previousItemButton.interactable = currentItems.Count > 1;

        nextItemButton.interactable = currentItems.Count > 1;

        MenuPreviewItem item = currentItems[currentItemIndex];

        itemNameText.text = item.displayName.ToUpperInvariant();

        previewStage.Show(item.previewPrefab);
    }

    public void PlayGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
