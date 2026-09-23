Shader "Presentation/Tutorial Floor"
{
    Properties
    {
        _Color("Floor", Color) = (.008,.015,.05,1)
        _StageSize("Stage Coverage", Float) = 32
        _PadRadius("Platform Radius", Range(.6,1.6)) = .78
        _GlowStrength("Rim Glow", Range(0,.3)) = .14
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 screenPosition:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _PadRadius, _GlowStrength, _StageSize;
            CBUFFER_END
            Varyings vert(Attributes input) { Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz); o.uv=input.uv; o.screenPosition=ComputeScreenPos(o.positionCS); return o; }
            half4 frag(Varyings input):SV_Target
            {
                // One static quad shared by every demonstration. Distances are in
                // stage units; derivative AA keeps thin lines quiet at oblique angles.
                float2 p=(input.uv-.5)*_StageSize;
                float radial=length(p), aa=max(fwidth(radial),.006);
                float rim=abs(radial-_PadRadius);
                float ring=1-smoothstep(.009,.009+aa,rim);
                float inset=1-smoothstep(.005,.005+aa,abs(radial-_PadRadius*.88));
                float disc=1-smoothstep(_PadRadius-aa,_PadRadius+aa,radial);
                float halo=exp(-rim*rim/.025)*_GlowStrength;

                half3 cyan=half3(0,.65,.85), violet=half3(.29,.08,.6);
                half3 accent=lerp(cyan,violet,smoothstep(.3,1.1,p.x)*.45);
                half3 baseColor=_Color.rgb+half3(.006,.012,.025)*disc;
                // Ground extends through the frustum, without radial cutoffs.
                // Blend the square render output into its existing wider navy
                // frame; this does not crop/scale the actor or move the camera.
                float2 screenUV=input.screenPosition.xy/input.screenPosition.w;
                float2 edge=min(screenUV,1-screenUV);
                float compositeFade=smoothstep(0,.12,min(edge.x,edge.y));
                // The full-frame UI backdrop now owns the ground/grid. Keep only
                // the camera-projected pad here, so it stays anchored under Amy.
                float padAlpha=saturate(disc*.75+ring*.8+inset*.12+halo*3);
                return half4(baseColor+accent*(ring*.75+inset*.18+halo*2),padAlpha*compositeFade*_Color.a);
            }
            ENDHLSL
        }
    }
}
