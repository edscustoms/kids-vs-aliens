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
    private Button rearView;
    private BikeRearViewHold rearHold;
    private BikeLaserLockView incoming, outgoing;
    private AlienBikeLaserWeapon weapon;
    public void Build(Transform safe,PlayerBikeRider source, RectTransform pause = null)
    {
        rider=source;
        use=Button(safe,"BikeUse","RIDE",new(.78f,.48f),new(.88f,.57f),()=>rider.UseBike());
        label=use.GetComponentInChildren<TMP_Text>();
        meters=Panel(safe,"BikeMeters",new(.026f,.55f),new(.235f,.67f));
        meters.GetComponent<NeonPanel>().raycastTarget=false;
        turbo=Meter(meters,"TURBO",.53f,out _);
        jump=Meter(meters,"JUMP",.04f,out jumpRow);
        use.gameObject.SetActive(false); meters.gameObject.SetActive(false);
        incoming = gameObject.AddComponent<BikeLaserLockView>(); incoming.Build(safe, "IncomingBikeLock");
        outgoing = gameObject.AddComponent<BikeLaserLockView>(); outgoing.Build(safe, "OutgoingBikeLock");
        if (pause != null)
        {
            rearView = Button(pause.parent, "BikeRearView", "", Vector2.one, Vector2.one, () => { });
            rearHold = rearView.gameObject.AddComponent<BikeRearViewHold>();
            var rect = (RectTransform)rearView.transform;
            rect.anchorMin = pause.anchorMin; rect.anchorMax = pause.anchorMax; rect.pivot = pause.pivot;
            rect.sizeDelta = pause.sizeDelta;
            rect.anchoredPosition = pause.anchoredPosition - Vector2.up * (pause.rect.height + 14);
            rearView.GetComponent<NeonPanel>().SetShape(NeonShape.Circle);
            var icon = Rect(rect, "LookBack", new Vector2(.2f, .2f), new Vector2(.8f, .8f)).gameObject.AddComponent<RearViewGlyph>();
            icon.raycastTarget = false;
            rearView.gameObject.SetActive(false);
        }
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
        weapon = riding && rider.Bike != null ? rider.Bike.GetComponent<AlienBikeLaserWeapon>() : null;
        bool combatVisible = weapon != null && weapon.combatCamera != null && Time.timeScale > 0;
        if (rearHold != null) rearHold.Bind(combatVisible ? weapon.combatCamera : null);
        if (rearView != null) rearView.gameObject.SetActive(combatVisible);
        if (combatVisible)
        {
            var threat = AlienBikeLaserWeapon.IncomingFor(rider.Bike);
            incoming.Present(threat, threat != null ? rider.Bike : null, weapon.combatCamera.OutputCamera, true);
            if (weapon.combatCamera.RearViewYaw < .01f) outgoing.Present(weapon.Target != null ? weapon : null, weapon.Target, weapon.combatCamera.OutputCamera, false);
            else outgoing.Hide();
            if (rearView != null) rearView.GetComponent<NeonPanel>().SetState(weapon.combatCamera.RearView ? NeonState.Selected : NeonState.Normal);
        }
        else { incoming.Hide(); outgoing.Hide(); }
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
