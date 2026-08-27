using System;
using UnityEngine;

public class PlayerCharacter : MonoBehaviour
{
    [SerializeField]
    private Transform visualRoot;

    [Tooltip(
        "Fallback used when GamePoc is launched directly or no menu character has been selected."
    )]
    [SerializeField]
    private CharacterVisual startingCharacterPrefab;

    public CharacterVisual ActiveVisual { get; private set; }

    public event Action<CharacterVisual> CharacterChanged;

    private void Awake()
    {
        CharacterVisual characterToSpawn =
            PlayerLoadoutState.SelectedCharacter != null
                ? PlayerLoadoutState.SelectedCharacter
                : startingCharacterPrefab;

        SetCharacter(characterToSpawn);
    }

    public void SetCharacter(CharacterVisual characterPrefab)
    {
        if (characterPrefab == null)
        {
            Debug.LogError($"{name}: No character prefab available.");
            return;
        }

        if (ActiveVisual != null)
        {
            Destroy(ActiveVisual.gameObject);
        }

        ActiveVisual = Instantiate(characterPrefab, visualRoot);

        ActiveVisual.transform.localPosition = Vector3.zero;

        ActiveVisual.transform.localRotation = Quaternion.identity;

        ActiveVisual.transform.localScale = Vector3.one;

        CharacterChanged?.Invoke(ActiveVisual);
    }
}
