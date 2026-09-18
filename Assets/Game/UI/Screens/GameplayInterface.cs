using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static InterfaceFactory;

// Extends the scene's established uGUI/suspension wiring. No gameplay action routing lives here.
[DefaultExecutionOrder(50)]
public sealed class GameplayInterface : MonoBehaviour
{
    [SerializeField] private PlayerCharacter player;
    [SerializeField] private UITheme theme;
    private TMP_Text feedback,badge;
    private float feedbackUntil, nextBadge;
    private KnowledgeLogView knowledge;
    private bool recoveryShown;
    public void Configure(PlayerCharacter source,UITheme style){player=source;theme=style;}
    private void Start()
    {
        UseTheme(theme);
        var safe=transform.Find("SafeArea");
        var menu=GetComponentInChildren<InGameMenuController>(true);
        var suspension=player.GetComponent<GameplaySuspensionController>();
        BuildPause(menu);
        var inventory=Overlay(menu.transform,"Screen_Inventory");
        inventory.gameObject.AddComponent<InventoryManagementView>().Build(player.GetComponent<PlayerInventory>(),menu.ShowMenu,menu.ResumeGame);
        var restart=Overlay(menu.transform,"Screen_Restart");
        var warning=Panel(restart,"Confirmation",new(.31f,.24f),new(.69f,.77f),true);
        Text(warning,"Title","HARD RESTART?",new(.07f,.76f),new(.93f,.94f),39,Magenta);
        Text(warning,"Warning","This will destroy your current run and restart this level.\n\nPermanent Knowledge is kept.\nThis cannot be undone.",new(.07f,.29f),new(.93f,.73f),29);
        Button(warning,"Cancel","CANCEL",new(.06f,.07f),new(.47f,.22f),menu.ShowMenu);
        Button(warning,"Restart","RESTART",new(.53f,.07f),new(.94f,.22f),menu.ConfirmRestart,true);
        menu.ConfigureAdditionalScreens(inventory.gameObject,restart.gameObject);
        inventory.gameObject.SetActive(false);restart.gameObject.SetActive(false);
        var log=Overlay(safe,"KnowledgeLog");knowledge=log.gameObject.AddComponent<KnowledgeLogView>();
        knowledge.Build(suspension,GetComponentInChildren<KnowledgeAcquiredPresenter>(true),null);log.gameObject.SetActive(false);
        var learn=Button(safe,"LearnButton","LEARN",new(.795f,.32f),new(.866f,.445f),()=>{
            if(!suspension.IsSuspended)knowledge.Open();
        });
        learn.GetComponent<NeonPanel>().radius=100;
        // Book mark is code-native and remains legible independently of font glyph coverage.
        var book=Rect(learn.transform,"Book",new(.31f,.57f),new(.69f,.86f));
        var left=Panel(book,"Left",Vector2.zero,new(.48f,1));left.GetComponent<NeonPanel>().radius=3;
        var right=Panel(book,"Right",new(.52f,0),Vector2.one);right.GetComponent<NeonPanel>().radius=3;
        foreach(var graphic in book.GetComponentsInChildren<Graphic>())graphic.raycastTarget=false;
        var learnLabel=learn.GetComponentInChildren<TMP_Text>();learnLabel.rectTransform.anchorMax=new(.95f,.53f);learnLabel.fontSizeMax=22;
        badge=Text(learn.transform,"Unread","",new(.70f,.76f),new(1.1f,1.08f),24,Green,TextAlignmentOptions.Center);
        // Keep HUD below modal screens. All action hit areas and callbacks remain as authored.
        learn.transform.SetAsFirstSibling();
        var toast=Panel(safe,"RunFeedback",new(.32f,.86f),new(.68f,.925f));toast.GetComponent<NeonPanel>().raycastTarget=false;
        feedback=Text(toast,"Message","",new(.025f,.08f),new(.975f,.92f),25,Green,TextAlignmentOptions.Center);
        toast.gameObject.SetActive(false);RunSaveService.Feedback+=ShowFeedback;
        RestyleTutorial();RestyleTouchControls();
        player.gameObject.AddComponent<CompactResourceDisplay>().Build(safe,player.GetComponent<PlayerHealth>());
        var oldHealth=FindAnyObjectByType<PlayerHealthUI>();if(oldHealth!=null)oldHealth.HideLegacyBars();
        var quick=FindAnyObjectByType<InventoryUI>();if(quick!=null)quick.ApplyPresentation(theme);
    }
    private void BuildPause(InGameMenuController menu)
    {
        foreach(Transform child in menu.MenuScreen.transform)child.gameObject.SetActive(false);
        var root=Overlay(menu.MenuScreen.transform,"PausePresentation");
        var panel=Panel(root,"PausePanel",new(.24f,.25f),new(.76f,.77f));
        Text(panel,"Eyebrow","PAUSED",new(.05f,.82f),new(.95f,.95f),25,Cyan);
        Text(panel,"Level",LevelName(gameObject.scene.name),new(.05f,.68f),new(.95f,.84f),42);
        Button(panel,"Resume","RESUME",new(.04f,.46f),new(.33f,.64f),menu.ResumeGame);
        Button(panel,"Settings","SETTINGS",new(.355f,.46f),new(.645f,.64f),menu.ShowOptions);
        Button(panel,"Inventory","INVENTORY",new(.67f,.46f),new(.96f,.64f),menu.ShowInventory);
        Button(panel,"Quit","QUIT TO MENU",new(.04f,.20f),new(.485f,.39f),menu.QuitToMenu);
        Button(panel,"Restart","HARD RESTART",new(.515f,.20f),new(.96f,.39f),menu.ShowRestart,true);
        Text(panel,"SafeCopy","Saves your run and returns to menu.",new(.045f,.045f),new(.48f,.18f),24,Muted,TextAlignmentOptions.Center);
        Text(panel,"DangerCopy","Discards this run and restarts the level.",new(.525f,.045f),new(.955f,.18f),24,Magenta,TextAlignmentOptions.Center);
        var options=menu.OptionsScreen;
        var background=Panel(options.transform,"SettingsSurface",new(.24f,.12f),new(.76f,.88f));background.SetAsFirstSibling();
    }
    private void RestyleTutorial()
    {
        var overlay=transform.Find("KnowledgeOverlay");if(overlay==null)return;
        foreach(var image in overlay.GetComponentsInChildren<Image>(true)) {
            if(image.name == "CardFill" || image.name == "InstructionPlate") image.enabled=false;
            if(image.name == "Card" || image.name == "PreviewFrame" || image.name == "Acknowledge") {
                image.enabled=false;
                var surface=Panel(image.transform,"StyledSurface",Vector2.zero,Vector2.one);surface.SetAsFirstSibling();
                var button=image.GetComponent<Button>();
                if(button!=null)button.targetGraphic=surface.GetComponent<NeonPanel>();
                if(image.name == "PreviewFrame" && image.GetComponent<RectMask2D>()==null)image.gameObject.AddComponent<RectMask2D>();
            }
        }
        foreach(var label in overlay.GetComponentsInChildren<TMP_Text>(true))
            if(label.text=="ACKNOWLEDGE"||label.text=="CONTINUE")label.text="GOT IT";
    }
    private void RestyleTouchControls()
    {
        foreach(var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            string type=behaviour.GetType().Name;
            if(type!="UIVirtualButton"&&type!="UIVirtualJoystick")continue;
            foreach(var image in behaviour.GetComponentsInChildren<Image>(true)) {
                if(image.name.Contains("Background")||image.name.Contains("Handle")||image.transform==behaviour.transform) {
                    if(theme!=null&&theme.iconCircle.sprite!=null){image.sprite=theme.iconCircle.sprite;image.color=new Color(.72f,.82f,1,.85f);}
                } else if(image.name == "Image_Icon") {
                    image.enabled=false;
                    var glyph=Rect(image.transform,"ActionSymbol",Vector2.zero,Vector2.one).gameObject.AddComponent<InterfaceGlyph>();
                    glyph.symbol=type=="UIVirtualJoystick"?InterfaceSymbol.Move:behaviour.name.Contains("Shoot")?InterfaceSymbol.Fire:behaviour.name.Contains("Jump")?InterfaceSymbol.Jump:InterfaceSymbol.Sprint;
                    glyph.raycastTarget=false;glyph.color=Color.white;
                } else image.color=new Color(.8f,.93f,1,.92f);
            }
        }
    }
    private void ShowFeedback(string message){feedback.text=message;feedback.transform.parent.gameObject.SetActive(true);feedback.transform.parent.SetAsLastSibling();feedbackUntil=Time.unscaledTime+6;}
    private void Update()
    {
        var run = ActiveRunController.Instance;
        if (!recoveryShown && run != null && !string.IsNullOrEmpty(run.RestoreError))
        {
            recoveryShown = true;
            var root = Overlay(transform, "RestoreRecovery");
            var panel = Panel(root, "Recovery", new(.25f,.25f), new(.75f,.75f), true);
            Text(panel, "Title", "RUN COULD NOT BE RESTORED", new(.05f,.7f), new(.95f,.95f), 34, Magenta);
            Text(panel, "Details", "Your snapshot has been preserved.\n\n" + run.RestoreError, new(.05f,.3f), new(.95f,.68f), 26);
            Button(panel, "Menu", "RETURN TO MENU", new(.15f,.07f), new(.85f,.24f), run.ReturnToMenuPreservingSnapshot);
        }
        if(feedback!=null&&Time.unscaledTime>=feedbackUntil)feedback.transform.parent.gameObject.SetActive(false);
        if(badge!=null&&Time.unscaledTime>=nextBadge){nextBadge=Time.unscaledTime+.5f;int count=PermanentProgress.Data.skills.Count(s=>!s.acknowledged);badge.text=count>0?"● "+count:"";}
    }
    private void OnDestroy()=>RunSaveService.Feedback-=ShowFeedback;
}

