Shader "KVA/Environment/Mesh Decal"
{
    Properties
    {
        [MainTexture] _BaseMap("Decal Atlas", 2D) = "white" {}
        [MainColor] _Color("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.001,1)) = 0.3
        _Fade("Camera Occlusion Visibility", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest+25" "CameraOcclusionLines"="Off" }
        // Each side has its OWN outward-facing mesh. Back faces must not expose reversed text
        // through the dither holes of the opposite panel when the excavator fades.
        Cull Back
        ZTest LEqual
        ZWrite On
        Blend One Zero
        Offset -1, -1
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color;
                float _Cutoff;
                float _Fade;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 screenPosition : TEXCOORD3;
                half fog : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.screenPosition = ComputeScreenPos(position.positionCS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _Color;
                clip(color.a - _Cutoff);
                // Same screen coordinates, Bayer ordering and 1-Bayer threshold as the
                // current SG_EnvironmentSurface (Dither node In=1 -> AlphaClipThreshold).
                float2 pixel = input.screenPosition.xy / input.screenPosition.w * _ScreenParams.xy;
                const float bayer[16] = {
                    1,9,3,11, 13,5,15,7, 4,12,2,10, 16,8,14,6
                };
                uint index = ((uint)pixel.x % 4) * 4 + (uint)pixel.y % 4;
                clip(_Fade - (1.0 - bayer[index] / 17.0));
                half3 normal = normalize(input.normalWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = input.screenPosition;
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord);
                half3 lighting = SampleSH(normal) + mainLight.color * saturate(dot(normal, mainLight.direction))
                    * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                color.rgb = MixFog(color.rgb * lighting, input.fog);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }
        // No shadow/depth-normal passes: the underlying machine supplies those surfaces.
    }
}
