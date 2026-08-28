using NUnit.Framework;
using UnityEngine;

[TestFixture]
[Category("Core")]
public class PlayerLoadoutStateTests
{
    private GameObject characterObject;
    private CharacterVisual character;
    private WeaponItemData weapon;

    [SetUp]
    public void SetUp()
    {
        PlayerLoadoutState.Clear();

        characterObject =
            new GameObject("TestCharacter");

        characterObject.AddComponent<Animator>();

        character =
            characterObject.AddComponent<CharacterVisual>();

        weapon =
            ScriptableObject.CreateInstance<WeaponItemData>();

        weapon.name =
            "TestWeapon";
    }

    [TearDown]
    public void TearDown()
    {
        PlayerLoadoutState.Clear();

        if (weapon != null)
            Object.DestroyImmediate(weapon);

        if (characterObject != null)
            Object.DestroyImmediate(characterObject);
    }

    [Test]
    public void Initialize_SetsSelectedLoadoutAndMarksStateInitialized()
    {
        PlayerLoadoutState.Initialize(
            character,
            weapon
        );

        Assert.That(
            PlayerLoadoutState.IsInitialized,
            Is.True
        );

        Assert.That(
            PlayerLoadoutState.SelectedCharacter,
            Is.SameAs(character)
        );

        Assert.That(
            PlayerLoadoutState.SelectedWeapon,
            Is.SameAs(weapon)
        );
    }

    [Test]
    public void SelectWeapon_NullRepresentsExplicitNoneSelection()
    {
        PlayerLoadoutState.Initialize(
            character,
            weapon
        );

        PlayerLoadoutState.SelectWeapon(
            null
        );

        Assert.That(
            PlayerLoadoutState.IsInitialized,
            Is.True,
            "Selecting NONE must still count as an initialized menu loadout."
        );

        Assert.That(
            PlayerLoadoutState.SelectedCharacter,
            Is.SameAs(character),
            "Clearing the weapon slot must not clear the selected character."
        );

        Assert.That(
            PlayerLoadoutState.SelectedWeapon,
            Is.Null,
            "NONE must be represented by a null weapon inside an initialized loadout."
        );
    }

    [Test]
    public void Clear_RemovesSelectionsAndAllowsSceneDefaultsAgain()
    {
        PlayerLoadoutState.Initialize(
            character,
            weapon
        );

        PlayerLoadoutState.Clear();

        Assert.That(
            PlayerLoadoutState.IsInitialized,
            Is.False
        );

        Assert.That(
            PlayerLoadoutState.SelectedCharacter,
            Is.Null
        );

        Assert.That(
            PlayerLoadoutState.SelectedWeapon,
            Is.Null
        );
    }
}
