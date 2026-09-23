using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static InterfaceFactory;

public sealed class InventoryManagementView : MonoBehaviour
{
    private PlayerInventory inventory;
    private RectTransform quickRow, bag;
    private TMP_Text details, hint, usage;
    private Image detailIcon;
    private InventoryDragSlot[] quick, backpack;
    private ItemType? category;
    private int selected = -1, selectedQuick = -1;
    private bool assigning;
    private readonly List<int> backpackIndices = new(25);
    public void Build(PlayerInventory source, Action back, Action resume)
    {
        inventory = source;
        var panel = Panel(transform, "InventoryPanel", new(.055f,.14f), new(.945f,.92f));
        Text(panel,"Title","INVENTORY",new(.035f,.88f),new(.7f,.985f),46);
        Text(panel,"Subtitle","PAUSED  /  MANAGE YOUR GEAR",new(.035f,.825f),new(.8f,.89f),23,Cyan);
        Button(panel,"Close","×",new(.92f,.885f),new(.975f,.985f),()=>back());
        var categories = new (string, ItemType?)[] { ("ALL",null),("WEAPONS",ItemType.Weapon),("THROWABLES",ItemType.Grenade),("CONSUMABLES",ItemType.Consumable),("ARMOR",ItemType.Armor),("KEY ITEMS",ItemType.Key),("KNOWLEDGE",ItemType.KnowledgeBook),("COMBAT",ItemType.UnarmedCombat) };
        for (int i=0;i<categories.Length;i++) {
            var entry=categories[i]; float y=.73f-i*.087f;
            Button(panel,"Category"+i,entry.Item1,new(.025f,y),new(.205f,y+.073f),()=>{category=entry.Item2;Refresh();});
        }
        var center=Panel(panel,"Items",new(.225f,.04f),new(.71f,.805f));
        Text(center,"QuickTitle","QUICK SLOTS",new(.025f,.9f),new(.97f,.985f),27,Cyan);
        quickRow=Rect(center,"QuickSlots",new(.025f,.60f),new(.975f,.89f));
        usage=Text(center,"BackpackTitle","BACKPACK",new(.025f,.47f),new(.98f,.57f),25,Cyan);
        bag=Scroll(center,new(.025f,.045f),new(.975f,.46f),out var backpackScroll);
        backpackScroll.name="Backpack";
        backpackScroll.scrollSensitivity=45;
        bag.gameObject.AddComponent<BackpackGridLayout>();
        bag.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var side=Panel(panel,"Details",new(.725f,.04f),new(.975f,.805f));
        Text(side,"Heading","ITEM DETAILS",new(.05f,.9f),new(.95f,.99f),27,Cyan);
        detailIcon=NeonVisuals.Icon(side,"ItemIcon",new(.14f,.58f),new(.86f,.87f));
        details=Text(side,"Description","Select an item",new(.06f,.27f),new(.94f,.55f),29,null,TextAlignmentOptions.TopLeft);
        Button(side,"Assign","ASSIGN TO QUICK SLOT",new(.045f,.13f),new(.955f,.235f),()=>{if(inventory.EntryItem(selected)!=null){assigning=true;hint.text="Tap the quick slot to assign this item.";}});
        Button(side,"Unassign","CLEAR QUICK SLOT",new(.045f,.015f),new(.955f,.115f),()=>{if(selectedQuick>=0)inventory.AssignQuickSlot(selectedQuick,-1);});
        Button(transform,"Back","‹  BACK TO PAUSE",new(.055f,.045f),new(.31f,.12f),()=>back());
        Button(transform,"Resume","RESUME GAME",new(.71f,.045f),new(.945f,.12f),()=>resume());
        var hintPanel=Panel(transform,"HintSurface",new(.33f,.045f),new(.695f,.12f));hintPanel.GetComponent<NeonPanel>().raycastTarget=false;
        hint=Text(transform,"Hint","Drag items to move. Touch: swipe to scroll, hold to move.",new(.33f,.045f),new(.695f,.12f),24,Muted,TextAlignmentOptions.Center);
        quick=new InventoryDragSlot[inventory.QuickSlotCount];backpack=new InventoryDragSlot[inventory.Capacity];
        for(int i=0;i<quick.Length;i++)quick[i]=CreateSlot(quickRow,i,quick.Length,true);
        for(int i=0;i<backpack.Length;i++)backpack[i]=CreateSlot(bag,i,backpack.Length,false);
        inventory.OnInventoryChanged+=Refresh;
        Refresh();
    }
    private InventoryDragSlot CreateSlot(RectTransform parent,int index,int count,bool isQuick)
    {
        var r=Panel(parent,"Slot"+index,new(index/(float)count+.006f,.025f),new((index+1)/(float)count-.006f,.975f));
        var slot=r.gameObject.AddComponent<InventoryDragSlot>();slot.Configure(this,index,isQuick);return slot;
    }
    public int OwnedIndexFor(int index,bool isQuick) => isQuick ? inventory.QuickSlotIndex(index) : index>=0&&index<backpackIndices.Count?backpackIndices[index]:-1;
    public ItemData ItemFor(int index,bool isQuick) => inventory.EntryItem(OwnedIndexFor(index,isQuick));
    public void Select(int index,bool isQuick)
    {
        UIAudioFeedback.Click();
        if(assigning&&isQuick&&inventory.EntryItem(selected)!=null){inventory.AssignQuickSlot(index,selected);assigning=false;hint.text="Quick slot updated";return;}
        selected=OwnedIndexFor(index,isQuick);
        selectedQuick=isQuick?index:-1;Refresh();
    }
    public void Drop(InventoryDragSlot source,InventoryDragSlot target)
    {
        if(source.Owner!=this||target.Owner!=this)return;
        int itemIndex=OwnedIndexFor(source.Index,source.IsQuick);
        if(inventory.EntryItem(itemIndex)==null)return;
        if(target.IsQuick)inventory.AssignQuickSlot(target.Index,itemIndex);
        else if(source.IsQuick)inventory.AssignQuickSlot(source.Index,-1);
        else { int destination=OwnedIndexFor(target.Index,false);if(inventory.SwapItems(itemIndex,destination))selected=destination; }
        assigning=false;hint.text="Inventory updated";
    }
    private void Refresh()
    {
        if(quick==null)return;
        backpackIndices.Clear();
        if(category==ItemType.UnarmedCombat) {
            if(inventory.LearnedCombat!=null)backpackIndices.Add(PlayerInventory.CombatEntry);
        } else {
            for(int owned=0;owned<inventory.Items.Count;owned++)backpackIndices.Add(owned);
        }
        for(int i=0;i<quick.Length;i++)quick[i].Refresh(ItemFor(i,true),true,selectedQuick==i);
        for(int i=0;i<backpack.Length;i++){
            var item=ItemFor(i,false);bool visible=category==null||item==null||item.itemType==category;
            backpack[i].gameObject.SetActive(category!=ItemType.UnarmedCombat||item!=null);
            int entry=OwnedIndexFor(i,false), assigned=-1;
            for(int slot=0;slot<inventory.QuickSlotCount;slot++)if(entry!=-1&&inventory.QuickSlotIndex(slot)==entry){assigned=slot;break;}
            backpack[i].Refresh(item,visible,selected!=-1&&selected==entry,assigned);
        }
        usage.text=category==ItemType.UnarmedCombat?"LEARNED COMBAT":$"BACKPACK  {inventory.Items.Count} / {inventory.Capacity}";
        var selectedItem=inventory.EntryItem(selected);
        detailIcon.sprite=InterfaceIconCatalog.ForItem(selectedItem);detailIcon.enabled=detailIcon.sprite!=null;
        string stats = selectedItem is WeaponItemData weapon ? $"\nDamage: {weapon.damage:g}   Range: {weapon.range:g} m\nMagazine: {weapon.magazineSize}   {weapon.fireMode}"
            : selectedItem is GrenadeItemData grenade ? $"\n{grenade.activationMode} activation\nFuse: {grenade.fuseTime:g} s"
            : selectedItem is KnowledgeBookItemData book && book.skill != null ? "\n" + book.skill.Description : "";
        details.text=selectedItem!=null?$"{selectedItem.itemName}\n<size=70%><color=#9FAAD9>{(selectedItem is UnarmedCombatItemData ? "Combat capability" : selectedItem.itemType.ToString())}</color>\n{(selectedItem is UnarmedCombatItemData ? "Permanently learned" : "Quantity: 1")}{stats}</size>":"Select an item";
    }
    private void OnDestroy(){if(inventory!=null)inventory.OnInventoryChanged-=Refresh;}
}

