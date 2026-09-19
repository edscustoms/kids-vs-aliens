Shader "UI/Knowledge Stage Backdrop"
{
    Properties
    {
        [PerRendererData] _MainTex("Texture",2D)="white"{}
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float2 local:TEXCOORD1; float4 color:COLOR; };
            float4 _ClipRect;
            v2f vert(appdata v) { v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.local=v.vertex.xy;o.color=v.color;return o; }
            fixed4 frag(v2f i):SV_Target
            {
                // Screen UV rises from the foreground toward a mid-frame horizon.
                // Inverse projection makes cells shrink into the distance, rather
                // than converge at the bottom of the preview like a wall/ceiling.
                float distanceToHorizon=.56-i.uv.y;
                float depth=max(distanceToHorizon,.025);
                float2 gridUV=float2((i.uv.x-.5)*2.4/depth,1.4/depth);
                float2 lineDistance=abs(frac(gridUV-.5)-.5)/max(fwidth(gridUV),.001);
                float lines=1-smoothstep(.35,1.25,min(lineDistance.x,lineDistance.y));
                float groundFade=smoothstep(.025,.28,distanceToHorizon);
                float illumination=exp(-dot((i.uv-float2(.5,.19))*float2(1.4,2),
                                            (i.uv-float2(.5,.19))*float2(1.4,2))*4);
                float3 baseColor=float3(.004,.008,.018)
                    +float3(.007,.026,.055)*groundFade*(.3+.7*illumination);
                float3 accent=lerp(float3(.015,.42,.68),float3(.15,.10,.36),i.uv.x*.3);
                float3 color=baseColor+accent*lines*groundFade*(.3+.7*illumination);
                float alpha=i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                    alpha*=UnityGet2DClipping(i.local,_ClipRect);
                #endif
                return fixed4(color*i.color.rgb,alpha);
            }
            ENDCG
        }
    }
}
