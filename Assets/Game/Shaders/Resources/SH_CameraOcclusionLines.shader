Shader "KVA/CameraOcclusionLines"
{
    Properties
    {
        _LineColor ("Line Color", Color) = (0.72, 0.80, 0.95, 0.30)
        _DashLengthPixels ("Dash Length Pixels", Float) = 14
        _DashFill ("Dash Fill", Range(0.05, 0.95)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+50"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "CameraOcclusionLines"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _LineColor;
                float _DashLengthPixels;
                float _DashFill;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float dashLength =
                    max(_DashLengthPixels, 1.0);

                // Screen-space diagonal dash pattern.
                // Since the mesh is MeshTopology.Lines,
                // this only breaks the thin structural lines.
                float phase =
                    frac(
                        (input.positionCS.x + input.positionCS.y)
                        / dashLength
                    );

                clip(_DashFill - phase);

                return _LineColor;
            }

            ENDHLSL
        }
    }
}
