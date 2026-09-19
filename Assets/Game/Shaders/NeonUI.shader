Shader "UI/Neon Surface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct Attributes { float4 vertex:POSITION; float4 color:COLOR; float4 localPoint:TEXCOORD0; float4 accent:TEXCOORD1; float4 secondary:TEXCOORD2; float4 config:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 vertex:SV_POSITION; float4 color:COLOR; float4 localPoint:TEXCOORD0; float4 accent:TEXCOORD1; float4 secondary:TEXCOORD2; float4 config:TEXCOORD3; float4 mask:TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
            float4 _Color, _ClipRect;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            Varyings vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color*_Color;
                o.localPoint=v.localPoint; o.accent=v.accent; o.secondary=v.secondary; o.config=v.config;
                float2 pixelSize=o.vertex.w/abs(mul((float2x2)UNITY_MATRIX_P,_ScreenParams.xy));
                float4 rect=clamp(_ClipRect,-2e10,2e10);
                o.mask=float4(v.vertex.xy*2-rect.xy-rect.zw,.25/(.25*float2(_UIMaskSoftnessX,_UIMaskSoftnessY)+abs(pixelSize)));
                return o;
            }
            float sdBox(float2 p,float2 b,float r) { float2 q=abs(p)-b+r; return length(max(q,0))+min(max(q.x,q.y),0)-r; }
            float segment(float2 p,float2 a,float2 b) { float2 ab=b-a; return length(p-a-ab*saturate(dot(p-a,ab)/max(dot(ab,ab),.0001))); }
            float stroke(float d,float width,float aa) { return 1-smoothstep(width-aa,width+aa,abs(d)); }
            float3 neonGradient(float3 a,float3 b,float t)
            {
                float3 c=lerp(a,b,saturate(t));
                float peak=max(c.r,max(c.g,c.b)), low=min(c.r,min(c.g,c.b));
                return max(c-low*.65,0)*peak/max(peak-low*.65,.001);
            }
            float glyph(float2 p,int symbol)
            {
                float d=10;
                if(symbol==0) {
                    p.x=abs(p.x);
                    // Two lobes joined to a tangent lower wedge, with a closed tip.
                    float lobes=length(p-float2(.19,.15))-.27;
                    float tip=max((p.x-p.y-.42184)*.7071,p.y+.04092);
                    return min(lobes,tip);
                }
                if(symbol==1) {
                    d=segment(p,float2(-.32,.35),float2(.32,.35));
                    d=min(d,segment(p,float2(-.32,.35),float2(-.27,-.10)));
                    d=min(d,segment(p,float2(.32,.35),float2(.27,-.10)));
                    d=min(d,segment(p,float2(-.27,-.10),float2(0,-.42)));
                    d=min(d,segment(p,float2(.27,-.10),float2(0,-.42))); return d-.04;
                }
                if(symbol==2) {
                    d=abs(length(p)-.29)-.025;
                    float2 q=abs(p); d=min(d,sdBox(q-float2(.36,0),float2(.13,.025),.02));
                    d=min(d,sdBox(q-float2(0,.36),float2(.025,.13),.02)); return min(d,length(p)-.035);
                }
                if(symbol==3 || symbol==4) {
                    if(symbol==4) p=float2(-p.y,p.x);
                    d=segment(p,float2(-.29,-.10),float2(0,.19));
                    d=min(d,segment(p,float2(0,.19),float2(.29,-.10)));
                    d=min(d,segment(p,float2(-.29,.17),float2(0,.46)));
                    d=min(d,segment(p,float2(0,.46),float2(.29,.17))); return d-.05;
                }
                if(symbol==5) {
                    float2 q=abs(p); d=abs(length(p)-.085)-.025;
                    d=min(d,sdBox(q-float2(.25,0),float2(.14,.028),.01));
                    d=min(d,sdBox(q-float2(0,.25),float2(.028,.14),.01));
                    d=min(d,segment(q,float2(.28,.1),float2(.39,0))-.022);
                    d=min(d,segment(q,float2(.1,.28),float2(0,.39))-.022); return d;
                }
                if(symbol==6) return min(sdBox(p-float2(.15,0),float2(.055,.29),.025),sdBox(p+float2(.15,0),float2(.055,.29),.025));
                if(symbol==7) {
                    float2 q=float2(abs(p.x),p.y);
                    d=segment(q,float2(0,-.30),float2(.18,-.23));
                    d=min(d,segment(q,float2(.18,-.23),float2(.37,-.27)));
                    d=min(d,segment(q,float2(.37,-.27),float2(.37,.30)));
                    d=min(d,segment(q,float2(.37,.30),float2(.18,.34)));
                    d=min(d,segment(q,float2(.18,.34),float2(0,.27)));
                    d=min(d,segment(q,float2(0,.27),float2(0,-.30)));return d-.027;
                }
                if(symbol==8) {
                    d=abs(sdBox(p+float2(0,.16),float2(.25,.20),.045))-.025;
                    d=min(d,max(abs(length(p-float2(0,.11))-.17)-.027,-p.y-.01));
                    return min(d,segment(p,float2(0,-.11),float2(0,-.23))-.032);
                }
                if(symbol==9) return min(segment(p,float2(-.25,-.25),float2(.25,.25)),segment(p,float2(-.25,.25),float2(.25,-.25)))-.045;
                return d;
            }
            float4 frag(Varyings i):SV_Target
            {
                float2 p=i.localPoint.xy,b=i.localPoint.zw;
                float aa=max(.35,max(fwidth(p.x),fwidth(p.y))*.75);
                int kind=(int)(i.config.y+.5);
                float visualState=fmod(floor(i.config.z+.5),8), interaction=i.config.w;
                bool details=i.config.z>7.5;
                float4 result;
                if(kind>=16) {
                    float size=max(min(b.x,b.y)*1.6,1);
                    float d=glyph(p/size,kind-16)*size;
                    float coverage=1-smoothstep(-aa,aa,d);
                    float halo=exp(-max(d,0)/max(1,size*.03))*.16;
                    result=float4(i.color.rgb,saturate(coverage+halo)*i.color.a);
                } else {
                    float2 uv=p/max(b,1)*.5+.5;
                    float3 left=i.accent.rgb,right=i.secondary.rgb;
                    float3 neon=neonGradient(left,right,smoothstep(0,1,uv.x*.86+(1-uv.y)*.14));
                    float disabled=(visualState==3 || visualState==4 || interaction<0)?1:0;
                    float empty=visualState==2?1:0, selected=visualState==1?1:0;
                    neon=lerp(neon,float3(.32,.37,.57),disabled*.8+empty*.48);
                    neon=lerp(neon,right,selected*.72);
                    float strength=(1+selected*.42+max(interaction,0)*.13)*(1-disabled*.4-empty*.25);
                    float r=min(i.accent.w,min(b.x,b.y));
                    float d=sdBox(p,b,r);
                    if(kind==2 || kind==3 || kind==7) d=length(p)-min(b.x,b.y);
                    float bw=max(i.secondary.w,.1),edge=stroke(d+bw*.5,bw*.5,aa);
                    float inside=1-smoothstep(-aa,aa,d),g=max(i.config.x,.01);
                    float outer=exp(-max(d,0)*3/g)*.29*(1-inside);
                    float inner=exp(-abs(d)*.22)*.15*inside;
                    float inset=stroke(d+max(4,bw*2.8),.45,aa)*.28;
                    float3 fill=i.color.rgb;
                    fill+=lerp(left,right,uv.x)*(.035*pow(saturate(uv.y),5)+.018*pow(saturate(uv.x),4));
                    fill+=neon*inner;
                    float decoration=0;
                    if(kind==0 && details && min(b.x,b.y)>45) {
                        float railY=b.y-min(24,b.y*.1);
                        decoration=stroke(segment(p,float2(-b.x+r,railY),float2(b.x-r,railY)),.45,aa)*.32;
                        decoration=max(decoration,stroke(segment(abs(p),b-float2(28,9),b-float2(9,28)),1.2,aa)*.65);
                    }
                    if(kind==4 && details) {
                        float2 q=abs(p),corner=b-min(b*.2,float2(16,16));
                        float marks=min(segment(q,corner-float2(12,0),corner),segment(q,corner-float2(0,12),corner));
                        decoration=stroke(marks,.9,aa)*(.65+selected*.3)*(1-empty*.65);
                    }
                    if(kind==3) {
                        float R=min(b.x,b.y),angle=atan2(p.y,p.x);
                        float dashed=step(.26,frac(angle*24/6.283185));
                        decoration=stroke(length(p)-R*.79,.6,aa)*dashed*.65;
                        decoration=max(decoration,stroke(length(p)-R*.56,.7,aa)*.3); fill*=.7;
                    }
                    if(kind==5) { fill=neonGradient(left,right,uv.x)*(.75+.25*uv.y); edge*=.22; inset=0; }
                    if(kind==6) { inside=0;edge=stroke(p.y,.65,aa); outer=exp(-abs(p.y)*.7)*.18; inset=0; }
                    if(kind==7) { fill=left*.85;neon=lerp(left,1,.45); inset=0; }
                    float alpha=saturate((inside+outer*strength)*i.color.a);
                    if(kind==6) alpha=saturate(edge+outer)*i.color.a;
                    float3 lit=fill+(edge*.95+inset+decoration)*neon*strength;
                    result=float4(lerp(neon,lit,inside),alpha);
                }
                #ifdef UNITY_UI_CLIP_RECT
                    float2 mask=saturate((_ClipRect.zw-_ClipRect.xy-abs(i.mask.xy))*i.mask.zw);
                    result.a*=mask.x*mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(result.a-.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
