Shader "Environment/Cliff Blend"
{
    Properties
    {
        _BaseMap("Quarry Albedo", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap("Quarry Normal", 2D) = "bump" {}
        _MaskMap("Quarry Packed Mask", 2D) = "white" {}
        _BlendMap("Transition Albedo", 2D) = "white" {}
        [Normal] _BlendNormal("Transition Normal", 2D) = "bump" {}
        _BlendMask("Transition Packed Mask", 2D) = "white" {}
        _ForestMap("Forest Albedo", 2D) = "white" {}
        [Normal] _ForestNormal("Forest Normal", 2D) = "bump" {}
        _ForestMask("Forest Packed Mask", 2D) = "white" {}
        _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            TEXTURE2D(_BlendMap); SAMPLER(sampler_BlendMap);
            TEXTURE2D(_BlendNormal); SAMPLER(sampler_BlendNormal);
            TEXTURE2D(_BlendMask); SAMPLER(sampler_BlendMask);
            TEXTURE2D(_ForestMap); SAMPLER(sampler_ForestMap);
            TEXTURE2D(_ForestNormal); SAMPLER(sampler_ForestNormal);
            TEXTURE2D(_ForestMask); SAMPLER(sampler_ForestMask);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Cull;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; half2 blend:TEXCOORD4; half fog:TEXCOORD5; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o=(Varyings)0; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS=half4(TransformObjectToWorldDir(v.tangentOS.xyz),v.tangentOS.w*GetOddNegativeScale());
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.blend=v.color.rg; o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half blend=saturate(i.blend.r);
                half3 albedo=lerp(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb,SAMPLE_TEXTURE2D(_BlendMap,sampler_BlendMap,i.uv).rgb,blend)*_BaseColor.rgb;
                half3 normalTS=normalize(lerp(UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv)),UnpackNormal(SAMPLE_TEXTURE2D(_BlendNormal,sampler_BlendNormal,i.uv)),blend));
                half4 mask=lerp(SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,i.uv),SAMPLE_TEXTURE2D(_BlendMask,sampler_BlendMask,i.uv),blend);
                albedo=lerp(albedo,SAMPLE_TEXTURE2D(_ForestMap,sampler_ForestMap,i.uv).rgb*_BaseColor.rgb,i.blend.g);
                normalTS=normalize(lerp(normalTS,UnpackNormal(SAMPLE_TEXTURE2D(_ForestNormal,sampler_ForestNormal,i.uv)),i.blend.g));
                mask=lerp(mask,SAMPLE_TEXTURE2D(_ForestMask,sampler_ForestMask,i.uv),i.blend.g);
                half3 n=normalize(i.normalWS), t=normalize(i.tangentWS.xyz), b=cross(n,t)*i.tangentWS.w;
                InputData input=(InputData)0; input.positionWS=i.positionWS; input.normalWS=normalize(TransformTangentToWorld(normalTS,half3x3(t,b,n)));
                input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS); input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                input.bakedGI=SampleSH(input.normalWS); input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS); input.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0; surface.albedo=albedo; surface.metallic=mask.r; surface.smoothness=mask.a*.65h;
                surface.normalTS=normalTS; surface.occlusion=mask.g; surface.alpha=1;
                half4 result=UniversalFragmentPBR(input,surface); result.rgb=MixFog(result.rgb,i.fog); return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
