using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static InterfaceFactory;

// Objective and CC are independent presenters sharing only reserved screen space.
public sealed class GameplayCommunicationView : MonoBehaviour
{
    [SerializeField] private ObjectiveController objectives;
    [SerializeField] private DialoguePlayer dialogue;
    [SerializeField] private TMP_FontAsset readableFont;
    [SerializeField] private Material textMaterial;
    private RectTransform objectivePanel, dialoguePanel;
    private TMP_Text objectiveText, speakerText, dialogueText;
    private bool built;

    public void Build(Transform safe, RectTransform runMessage, RectTransform systemFeedback)
    {
        if (built) return;
        objectivePanel = Surface(safe,"ObjectiveTracker");
        objectiveText = Label(objectivePanel,"Objective","",new(.04f,.10f),new(.96f,.90f),31);
        objectiveText.fontStyle = FontStyles.Bold;
        objectiveText.alignment = TextAlignmentOptions.Center;
        dialoguePanel = Surface(safe,"DialogueCC");
        speakerText = Label(dialoguePanel,"Speaker","",new(.035f,.71f),new(.965f,.96f),22);
        speakerText.fontStyle = FontStyles.Bold; speakerText.color = Cyan;
        dialogueText = Label(dialoguePanel,"Transcript","",new(.035f,.07f),new(.965f,.72f),29);
        dialogueText.overflowMode = TextOverflowModes.Truncate;
        var layout = GetComponent<GameplayMessageLayout>() ?? gameObject.AddComponent<GameplayMessageLayout>();
        layout.Arrange(objectivePanel,runMessage,systemFeedback,dialoguePanel);
        built = true; Subscribe(); RefreshObjective(); RefreshDialogue();
    }
    private RectTransform Surface(Transform parent, string name)
    {
        var panel = Rect(parent,name,Vector2.zero,Vector2.one);
        var image = panel.gameObject.AddComponent<Image>(); image.color = new Color(.025f,.035f,.065f,.78f); image.raycastTarget = false;
        return panel;
    }
    private TMP_Text Label(Transform parent,string name,string content,Vector2 min,Vector2 max,float size)
    {
        var text = Text(parent,name,content,min,max,size);
        if (readableFont != null) text.font = readableFont;
        if (textMaterial != null) text.fontSharedMaterial = textMaterial;
        text.richText = false; text.fontSizeMin = 20;
        return text;
    }
    private void RefreshObjective()
    {
        if (!built) return;
        var active = objectives != null ? objectives.ActiveObjective : null;
        objectivePanel.gameObject.SetActive(active != null);
        if (active == null) return;
        objectiveText.text = (active.title ?? "").ToUpperInvariant();
        if (active.progressMode == ObjectiveProgressMode.Count) objectiveText.text += $"  {objectives.ProgressOf(active)}/{active.targetCount}";
    }
    private void RefreshDialogue()
    {
        if (!built) return;
        var line = dialogue != null ? dialogue.CurrentLine : null;
        dialoguePanel.gameObject.SetActive(line != null);
        if (line == null) return;
        speakerText.text = (dialogue.Speaker != null ? dialogue.Speaker.displayName : "").ToUpperInvariant();
        dialogueText.text = line.text;
    }
    private void Subscribe()
    {
        if (objectives != null) objectives.Changed += RefreshObjective;
        if (dialogue != null) dialogue.Changed += RefreshDialogue;
    }
    private void OnEnable() { if (built) { Subscribe(); RefreshObjective(); RefreshDialogue(); } }
    private void OnDisable()
    {
        if (objectives != null) objectives.Changed -= RefreshObjective;
        if (dialogue != null) dialogue.Changed -= RefreshDialogue;
    }
}
