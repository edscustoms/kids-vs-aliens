Shader "Vehicles/Heavy Laser"
{
    Properties { [HDR] _Color("Energy", Color) = (5,.15,1,1) _Round("Round flash", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Round;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv; output.color=input.color; return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half edge=saturate(1-lerp(abs(input.uv.y*2-1), length(input.uv*2-1), _Round));
                return half4(_Color.rgb*input.color.rgb, edge*edge*input.color.a*_Color.a);
            }
            ENDHLSL
        }
    }
}
