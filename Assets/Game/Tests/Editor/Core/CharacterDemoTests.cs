using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture, Category("Core")]
public sealed class CharacterDemoTests
{
    [TestCase("Amy")]
    [TestCase("SportyGranny")]
    public void ActualCharactersSupportSharedContract_AndRealEquipment(string characterName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<CharacterVisual>(
            $"Assets/Game/Prefabs/Player/Characters/{characterName}.prefab"
        );
        Assert.That(prefab, Is.Not.Null);
        Assert.That(PreviewVisualSafety.IsVisualPrefab(prefab.gameObject), Is.True);
        var root = new GameObject("Inactive demo staging");
        root.SetActive(false);
        CharacterVisual actor = Object.Instantiate(prefab, root.transform);
        var weaponAdapter = ScriptableObject.CreateInstance<WeaponPreviewEquipmentAdapter>();
        var grenadeAdapter = ScriptableObject.CreateInstance<GrenadePreviewEquipmentAdapter>();
        try
        {
            // Contract check, not a brittle animation-frame/pose assertion.
            var driver = new CharacterAnimatorDriver(actor.Animator, actor.AnimationActions);
            Assert.That(driver.IsCompatible, Is.True);
            Assert.That(
                driver.TryPlayAction(CharacterActionId.GrenadeThrow),
                Is.False,
                "Unimplemented actions must fall back without inventing a throw."
            );
            foreach (string weaponName in new[] { "PlasmaPistolItem", "PlasmaRifleItem" })
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponItemData>(
                    $"Assets/Game/Items/Weapons/{weaponName}.asset"
                );
                Assert.That(
                    weaponAdapter.TryAttach(weapon, actor, root.transform, out var style),
                    Is.True
                );
                Assert.That(style, Is.EqualTo(weapon.animationStyle));
                WeaponInstance instance = actor.GetComponentInChildren<WeaponInstance>(true);
                Assert.That(
                    Vector3.Distance(instance.GripPoint.position, actor.WeaponSocket.position),
                    Is.LessThan(0.001f)
                );
                Object.DestroyImmediate(instance.gameObject);
            }
            var grenade = AssetDatabase.LoadAssetAtPath<GrenadeItemData>(
                "Assets/Game/Data/Items/Grenades/ElectricGrenade.asset"
            );
            Assert.That(
                grenadeAdapter.TryAttach(grenade, actor, root.transform, out var grenadeStyle),
                Is.True
            );
            Assert.That(grenadeStyle, Is.EqualTo(WeaponAnimationStyle.Unarmed));
            Assert.That(actor.GetComponentInChildren<GrenadeInstance>(true), Is.Null);
            Assert.That(actor.GetComponentInChildren<PlayerShooter>(true), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(weaponAdapter);
            Object.DestroyImmediate(grenadeAdapter);
        }
    }

    [Test]
    public void UnsafePrefabAndMissingPropAreRejectedBeforeInstantiation()
    {
        var unsafePrefab = new GameObject("Gameplay object");
        unsafePrefab.AddComponent<PlayerFeedback>();
        var adapter = ScriptableObject.CreateInstance<WeaponPreviewEquipmentAdapter>();
        var item = ScriptableObject.CreateInstance<WeaponItemData>();
        try
        {
            Assert.That(PreviewVisualSafety.IsVisualPrefab(unsafePrefab), Is.False);
            Assert.That(adapter.TryAttach(item, null, null, out _), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(unsafePrefab);
            Object.DestroyImmediate(adapter);
            Object.DestroyImmediate(item);
        }
    }
}
