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
    [Header("Content width (canvas units, also limited by the safe region)")]
    [SerializeField] private Vector2 objectiveWidth = new(190,560);
    [SerializeField] private Vector2 dialogueWidth = new(220,860);
    private RectTransform objectivePanel, dialoguePanel;
    private RectTransform objectiveIcon, speakerDivider;
    private RectTransform safeArea;
    private Vector2 lastSafeSize;
    private TMP_Text objectiveText, progressText, speakerText, dialogueText;
    private bool built;

    public void Build(Transform safe, RectTransform runMessage, RectTransform systemFeedback)
    {
        if (built) return;
        objectivePanel = Surface(safe,"ObjectiveTracker",true);
        safeArea = (RectTransform)safe;
        var icon = objectiveIcon = Rect(objectivePanel,"ObjectiveIcon",new(.025f,.15f),new(.13f,.85f));
        var glyph = icon.gameObject.AddComponent<InterfaceGlyph>();
        glyph.symbol = InterfaceSymbol.Fire; glyph.color = Cyan; glyph.raycastTarget = false;
        objectiveText = Label(objectivePanel,"Objective","",new(.15f,.12f),new(.82f,.88f),24);
        objectiveText.characterSpacing = 1;
        progressText = Label(objectivePanel,"Progress","",new(.83f,.12f),new(.96f,.88f),24);
        progressText.fontStyle = FontStyles.Bold; progressText.color = Cyan;
        progressText.alignment = TextAlignmentOptions.Center;
        dialoguePanel = Surface(safe,"DialogueCC",false);
        speakerText = Label(dialoguePanel,"Speaker","",new(.03f,.12f),new(.15f,.88f),22);
        speakerText.fontStyle = FontStyles.Bold; speakerText.color = Cyan;
        var divider = Rect(dialoguePanel,"SpeakerDivider",new(.165f,.2f),new(.1665f,.8f)).gameObject.AddComponent<Image>();
        divider.color = new Color(0,.8f,1,.6f); divider.raycastTarget = false;
        speakerDivider = (RectTransform)divider.transform;
        dialogueText = Label(dialoguePanel,"Transcript","",new(.185f,.12f),new(.975f,.88f),23);
        var layout = GetComponent<GameplayMessageLayout>() ?? gameObject.AddComponent<GameplayMessageLayout>();
        layout.Arrange(objectivePanel,runMessage,systemFeedback,dialoguePanel);
        built = true; Subscribe(); RefreshObjective(); RefreshDialogue();
        Canvas.preWillRenderCanvases += RefreshForScreenSize;
    }
    private RectTransform Surface(Transform parent, string name, bool objective)
    {
        var panel = Panel(parent,name,Vector2.zero,Vector2.one);
        var frame = panel.GetComponent<NeonPanel>();
        frame.color = new Color(.008f,.016f,.04f,.83f);
        frame.accent = Cyan; frame.secondary = objective ? Cyan : new Color(.55f,.12f,.7f);
        frame.radius = objective ? 100 : 14; frame.border = 1.1f; frame.glow = 3;
        frame.details = false; frame.raycastTarget = false; frame.SetVerticesDirty();
        return panel;
    }
    private TMP_Text Label(Transform parent,string name,string content,Vector2 min,Vector2 max,float size)
    {
        var text = Text(parent,name,content,min,max,size);
        if (readableFont != null) text.font = readableFont;
        if (textMaterial != null) text.fontSharedMaterial = textMaterial;
        text.richText = false; text.fontSizeMin = 18; text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }
    private void RefreshObjective()
    {
        if (!built) return;
        var active = objectives != null ? objectives.ActiveObjective : null;
        objectivePanel.gameObject.SetActive(active != null);
        if (active == null) return;
        objectiveText.text = (active.title ?? "").ToUpperInvariant();
        bool count = active.progressMode == ObjectiveProgressMode.Count;
        progressText.gameObject.SetActive(count);
        progressText.text = count ? $"{objectives.ProgressOf(active)}/{active.targetCount}" : "";
        RefreshLayout();
    }
    private void RefreshDialogue()
    {
        if (!built) return;
        var line = dialogue != null ? dialogue.CurrentLine : null;
        dialoguePanel.gameObject.SetActive(line != null);
        if (line == null) return;
        speakerText.text = (dialogue.Speaker != null ? dialogue.Speaker.displayName : "").ToUpperInvariant();
        dialogueText.text = line.text;
        RefreshLayout();
    }
    private void RefreshForScreenSize()
    {
        if (built && safeArea.rect.size != lastSafeSize) RefreshLayout();
    }
    public void RefreshLayout()
    {
        if (!built) return;
        lastSafeSize = safeArea.rect.size;
        if (lastSafeSize.x <= 0 || lastSafeSize.y <= 0) return;
        const float padding = 16, gap = 10, iconWidth = 26;
        float count = progressText.gameObject.activeSelf ? progressText.GetPreferredValues(progressText.text).x : 0;
        float objectiveExtras = padding*2 + iconWidth + gap + (count > 0 ? gap+count : 0);
        Rect objectiveRegion = GameplayMessageLayout.ObjectiveRegion;
        Vector2 objectiveSize = Fit(objectiveText,objectiveExtras,objectiveWidth,lastSafeSize.x*objectiveRegion.width,lastSafeSize.y*objectiveRegion.height);
        SizePanel(objectivePanel,new(objectiveRegion.center.x,objectiveRegion.yMax),new(.5f,1),objectiveSize);
        Place(objectiveIcon,padding,iconWidth,26);
        Place(objectiveText.rectTransform,padding+iconWidth+gap,objectiveSize.x-objectiveExtras,objectiveSize.y-12);
        Place(progressText.rectTransform,objectiveSize.x-padding-count,count,objectiveSize.y-12);

        float speaker = speakerText.GetPreferredValues(speakerText.text).x;
        float dialogueExtras = padding*2 + speaker + gap*2 + 1;
        Rect dialogueRegion = GameplayMessageLayout.DialogueRegion;
        Vector2 dialogueSize = Fit(dialogueText,dialogueExtras,dialogueWidth,lastSafeSize.x*dialogueRegion.width,lastSafeSize.y*dialogueRegion.height);
        SizePanel(dialoguePanel,new(dialogueRegion.center.x,dialogueRegion.yMin),new(.5f,0),dialogueSize);
        Place(speakerText.rectTransform,padding,speaker,dialogueSize.y-12);
        Place(speakerDivider,padding+speaker+gap,1,dialogueSize.y-16);
        Place(dialogueText.rectTransform,padding+speaker+gap*2+1,dialogueSize.x-dialogueExtras,dialogueSize.y-12);
    }
    private static Vector2 Fit(TMP_Text text,float extras,Vector2 limits,float regionWidth,float regionHeight)
    {
        text.enableAutoSizing = false;
        float maximum = Mathf.Min(limits.y,regionWidth);
        float width = Mathf.Clamp(extras+text.GetPreferredValues(text.text).x,Mathf.Min(limits.x,maximum),maximum);
        float height = Mathf.Max(40,text.GetPreferredValues(text.text,Mathf.Max(1,width-extras),float.PositiveInfinity).y+12);
        // Exceptional long content wraps in its reserved region, then uses TMP's readable fallback.
        text.enableAutoSizing = height > regionHeight;
        return new Vector2(width,Mathf.Min(height,regionHeight));
    }
    private static void SizePanel(RectTransform panel,Vector2 anchor,Vector2 pivot,Vector2 size)
    {
        panel.anchorMin = panel.anchorMax = anchor; panel.pivot = pivot;
        panel.anchoredPosition = Vector2.zero; panel.sizeDelta = size;
    }
    private static void Place(RectTransform rect,float left,float width,float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0,.5f); rect.pivot = new Vector2(0,.5f);
        rect.anchoredPosition = new Vector2(left,0); rect.sizeDelta = new Vector2(width,height);
    }
    private void Subscribe()
    {
        if (objectives != null) objectives.Changed += RefreshObjective;
        if (dialogue != null) dialogue.Changed += RefreshDialogue;
    }
    private void OnEnable() { if (built) { Subscribe(); RefreshObjective(); RefreshDialogue(); Canvas.preWillRenderCanvases += RefreshForScreenSize; } }
    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= RefreshForScreenSize;
        if (objectives != null) objectives.Changed -= RefreshObjective;
        if (dialogue != null) dialogue.Changed -= RefreshDialogue;
    }
}
