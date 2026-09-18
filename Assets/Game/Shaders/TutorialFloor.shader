Shader "Presentation/Tutorial Floor"
{
    Properties { _Color("Floor", Color) = (.008,.015,.05,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            Varyings vert(Attributes input) { Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz); o.uv=input.uv; return o; }
            half4 frag(Varyings input):SV_Target
            {
                float2 p=(input.uv-.5)*12;
                float2 grid=abs(frac(p-.5)-.5)/max(fwidth(p),.001);
                float gridLine=1-saturate(min(grid.x,grid.y));
                float radial=length(input.uv-.5);
                float ring=1-smoothstep(.002,.009,abs(radial-.12));
                float fade=1-smoothstep(.18,.49,radial);
                half3 accent=lerp(half3(0,.65,.85),half3(.45,.12,.85),input.uv.x);
                return half4(_Color.rgb+accent*(gridLine*.12+ring*.55),fade);
            }
            ENDHLSL
        }
    }
}
