using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static InterfaceFactory;

[DefaultExecutionOrder(80)]
public sealed class ActiveRunMenu : MonoBehaviour
{
    [SerializeField] private UITheme theme;
    public void Configure(UITheme value) => theme = value;
    private UIScreenRouter router;
    private RectTransform screen, confirmation;
    private TMP_Text metadata, message;
    private Button continueButton;
    private KnowledgeLogView knowledge;
    private string newScene;
    private void Start()
    {
        UseTheme(theme);
        router=GetComponent<UIScreenRouter>();
        screen=Overlay(transform,"Screen_ActiveRun");
        var panel=Panel(screen,"RunPanel",new(.245f,.065f),new(.755f,.94f));
        Text(panel,"Title","ACTIVE RUN FOUND",new(.06f,.855f),new(.94f,.975f),43,Cyan);
        metadata=Text(panel,"Metadata","",new(.06f,.78f),new(.94f,.85f),27,Muted);
        continueButton=Button(panel,"Continue","CONTINUE",new(.06f,.59f),new(.94f,.755f),Continue);
        var continueLabel=continueButton.GetComponentInChildren<TMP_Text>();continueLabel.fontSizeMax=46;continueLabel.fontStyle=FontStyles.Bold;continueLabel.characterSpacing=3;
        continueButton.GetComponent<NeonPanel>().border=3;continueButton.GetComponent<NeonPanel>().glow=10;
        Text(panel,"ResumeCopy","Resume your current run where you left off.",new(.1f,.48f),new(.9f,.58f),26,Muted,TextAlignmentOptions.Center);
        Button(panel,"NewGame","NEW GAME",new(.06f,.33f),new(.94f,.465f),()=>confirmation.gameObject.SetActive(true));
        Text(panel,"ReplaceCopy","Replaces this active run. Permanent Knowledge is kept.",new(.08f,.245f),new(.92f,.32f),23,Muted,TextAlignmentOptions.Center);
        Button(panel,"Options","OPTIONS",new(.06f,.10f),new(.48f,.22f),()=>{screen.gameObject.SetActive(false);router.OptionsReturn=()=>screen.gameObject.SetActive(true);router.ShowOptions();});
        Button(panel,"Knowledge","KNOWLEDGE LOG",new(.51f,.10f),new(.94f,.22f),()=>knowledge.Open());
        Button(screen,"Back","‹ BACK",new(.03f,.055f),new(.18f,.135f),()=>{screen.gameObject.SetActive(false);router.ShowMainMenu();});
        message=Text(screen,"Feedback","",new(.15f,.005f),new(.85f,.06f),23,Magenta,TextAlignmentOptions.Center);
        confirmation=Overlay(screen,"ReplaceConfirmation");
        var warning=Panel(confirmation,"Warning",new(.28f,.25f),new(.72f,.77f),true);
        Text(warning,"Title","REPLACE ACTIVE RUN?",new(.07f,.73f),new(.93f,.94f),36,Magenta);
        Text(warning,"Description","Your current run will be discarded. A fresh game starts from the beginning.\n\nPermanent Knowledge and settings are kept. This cannot be undone.",new(.07f,.29f),new(.93f,.71f),28);
        Button(warning,"Cancel","CANCEL",new(.06f,.06f),new(.47f,.23f),()=>confirmation.gameObject.SetActive(false));
        Button(warning,"Replace","NEW GAME",new(.53f,.06f),new(.94f,.23f),()=>{if(!RunSaveService.StartFresh(newScene,true))message.text=RunSaveService.LastError;else UIAudioFeedback.ConfirmGameplay(true);},true);
        confirmation.gameObject.SetActive(false);screen.gameObject.SetActive(false);
        var logRoot=Overlay(transform,"Screen_KnowledgeLog");knowledge=logRoot.gameObject.AddComponent<KnowledgeLogView>();knowledge.Build(null,null,null);logRoot.gameObject.SetActive(false);
        // Existing menu controls, labels, arrows and preview remain owned by MenuController.
        foreach(var button in GetComponentsInChildren<UIButton>(true)) {
            if(button.Label!=null) {button.Label.fontStyle=FontStyles.Bold;button.Label.characterSpacing=2;}
        }
    }
    public void Play(string scene)
    {
        newScene=scene;
        if(RunSaveService.TryReadActive(out var saved)&&saved==null){if(!RunSaveService.StartFresh(scene,false))ShowError();else UIAudioFeedback.ConfirmGameplay(true);return;}
        router.HideScreens();screen.gameObject.SetActive(true);confirmation.gameObject.SetActive(false);
        continueButton.interactable=saved!=null;
        metadata.text=saved!=null?$"{LevelName(saved.sceneName)}   ·   {System.TimeSpan.FromSeconds(saved.elapsedSeconds):hh\\:mm\\:ss}":"Existing save needs attention";
        message.text=RunSaveService.LastError??"";
    }
    private void Continue(){if(!RunSaveService.Continue())ShowError();else UIAudioFeedback.ConfirmGameplay(true);}
    private void ShowError(){screen.gameObject.SetActive(true);message.text=RunSaveService.LastError??"Unable to start this run.";}
    private void OnApplicationPause(bool paused){if(paused){PermanentProgress.Flush();PlayerPrefs.Save();}}
    private void OnApplicationQuit(){PermanentProgress.Flush();PlayerPrefs.Save();}
}
