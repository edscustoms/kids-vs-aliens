using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        bag=Rect(center,"Backpack",new(.025f,.075f),new(.975f,.45f));
        var side=Panel(panel,"Details",new(.725f,.04f),new(.975f,.805f));
        Text(side,"Heading","ITEM DETAILS",new(.05f,.9f),new(.95f,.99f),27,Cyan);
        detailIcon=Rect(side,"ItemIcon",new(.15f,.57f),new(.85f,.87f)).gameObject.AddComponent<Image>();
        detailIcon.preserveAspect=true;detailIcon.raycastTarget=false;
        details=Text(side,"Description","Select an item",new(.06f,.27f),new(.94f,.55f),29,null,TextAlignmentOptions.TopLeft);
        Button(side,"Assign","ASSIGN TO QUICK SLOT",new(.045f,.13f),new(.955f,.235f),()=>{if(selected>=0){assigning=true;hint.text="Tap the quick slot to assign this item.";}});
        Button(side,"Unassign","CLEAR QUICK SLOT",new(.045f,.015f),new(.955f,.115f),()=>{if(selectedQuick>=0)inventory.AssignQuickSlot(selectedQuick,-1);});
        Button(transform,"Back","‹  BACK TO PAUSE",new(.055f,.045f),new(.31f,.12f),()=>back());
        Button(transform,"Resume","RESUME GAME",new(.71f,.045f),new(.945f,.12f),()=>resume());
        var hintPanel=Panel(transform,"HintSurface",new(.33f,.045f),new(.695f,.12f));hintPanel.GetComponent<NeonPanel>().raycastTarget=false;
        hint=Text(transform,"Hint","Drag items to rearrange or assign quick slots",new(.33f,.045f),new(.695f,.12f),24,Muted,TextAlignmentOptions.Center);
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
    public ItemData ItemFor(int index,bool isQuick) => isQuick ? inventory.QuickSlotItem(index) : index<inventory.Items.Count?inventory.Items[index]:null;
    public void Select(int index,bool isQuick)
    {
        if(assigning&&isQuick&&selected>=0){inventory.AssignQuickSlot(index,selected);assigning=false;hint.text="Quick slot updated";return;}
        selected=isQuick?inventory.QuickSlotIndex(index):index<inventory.Items.Count?index:-1;
        selectedQuick=isQuick?index:-1;Refresh();
    }
    public void Drop(InventoryDragSlot source,InventoryDragSlot target)
    {
        if(source.Owner!=this||target.Owner!=this)return;
        int itemIndex=source.IsQuick?inventory.QuickSlotIndex(source.Index):source.Index;
        if(itemIndex<0||itemIndex>=inventory.Items.Count)return;
        if(target.IsQuick)inventory.AssignQuickSlot(target.Index,itemIndex);
        else if(source.IsQuick)inventory.AssignQuickSlot(source.Index,-1);
        else inventory.SwapItems(itemIndex,target.Index);
        assigning=false;hint.text="Inventory updated";
    }
    private void Refresh()
    {
        if(quick==null)return;
        for(int i=0;i<quick.Length;i++)quick[i].Refresh(ItemFor(i,true),true,selectedQuick==i);
        for(int i=0;i<backpack.Length;i++){
            var item=ItemFor(i,false);bool visible=category==null||item==null||item.itemType==category;
            backpack[i].Refresh(item,visible,selected==i);
        }
        usage.text=$"BACKPACK  {inventory.Items.Count} / {inventory.Capacity}";
        var selectedItem=selected>=0&&selected<inventory.Items.Count?inventory.Items[selected]:null;
        detailIcon.sprite=selectedItem!=null?selectedItem.icon:null;detailIcon.enabled=detailIcon.sprite!=null;
        string stats = selectedItem is WeaponItemData weapon ? $"\nDamage: {weapon.damage:g}   Range: {weapon.range:g} m\nMagazine: {weapon.magazineSize}   {weapon.fireMode}"
            : selectedItem is GrenadeItemData grenade ? $"\n{grenade.activationMode} activation\nFuse: {grenade.fuseTime:g} s"
            : selectedItem is KnowledgeBookItemData book && book.skill != null ? "\n" + book.skill.Description : "";
        details.text=selectedItem!=null?$"{selectedItem.itemName}\n<size=70%><color=#9FAAD9>{selectedItem.itemType}</color>\nQuantity: 1{stats}</size>":"Select an item";
    }
    private void OnDestroy(){if(inventory!=null)inventory.OnInventoryChanged-=Refresh;}
}

public sealed class InventoryDragSlot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public InventoryManagementView Owner {get;private set;}
    public int Index {get;private set;}
    public bool IsQuick {get;private set;}
    private TMP_Text label;
    private Image icon;
    private RectTransform ghost;
    private Canvas canvas;
    private bool available, filtered;
    private ItemData draggedItem;
    public void Configure(InventoryManagementView owner,int index,bool quick)
    {
        Owner=owner;Index=index;IsQuick=quick;
        icon=Rect(transform,"Icon",new(.15f,.3f),new(.85f,.86f)).gameObject.AddComponent<Image>();icon.preserveAspect=true;icon.raycastTarget=false;
        label=Text(transform,"Name","",new(.05f,.04f),new(.95f,.31f),20,null,TextAlignmentOptions.Center);
        Text(transform,"Number",(index+1).ToString(),new(.08f,.8f),new(.35f,.98f),21,Cyan);
    }
    public void Refresh(ItemData item,bool visible,bool selected)
    {
        filtered=!visible;available=visible&&item!=null;icon.sprite=available?item.icon:null;icon.enabled=icon.sprite!=null;
        label.text=!visible?"—":item==null?"EMPTY":item.itemName;
        label.color=available?Color.white:Muted;
        var panel=GetComponent<NeonPanel>();panel.SetAccent(selected?Cyan:Violet);panel.border=selected?3:1;
    }
    public void OnPointerClick(PointerEventData e){if(!filtered&&e.button==PointerEventData.InputButton.Left)Owner.Select(Index,IsQuick);}
    public void OnBeginDrag(PointerEventData e)
    {
        if(!available||e.button!=PointerEventData.InputButton.Left)return;
        draggedItem=Owner.ItemFor(Index,IsQuick);
        canvas=GetComponentInParent<Canvas>().rootCanvas;
        ghost=Panel(canvas.transform,"DraggedItem",Vector2.zero,Vector2.zero);ghost.sizeDelta=new(150,110);
        ghost.gameObject.AddComponent<CanvasGroup>().blocksRaycasts=false;
        Text(ghost,"Item",Owner.ItemFor(Index,IsQuick).itemName,new(.04f,.04f),new(.96f,.96f),22,null,TextAlignmentOptions.Center);
        OnDrag(e);
    }
    public void OnDrag(PointerEventData e)
    {
        if(ghost==null)return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,e.position,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var p);
        ghost.localPosition=p;
    }
    public void OnDrop(PointerEventData e){var source=e.pointerDrag!=null?e.pointerDrag.GetComponent<InventoryDragSlot>():null;if(!filtered&&source!=null&&source.ghost!=null&&source.Owner.ItemFor(source.Index,source.IsQuick)==source.draggedItem)Owner.Drop(source,this);}
    public void OnEndDrag(PointerEventData e)=>ClearGhost();
    private void OnDisable()=>ClearGhost();
    private void ClearGhost(){if(ghost!=null)Destroy(ghost.gameObject);ghost=null;draggedItem=null;}
}
