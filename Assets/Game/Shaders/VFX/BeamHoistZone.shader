Shader "KVA/Beam Hoist Zone"
{
    Properties
    {
        [HDR] _Violet ("Violet energy", Color) = (1.2,.08,2.6,1)
        [HDR] _Cyan ("Cyan highlights", Color) = (.05,2.2,2.8,1)
        _Strength ("Presentation strength", Float) = 0
        _Detail ("Border / Glyph / Core / Cyan", Vector) = (2.2,1.7,2.8,2.5)
        _Motion ("Pulse speed / Amount / Perimeter speed / Active", Vector) = (1.4,.12,.6,0)
        _Fill ("Ground fill", Range(0,1)) = .13
        [HideInInspector] _Mote ("Mote material", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Pass
        {
            Name "HoistHologram"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 size:TEXCOORD1; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 size:TEXCOORD1; half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
                float4 _Violet, _Cyan, _Detail, _Motion;
                float _Strength, _Fill, _Mote;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.uv=v.uv; o.size=v.size; o.color=v.color; return o;
            }
            float stroke(float distance, float width)
            {
                float aa=max(fwidth(distance),.001);
                return 1-smoothstep(width, width+aa, abs(distance));
            }
            half4 Frag(Varyings i):SV_Target
            {
                if (_Mote>.5)
                {
                    float glow=saturate(1-length(i.uv*2-1));
                    return half4(i.color.rgb*3, glow*glow*i.color.a);
                }
                float2 size=max(i.size,.1), p=(i.uv-.5)*size, edge=size*.5-abs(p);
                float e=min(edge.x,edge.y);
                float perimeter=edge.x<edge.y ? p.y+size.y*.5 : p.x+size.x*.5;
                float segments=step(.18,frac(perimeter/.36));
                float border=stroke(e-.032,.014)*segments;
                float brackets=stroke(e-.11,.027);
                float cornerLength=min(min(size.x,size.y)*.23,.65);
                float corners=step(edge.x,cornerLength)*step(edge.y,cornerLength);
                float cyan=brackets*corners;
                float radius=min(size.x,size.y)*.23;
                float r=length(p), angle=atan2(p.y,p.x);
                float arcs=step(.22,frac(angle*3.8197+_Time.y*.06));
                float rings=(stroke(r-radius,.009)+stroke(r-radius*.74,.006))*.55
                    +stroke(r-radius*1.18,.013)*arcs*.65;
                float diamond=stroke(abs(p.x)+abs(p.y)-radius*.22,.012);
                float core=exp2(-r*r/max(.002,radius*radius*.025))*1.2+diamond*.6;
                float2 q=size.x>=size.y?p:p.yx;
                float w=max(size.x,size.y), h=min(size.x,size.y);
                float chevrons=0;
                for(int n=0;n<2;n++)
                {
                    float x=abs(q.x)-w*(.27+n*.10);
                    chevrons+=stroke(abs(q.y)-x*.9,.05)*step(0,x)*step(x,w*.08)*step(abs(q.y),h*.24);
                }
                float2 grid=abs(frac(p/.17+.5)-.5)*.17;
                float fineGrid=max(stroke(grid.x,.0015),stroke(grid.y,.0015))*.045;
                float pulse=1+sin(_Time.y*_Motion.x*6.283)*_Motion.y;
                float travel=pow(saturate(.5+.5*sin(perimeter*4-_Time.y*_Motion.z*6.283)),8)*border*.45;
                float fill=_Fill*(.65+.35*exp2(-r*r)) + fineGrid + exp2(-abs(e-.032)*50)*.13;
                float3 color=_Violet.rgb*(fill+border*_Detail.x+chevrons*_Detail.y+rings*.8+travel)
                    +_Cyan.rgb*(cyan*_Detail.w+rings*.12)
                    +float3(2.5,1.1,3)*core*_Detail.z;
                return half4(color*pulse*_Strength, smoothstep(0,.01,e));
            }
            ENDHLSL
        }
    }
}
