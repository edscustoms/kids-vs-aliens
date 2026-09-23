using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using static InterfaceFactory;

// The caller already owns the Options screen and its pause lease.
public static class ProgressResetView
{
    public static void Build(Transform options, Vector2 min, Vector2 max)
    {
        if(options.Find("ResetProgress")!=null)return;
        EnsureObjects(options,min,max);
        var confirmation=options.Find("ResetProgressConfirmation");
        var message=confirmation.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Consequences");
        options.Find("ResetProgress").GetComponent<Button>().onClick.AddListener(()=>confirmation.gameObject.SetActive(true));
        confirmation.GetComponentsInChildren<Button>(true).First(b=>b.name=="CancelReset").onClick.AddListener(()=>confirmation.gameObject.SetActive(false));
        confirmation.GetComponentsInChildren<Button>(true).First(b=>b.name=="ConfirmReset").onClick.AddListener(()=>ConfirmReset(message));
    }

    // Creation only: existing authored transforms, styles and text are never rewritten.
    public static void EnsureObjects(Transform options, Vector2 min, Vector2 max)
    {
        if(options.Find("ResetProgress")==null)
            Button(options,"ResetProgress","RESET GAME PROGRESS",min,max,null,true);
        if(options.Find("ResetProgressConfirmation")!=null)return;
        var confirmation=Overlay(options,"ResetProgressConfirmation");
        var panel=Panel(confirmation,"Warning",new(.25f,.17f),new(.75f,.85f),true);
        var body=Rect(panel,"OpaqueBody",new(.014f,.022f),new(.986f,.976f)).gameObject.AddComponent<Image>();
        body.color=new Color(.012f,.015f,.04f,1);body.raycastTarget=false;
        Text(panel,"Title","RESET ALL PROGRESS?",new(.06f,.79f),new(.94f,.96f),38,Magenta);
        var message=Text(panel,"Consequences","This permanently deletes:\n• Current run\n• Learned Knowledge\n• Skill/proficiency progress and permanent unlocks\n\nYour settings will be kept.\nThis cannot be undone.",new(.07f,.28f),new(.93f,.77f),29);
        message.textWrappingMode=TextWrappingModes.Normal;
        Button(panel,"CancelReset","CANCEL",new(.06f,.07f),new(.44f,.22f),null);
        Button(panel,"ConfirmReset","RESET EVERYTHING",new(.48f,.07f),new(.94f,.22f),null,true);
        confirmation.gameObject.SetActive(false);
    }

    public static void ConfirmReset(TMP_Text message)
    {
        if(RunSaveService.ResetGameProgress()){SceneManager.LoadScene("Menu");UIAudioFeedback.Click(true);}
        else if(message!=null)message.text=RunSaveService.LastError;
    }
}
