using System;
using UnityEngine;

public class PlayerCharacter : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private CharacterVisual startingCharacterPrefab;

    public CharacterVisual ActiveVisual { get; private set; }

    public event Action<CharacterVisual> CharacterChanged;

    private void Awake()
    {
        SetCharacter(startingCharacterPrefab);
    }

    public void SetCharacter(CharacterVisual characterPrefab)
    {
        if (characterPrefab == null)
            return;

        if (ActiveVisual != null)
            Destroy(ActiveVisual.gameObject);

        ActiveVisual = Instantiate(characterPrefab, visualRoot);

        ActiveVisual.transform.localPosition = Vector3.zero;
        ActiveVisual.transform.localRotation = Quaternion.identity;
        ActiveVisual.transform.localScale = Vector3.one;

        CharacterChanged?.Invoke(ActiveVisual);
    }
}