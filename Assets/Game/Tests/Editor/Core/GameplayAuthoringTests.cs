using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GameplayAuthoringTests
{
    [Test] public void DialogueResolvesGirlBoySpecialAndFallbackWithoutVoice()
    {
        var message=ScriptableObject.CreateInstance<DialogueMessage>(); var speaker=ScriptableObject.CreateInstance<DialogueSpeaker>();
        try
        {
            var special=new DialogueMessage.Variant();
            message.specialOverrides=new[]{new DialogueMessage.SpecialOverride{specialCharacterId="amy",content=special}};
            speaker.voiceType=DialogueVoiceType.Girl; Assert.That(message.Resolve(speaker),Is.SameAs(message.girl));
            speaker.voiceType=DialogueVoiceType.Boy; Assert.That(message.Resolve(speaker),Is.SameAs(message.boy));
            speaker.specialCharacterId="amy"; Assert.That(message.Resolve(speaker),Is.SameAs(special));
            speaker.voiceType=DialogueVoiceType.Girl; Assert.That(message.Resolve(speaker),Is.SameAs(special));
            speaker.specialCharacterId="other"; Assert.That(message.Resolve(speaker),Is.SameAs(message.girl));
            Assert.That(new DialogueMessage.Line().voice,Is.Null);
        }
        finally { Object.DestroyImmediate(message); Object.DestroyImmediate(speaker); }
    }

    [Test] public void ReusableEncounterSupportsVisualPlacementAndExistingEquipmentWithoutNewCombatCode()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        try
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(GameplayAuthoringSetup.EncounterPath);
            Assert.That(source,Is.Not.Null);
            Assert.That(source.GetComponentsInChildren<RunWorldObject>(true).All(e=>string.IsNullOrEmpty(e.Id)),Is.True,"Prefab contains no shared scene identities");
            var root=(GameObject)PrefabUtility.InstantiatePrefab(source);
            var encounter=root.GetComponent<AuthoredEncounter>();
            var enemyPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Enemies/PF_Enemy_Melee_POC_V1.prefab");
            var added=GameplayAuthoringSetup.AddMember(encounter,enemyPrefab);
            added.transform.SetPositionAndRotation(new Vector3(7,2,9),Quaternion.Euler(0,125,0));
            var equipment=new SerializedObject(added.GetComponent<EnemyEquipment>());
            equipment.FindProperty("profile").objectReferenceValue=AssetDatabase.LoadAssetAtPath<EnemyCombatProfile>(EnemyCombatantSetup.ProfilePath);
            foreach(string weaponName in new[]{"PlasmaPistolItem","PlasmaRifleItem"})
            {
                var weapon=AssetDatabase.LoadAssetAtPath<WeaponItemData>($"Assets/Game/Items/Weapons/{weaponName}.asset");
                equipment.FindProperty("startingWeapon").objectReferenceValue=weapon; equipment.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(added.GetComponent<EnemyEquipment>().Profile.Allows(weapon),Is.True);
            }
            equipment.FindProperty("startingWeapon").objectReferenceValue=null; equipment.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(added.gameObject.activeSelf,Is.False);
            Assert.That(added.transform.position,Is.EqualTo(new Vector3(7,2,9)));
            Assert.That(Quaternion.Angle(added.transform.rotation,Quaternion.Euler(0,125,0)),Is.LessThan(.01f));
            var ids=root.GetComponentsInChildren<RunWorldObject>(true).Select(e=>e.Id).ToArray();
            GameplayAuthoringSetup.PrepareIdentities(root);
            Assert.That(root.GetComponentsInChildren<RunWorldObject>(true).Select(e=>e.Id),Is.EqualTo(ids));
            Assert.That(ids.All(id=>!string.IsNullOrEmpty(id))&&ids.Distinct().Count()==ids.Length,Is.True);
            Assert.That(root.GetComponent<BoxCollider>().isTrigger,Is.True);
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
    }

    [TestCase("ConstructionSite")]
    [TestCase("GamePoc")]
    public void SharedRepairPreservesAuthoredSettingsAndDoesNotDuplicateOwners(string name)
    {
        using var fixture=new DisposableTestAssets();
        EditorSceneManager.OpenScene(fixture.Copy($"Assets/Game/Scenes/{name}.unity"));
        try
        {
            var player=Object.FindAnyObjectByType<PlayerCharacter>();
            var presentation=Object.FindAnyObjectByType<GameplayInterface>().gameObject;
            GameplayAuthoringSetup.ConfigureScene(player,presentation);
            var owner=Object.FindAnyObjectByType<ObjectiveController>();
            var data=new SerializedObject(owner);
            var opening=AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>(GameplayAuthoringSetup.ObjectivePath);
            data.FindProperty("openingObjective").objectReferenceValue=opening; data.ApplyModifiedPropertiesWithoutUndo();
            var dialogue=Object.FindAnyObjectByType<DialoguePlayer>(); var settings=new SerializedObject(dialogue);
            settings.FindProperty("charactersPerSecond").floatValue=21; settings.ApplyModifiedPropertiesWithoutUndo();
            string id=owner.GetComponent<RunWorldObject>().Id;
            GameplayAuthoringSetup.ConfigureScene(player,presentation); GameplayAuthoringSetup.ConfigureScene(player,presentation);
            Assert.That(Object.FindObjectsByType<ObjectiveController>(FindObjectsInactive.Include).Length,Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<DialoguePlayer>(FindObjectsInactive.Include).Length,Is.EqualTo(1));
            Assert.That(owner.GetComponent<RunWorldObject>().Id,Is.EqualTo(id));
            Assert.That(new SerializedObject(owner).FindProperty("openingObjective").objectReferenceValue,Is.SameAs(opening));
            Assert.That(new SerializedObject(dialogue).FindProperty("charactersPerSecond").floatValue,Is.EqualTo(21));
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
    }
}
