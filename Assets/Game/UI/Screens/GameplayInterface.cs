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
    private KnowledgeAcquiredPresenter tutorialPresenter;
    private Image tutorialIcon;
    private SkillData displayedTutorialIcon;
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
        learn.GetComponent<NeonPanel>().SetShape(NeonShape.Circle);
        NeonVisuals.Symbol(learn.transform,InterfaceSymbol.Book,new(.27f,.47f),new(.73f,.90f));
        var learnLabel=learn.GetComponentInChildren<TMP_Text>();learnLabel.rectTransform.anchorMax=new(.95f,.53f);learnLabel.fontSizeMax=22;
        var badgeRoot=Panel(learn.transform,"UnreadBadge",new(.72f,.76f),new(1.02f,1.08f));
        var badgeSurface=badgeRoot.GetComponent<NeonPanel>();badgeSurface.SetShape(NeonShape.Badge);badgeSurface.accent=Green;badgeSurface.raycastTarget=false;
        badge=Text(badgeRoot,"Unread","",new(.12f,.1f),new(.88f,.9f),22,new Color(.01f,.05f,.03f),TextAlignmentOptions.Center);
        // Keep HUD below modal screens. All action hit areas and callbacks remain as authored.
        learn.transform.SetAsFirstSibling();
        var toast=Panel(safe,"RunFeedback",new(.32f,.86f),new(.68f,.925f));toast.GetComponent<NeonPanel>().raycastTarget=false;
        toast.GetComponent<NeonPanel>().radius=100;toast.GetComponent<NeonPanel>().secondary=Green;
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
        var content=overlay.Find("SafeArea/Card/Content");
        if(content!=null) {
            tutorialPresenter=GetComponentInChildren<KnowledgeAcquiredPresenter>(true);
            tutorialIcon=NeonVisuals.Icon(content,"KnowledgeIcon",new(.25f,.885f),new(.33f,.97f));
            tutorialIcon.enabled=false;
            var heading=content.Find("Heading") as RectTransform;
            if(heading!=null){heading.anchorMin=new(.34f,.885f);heading.anchorMax=new(.88f,.97f);heading.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;}
        }
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
        foreach(var joystick in FindObjectsByType<UIVirtualJoystick>(FindObjectsInactive.Include)) {
            var background=joystick.containerRect!=null?joystick.containerRect.GetComponent<Image>():null;
            var handle=joystick.handleRect!=null?joystick.handleRect.GetComponent<Image>():null;
            if(background!=null)NeonVisuals.Replace(background,NeonShape.Joystick,theme);
            if(handle!=null)NeonVisuals.Replace(handle,NeonShape.Circle,theme);
            foreach(var image in joystick.GetComponentsInChildren<Image>(true))
                if(image.name=="Image_Icon"){image.enabled=false;NeonVisuals.Symbol(image.transform,InterfaceSymbol.Move,Vector2.zero,Vector2.one);}
        }
        foreach(var control in FindObjectsByType<UIVirtualButton>(FindObjectsInactive.Include)) {
            foreach(var image in control.GetComponentsInChildren<Image>(true)) {
                if(image.name=="Image_Icon") {
                    image.enabled=false;
                    var symbol=control.name.Contains("Shoot")?InterfaceSymbol.Fire:control.name.Contains("Jump")?InterfaceSymbol.Jump:InterfaceSymbol.Sprint;
                    NeonVisuals.Symbol(image.transform,symbol,Vector2.zero,Vector2.one);
                } else if(image.name.Contains("Background")||image.transform==control.transform) {
                    var surface=NeonVisuals.Replace(image,NeonShape.Circle,theme);
                    NeonVisuals.Feedback(control.gameObject,surface);
                }
            }
        }
    }
    private void ShowFeedback(string message){feedback.text=message;feedback.transform.parent.gameObject.SetActive(true);feedback.transform.parent.SetAsLastSibling();feedbackUntil=Time.unscaledTime+6;}
    private void Update()
    {
        // Observe the presentation only; never enqueue, unlock or advance tutorials.
        if(tutorialIcon!=null && tutorialPresenter!=null && displayedTutorialIcon!=tutorialPresenter.CurrentSkill) {
            displayedTutorialIcon=tutorialPresenter.CurrentSkill;
            tutorialIcon.sprite=InterfaceIconCatalog.ForSkill(displayedTutorialIcon);
            tutorialIcon.enabled=tutorialIcon.sprite!=null;
        }
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
        if(badge!=null&&Time.unscaledTime>=nextBadge){nextBadge=Time.unscaledTime+.5f;int count=PermanentProgress.Data.skills.Count(s=>!s.acknowledged);badge.text=count>0?count.ToString():"";badge.transform.parent.gameObject.SetActive(count>0);}
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
        var row=Panel(parent,name+"Frame",new(0,y),new(1,y+.43f));
        var shell=row.GetComponent<NeonPanel>();shell.radius=100;shell.glow=8;shell.raycastTarget=false;shell.details=false;
        var symbol=NeonVisuals.Symbol(row,name=="Health"?InterfaceSymbol.Health:InterfaceSymbol.Armor,new(.03f,.12f),new(.12f,.88f));
        symbol.color=Cyan;
        var divider=Panel(row,"Divider",new(.145f,.22f),new(.15f,.78f));divider.GetComponent<NeonPanel>().glow=0;divider.GetComponent<NeonPanel>().raycastTarget=false;
        var track=Panel(row,name,new(.19f,.28f),new(.83f,.72f));var panel=track.GetComponent<NeonPanel>();panel.radius=100;panel.glow=0;panel.raycastTarget=false;panel.details=false;
        var fill=Rect(track,"Fill",Vector2.zero,Vector2.one);var graphic=fill.gameObject.AddComponent<NeonPanel>();graphic.SetShape(NeonShape.Fill);graphic.radius=100;graphic.glow=4;graphic.color=Color.white;graphic.accent=Cyan;graphic.secondary=name=="Health"?Magenta:Violet;graphic.raycastTarget=false;
        number=Text(row,name+"Value","",new(.845f,.1f),new(.97f,.9f),25,null,TextAlignmentOptions.Center);
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
