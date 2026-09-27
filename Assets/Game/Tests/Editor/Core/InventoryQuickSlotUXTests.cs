using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class InventoryQuickSlotUXTests
{
    private GameObject owner, canvas;
    private PlayerInventory inventory;
    private PlayerSkillState skills;
    private PlayerMeleeController melee;
    private GrenadeItemData[] items;
    private KnowledgeBookItemData book;
    private float oldTimeScale;

    [SetUp] public void Setup()
    {
        oldTimeScale=Time.timeScale; Time.timeScale=1;
        PlayerSkillState.ResetRuntimeSkills();
        owner=new GameObject("Inventory UX fixture");
        skills=owner.AddComponent<PlayerSkillState>();
        melee=owner.AddComponent<PlayerMeleeController>();
        book=AssetDatabase.LoadAssetAtPath<KnowledgeBookItemData>("Assets/Game/Data/Items/KnowledgeBooks/FightingBook.asset");
        var data=new SerializedObject(melee);
        data.FindProperty("defaultCombatItem").objectReferenceValue=book.grantedItem;
        data.ApplyModifiedPropertiesWithoutUndo();
        inventory=owner.AddComponent<PlayerInventory>();
        Call(melee,"Awake"); Call(inventory,"Awake");
        items=Enumerable.Range(0,6).Select(_=>ScriptableObject.CreateInstance<GrenadeItemData>()).ToArray();
    }

    [TearDown] public void Cleanup()
    {
        if(canvas!=null)Object.DestroyImmediate(canvas);
        Object.DestroyImmediate(owner);
        foreach(var item in items)Object.DestroyImmediate(item);
        PlayerSkillState.ResetRuntimeSkills(); Time.timeScale=oldTimeScale;
    }

    [TestCase(0)] [TestCase(2)] [TestCase(5)]
    public void LearningBookAssignsFirstEmptyAfterConsumptionWithoutOverwriting(int occupied)
    {
        var assignments=new[]{-1,-1,-1,-1,-1};
        for(int i=0;i<occupied;i++)assignments[i]=i;
        inventory.RestoreSavedItems(items.Cast<ItemData>().Append(book).ToArray(),assignments);
        inventory.UseItem(6);
        Assert.That(skills.HasSkill(book.skill),Is.True);
        Assert.That(inventory.SelectedItem,Is.SameAs(book.grantedItem));
        Assert.That(inventory.Items.Contains(book),Is.False);
        for(int i=0;i<occupied;i++)Assert.That(inventory.QuickSlotItem(i),Is.SameAs(items[i]));
        if(occupied<5)Assert.That(inventory.QuickSlotIndex(occupied),Is.EqualTo(PlayerInventory.CombatEntry));
        else Assert.That(inventory.CaptureQuickSlots(),Is.EqualTo(assignments));
        Assert.That(inventory.LearnedCombat,Is.SameAs(book.grantedItem));
    }

    [Test] public void ConsumedBookUsesEarlierHoleRatherThanItsFormerAssignedSlot()
    {
        inventory.RestoreSavedItems(new ItemData[]{items[0],book},new[]{0,-1,-1,1,-1});
        inventory.UseQuickSlot(3);
        Assert.That(inventory.CaptureQuickSlots(),Is.EqualTo(new[]{0,PlayerInventory.CombatEntry,-1,-1,-1}));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(5)]
    public void FreshAttemptAssignsLearnedCombatOnlyToFirstEmptySlot(int occupied)
    {
        skills.UnlockSkill(book.skill);
        var assignments=new[]{-1,-1,-1,-1,-1};
        for(int i=0;i<occupied;i++)assignments[i]=i;
        inventory.RestoreSavedItems(items,assignments);
        inventory.InitializeFreshAttemptCombat();
        if(occupied<5)assignments[occupied]=PlayerInventory.CombatEntry;
        Assert.That(inventory.CaptureQuickSlots(),Is.EqualTo(assignments));
        Assert.That(inventory.SelectedItem,occupied<5?Is.SameAs(book.grantedItem):Is.Null);
        int changes=0; inventory.OnInventoryChanged+=()=>changes++;
        inventory.InitializeFreshAttemptCombat();
        Assert.That(inventory.CaptureQuickSlots(),Is.EqualTo(assignments));
        Assert.That(changes,Is.Zero,"Already assigned or full is a no-op");
    }

    [Test] public void FreshAttemptPreservesExistingCombatSlotAndSelection()
    {
        skills.UnlockSkill(book.skill);
        inventory.AssignQuickSlot(4,PlayerInventory.CombatEntry);
        inventory.InitializeFreshAttemptCombat();
        Assert.That(inventory.CaptureQuickSlots(),Is.EqualTo(new[]{-1,-1,-1,-1,PlayerInventory.CombatEntry}));
        Assert.That(inventory.SelectedItem,Is.Null,"Existing assignment must not change selection");
    }

    [Test] public void FreshAttemptSelectsCombatDuringArrivalWithoutReleasingInputSuspension()
    {
        skills.UnlockSkill(book.skill);
        var input=owner.AddComponent<StarterAssets.StarterAssetsInputs>();
        Call(melee,"Awake"); Call(inventory,"Awake");
        input.SetGameplayInputBlocked(true);
        Time.timeScale=0;
        inventory.InitializeFreshAttemptCombat();
        Assert.That(inventory.QuickSlotItem(0),Is.SameAs(book.grantedItem));
        Assert.That(inventory.SelectedItem,Is.SameAs(book.grantedItem));
        Assert.That(input.GameplayInputBlocked,Is.True);
        Assert.That(Time.timeScale,Is.Zero);
    }

    [Test] public void VisibleInventoryAssignClearRecoveryFullFeedbackAndNavigation()
    {
        skills.UnlockSkill(book.skill);
        inventory.RestoreSavedItems(items,new[]{0,-1,2,-1,4});
        canvas=new GameObject("Inventory UX Canvas",typeof(Canvas));
        var view=canvas.AddComponent<InventoryManagementView>();
        int back=0,resume=0;
        view.Build(inventory,()=>back++,()=>resume++);
        Assert.That(view.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("Category")),Is.False);
        Assert.That(view.GetComponentsInChildren<InventoryDragSlot>().Count(s=>!s.IsQuick&&s.Index>=0),Is.EqualTo(25));
        Assert.That(view.transform.Find("InventoryPanel/Items/LearnedCombat/Fighting").gameObject.activeInHierarchy,Is.True);
        view.Select(PlayerInventory.CombatEntry,false);
        Click(view,"Assign");
        Assert.That(inventory.QuickSlotIndex(1),Is.EqualTo(PlayerInventory.CombatEntry));
        view.Select(1,true); Click(view,"Unassign");
        Assert.That(inventory.QuickSlotIndex(1),Is.EqualTo(-1));
        Assert.That(view.ItemFor(PlayerInventory.CombatEntry,false),Is.SameAs(book.grantedItem));
        view.Select(PlayerInventory.CombatEntry,false); Click(view,"Assign");
        Assert.That(inventory.QuickSlotIndex(1),Is.EqualTo(PlayerInventory.CombatEntry));
        view.Select(5,false); Click(view,"Assign");
        Assert.That(inventory.QuickSlotIndex(3),Is.EqualTo(5));
        var full=inventory.CaptureQuickSlots();
        view.Select(1,false); Click(view,"Assign");
        Assert.That(inventory.CaptureQuickSlots(),Is.EqualTo(full));
        Assert.That(view.transform.Find("Hint").GetComponent<TMP_Text>().text,Is.EqualTo("QUICK SLOTS FULL"));
        // Clicking a slot after a full denial only selects it; no latent assignment mode.
        view.Select(0,true); Assert.That(inventory.CaptureQuickSlots(),Is.EqualTo(full));
        Click(view,"Back"); Click(view,"Resume");
        Assert.That(back,Is.EqualTo(1)); Assert.That(resume,Is.EqualTo(1));
    }

    [Test] public void BeamKnowledgeRemainsContextualWithoutCombatAssignment()
    {
        var beam=AssetDatabase.LoadAssetAtPath<KnowledgeBookItemData>("Assets/Game/Data/Items/KnowledgeBooks/BeamHoistBook.asset");
        inventory.TryAddItem(beam); inventory.UseQuickSlot(0);
        inventory.InitializeFreshAttemptCombat();
        Assert.That(skills.HasSkill(beam.skill),Is.True);
        Assert.That(inventory.CaptureQuickSlots(),Is.EqualTo(new[]{-1,-1,-1,-1,-1}));
        Assert.That(inventory.SelectedItem,Is.Null);
    }

    internal static void Click(InventoryManagementView view,string name) =>
        view.GetComponentsInChildren<Button>(true).Single(b=>b.name==name).onClick.Invoke();
    private static void Call(object target,string method) => target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,null);
}

