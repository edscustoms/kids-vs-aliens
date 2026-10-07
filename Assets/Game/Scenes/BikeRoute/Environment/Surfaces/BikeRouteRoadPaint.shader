Shader "Environment/BikeRoute Road Paint"
{
    Properties { _BaseColor("Paint",Color)=(1,1,1,1) _Wear("Wear",Range(0,1))=.35 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Transparent" }
        Cull Back ZWrite Off Offset -1,-1 Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;half _Wear;
            CBUFFER_END
            #include "BikeRouteSurfaceSampling.hlsl"
            struct Varyings { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half fog:TEXCOORD2; };
            Varyings Vert(float4 vertex:POSITION,float3 normal:NORMAL)
            {
                Varyings o;VertexPositionInputs p=GetVertexPositionInputs(vertex.xyz);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(normal);
                o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.positionWS.xz;
                float footprint=max(length(ddx(p)),length(ddy(p)));
                half pits=lerp(smoothstep(.24,.62,SurfaceNoise(p*32)),.7,saturate(footprint*18));
                half scuffs=lerp(.78,1,SurfaceNoise(p*.8));
                half opacity=lerp(1,pits*scuffs,_Wear);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 normal=normalize(i.normalWS);
                half3 illumination=SampleSH(normal)+light.color*saturate(dot(normal,light.direction))*light.shadowAttenuation*light.distanceAttenuation;
                half3 color=_BaseColor.rgb*illumination;
                return half4(MixFog(color,InitializeInputDataFog(float4(i.positionWS,1),i.fog)),opacity*.92);
            }
            ENDHLSL
        }
    }
}
