using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class AlienAuthoringTests
{
    [TestCase("ConstructionSite"),TestCase("GamePoc")]
    public void SharedSceneRepairPreservesAuthoredEncounterChoices(string name)
    {
        var previous=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/Game/Scenes/"+name+".unity");
            var actors=Object.FindObjectsByType<EnemyActor>(FindObjectsInactive.Include);
            var weapons=actors.Select(a=>new SerializedObject(a.GetComponent<EnemyEquipment>()).FindProperty("startingWeapon").objectReferenceValue).ToArray();
            int count=actors.Sum(a=>a.GetComponentsInChildren<Component>(true).Length);
            EnemyCombatantSetup.ConfigureScene(scene);EnemyCombatantSetup.ConfigureScene(scene);
            Assert.That(actors.Sum(a=>a.GetComponentsInChildren<Component>(true).Length),Is.EqualTo(count));
            for(int i=0;i<actors.Length;i++)
                Assert.That(new SerializedObject(actors[i].GetComponent<EnemyEquipment>()).FindProperty("startingWeapon").objectReferenceValue,Is.EqualTo(weapons[i]));
        }
        finally
        {
            if(previous.Any(x=>x.isLoaded&&x.isActive))EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
    [Test] public void RepairPreservesLoadoutPickupTuningAndDoesNotDuplicate()
    {
        var root=PrefabUtility.LoadPrefabContents(EnemyCombatantSetup.PrefabPath);
        try
        {
            var equipment=root.GetComponent<EnemyEquipment>();var settings=new SerializedObject(equipment);
            Assert.That(settings.FindProperty("startingWeapon").objectReferenceValue,Is.Null,"Canonical prefab must remain unarmed");
            settings.FindProperty("startingWeapon").objectReferenceValue=EnemyCombatantSetup.Weapon("PlasmaRifleItem");settings.ApplyModifiedPropertiesWithoutUndo();
            var pickup=new SerializedObject(root.GetComponent<EnemyWeaponAwareness>());Assert.That(pickup.FindProperty("allowWeaponPickup").boolValue,Is.False);
            pickup.FindProperty("allowWeaponPickup").boolValue=true;pickup.ApplyModifiedPropertiesWithoutUndo();
            var socket=root.GetComponent<EnemyCombatPresentation>().SocketFor(WeaponAnimationStyle.Rifle);socket.localPosition+=new Vector3(.01f,.02f,.03f);var position=socket.localPosition;
            int count=root.GetComponentsInChildren<Component>(true).Length;
            EnemyCombatantSetup.Configure(root);EnemyCombatantSetup.Configure(root);
            Assert.That(root.GetComponentsInChildren<Component>(true).Length,Is.EqualTo(count));
            Assert.That(socket.localPosition,Is.EqualTo(position));settings.Update();pickup.Update();
            Assert.That(settings.FindProperty("startingWeapon").objectReferenceValue,Is.EqualTo(EnemyCombatantSetup.Weapon("PlasmaRifleItem")));
            Assert.That(pickup.FindProperty("allowWeaponPickup").boolValue,Is.True);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    [Test] public void ProfilesKeepPlayerSkillsOutOfCompatibility()
    {
        var profile=AssetDatabase.LoadAssetAtPath<EnemyCombatProfile>(EnemyCombatantSetup.ProfilePath);
        Assert.That(profile.Allows(EnemyCombatantSetup.Weapon("PlasmaPistolItem")),Is.True);
        Assert.That(profile.Allows(EnemyCombatantSetup.Weapon("PlasmaRifleItem")),Is.True);
        Assert.That(profile.Allows(null),Is.False);
    }
    [Test] public void OnlyTheTwoAuthoredGateAliensAreArmed()
    {
        var previous=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
            var equipped=Object.FindObjectsByType<EnemyEquipment>(FindObjectsInactive.Include)
                .Where(e=>new SerializedObject(e).FindProperty("startingWeapon").objectReferenceValue!=null).ToArray();
            Assert.That(equipped.Length,Is.EqualTo(2));
            foreach(var e in equipped)Assert.That(new SerializedObject(e).FindProperty("dropWeaponOnDeath").boolValue,Is.True);
            Assert.That(equipped.Select(e=>e.name),Is.EquivalentTo(new[]{"PF_Enemy_Melee_POC_V1 (7)","PF_Enemy_Melee_POC_V1 (8)"}));
            EditorSceneManager.OpenScene("Assets/Game/Scenes/GamePoc.unity");
            foreach(var e in Object.FindObjectsByType<EnemyEquipment>(FindObjectsInactive.Include))
                Assert.That(new SerializedObject(e).FindProperty("startingWeapon").objectReferenceValue,Is.Null);
        }
        finally
        {
            if (previous.Any(x=>x.isLoaded && x.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
}
