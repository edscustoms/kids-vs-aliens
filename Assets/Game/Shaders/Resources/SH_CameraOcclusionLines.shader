Shader "CameraOcclusionLines"
{
    Properties
    {
        _LineColor ("Line Color", Color) = (.72,.80,.95,.30)
        _DashLengthPixels ("Dash Length Pixels", Float) = 14
        _DashFill ("Dash Fill", Range(.05,.95)) = .55
        [HideInInspector] _MaskBaseMap ("Cutout", 2D) = "white" {}
        [HideInInspector] _MaskUV ("UV", Vector) = (1,1,0,0)
        [HideInInspector] _MaskCutoff ("Cutoff", Float) = .5
        [HideInInspector] _MaskAlpha ("Alpha", Float) = 1
        [HideInInspector] _MaskAlphaClip ("Cutout enabled", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ActualRendererMask"
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D_X_FLOAT(_OcclusionSceneDepth);
            TEXTURE2D(_MaskBaseMap); SAMPLER(sampler_MaskBaseMap);
            float4 _OcclusionMaskSize;
            float _OcclusionSilhouetteStrength;
            CBUFFER_START(UnityPerMaterial)
                float4 _MaskUV;
                float _MaskCutoff, _MaskAlpha, _MaskAlphaClip;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float eyeDepth:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.eyeDepth = -TransformWorldToView(world).z;
                o.uv = v.uv * _MaskUV.xy + _MaskUV.zw;
                return o;
            }
            half2 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if (_MaskAlphaClip > .5) clip(SAMPLE_TEXTURE2D(_MaskBaseMap,sampler_MaskBaseMap,i.uv).a * _MaskAlpha - _MaskCutoff);
                float2 uv = i.positionCS.xy * _OcclusionMaskSize.zw;
                float scene = SAMPLE_TEXTURE2D_X(_OcclusionSceneDepth,sampler_PointClamp,uv).r;
                float sceneEye = unity_OrthoParams.w > .5 ? LinearDepthToEyeDepth(scene) : LinearEyeDepth(scene,_ZBufferParams);
                // The half-resolution sample must not cut noisy self-occlusion holes in angled faces.
                float bias = .03 + 2 * fwidth(i.eyeDepth);
                clip(sceneEye + bias - i.eyeDepth);
                return half2(1, _OcclusionSilhouetteStrength);
            }
            ENDHLSL
        }
        Pass
        {
            Name "VisibleUnionContour"
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { uint vertexID:SV_VertexID; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 texcoord:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.texcoord = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }
            TEXTURE2D_X(_OcclusionSilhouetteMask);
            float4 _LineColor, _OutlineStep;
            float _DashLengthPixels, _DashFill;
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.texcoord;
                half2 center = SAMPLE_TEXTURE2D_X(_OcclusionSilhouetteMask,sampler_LinearClamp,uv).rg;
                half coverage = center.r, strength = center.g;
                [unroll] for (int y=-1; y<=1; ++y)
                [unroll] for (int x=-1; x<=1; ++x)
                {
                    half2 sample = SAMPLE_TEXTURE2D_X(_OcclusionSilhouetteMask,sampler_LinearClamp,uv+float2(x,y)*_OutlineStep.xy).rg;
                    coverage = max(coverage,sample.r);
                    strength = max(strength,sample.g);

                }
                // Coverage only: overlapping child renderers produce one contour, not internal seams.
                half edge = saturate(coverage-center.r);
                float phase = frac(dot(uv*_OutlineStep.zw,float2(1,1))/max(2,_DashLengthPixels));
                float aa = max(fwidth(phase),.01);
                half dash = 1-smoothstep(_DashFill-aa,_DashFill+aa,phase);
                return half4(_LineColor.rgb, _LineColor.a*strength*edge*dash);
            }
            ENDHLSL
        }
    }
}
