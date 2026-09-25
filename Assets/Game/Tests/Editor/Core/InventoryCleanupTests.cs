using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[TestFixture, Category("ProceduralUI")]
public sealed class InventoryCleanupTests
{
    [Test] public void LearnedBookRejectionDoesNotInsertOrNotifyOrUnlockAgain()
    {
        var owner=new GameObject("Book pickup test");
        var skill=ScriptableObject.CreateInstance<SkillData>();
        var book=ScriptableObject.CreateInstance<KnowledgeBookItemData>();
        try {
            var serialized=new SerializedObject(skill);serialized.FindProperty("id").stringValue=Guid.NewGuid().ToString();serialized.ApplyModifiedProperties();
            book.skill=skill;book.itemType=ItemType.KnowledgeBook;
            var skills=owner.AddComponent<PlayerSkillState>();var inventory=owner.AddComponent<PlayerInventory>();
            int unlocks=0,changes=0;skills.SkillUnlocked+=_=>unlocks++;inventory.OnInventoryChanged+=()=>changes++;
            Assert.That(inventory.TryAddItem(book,out var first),Is.True);Assert.That(first,Is.EqualTo(InventoryAddFailure.None));
            Assert.That(inventory.TryAddItem(book),Is.True); // Two copies obtained before learning.
            // Same first-time consume path, including removal of the learned book.
            inventory.UseItem(0);
            Assert.That(skills.HasSkill(skill),Is.True);Assert.That(inventory.Items.Count,Is.Zero);
            int before=changes;
            Assert.That(inventory.TryAddItem(book,out var duplicate),Is.False);
            Assert.That(duplicate,Is.EqualTo(InventoryAddFailure.AlreadyLearned));
            Assert.That(inventory.Items.Count,Is.Zero);Assert.That(changes,Is.EqualTo(before));Assert.That(unlocks,Is.EqualTo(1));
            var normal=ScriptableObject.CreateInstance<GrenadeItemData>();
            try {
                inventory.RestoreSavedItems(new ItemData[]{book,normal,book},new[]{0,1,2,-1,-1});
                Assert.That(inventory.Items,Is.EqualTo(new[]{normal}));
                Assert.That(inventory.CaptureQuickSlots(),Is.EqualTo(new[]{-1,0,-1,-1,-1}));
                Assert.That(unlocks,Is.EqualTo(1));
            } finally { Object.DestroyImmediate(normal); }
        } finally { Object.DestroyImmediate(owner);Object.DestroyImmediate(book);Object.DestroyImmediate(skill); }
    }

    [Test] public void BackpackKeepsAssignedItemsWithoutDuplicatingOwnership()
    {
        var owner=new GameObject("Inventory");var canvas=new GameObject("UI",typeof(Canvas));
        var items=Enumerable.Range(0,7).Select(_=>ScriptableObject.CreateInstance<GrenadeItemData>()).ToArray();
        try {
            var inventory=owner.AddComponent<PlayerInventory>();
            inventory.RestoreSavedItems(items,new[]{-1,-1,-1,-1,-1});
            var view=canvas.AddComponent<InventoryManagementView>();view.Build(inventory,()=>{},()=>{});
            inventory.AssignQuickSlot(0,2);
            Assert.That(view.ItemFor(2,false),Is.SameAs(items[2]));
            Assert.That(Enumerable.Range(0,7).Select(i=>view.ItemFor(i,false)),Is.EqualTo(items));
            inventory.AssignQuickSlot(0,4);
            Assert.That(view.ItemFor(2,false),Is.SameAs(items[2]));
            inventory.AssignQuickSlot(1,3);inventory.AssignQuickSlot(1,4);
            Assert.That(inventory.QuickSlotIndex(0),Is.EqualTo(-1));
            Assert.That(inventory.QuickSlotIndex(1),Is.EqualTo(4));
            Assert.That(view.ItemFor(3,false),Is.SameAs(items[3]));
            var assignments=inventory.CaptureQuickSlots();
            var json=JsonUtility.ToJson(new ActiveRunSave{player=new SavedPlayer{items=Enumerable.Range(0,7).Select(i=>i.ToString()).ToList(),quickSlots=assignments}});
            var restored=JsonUtility.FromJson<ActiveRunSave>(json);
            inventory.RestoreSavedItems(restored.player.items.Select(i=>(ItemData)items[int.Parse(i)]).ToArray(),restored.player.quickSlots);
            Assert.That(inventory.Items.Count,Is.EqualTo(7));Assert.That(view.ItemFor(4,false),Is.SameAs(items[4]));
            inventory.AssignQuickSlot(1,-1);
            Assert.That(Enumerable.Range(0,7).Select(i=>view.ItemFor(i,false)),Is.EqualTo(items));
        } finally { Object.DestroyImmediate(canvas);Object.DestroyImmediate(owner);foreach(var item in items)Object.DestroyImmediate(item); }
    }

    [TestCase(5)] [TestCase(25)]
    public void ExistingSaveRepresentationRestoresBothCapacitiesAndQuickAssignments(int count)
    {
        var owner=new GameObject("Backpack test");var item=ScriptableObject.CreateInstance<GrenadeItemData>();
        try {
            var inventory=owner.AddComponent<PlayerInventory>();
            Assert.That(inventory.Capacity,Is.EqualTo(25));Assert.That(inventory.QuickSlotCount,Is.EqualTo(5));
            var save=new ActiveRunSave();save.player.items=Enumerable.Repeat("fixture-item",count).ToList();save.player.quickSlots=new[]{-1,-1,-1,-1,count-1};
            var restored=JsonUtility.FromJson<ActiveRunSave>(JsonUtility.ToJson(save));
            inventory.RestoreSavedItems(restored.player.items.Select(_=>(ItemData)item).ToArray(),restored.player.quickSlots);
            Assert.That(inventory.Items.Count,Is.EqualTo(1));Assert.That(inventory.CountAt(0),Is.EqualTo(count));Assert.That(inventory.QuickSlotIndex(4),Is.EqualTo(0));
            Assert.That(inventory.TryAddItem(item),Is.True);Assert.That(inventory.CountAt(0),Is.EqualTo(count+1));
        } finally { Object.DestroyImmediate(owner);Object.DestroyImmediate(item); }
    }
}
