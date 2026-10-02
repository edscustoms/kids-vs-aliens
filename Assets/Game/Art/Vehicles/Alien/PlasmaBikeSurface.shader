Shader "Vehicles/Plasma Bike Surface"
{
    Properties
    {
        _BaseColor ("Body Color", Color) = (.1,.12,.2,1)
        [HDR] _EmissionColor ("Plasma Emission", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "BikeSurface"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                // Small mobile prop: stable ambient/facet shading keeps the saddle and
                // silhouette readable in dark levels without adding lights or shadows.
                half light=.38h+.62h*saturate(dot(normalize(input.normalWS),normalize(half3(-.35h,1,.25h))));
                return half4(_BaseColor.rgb*light+_EmissionColor.rgb,1);
            }
            ENDHLSL
        }
    }
}
