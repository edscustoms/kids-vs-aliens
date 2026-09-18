using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

[TestFixture, Category("RunInterface")]
public sealed class RunPersistenceTests
{
    private string directory;
    [SetUp] public void Setup(){directory=Path.Combine(Application.temporaryCachePath,"RunTests",Guid.NewGuid().ToString("N"));}
    [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    [Test] public void TornLatestGenerationFallsBackWithoutLosingPreviousSnapshot()
    {
        var store=new RecoverableJsonStore(directory,"run");
        store.Write(new ActiveRunSave{runId="first"});store.Write(new ActiveRunSave{runId="second"});
        File.WriteAllText(Path.Combine(directory,"run.0.json"),"{torn");
        Assert.That(store.Read<ActiveRunSave>().runId,Is.EqualTo("first"));
        store.Write(new ActiveRunSave{runId="recovered"});Assert.That(store.Read<ActiveRunSave>().runId,Is.EqualTo("recovered"));
    }
    [Test] public void ChecksumRejectsTamperedPayload()
    {
        var store=new RecoverableJsonStore(directory,"run");store.Write(new ActiveRunSave{runId="safe"});
        string path=Path.Combine(directory,"run.1.json");File.WriteAllText(path,File.ReadAllText(path).Replace("safe","evil"));
        Assert.Throws<IOException>(()=>store.Read<ActiveRunSave>());Assert.That(File.Exists(path),Is.True);
    }
    [Test] public void DiscardTombstoneSupersedesRecoverableOldRun()
    {
        var store=new RecoverableJsonStore(directory,"run");store.Write(new ActiveRunSave{runId="old"});
        store.Write(new ActiveRunSave{discarded=true});Assert.That(store.Read<ActiveRunSave>().discarded,Is.True);
    }
    [Test] public void ActiveRunRoundTripsFunctionalState()
    {
        var save=new ActiveRunSave{runId="run",elapsedSeconds=42,sceneName="ConstructionSite"};
        save.player.position=new Vector3(3,4,5);save.player.items.Add("grenade-id");save.player.quickSlots=new[]{-1,0,-1,-1,-1};save.player.ammo=7;
        save.world.Add(new SavedWorldObject{id="chest",chestOpen=true});save.world.Add(new SavedWorldObject{id="pickup",removed=true});
        var store=new RecoverableJsonStore(directory,"run");store.Write(save);var restored=store.Read<ActiveRunSave>();
        Assert.That(restored.player.position,Is.EqualTo(save.player.position));Assert.That(restored.player.quickSlots[1],Is.Zero);
        Assert.That(restored.player.ammo,Is.EqualTo(7));Assert.That(restored.world[0].chestOpen,Is.True);Assert.That(restored.world[1].removed,Is.True);
    }
    [Test] public void QuickSlotReorderAndRemovalPreserveOneInventory()
    {
        var owner=new GameObject("Inventory test");var inventory=owner.AddComponent<PlayerInventory>();
        var a=ScriptableObject.CreateInstance<GrenadeItemData>();var b=ScriptableObject.CreateInstance<GrenadeItemData>();
        try {
            inventory.TryAddItem(a);inventory.TryAddItem(b);inventory.AssignQuickSlot(4,0);inventory.SwapItems(0,1);
            Assert.That(inventory.Items.Count,Is.EqualTo(2));Assert.That(inventory.QuickSlotItem(4),Is.SameAs(a));
            inventory.TryConsumeGrenade(b);Assert.That(inventory.QuickSlotItem(4),Is.SameAs(a));Assert.That(inventory.QuickSlotItem(1),Is.Null);
            var assignments=inventory.CaptureQuickSlots();inventory.RestoreSavedItems(new ItemData[]{a},assignments);
            Assert.That(inventory.QuickSlotItem(4),Is.SameAs(a));
        }finally{UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
    }
    [Test] public void InvalidSavedAssignmentDoesNotMutateInventory()
    {
        var owner=new GameObject("Inventory test");var inventory=owner.AddComponent<PlayerInventory>();var item=ScriptableObject.CreateInstance<GrenadeItemData>();
        try{inventory.TryAddItem(item);Assert.Throws<ArgumentException>(()=>inventory.RestoreSavedItems(new ItemData[]{item},new[]{9,-1,-1,-1,-1}));Assert.That(inventory.Items[0],Is.SameAs(item));}
        finally{UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(item);}
    }
    [Test] public void LifecycleLeaseDoesNotReleaseExistingPauseOwner()
    {
        var owner=new GameObject("Suspension");var suspension=owner.AddComponent<GameplaySuspensionController>();
        try {
            using(var pause=suspension.Acquire(SuspensionReason.ManualPause)){
                using(var lifecycle=suspension.Acquire(SuspensionReason.ApplicationLifecycle)){Assert.That(Time.timeScale,Is.Zero);Assert.That(suspension.OwnerCount,Is.EqualTo(2));}
                Assert.That(Time.timeScale,Is.Zero);Assert.That(pause.IsActive,Is.True);
            }
            Assert.That(suspension.IsSuspended,Is.False);
        }finally{UnityEngine.Object.DestroyImmediate(owner);Time.timeScale=1;}
    }
    [Test] public void InventoryBackPreservesManualPause()
    {
        var owner=new GameObject("Player");var suspension=owner.AddComponent<GameplaySuspensionController>();var root=new GameObject("Menu");
        var menu=root.AddComponent<InGameMenuController>();var main=new GameObject("Pause");var options=new GameObject("Options");var inventory=new GameObject("Inventory");var restart=new GameObject("Restart");
        try {
            menu.Configure(suspension,main,options,null);menu.ConfigureAdditionalScreens(inventory,restart);
            menu.OpenMenu();menu.ShowInventory();Assert.That(inventory.activeSelf,Is.True);
            menu.ShowMenu();Assert.That(main.activeSelf,Is.True);Assert.That(inventory.activeSelf,Is.False);Assert.That(Time.timeScale,Is.Zero);
            using(var knowledge=suspension.Acquire(SuspensionReason.KnowledgePresentation)){menu.ResumeGame();Assert.That(Time.timeScale,Is.Zero);}
            Assert.That(suspension.IsSuspended,Is.False);
        }finally{foreach(var go in new[]{root,main,options,inventory,restart,owner})UnityEngine.Object.DestroyImmediate(go);Time.timeScale=1;}
    }
    [Test] public void InventorySlotsImplementTouchPointerDragAndDrop()
    {
        Assert.That(typeof(IBeginDragHandler).IsAssignableFrom(typeof(InventoryDragSlot)),Is.True);
        Assert.That(typeof(IDropHandler).IsAssignableFrom(typeof(InventoryDragSlot)),Is.True);
    }
    [Test] public void OneShotSpawnerRestoresCompletionWithoutSpawningAgain()
    {
        var owner=new GameObject("Spawner");
        try {
            var spawner=owner.AddComponent<ItemSpawner>();spawner.RestoreRunState("{\"spawned\":true,\"remaining\":0}");
            var entity=owner.AddComponent<RunWorldObject>();entity.ConfigureIdentity("test-spawner");
            var saved=entity.Capture();Assert.That(saved.parts.Single().key,Is.EqualTo("item-spawner"));
            Assert.That(saved.parts.Single().json,Does.Contain("\"spawned\":true"));
            entity.Restore(saved);Assert.That(spawner.IsInvoking(),Is.False);
        }finally{UnityEngine.Object.DestroyImmediate(owner);}
    }
    [Test] public void VectorPanelCreatesItsRequiredCanvasRenderer()
    {
        var owner=new GameObject("Canvas",typeof(Canvas));
        try {
            var panel=InterfaceFactory.Panel(owner.transform,"Panel",Vector2.zero,Vector2.one);
            Assert.That(panel.GetComponent<CanvasRenderer>(),Is.Not.Null);
            Assert.That(panel.GetComponent<NeonPanel>().color.a,Is.GreaterThan(.9f));
        }finally{UnityEngine.Object.DestroyImmediate(owner);}
    }
    [Test] public void InactiveEnemyCaptureAndRestoreSurviveDelayedAwake()
    {
        var owner = new GameObject("Inactive encounter enemy");
        owner.SetActive(false);
        try {
            var health = owner.AddComponent<EnemyHealth>();
            var entity = owner.AddComponent<RunWorldObject>();entity.ConfigureIdentity("inactive-enemy");
            var saved = entity.Capture();
            Assert.That(saved.health, Is.EqualTo(health.MaxHealth));
            Assert.That(saved.removed, Is.False);
            saved.health = 12;
            entity.Restore(saved);
            typeof(EnemyHealth).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(health, null);
            Assert.That(health.CurrentHealth, Is.EqualTo(12));
            Assert.That(health.IsDead, Is.False);
        } finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

}
