using UnityEngine;
using UnityEngine.UI;

public enum InterfaceSymbol { Health, Armor, Fire, Jump, Sprint, Move }

// Small vector symbols; no font fallback or opaque bitmap backgrounds.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class InterfaceGlyph : MaskableGraphic
{
    public InterfaceSymbol symbol;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        switch(symbol)
        {
            case InterfaceSymbol.Health:
                Line(mesh,new(.5f,.19f),new(.5f,.81f),.16f);Line(mesh,new(.19f,.5f),new(.81f,.5f),.16f);break;
            case InterfaceSymbol.Armor:
                Line(mesh,new(.21f,.8f),new(.79f,.8f));Line(mesh,new(.21f,.8f),new(.25f,.4f));Line(mesh,new(.79f,.8f),new(.75f,.4f));Line(mesh,new(.25f,.4f),new(.5f,.16f));Line(mesh,new(.75f,.4f),new(.5f,.16f));break;
            case InterfaceSymbol.Fire:
                Circle(mesh,new(.5f,.5f),.25f);Line(mesh,new(.5f,.1f),new(.5f,.35f));Line(mesh,new(.5f,.65f),new(.5f,.9f));Line(mesh,new(.1f,.5f),new(.35f,.5f));Line(mesh,new(.65f,.5f),new(.9f,.5f));break;
            case InterfaceSymbol.Jump:
                Line(mesh,new(.2f,.38f),new(.5f,.68f));Line(mesh,new(.5f,.68f),new(.8f,.38f));Line(mesh,new(.2f,.60f),new(.5f,.9f));Line(mesh,new(.5f,.9f),new(.8f,.60f));break;
            case InterfaceSymbol.Sprint:
                Line(mesh,new(.16f,.23f),new(.44f,.5f));Line(mesh,new(.44f,.5f),new(.16f,.77f));Line(mesh,new(.49f,.23f),new(.77f,.5f));Line(mesh,new(.77f,.5f),new(.49f,.77f));break;
            default:
                Line(mesh,new(.5f,.15f),new(.5f,.85f));Line(mesh,new(.15f,.5f),new(.85f,.5f));
                Line(mesh,new(.35f,.7f),new(.5f,.85f));Line(mesh,new(.65f,.7f),new(.5f,.85f));Line(mesh,new(.35f,.3f),new(.5f,.15f));Line(mesh,new(.65f,.3f),new(.5f,.15f));
                Line(mesh,new(.3f,.35f),new(.15f,.5f));Line(mesh,new(.3f,.65f),new(.15f,.5f));Line(mesh,new(.7f,.35f),new(.85f,.5f));Line(mesh,new(.7f,.65f),new(.85f,.5f));break;
        }
    }
    private void Circle(VertexHelper mesh,Vector2 center,float radius)
    {
        for(int i=0;i<32;i++){
            float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;
            Line(mesh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,.04f);
        }
    }
    private void Line(VertexHelper mesh,Vector2 from,Vector2 to,float thickness=.06f)
    {
        var rect=GetPixelAdjustedRect();float size=Mathf.Min(rect.width,rect.height);
        from=rect.center+(from-Vector2.one*.5f)*size;to=rect.center+(to-Vector2.one*.5f)*size;
        var d=(to-from).normalized;var offset=new Vector2(-d.y,d.x)*(thickness*size*.5f);int index=mesh.currentVertCount;
        mesh.AddVert(from-offset,color,Vector2.zero);mesh.AddVert(from+offset,color,Vector2.zero);mesh.AddVert(to+offset,color,Vector2.zero);mesh.AddVert(to-offset,color,Vector2.zero);
        mesh.AddTriangle(index,index+1,index+2);mesh.AddTriangle(index,index+2,index+3);
    }
}
