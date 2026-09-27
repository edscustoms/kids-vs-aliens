Shader "Game/Alien Ground Aurora"
{
    Properties
    {
        _Noise ("Baked flow noise", 2D) = "white" {}
        [HDR] _Tint ("Energy color / opacity", Color) = (.1,1.2,1,.4)
        _Motion ("Age, speed, authored phase", Vector) = (0,.2,0,0)
        _Footprint ("Perimeter softness, irregularity, shape offsets", Vector) = (.4,.85,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float4 _Motion;
                float4 _Footprint;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 section:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float phase:TEXCOORD1; float2 footprint:TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.footprint = v.positionOS.xz * 2;
                float flow = _Motion.x * _Motion.y;
                float phase = _Motion.z + v.section.x;
                // Authored age drives rolling banks, including while the world is paused.
                // The mesh bounds and Editor clearance include this entire displacement.
                float lift = sin(v.uv.y * 3.14159) * sin(v.uv.x * 3.14159);
                v.positionOS.y += .12 * sin(v.uv.x * 9 - flow * 3 + phase) * lift;
                v.positionOS.z += .065 * sin(v.uv.x * 7 - flow * 2.4 + phase) * lift;
                v.positionOS.x += .035 * sin(flow * 2 + phase + v.uv.y * 3) * lift;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.phase = phase;
                return o;
            }
            half Perimeter(float2 p)
            {
                // A static, authored union of uneven lobes. Only opacity changes: the accepted
                // mesh, vertical volume, colors and animated flow keep their existing behavior.
                half2 erosion = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p*.7 + _Footprint.zw).rg;
                float2 warped = p + (erosion-.5)*_Footprint.y*.35;
                float bend = sin(_Footprint.z)*.22;
                float main = length((warped-float2(-.28,bend))/float2(.78,.48))-1;
                float upper = length((warped-float2(.23,.34 + sin(_Footprint.w)*.12))/float2(.64,.48))-1;
                float lower = length((warped-float2(.4,-.36))/float2(.49,.42))-1;
                float field = lerp(length(warped/float2(1,.82))-1, min(main,min(upper,lower)), _Footprint.y);
                field += (erosion.g-.5)*.45*_Footprint.y;
                half softness = max(.1,_Footprint.x);
                half perimeter = 1-smoothstep(-softness, .04, field);
                // Zero on every outer side, even where a lobe approaches the mesh perimeter.
                return perimeter * (1-smoothstep(1-softness*.55,1.04,max(abs(p.x),abs(p.y))));
            }
            half4 frag(Varyings i):SV_Target
            {
                float flow = _Motion.x * _Motion.y;
                float2 drift = float2(-flow * .28 + i.phase, flow * .16);
                half2 broad = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, i.uv * float2(1.8,.8) + drift).rg;
                // Two coherent scrolling fields: the broad bank bends the finer energy filaments.
                half2 detail = SAMPLE_TEXTURE2D(_Noise, sampler_Noise,
                    i.uv * float2(3.5,1.4) + (broad - .5) * .65 - drift * .65).rg;
                half across = abs(i.uv.y-.5 + .12*sin(i.uv.x*9+i.phase) + .12*(broad.r-.5));
                half edge = smoothstep(0,.13,i.uv.x) * smoothstep(0,.13,1-i.uv.x)
                    * (1-smoothstep(.13,.48,across))
                    * smoothstep(0,.1,i.uv.y) * smoothstep(0,.1,1-i.uv.y);
                edge *= Perimeter(i.footprint);
                half mist = smoothstep(.26,.72,broad.r*.65+detail.g*.35);
                half filament = smoothstep(.55,.95,sin(i.uv.y*18+broad.r*8+detail.g*3))
                    * smoothstep(.32,.68,detail.r);
                half breathing = .88 + .12 * sin(flow * 2 + i.phase + i.uv.x * 5);
                half opacity = edge * (mist*.52 + filament*.32) * breathing * _Tint.a;
                // Keep hue aligned across the layers so cyan + violet overlap does not wash out to white.
                half accent = smoothstep(.65,.93,.5+.5*sin(i.uv.x*5+_Motion.z+flow*.25));
                half3 tint = lerp(_Tint.rgb, half3(.8,.06,1.15), accent*.9);
                return half4(tint * (.8 + filament*.25), opacity);
            }
            ENDHLSL
        }
    }
}