public sealed class InventoryQuickSlotUXPlayTests
{
    private const string Key="InventoryQuickSlotUX.Saves";
    [UnitySetUp] public IEnumerator Setup()
    {
        SessionState.SetString(Key,Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY")??"");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",System.IO.Path.GetFullPath("Logs/InventoryUX-"+Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        yield return new EnterPlayMode();
        Application.runInBackground=true;
        yield return WaitReady();
        ActiveRunController.Instance.GetComponent<BeamTransportController>().CancelTransport();
        Resume();
        yield return EditorTestFrame.Next();
        Assert.That(ActiveRunController.Instance.GetComponent<StarterAssets.StarterAssetsInputs>().CanProcessGameplayInput,Is.True);
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(Application.isPlaying){ActiveRunController.Instance?.PrepareToLeave();yield return new ExitPlayMode();}
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",SessionState.GetString(Key,""));
        SessionState.EraseString(Key); Time.timeScale=1;
    }

    [UnityTest] public IEnumerator BookUpdatesHudImmediatelyThenContinueAndRestartKeepCorrectOwnership()
    {
        var run=ActiveRunController.Instance;
        var inventory=run.GetComponent<PlayerInventory>();
        var book=AssetDatabase.LoadAssetAtPath<KnowledgeBookItemData>("Assets/Game/Data/Items/KnowledgeBooks/FightingBook.asset");
        Assert.That(inventory.CaptureQuickSlots(),Is.EqualTo(new[]{-1,-1,-1,-1,-1}),"Authored fresh start has empty slots");
        Assert.That(inventory.TryAddItem(book),Is.True);
        inventory.UseQuickSlot(0);
        Assert.That(inventory.QuickSlotItem(0),Is.SameAs(book.grantedItem));
        Assert.That(inventory.SelectedItem,Is.SameAs(book.grantedItem));
        AssertHud(book.grantedItem);
        // The existing Knowledge presentation remains; no Inventory interaction is needed.
        yield return EditorTestFrame.Next(); yield return EditorTestFrame.Next();
        Object.FindAnyObjectByType<KnowledgeAcquiredPresenter>().Close(); Resume();
        yield return EditorTestFrame.Next();
        Assert.That(run.GetComponent<PlayerMeleeController>().TryAttack(),Is.True,"Fighting is immediately available");
        yield return EditorTestFrame.Next();
        Assert.That(run.Save(),Is.True); run.PrepareToLeave();
        Assert.That(RunSaveService.Continue(),Is.True,RunSaveService.LastError);
        yield return EditorTestFrame.Next(); yield return WaitReady();
        inventory=ActiveRunController.Instance.GetComponent<PlayerInventory>();
        Assert.That(inventory.QuickSlotIndex(0),Is.EqualTo(PlayerInventory.CombatEntry));
        Assert.That(inventory.SelectedItem,Is.SameAs(book.grantedItem));
        AssertHud(book.grantedItem);
        var menu=Object.FindAnyObjectByType<InGameMenuController>();
        Assert.That(menu.IsOpen,Is.True,"Continue retains its paused flow");
        menu.ShowInventory(); yield return EditorTestFrame.Next();
        var view=Object.FindAnyObjectByType<InventoryManagementView>();
        Assert.That(view,Is.Not.Null);
        view.Select(0,true); InventoryQuickSlotUXTests.Click(view,"Unassign");
        Assert.That(inventory.QuickSlotItem(0),Is.Null);
        Assert.That(view.ItemFor(PlayerInventory.CombatEntry,false),Is.SameAs(book.grantedItem));
        view.Select(PlayerInventory.CombatEntry,false);
        ProceduralUIReview.Capture("inventory-ux-1080",1920,1080);
        ProceduralUIReview.Capture("inventory-ux-720",1280,720);
        ProceduralUIReview.Capture("inventory-ux-4x3",1024,768);
        InventoryQuickSlotUXTests.Click(view,"Assign"); AssertHud(book.grantedItem);
        InventoryQuickSlotUXTests.Click(view,"Back"); Assert.That(menu.MenuScreen.activeInHierarchy,Is.True);
        menu.ShowInventory(); InventoryQuickSlotUXTests.Click(view,"Resume"); Assert.That(menu.IsOpen,Is.False);
        inventory.AssignQuickSlot(0,-1);
        run=ActiveRunController.Instance; Assert.That(run.Save(),Is.True); run.PrepareToLeave();
        Assert.That(RunSaveService.Continue(),Is.True,RunSaveService.LastError);
        yield return EditorTestFrame.Next(); yield return WaitReady();
        inventory=ActiveRunController.Instance.GetComponent<PlayerInventory>();
        Assert.That(inventory.QuickSlotItem(0),Is.Null,"Continue must preserve deliberate clearing");
        Assert.That(inventory.LearnedCombat,Is.SameAs(book.grantedItem));
        Assert.That(ActiveRunController.Instance.RestartFromBeginning(),Is.True);
        yield return EditorTestFrame.Next(); yield return WaitReady();
        inventory=ActiveRunController.Instance.GetComponent<PlayerInventory>();
        Assert.That(inventory.LearnedCombat,Is.SameAs(book.grantedItem),"Hard Restart keeps permanent Knowledge");
        Assert.That(inventory.CaptureQuickSlots(),Is.EqualTo(new[]{PlayerInventory.CombatEntry,-1,-1,-1,-1}),"Hard Restart assigns permanent Fighting automatically");
        Assert.That(inventory.QuickSlotItem(0),Is.SameAs(book.grantedItem));
        Assert.That(inventory.SelectedItem,Is.SameAs(book.grantedItem));
    }

    [UnityTest] public IEnumerator FreshRunKeepsExplicitWeaponSelectionAndContinueKeepsExactCombatSlot()
    {
        var combat=ActiveRunController.Instance.GetComponent<PlayerMeleeController>().DefaultCombatItem;
        ActiveRunController.Instance.GetComponent<PlayerSkillState>().UnlockSkill(combat.requiredSkill);
        PermanentProgress.Acknowledge(combat.requiredSkill);
        var pistol=AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/PlasmaPistolItem.asset");
        PlayerLoadoutState.SelectWeapon(pistol);
        Assert.That(RunSaveService.StartFresh("ConstructionSite",true),Is.True);
        yield return EditorTestFrame.Next(); yield return WaitReady();
        var inventory=ActiveRunController.Instance.GetComponent<PlayerInventory>();
        Assert.That(inventory.QuickSlotItem(0),Is.SameAs(pistol));
        Assert.That(inventory.QuickSlotItem(1),Is.SameAs(combat));
        Assert.That(inventory.SelectedItem,Is.SameAs(pistol),"Explicit fresh-loadout selection wins");
        inventory.AssignQuickSlot(4,PlayerInventory.CombatEntry);
        var assignments=inventory.CaptureQuickSlots();
        Assert.That(ActiveRunController.Instance.Save(),Is.True);
        ActiveRunController.Instance.PrepareToLeave();
        Assert.That(RunSaveService.Continue(),Is.True,RunSaveService.LastError);
        yield return EditorTestFrame.Next(); yield return WaitReady();
        inventory=ActiveRunController.Instance.GetComponent<PlayerInventory>();
        Assert.That(inventory.CaptureQuickSlots(),Is.EqualTo(assignments),"Continue does not move Fighting to an earlier empty slot");
        Assert.That(inventory.SelectedItem,Is.SameAs(pistol));
        PlayerLoadoutState.SelectWeapon(null);
        Assert.That(RunSaveService.StartFresh("ConstructionSite",true),Is.True);
        yield return EditorTestFrame.Next(); yield return WaitReady();
        inventory=ActiveRunController.Instance.GetComponent<PlayerInventory>();
        Assert.That(inventory.CaptureQuickSlots(),Is.EqualTo(new[]{PlayerInventory.CombatEntry,-1,-1,-1,-1}));
        Assert.That(inventory.SelectedItem,Is.SameAs(combat));
    }

    private static void AssertHud(ItemData item)
    {
        var hud=Object.FindAnyObjectByType<InventoryUI>(FindObjectsInactive.Include);
        var data=new SerializedObject(hud);
        var button=(Button)data.FindProperty("slotButtons").GetArrayElementAtIndex(0).objectReferenceValue;
        var icon=button.transform.Find("ItemIconArea/ItemIcon").GetComponent<Image>();
        Assert.That(button.interactable,Is.True); Assert.That(icon.enabled,Is.True);
        Assert.That(icon.sprite,Is.SameAs(InterfaceIconCatalog.ForItem(item)));
    }
    private static void Resume()
    {
        ActiveRunController.Instance.SendMessage("OnApplicationPause",false);
        ActiveRunController.Instance.SendMessage("OnApplicationFocus",true);
        Object.FindAnyObjectByType<InGameMenuController>().ResumeGame();
    }
    private static IEnumerator WaitReady()
    {
        double deadline=EditorApplication.timeSinceStartup+20;
        while(ActiveRunController.Instance==null||!ActiveRunController.Instance.IsReady)
        {
            Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(deadline));
            yield return EditorTestFrame.Next();
        }
        yield return EditorTestFrame.Next();
    }
}
