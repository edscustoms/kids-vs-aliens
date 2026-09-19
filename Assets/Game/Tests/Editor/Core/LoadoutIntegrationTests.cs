using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[TestFixture, Category("LoadoutIntegration")]
public sealed class LoadoutIntegrationTests
{
    [Test]
    public void LearnedCombatUsesNoStorageAndAssignmentSurvivesMigrationAndRoundTrip()
    {
        var owner=new GameObject("Virtual combat test");
        var combat=AssetDatabase.LoadAssetAtPath<UnarmedCombatItemData>("Assets/Game/Data/Items/UnarmedCombat/Fighting.asset");
        var weapon=ScriptableObject.CreateInstance<WeaponItemData>();
        try {
            PlayerSkillState.ResetRuntimeSkills();
            var skills=owner.AddComponent<PlayerSkillState>();
            var melee=owner.AddComponent<PlayerMeleeController>();
            var data=new SerializedObject(melee);data.FindProperty("defaultCombatItem").objectReferenceValue=combat;data.ApplyModifiedPropertiesWithoutUndo();
            var inventory=owner.AddComponent<PlayerInventory>();
            var inventoryData=new SerializedObject(inventory);
            inventoryData.FindProperty("playerMeleeController").objectReferenceValue=melee;
            inventoryData.FindProperty("playerSkillState").objectReferenceValue=skills;
            inventoryData.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(inventory.AssignQuickSlot(1,PlayerInventory.CombatEntry),Is.False);
            skills.UnlockSkill(combat.requiredSkill);
            Assert.That(inventory.LearnedCombat,Is.SameAs(combat));
            Assert.That(InterfaceIconCatalog.ForItem(combat),Is.Not.Null);
            Assert.That(inventory.TryAddItem(combat),Is.False);
            Assert.That(inventory.EnsureOwnedWeapon(weapon),Is.True);
            Assert.That(inventory.EnsureOwnedWeapon(weapon),Is.True);
            Assert.That(inventory.Items.Count,Is.EqualTo(1));
            Assert.That(inventory.AssignQuickSlot(1,PlayerInventory.CombatEntry),Is.True);
            inventory.RestoreSavedItems(new ItemData[]{weapon},inventory.CaptureQuickSlots());
            Assert.That(inventory.QuickSlotItem(1),Is.SameAs(combat));
            inventory.RestoreSavedItems(new ItemData[]{combat,weapon},new[]{0,1,-1,-1,-1});
            Assert.That(inventory.Items,Is.EqualTo(new[]{weapon}));
            Assert.That(inventory.QuickSlotItem(0),Is.SameAs(combat));
            Assert.That(inventory.QuickSlotIndex(1),Is.EqualTo(0));
            inventory.AssignQuickSlot(4,PlayerInventory.CombatEntry);
            Assert.That(inventory.QuickSlotItem(0),Is.Null);
            Assert.That(inventory.Items.Count,Is.EqualTo(1));
        } finally { Object.DestroyImmediate(owner);Object.DestroyImmediate(weapon);PlayerSkillState.ResetRuntimeSkills(); }
    }

    [TestCase(0)] [TestCase(1)]
    public void FullResetClearsBothGenerationsAndKeepsSettings(int corrupt)
    {
        string previous=Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY");
        string directory=Path.GetFullPath("Logs/ResetTest-"+Guid.NewGuid().ToString("N"));
        const string preference="LoadoutIntegration.SettingsProbe";
        try {
            Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",directory);
            PlayerPrefs.SetInt(preference,83);
            var permanent=new PermanentSave();permanent.skills.Add(new SavedSkill{id="learned",xp=500});
            RunSaveService.PermanentStore.Write(permanent);RunSaveService.PermanentStore.Write(permanent);
            RunSaveService.ActiveStore.Write(new ActiveRunSave{sceneName="GamePoc",runId="old"});
            Assert.That(RunSaveService.ResetGameProgress(),Is.True);
            // Force fallback to either generation: neither may resurrect progress.
            File.WriteAllText(Path.Combine(directory,$"permanent.{corrupt}.json"),"torn");
            File.WriteAllText(Path.Combine(directory,$"active-run.{corrupt}.json"),"torn");
            Assert.That(RunSaveService.PermanentStore.Read<PermanentSave>().skills,Is.Empty);
            Assert.That(RunSaveService.TryReadActive(out var run),Is.True);Assert.That(run,Is.Null);
            Assert.That(PlayerPrefs.GetInt(preference),Is.EqualTo(83));
        } finally { Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",previous);PlayerPrefs.DeleteKey(preference); }
    }
}
