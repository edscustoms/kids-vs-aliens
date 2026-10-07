Shader "Environment/BikeRoute Asphalt"
{
    Properties
    {
        _BaseMap("Asphalt",2D)="white"{}
        [Normal] _BumpMap("Asphalt normal",2D)="bump"{}
        _MetallicGlossMap("Asphalt packed mask",2D)="white"{}
        _BaseColor("Asphalt tint",Color)=(.86,.89,.92,1)
        _GravelMap("Edge aggregate",2D)="white"{}
        [Normal] _GravelNormal("Aggregate normal",2D)="bump"{}
        _GravelMask("Aggregate packed mask",2D)="white"{}
        _EdgeColor("Dust tint",Color)=(.66,.59,.47,1)
        _DetailScale("Detail metres",Float)=3.2
        _BumpScale("Normal strength",Range(0,2))=.65
        _Smoothness("Smoothness",Range(0,1))=.32
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap);SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_GravelMap);TEXTURE2D(_GravelNormal);TEXTURE2D(_GravelMask);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor,_EdgeColor;
                float _DetailScale,_BumpScale,_Smoothness;
            CBUFFER_END
            #include "BikeRouteSurfaceSampling.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half fog:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.uv=v.uv;o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                RoadSample asphalt=SampleRoadSurface(TEXTURE2D_ARGS(_BaseMap,sampler_BaseMap),TEXTURE2D_ARGS(_BumpMap,sampler_BumpMap),
                    TEXTURE2D_ARGS(_MetallicGlossMap,sampler_MetallicGlossMap),i.positionWS.xz/_DetailScale);
                RoadSample gravel=SampleRoadSurface(TEXTURE2D_ARGS(_GravelMap,sampler_BaseMap),TEXTURE2D_ARGS(_GravelNormal,sampler_BumpMap),
                    TEXTURE2D_ARGS(_GravelMask,sampler_MetallicGlossMap),i.positionWS.xz/2.4);
                float macro=SurfaceNoise(i.positionWS.xz*.038);
                float edgeNoise=SurfaceNoise(i.positionWS.xz*.65);
                // Native road UV.x spans the exact authored edges. Only shading creeps
                // inward; no widening, extra road layer or collider is involved.
                float margin=min(i.uv.x,1-i.uv.x);
                float dustyWidth=lerp(.035,.105,SurfaceNoise(i.positionWS.xz*.13));
                half dust=(1-smoothstep(.005,dustyWidth,margin+edgeNoise*.016))*.8;
                half grit=smoothstep(.18,.68,SurfaceNoise(i.positionWS.xz*11));
                dust*=lerp(.67,1,grit);
                SurfaceData surface=(SurfaceData)0;
                half luminance=dot(asphalt.color,half3(.2126,.7152,.0722));
                surface.albedo=lerp(luminance.xxx,asphalt.color,.38)*_BaseColor.rgb*lerp(.76,1.14,macro);
                surface.albedo=lerp(surface.albedo,gravel.color*_EdgeColor.rgb,dust);
                surface.smoothness=lerp(asphalt.smoothness*_Smoothness,gravel.smoothness*.15,dust);
                surface.occlusion=lerp(asphalt.ao,gravel.ao,dust);surface.alpha=1;surface.normalTS=half3(0,0,1);
                half2 bump=lerp(asphalt.bump,gravel.bump,dust)*_BumpScale;
                half3 normal=normalize(i.normalWS),perturb=half3(bump.x,0,bump.y);
                perturb-=normal*dot(perturb,normal);
                InputData input=(InputData)0;input.positionWS=i.positionWS;input.positionCS=i.positionCS;
                input.normalWS=normalize(normal+perturb);input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);input.bakedGI=SampleSH(input.normalWS);
                input.shadowMask=half4(1,1,1,1);input.vertexLighting=VertexLighting(i.positionWS,input.normalWS);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                half4 color=UniversalFragmentPBR(input,surface);
                color.rgb=MixFog(color.rgb,InitializeInputDataFog(float4(i.positionWS,1),i.fog));return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
