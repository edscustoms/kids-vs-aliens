Shader "Kids VS Aliens/Environment/Cement Bag Projected Print"
{
    Properties
    {
        _BaseColor ("Cardboard Color", Color) = (0.46, 0.30, 0.19, 1)
        _PrintMap ("Print Overlay", 2D) = "white" {}
        _AccentColor ("Side Accent Color", Color) = (1, 0.7, 0.05, 1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.10
        _PrintOpacity ("Print Opacity", Range(0,1)) = 1.0
        _PrintCutoff ("Print Alpha Cleanup", Range(0,0.5)) = 0.10
        _ProjectionSharpness ("Projection Sharpness", Range(1,16)) = 8.0
        _SidePrintStrength ("Side Print Strength", Range(0,1)) = 0.82
        _SideBandCenter ("Side Print Band Center", Range(0,1)) = 0.50
        _SideBandScale ("Side Print Band Scale", Range(0.05,1)) = 0.22
        _SideAccentStrength ("Side Accent Strength", Range(0,0.5)) = 0.14
        _BagBoundsMin ("Bag Bounds Min", Vector) = (-0.33, 0, -0.21, 0)
        _BagBoundsSize ("Bag Bounds Size", Vector) = (0.66, 0.165, 0.418, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_PrintMap);
            SAMPLER(sampler_PrintMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _AccentColor;
                half _Smoothness;
                half _PrintOpacity;
                half _PrintCutoff;
                half _ProjectionSharpness;
                half _SidePrintStrength;
                half _SideBandCenter;
                half _SideBandScale;
                half _SideAccentStrength;
                float4 _BagBoundsMin;
                float4 _BagBoundsSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 staticLightmapUV : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                half3 normalOS : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
                half fogFactor : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);

                return output;
            }

            half4 SamplePrint(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_PrintMap, sampler_PrintMap, saturate(uv));
            }

            half4 ProjectPrint(float3 positionOS, half3 normalOS, out half sideWeight)
            {
                float3 safeSize = max(_BagBoundsSize.xyz, float3(0.0001, 0.0001, 0.0001));
                float3 uvw = saturate((positionOS - _BagBoundsMin.xyz) / safeSize);

                half3 n = normalize(normalOS);
                half3 weights = pow(abs(n), max(_ProjectionSharpness, 1.0h));
                weights /= max(weights.x + weights.y + weights.z, 0.0001h);

                // Broad faces (top + bottom): the source art is portrait 2:3.
                // Rotate it 90 degrees so it naturally fits the bag's ~0.60 x 0.38 face.
                float2 uvBroad = float2(uvw.z, 1.0 - uvw.x);

                // Vertical faces intentionally use a narrow crop through the source artwork.
                // This gives us believable side printing without needing three extra textures.
                float sideY = _SideBandCenter + (uvw.y - 0.5) * _SideBandScale;
                float2 uvLongSide = float2(uvw.x, sideY);
                float2 uvShortSide = float2(uvw.z, sideY);

                half4 broad = SamplePrint(uvBroad);
                half4 longSide = SamplePrint(uvLongSide);
                half4 shortSide = SamplePrint(uvShortSide);

                sideWeight = saturate(weights.x + weights.z);

                half4 result = broad * weights.y;
                result += longSide * weights.z * _SidePrintStrength;
                result += shortSide * weights.x * _SidePrintStrength;
                return result;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half sideWeight;
                half4 printSample = ProjectPrint(input.positionOS, input.normalOS, sideWeight);

                // Remove the faint AI-generated glow / almost-transparent pixels around the artwork.
                half printMask = saturate(
                    (printSample.a - _PrintCutoff) /
                    max(1.0h - _PrintCutoff, 0.001h)
                );
                printMask *= _PrintOpacity;

                half3 albedo = _BaseColor.rgb;

                // A very subtle brand-colored side band gives the narrow edges intentional packaging detail.
                float3 safeSize = max(_BagBoundsSize.xyz, float3(0.0001, 0.0001, 0.0001));
                float3 uvw = saturate((input.positionOS - _BagBoundsMin.xyz) / safeSize);
                half centerBand = 1.0h - smoothstep(0.18h, 0.34h, abs((half)uvw.y - 0.5h));
                albedo = lerp(albedo, _AccentColor.rgb, centerBand * sideWeight * _SideAccentStrength);

                // The actual label ink is blended directly into the bag surface.
                albedo = lerp(albedo, printSample.rgb, printMask);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = VertexLighting(input.positionWS, inputData.normalWS);
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1,1,1,1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = 0.0h;
                surfaceData.specular = half3(0.04h, 0.04h, 0.04h);
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0,0,1);
                surfaceData.emission = half3(0,0,0);
                surfaceData.occlusion = 1.0h;
                surfaceData.alpha = 1.0h;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0h;
                return color;
            }
            ENDHLSL
        }

        // Opaque bags can reuse URP/Lit's standard depth/shadow passes.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack Off
}
