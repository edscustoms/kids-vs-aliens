using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static InterfaceFactory;

public sealed class KnowledgeLogView : MonoBehaviour
{
    private GameplaySuspensionController suspension;
    private GameplaySuspensionController.Lease lease;
    private KnowledgeAcquiredPresenter presenter;
    private RectTransform content;
    private TMP_Text details;
    private Image entryIcon;
    private SkillData reviewing;
    private Action closed;
    public void Build(GameplaySuspensionController owner, KnowledgeAcquiredPresenter tutorial, Action onClose)
    {
        suspension=owner;presenter=tutorial;closed=onClose;
        var panel=Panel(transform,"Knowledge",new(.14f,.12f),new(.86f,.9f));
        Text(panel,"Title","KNOWLEDGE LOG",new(.035f,.87f),new(.83f,.985f),43,Cyan);
        Text(panel,"Tagline","LEARN. ADAPT. FIGHT BACK.",new(.035f,.80f),new(.87f,.89f),23,Muted);
        Button(panel,"Close","×",new(.91f,.88f),new(.975f,.98f),Close);
        content=Scroll(panel,new(.025f,.06f),new(.49f,.78f),out _);
        var side=Panel(panel,"Entry",new(.51f,.06f),new(.97f,.78f));
        entryIcon=NeonVisuals.Icon(side,"KnowledgeIcon",new(.31f,.66f),new(.69f,.94f));
        entryIcon.enabled=false;
        details=Text(side,"Content","Select learned Knowledge to review its instructions.",new(.055f,.21f),new(.945f,.64f),30,null,TextAlignmentOptions.TopLeft);
        Button(side,"Review","REVIEW / GOT IT",new(.055f,.04f),new(.945f,.17f),Review);
    }
    public void Open()
    {
        if(suspension!=null&&lease==null)lease=suspension.Acquire(SuspensionReason.Modal);
        gameObject.SetActive(true);transform.SetAsLastSibling();Refresh();
    }
    private void Refresh()
    {
        foreach(Transform child in content)Destroy(child.gameObject);
        var skills=RunContentCatalog.Instance.entries.Select(e=>e.asset).OfType<SkillData>()
            .Where(s=>PermanentProgress.Data.skills.Any(record=>record.id==s.Id)).OrderBy(s=>s.DisplayName).ToArray();
        content.sizeDelta=new(0,Mathf.Max(110,skills.Length*128)+24);
        for(int i=0;i<skills.Length;i++){
            var skill=skills[i];var b=Button(content,"Entry"+i,(PermanentProgress.IsUnread(skill)?"•  ":"")+skill.DisplayName,Vector2.zero,Vector2.one,()=>Select(skill));
            var r=(RectTransform)b.transform;r.anchorMin=new(0,1);r.anchorMax=Vector2.one;r.pivot=new(.5f,1);r.offsetMin=new(16,-i*128-128);r.offsetMax=new(-16,-i*128-16);
            var icon=NeonVisuals.Icon(b.transform,"KnowledgeIcon",new(.035f,.10f),new(.245f,.90f));
            icon.sprite=InterfaceIconCatalog.ForSkill(skill);icon.enabled=icon.sprite!=null;
            b.GetComponentInChildren<TMP_Text>().rectTransform.anchorMin=new(.27f,.1f);
        }
        if(skills.Length==0)Text(content,"Empty","No Knowledge learned yet. Find Knowledge Books while exploring.",Vector2.zero,Vector2.one,26,Muted);
    }
    private void Select(SkillData skill)
    {
        reviewing=skill;var tutorial=skill.TutorialData;
        entryIcon.sprite=InterfaceIconCatalog.ForSkill(skill);entryIcon.enabled=entryIcon.sprite!=null;
        details.text=$"<color=#00E1FF>{skill.DisplayName}</color>\n\n{(tutorial!=null?tutorial.shortDescription:skill.Description)}\n\n{(tutorial!=null?tutorial.instructions:"")}";
    }
    private void Review()
    {
        if(reviewing==null)return;
        if(presenter!=null){gameObject.SetActive(false);presenter.PresentationClosed+=ReturnFromTutorial;if(!presenter.Review(reviewing))ReturnFromTutorial();}
        else {PermanentProgress.Acknowledge(reviewing);Refresh();}
    }
    private void ReturnFromTutorial(){presenter.PresentationClosed-=ReturnFromTutorial;gameObject.SetActive(true);Refresh();}
    public void Close(){gameObject.SetActive(false);lease?.Dispose();lease=null;closed?.Invoke();}
    private void OnDestroy(){if(presenter!=null)presenter.PresentationClosed-=ReturnFromTutorial;lease?.Dispose();}
}
