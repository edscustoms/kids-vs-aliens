Shader "KVA/Beam Volume"
{
    Properties
    {
        [HDR] _Tint ("Violet volume", Color) = (.7,.12,1.5,.16)
        [HDR] _EdgeTint ("Cyan edge support", Color) = (.2,.6,1.3,.12)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _EdgeTint;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half rim=pow(1-saturate(abs(dot(normalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS)))),2);
                half4 energy=lerp(_Tint,_EdgeTint,rim*.45);
                energy.a*=.35+.65*rim;
                return energy;
            }
            ENDHLSL
        }
    }
}