public sealed class InventoryDragSlot : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public InventoryManagementView Owner {get;private set;}
    public int Index {get;private set;}
    public bool IsQuick {get;private set;}
    private TMP_Text label, number;
    private Image icon;
    private RectTransform ghost;
    private Canvas canvas;
    private bool available, filtered;
    private ItemData draggedItem;
    private int draggedOwnedIndex;
    private ScrollRect scroll;
    private bool scrolling;
    private float pressedAt;
    private Vector2 dragPosition;
    private Camera dragCamera;
    public void Configure(InventoryManagementView owner,int index,bool quick)
    {
        Owner=owner;Index=index;IsQuick=quick;
        GetComponent<NeonPanel>().SetShape(NeonShape.Slot);
        NeonVisuals.Feedback(gameObject, GetComponent<NeonPanel>());
        scroll=quick?null:GetComponentInParent<ScrollRect>();
        icon=NeonVisuals.Icon(transform,"Icon",new(.12f,.33f),new(.88f,.82f));
        label=Text(transform,"Name","",new(.06f,.035f),new(.94f,.32f),20,null,TextAlignmentOptions.Center);
        number=Text(transform,"Number",(index+1).ToString(),new(.1f,.82f),new(.9f,.99f),quick?21:14,Cyan,TextAlignmentOptions.Center);
    }
    public void Refresh(ItemData item,bool visible,bool selected,int assigned=-1)
    {
        filtered=!visible;available=visible&&item!=null;icon.sprite=available?InterfaceIconCatalog.ForItem(item):null;icon.enabled=icon.sprite!=null;
        label.text=!visible?"—":item==null?"EMPTY":item.itemName;
        number.text=available&&assigned>=0?$"SLOT {assigned+1}":(Index+1).ToString();
        icon.color=assigned>=0?new Color(1,1,1,.55f):Color.white;
        label.color=available?Color.white:Muted;
        GetComponent<NeonPanel>().SetState(!visible ? NeonState.Disabled : selected ? NeonState.Selected : item == null ? NeonState.Empty : NeonState.Normal);
    }
    public void OnPointerClick(PointerEventData e){if(!filtered&&e.button==PointerEventData.InputButton.Left)Owner.Select(Index,IsQuick);}
    public void OnPointerDown(PointerEventData e) => pressedAt=Time.unscaledTime;
    public void OnInitializePotentialDrag(PointerEventData e) { if(scroll!=null)scroll.OnInitializePotentialDrag(e); }
    public void OnBeginDrag(PointerEventData e)
    {
        if(e.button!=PointerEventData.InputButton.Left)return;
        bool touch=e is ExtendedPointerEventData pointer ? pointer.pointerType==UIPointerType.Touch : e.pointerId>=0;
        scrolling=scroll!=null&&(!available||(touch&&Time.unscaledTime-pressedAt<.3f));
        e.eligibleForClick=false;
        if(scrolling){scroll.OnBeginDrag(e);return;}
        if(!available||e.button!=PointerEventData.InputButton.Left)return;
        draggedItem=Owner.ItemFor(Index,IsQuick);
        draggedOwnedIndex=Owner.OwnedIndexFor(Index,IsQuick);
        canvas=GetComponentInParent<Canvas>().rootCanvas;
        ghost=Panel(canvas.transform,"DraggedItem",Vector2.zero,Vector2.zero);ghost.sizeDelta=new(150,110);
        ghost.gameObject.AddComponent<CanvasGroup>().blocksRaycasts=false;
        Text(ghost,"Item",Owner.ItemFor(Index,IsQuick).itemName,new(.04f,.04f),new(.96f,.96f),22,null,TextAlignmentOptions.Center);
        OnDrag(e);
    }
    public void OnDrag(PointerEventData e)
    {
        if(scrolling){scroll.OnDrag(e);return;}
        if(ghost==null)return;
        dragPosition=e.position;dragCamera=e.pressEventCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,e.position,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var p);
        ghost.localPosition=p;
    }
    private void LateUpdate()
    {
        // A held item can reach off-screen rows without releasing the drag.
        if(ghost==null||scroll==null)return;
        var viewport=scroll.viewport;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,dragPosition,dragCamera,out var point);
        var bounds=viewport.rect;
        if(!bounds.Contains(point))return;
        float overflow=scroll.content.rect.height-bounds.height;
        if(overflow<=0)return;
        float edge=Mathf.Min(36,bounds.height*.2f);
        float direction=point.y>bounds.yMax-edge?1:point.y<bounds.yMin+edge?-1:0;
        if(direction==0)return;
        scroll.StopMovement();
        scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+direction*260*Time.unscaledDeltaTime/overflow);
    }
    public void OnDrop(PointerEventData e){var source=e.pointerDrag!=null?e.pointerDrag.GetComponent<InventoryDragSlot>():null;if(!filtered&&source!=null&&source.ghost!=null&&source.Owner.OwnedIndexFor(source.Index,source.IsQuick)==source.draggedOwnedIndex&&source.Owner.ItemFor(source.Index,source.IsQuick)==source.draggedItem)Owner.Drop(source,this);}
    public void OnEndDrag(PointerEventData e){if(scrolling)scroll.OnEndDrag(e);scrolling=false;ClearGhost();}
    private void OnDisable(){scrolling=false;ClearGhost();}
    private void ClearGhost(){if(ghost!=null)Destroy(ghost.gameObject);ghost=null;draggedItem=null;}
}
