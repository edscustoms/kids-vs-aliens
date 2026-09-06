using NUnit.Framework;
using System.Reflection;
using UnityEngine;

[TestFixture]
[Category("Core")]
public class PlayerInventoryGrenadeTests
{
    private GameObject playerObject;
    private PlayerInventory inventory;
    private GrenadeItemData grenade;
    private WeaponItemData weapon;

    [SetUp]
    public void SetUp()
    {
        playerObject =
            new GameObject("GrenadeInventoryTestPlayer");

        inventory =
            playerObject.AddComponent<PlayerInventory>();

        grenade =
            ScriptableObject.CreateInstance<GrenadeItemData>();

        weapon =
            ScriptableObject.CreateInstance<WeaponItemData>();
    }

    [TearDown]
    public void TearDown()
    {
        if (grenade != null)
            Object.DestroyImmediate(grenade);

        if (weapon != null)
            Object.DestroyImmediate(weapon);

        if (playerObject != null)
            Object.DestroyImmediate(playerObject);
    }

    [Test]
    public void TryAddItem_GrenadesUseRegularInventoryCapacity()
    {
        Assert.That(inventory.TryAddItem(grenade), Is.True);
        Assert.That(inventory.TryAddItem(grenade), Is.True);
        Assert.That(inventory.TryAddItem(grenade), Is.True);
        Assert.That(inventory.TryAddItem(grenade), Is.True);
        Assert.That(inventory.TryAddItem(grenade), Is.True);
        Assert.That(inventory.TryAddItem(grenade), Is.False);

        Assert.That(inventory.GrenadeCount, Is.EqualTo(5));
        Assert.That(inventory.Items.Count, Is.EqualTo(5));
    }

    [Test]
    public void GrenadesAndWeaponsShareRegularInventoryCapacity()
    {
        Assert.That(inventory.TryAddItem(grenade), Is.True);
        Assert.That(inventory.TryAddItem(grenade), Is.True);
        Assert.That(inventory.TryAddItem(grenade), Is.True);
        Assert.That(inventory.TryAddItem(weapon), Is.True);
        Assert.That(inventory.TryAddItem(weapon), Is.True);
        Assert.That(inventory.TryAddItem(grenade), Is.False);

        Assert.That(inventory.GrenadeCount, Is.EqualTo(3));
        Assert.That(inventory.Items.Count, Is.EqualTo(5));
    }

    [Test]
    public void TryConsumeGrenade_RemovesExactlyOneOccurrence()
    {
        inventory.TryAddItem(grenade);
        inventory.TryAddItem(grenade);

        int notificationCount = 0;

        inventory.OnInventoryChanged +=
            () => notificationCount++;

        Assert.That(
            inventory.TryConsumeGrenade(grenade),
            Is.True);

        Assert.That(inventory.GrenadeCount, Is.EqualTo(1));
        Assert.That(inventory.Items.Count, Is.EqualTo(1));
        Assert.That(notificationCount, Is.EqualTo(1));
    }

    [Test]
    public void CarryVisuals_ClampOwnedGrenadesToAvailableSockets()
    {
        GameObject characterObject =
            new GameObject("GrenadeCarryTestCharacter");

        CharacterVisual character =
            characterObject.AddComponent<CharacterVisual>();

        Transform[] sockets = new Transform[3];

        for (int i = 0; i < sockets.Length; i++)
        {
            GameObject socket = new GameObject($"Socket_{i}");
            socket.transform.SetParent(characterObject.transform, false);
            sockets[i] = socket.transform;
        }

        SetPrivateField(character, "grenadeCarrySockets", sockets);

        PlayerCharacter playerCharacter =
            playerObject.AddComponent<PlayerCharacter>();

        PropertyInfo activeVisualProperty =
            typeof(PlayerCharacter).GetProperty(
                nameof(PlayerCharacter.ActiveVisual));

        activeVisualProperty.GetSetMethod(true).Invoke(
            playerCharacter,
            new object[] { character });

        GameObject stowedVisual =
            new GameObject("StowedGrenadeTestVisual");

        grenade.stowedPrefab = stowedVisual;

        for (int i = 0; i < 5; i++)
            Assert.That(inventory.TryAddItem(grenade), Is.True);

        GrenadeCarryVisuals carry =
            playerObject.AddComponent<GrenadeCarryVisuals>();

        SetPrivateField(carry, "inventory", inventory);
        SetPrivateField(carry, "playerCharacter", playerCharacter);

        carry.Refresh();

        int visibleCount = 0;

        for (int i = 0; i < sockets.Length; i++)
            visibleCount += sockets[i].childCount;

        Assert.That(inventory.GrenadeCount, Is.EqualTo(5));
        Assert.That(visibleCount, Is.EqualTo(3));

        Object.DestroyImmediate(stowedVisual);
        Object.DestroyImmediate(characterObject);
    }

    private static void SetPrivateField(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
