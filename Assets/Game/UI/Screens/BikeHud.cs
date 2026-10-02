using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static InterfaceFactory;

public sealed class BikeHud : MonoBehaviour
{
    private PlayerBikeRider rider;
    private Button use;
    private TMP_Text label;
    private RectTransform meters, turbo, jump;
    private GameObject jumpRow;
    public void Build(Transform safe,PlayerBikeRider source)
    {
        rider=source;
        use=Button(safe,"BikeUse","RIDE",new(.78f,.48f),new(.88f,.57f),()=>rider.UseBike());
        label=use.GetComponentInChildren<TMP_Text>();
        meters=Panel(safe,"BikeMeters",new(.026f,.55f),new(.235f,.67f));
        meters.GetComponent<NeonPanel>().raycastTarget=false;
        turbo=Meter(meters,"TURBO",.53f,out _);
        jump=Meter(meters,"JUMP",.04f,out jumpRow);
        use.gameObject.SetActive(false); meters.gameObject.SetActive(false);
    }
    private RectTransform Meter(Transform parent,string title,float y,out GameObject row)
    {
        var r=Rect(parent,title,new(.04f,y),new(.96f,y+.40f));row=r.gameObject;
        Text(r,"Label",title,new(0,0),new(.27f,1),20,Cyan);
        var track=Panel(r,"Track",new(.3f,.2f),new(1,.8f));track.GetComponent<NeonPanel>().raycastTarget=false;
        var fill=Rect(track,"Fill",Vector2.zero,Vector2.one);var graphic=fill.gameObject.AddComponent<NeonPanel>();
        graphic.SetShape(NeonShape.Fill);graphic.accent=Cyan;graphic.secondary=Magenta;graphic.raycastTarget=false;
        return fill;
    }
    private void Update()
    {
        if(rider==null || use==null) return;
        bool riding=rider.IsDriving;
        bool visible=riding || (!rider.IsBusy && rider.NearbyBike!=null);
        use.gameObject.SetActive(visible);
        string text=riding?"DISMOUNT":"RIDE"; if(label.text!=text)label.text=text;
        meters.gameObject.SetActive(riding);
        if (!riding) use.interactable=true;
        if(!riding) return;
        turbo.anchorMax=new(rider.Bike.Turbo01,1);
        jumpRow.SetActive(rider.Bike.IsCharging);jump.anchorMax=new(rider.Bike.JumpCharge01,1);
        use.interactable=rider.Bike.Speed<=rider.Bike.safeDismountSpeed && rider.Bike.IsGrounded;
    }
}
