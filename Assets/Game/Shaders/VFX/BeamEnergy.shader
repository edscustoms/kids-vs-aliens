Shader "KVA/Beam Energy"
{
    Properties
    {
        [HDR] _Tint ("Emission", Color) = (2,1,3,1)
        _Round ("Round particle (otherwise ribbon)", Float) = 1
        _BeamVisibility ("Beam visibility", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Round;
                half _BeamVisibility;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.color=v.color; o.uv=v.uv; return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 uv=i.uv*2-1;
                float d=lerp(abs(uv.y),length(uv),_Round);
                half glow=exp2(-5*d*d)*saturate((1-d)*5);
                half core=exp2(-38*d*d);
                return half4(i.color.rgb*_Tint.rgb*(.55+core),i.color.a*_Tint.a*glow*_BeamVisibility);
            }
            ENDHLSL
        }
    }
}
