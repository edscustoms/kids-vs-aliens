using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Invoked explicitly by menu setup, never from editor validation/layout callbacks.
public static class ProgressResetMenuSetup
{
    public static void Configure(RectTransform options, UITheme theme)
    {
        InterfaceFactory.UseTheme(theme);
        ProgressResetView.EnsureObjects(options,new(.34f,.24f),new(.66f,.31f));
        var controller=options.GetComponent<OptionsScreenController>();
        var confirmation=options.Find("ResetProgressConfirmation");
        var message=confirmation.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Consequences");
        controller.ConfigureReset(confirmation.gameObject,message);
        EditorUtility.SetDirty(controller);
        Wire(options.Find("ResetProgress").GetComponent<Button>(),controller.ShowResetConfirmation);
        Wire(confirmation.GetComponentsInChildren<Button>(true).First(b=>b.name=="CancelReset"),controller.CancelReset);
        Wire(confirmation.GetComponentsInChildren<Button>(true).First(b=>b.name=="ConfirmReset"),controller.ConfirmReset);
    }
    private static void Wire(Button button, UnityAction action)
    {
        for(int i=0;i<button.onClick.GetPersistentEventCount();i++)
            if(button.onClick.GetPersistentTarget(i)==action.Target as Object
                && button.onClick.GetPersistentMethodName(i)==action.Method.Name)return;
        UnityEventTools.AddPersistentListener(button.onClick,action);
        EditorUtility.SetDirty(button);
    }
}