public sealed class CompactResourceDisplay : MonoBehaviour
{
    private PlayerHealth source;
    private RectTransform healthFill,armorFill;
    private TMP_Text healthText,armorText;
    private int shownHealth=-1,shownArmor=-1;
    public void Build(Transform parent,PlayerHealth health)
    {
        source=health;
        var root=Rect(parent,"Resources",new(.026f,.875f),new(.235f,.978f));root.SetAsFirstSibling();
        healthFill=Bar(root,"Health","+",.55f,Green,out healthText);
        armorFill=Bar(root,"Armor","◇",.06f,Cyan,out armorText);
    }
    private RectTransform Bar(Transform parent,string name,string glyph,float y,Color color,out TMP_Text number)
    {
        var symbol=Rect(parent,name+"Icon",new(0,y),new(.085f,y+.37f)).gameObject.AddComponent<InterfaceGlyph>();
        symbol.symbol=name=="Health"?InterfaceSymbol.Health:InterfaceSymbol.Armor;symbol.color=color;symbol.raycastTarget=false;
        var track=Panel(parent,name,new(.12f,y+.12f),new(.83f,y+.29f));var panel=track.GetComponent<NeonPanel>();panel.radius=8;panel.glow=0;panel.raycastTarget=false;
        var fill=Rect(track,"Fill",Vector2.zero,Vector2.one);var graphic=fill.gameObject.AddComponent<NeonPanel>();graphic.radius=8;graphic.glow=0;graphic.color=color;graphic.accent=color;graphic.secondary=Violet;graphic.raycastTarget=false;
        number=Text(parent,name+"Value","",new(.85f,y),new(1,y+.4f),27,null,TextAlignmentOptions.Center);
        return fill;
    }
    private void Update()
    {
        if(source==null||healthFill==null)return;
        float t=1-Mathf.Exp(-12*Time.unscaledDeltaTime);
        healthFill.anchorMax=new(Mathf.Lerp(healthFill.anchorMax.x,source.HealthNormalized,t),1);
        armorFill.anchorMax=new(Mathf.Lerp(armorFill.anchorMax.x,source.ArmorNormalized,t),1);
        int health=Mathf.CeilToInt(source.CurrentHealth),armor=Mathf.CeilToInt(source.CurrentArmor);
        if(health!=shownHealth){shownHealth=health;healthText.text=health.ToString();}
        if(armor!=shownArmor){shownArmor=armor;armorText.text=armor.ToString();}
    }
}
