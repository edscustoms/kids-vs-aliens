Shader "Environment/BikeRoute Terrain"
{
    Properties
    {
        _BaseColor("Rock palette", Color) = (.78,.80,.83,1)
        _TerrainWorldSize("Terrain width / depth", Vector) = (2048,2048,0,0)
        [Toggle(_CLIFF_PROJECTION)] _CliffProjection("Project rock onto steep faces", Float) = 1
        _CliffScale("Cliff detail scale", Range(.25,2)) = .7
        _CliffVariation("Cliff mineral variation", Range(0,1)) = .65
        _CliffNormalStrength("Cliff normal strength", Range(0,2)) = 1.25
        _GroundStrength("Ground treatment", Range(0,1)) = 0
        _GroundNormalStrength("Ground relief strength", Range(0,3)) = 1.8
        [NoScaleOffset] _GroundColors("Ground families: color / AO", 2DArray) = "" {}
        [NoScaleOffset] _GroundRelief("Ground families: normal XY / smoothness", 2DArray) = "" {}
        [NoScaleOffset] _GroundRoadDistance("Baked asphalt distance (32m)", 2D) = "white" {}
        [HideInInspector] _GroundField("Ground field origin / size", Vector) = (0,0,2048,2048)
        [HideInInspector] _Control("Control", 2D) = "red" {}
        [HideInInspector] _Splat0("Quarry", 2D) = "grey" {}
        [HideInInspector] _Splat1("Dark rock", 2D) = "grey" {}
        [HideInInspector] _Splat2("Forest ground", 2D) = "grey" {}
        [HideInInspector] _Splat3("Forest floor", 2D) = "grey" {}
        [HideInInspector] _Normal0("Normal 0", 2D) = "bump" {}
        [HideInInspector] _Normal1("Normal 1", 2D) = "bump" {}
        [HideInInspector] _Normal2("Normal 2", 2D) = "bump" {}
        [HideInInspector] _Normal3("Normal 3", 2D) = "bump" {}
        [HideInInspector] _Mask0("Mask 0", 2D) = "white" {}
        [HideInInspector] _Mask1("Mask 1", 2D) = "white" {}
        [HideInInspector] _Mask2("Mask 2", 2D) = "white" {}
        [HideInInspector] _Mask3("Mask 3", 2D) = "white" {}
        [HideInInspector] _MainTex("Basemap", 2D) = "grey" {}
        [HideInInspector] _TerrainHolesTexture("Holes", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Geometry-100" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "TerrainCompatible"="True" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _ALPHATEST_ON
            #pragma shader_feature_local _CLIFF_PROJECTION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // Terrain supplies layer transforms and patch instancing. This one-level
            // material projects its existing four maps; it never adds a cliff mesh.
            float4 _TerrainWorldSize;
            float _CliffScale, _CliffVariation, _CliffNormalStrength;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half fog:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o=(Varyings)0; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                TerrainInstancing(v.positionOS,v.normalOS,v.uv);
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.uv=v.uv;
                o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            struct Layer { half3 albedo; half3 perturbation; half4 mask; };
            float2 RockHash(float2 p)
            {
                float3 h=frac(float3(p.xyx)*float3(.1031,.1030,.0973));
                h+=dot(h,h.yzx+33.33);
                return frac((h.xx+h.yz)*h.zy);
            }
            float RockNoise(float2 p)
            {
                float2 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(RockHash(cell).x,RockHash(cell+float2(1,0)).x,f.x),
                    lerp(RockHash(cell+float2(0,1)).x,RockHash(cell+1).x,f.x),f.y);
            }
            Layer BlendRock(Layer a,Layer b,half t)
            {
                Layer l;
                l.albedo=lerp(a.albedo,b.albedo,t);
                l.perturbation=lerp(a.perturbation,b.perturbation,t);
                l.mask=lerp(a.mask,b.mask,t); return l;
            }
            Layer SampleProjection(TEXTURE2D_PARAM(albedoMap,s),TEXTURE2D_PARAM(normalMap,ns),TEXTURE2D_PARAM(maskMap,ms),
                float2 uv,float2 dx,float2 dy,int axis,half normalScale)
            {
                Layer l;
                l.albedo=SAMPLE_TEXTURE2D_GRAD(albedoMap,s,uv,dx,dy).rgb;
                half2 bump=UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(normalMap,ns,uv,dx,dy),normalScale).xy;
                l.perturbation=axis==0?half3(0,bump.y,bump.x):axis==1?half3(bump.x,0,bump.y):half3(bump.x,bump.y,0);
                l.mask=SAMPLE_TEXTURE2D_GRAD(maskMap,ms,uv,dx,dy); return l;
            }
            Layer SampleCliffPatch(TEXTURE2D_PARAM(a,s),TEXTURE2D_PARAM(n,ns),TEXTURE2D_PARAM(m,ms),
                float2 uv,float2 dx,float2 dy,int axis,half scale,float2 seed)
            {
                float2 turn=normalize(float2(1,(seed.y-.5)*.45));
                float2x2 rotation=float2x2(turn.x,-turn.y,turn.y,turn.x);
                float frequency=_CliffScale*lerp(.82,1.18,seed.x);
                Layer l=SampleProjection(TEXTURE2D_ARGS(a,s),TEXTURE2D_ARGS(n,ns),TEXTURE2D_ARGS(m,ms),
                    mul(rotation,uv)*frequency+seed*7,mul(rotation,dx)*frequency,mul(rotation,dy)*frequency,axis,scale);
                // Rotate the sampled normal back into the projection's world basis.
                half2 bump=axis==0?l.perturbation.zy:axis==1?l.perturbation.xz:l.perturbation.xy;
                bump=mul(bump,rotation);
                l.perturbation=axis==0?half3(0,bump.y,bump.x):axis==1?half3(bump.x,0,bump.y):half3(bump.x,bump.y,0);
                return l;
            }
            Layer SampleCliff(TEXTURE2D_PARAM(a,s),TEXTURE2D_PARAM(n,ns),TEXTURE2D_PARAM(m,ms),
                float2 uv,float2 dx,float2 dy,int axis,half scale,half steep)
            {
                // Ground retains exactly the original sampling. On cliffs, two offset
                // samples share albedo/normal/mask coordinates, with explicit mip gradients.
                // Continuous noise selects the pair; at integer crossings B becomes A.
                if(steep<=0)
                    return SampleProjection(TEXTURE2D_ARGS(a,s),TEXTURE2D_ARGS(n,ns),TEXTURE2D_ARGS(m,ms),uv,dx,dy,axis,scale);
                float2 cliffUV=uv*_CliffScale;
                float selection=RockNoise(cliffUV*.43)*8;
                float index=floor(selection);
                float2 offsetA=RockHash(float2(index,17.3)),offsetB=RockHash(float2(index+1,17.3));
                Layer first=SampleCliffPatch(TEXTURE2D_ARGS(a,s),TEXTURE2D_ARGS(n,ns),TEXTURE2D_ARGS(m,ms),uv,dx,dy,axis,scale,offsetA);
                Layer second=SampleCliffPatch(TEXTURE2D_ARGS(a,s),TEXTURE2D_ARGS(n,ns),TEXTURE2D_ARGS(m,ms),uv,dx,dy,axis,scale,offsetB);
                half contrast=dot(first.albedo-second.albedo,half3(.2126,.7152,.0722));
                float fraction=frac(selection);
                Layer result=BlendRock(first,second,smoothstep(.15,.85,fraction+contrast*fraction*(1-fraction)));
                if(steep<1)
                {
                    Layer ground=SampleProjection(TEXTURE2D_ARGS(a,s),TEXTURE2D_ARGS(n,ns),TEXTURE2D_ARGS(m,ms),uv,dx,dy,axis,scale);
                    result=BlendRock(ground,result,steep);
                }
                return result;
            }
            Layer ProjectLayer(TEXTURE2D_PARAM(a,s),TEXTURE2D_PARAM(n,ns),TEXTURE2D_PARAM(m,ms),
                float3 p,float3 dx,float3 dy,float4 st,half3 axes,half scale,half steep)
            {
                float2 tiling=st.xy/_TerrainWorldSize.xy;
                Layer result=(Layer)0;
                // Ignore negligible axes. Flat ground uses one projection; most rock
                // faces use one or two. Gradients remain valid across the branches.
                if(axes.x>0)
                {
                    Layer q=SampleCliff(TEXTURE2D_ARGS(a,s),TEXTURE2D_ARGS(n,ns),TEXTURE2D_ARGS(m,ms),
                        p.zy*tiling+st.zw,dx.zy*tiling,dy.zy*tiling,0,scale,steep);
                    result.albedo+=q.albedo*axes.x;
                    result.perturbation+=q.perturbation*axes.x; result.mask+=q.mask*axes.x;
                }
                if(axes.y>0)
                {
                    Layer q=SampleCliff(TEXTURE2D_ARGS(a,s),TEXTURE2D_ARGS(n,ns),TEXTURE2D_ARGS(m,ms),
                        p.xz*tiling+st.zw,dx.xz*tiling,dy.xz*tiling,1,scale,steep);
                    result.albedo+=q.albedo*axes.y;
                    result.perturbation+=q.perturbation*axes.y; result.mask+=q.mask*axes.y;
                }
                if(axes.z>0)
                {
                    Layer q=SampleCliff(TEXTURE2D_ARGS(a,s),TEXTURE2D_ARGS(n,ns),TEXTURE2D_ARGS(m,ms),
                        p.xy*tiling+st.zw,dx.xy*tiling,dy.xy*tiling,2,scale,steep);
                    result.albedo+=q.albedo*axes.z;
                    result.perturbation+=q.perturbation*axes.z; result.mask+=q.mask*axes.z;
                }
                return result;
            }
            #include "Surfaces/BikeRouteGround.hlsl"
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                #ifdef _ALPHATEST_ON
                ClipHoles(i.uv);
                #endif
                half3 normal=normalize(i.normalWS);
                half4 weights=SAMPLE_TEXTURE2D(_Control,sampler_Control,(i.uv*(_Control_TexelSize.zw-1)+.5)*_Control_TexelSize.xy);
                // Leaves/soil collect on shelves and the floor, not stretched up a cliff.
                half steep=1-smoothstep(.35h,.78h,normal.y);
                half soil=weights.z+weights.w;
                weights.xy+=soil*steep*half2(.58h,.36h); weights.zw*=1-steep*.94h;
                weights=max(0,weights-.004h);weights/=max(.001h,dot(weights,half4(1,1,1,1)));
                half3 axes=half3(0,1,0);
                #ifdef _CLIFF_PROJECTION
                axes=pow(abs(normal),8);axes/=max(.001h,dot(axes,half3(1,1,1)));
                axes=max(0,axes-.025h);axes/=max(.001h,dot(axes,half3(1,1,1)));
                #endif
                float3 dx=ddx(i.positionWS),dy=ddy(i.positionWS);
                // Only shallow ground changes. All accepted cliff pixels (steep > 0)
                // retain the original projection, layers, minerals and normal response.
                half groundBlend=_GroundStrength*smoothstep(.80h,.94h,normal.y);
                Layer blended=(Layer)0;
                #define MIX_LAYER(N,WEIGHT) if(WEIGHT>0) { \
                    Layer l=ProjectLayer(TEXTURE2D_ARGS(_Splat##N,sampler_Splat0), \
                        TEXTURE2D_ARGS(_Normal##N,sampler_Normal0),TEXTURE2D_ARGS(_Mask##N,sampler_Mask0), \
                        i.positionWS,dx,dy,_Splat##N##_ST,axes,_NormalScale##N,steep); \
                    blended.albedo+=l.albedo*_DiffuseRemapScale##N.rgb*WEIGHT; \
                    blended.perturbation+=l.perturbation*WEIGHT; blended.mask+=l.mask*WEIGHT; }
                if(groundBlend<1)
                {
                    MIX_LAYER(0,weights.x) MIX_LAYER(1,weights.y) MIX_LAYER(2,weights.z) MIX_LAYER(3,weights.w)
                }
                half luminance=dot(blended.albedo,half3(.2126,.7152,.0722));
                half strata=1+steep*.055h*sin(i.positionWS.y*.82+sin(i.positionWS.x*.031+i.positionWS.z*.019)*1.3);
                half variation=.96h+.04h*sin(i.positionWS.x*.043+sin(i.positionWS.z*.029));
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=lerp(luminance.xxx,blended.albedo,.76h)*_BaseColor.rgb*strata*variation;
                surface.metallic=0;surface.smoothness=blended.mask.a*.48h;
                surface.occlusion=lerp(1,max(.45h,blended.mask.g),.75h);surface.alpha=1;surface.normalTS=half3(0,0,1);
                // Broad mineral beds follow world height, bent by slow geological drift.
                // The existing painted quarry/dark/earth regions remain authoritative;
                // these low-contrast deposits break up faces within each region.
                if(steep>0)
                {
                    float macro=RockNoise(i.positionWS.xz*.024);
                    float drift=RockNoise(i.positionWS.xz*.009+31)*7;
                    float beds=RockNoise(float2(i.positionWS.y*.22+drift,(i.positionWS.x+i.positionWS.z)*.012));
                    half mineral=smoothstep(.28,.76,beds*.7+macro*.3);
                    half3 deposits=lerp(half3(.77,.76,.74),half3(1.48,1.39,1.25),mineral);
                    half weathering=lerp(.87,1.10,macro);
                    surface.albedo*=lerp(half3(1,1,1),deposits*weathering,steep*_CliffVariation);
                    // Exposed faces weather lighter than the same rock on the ground.
                    surface.albedo*=lerp(1,1.22+weights.y*.6,steep);
                    surface.smoothness*=lerp(1,lerp(.7,1.12,mineral),steep*_CliffVariation);
                }
                InputData input=(InputData)0;input.positionWS=i.positionWS;input.positionCS=i.positionCS;
                half3 bump=blended.perturbation;
                // Keep the projected slope perturbation tangent to the actual cliff face.
                bump-=normal*dot(bump,normal)*steep;
                input.normalWS=normalize(normal+bump*lerp(.65h,_CliffNormalStrength,steep));
                if(groundBlend>0)
                    ApplyGround(i.positionWS,dx,dy,normal,soil,groundBlend,surface,input.normalWS);
                input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                input.bakedGI=SampleSH(input.normalWS);input.shadowMask=half4(1,1,1,1);
                input.vertexLighting=VertexLighting(i.positionWS,input.normalWS);
                input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                half4 color=UniversalFragmentPBR(input,surface);
                color.rgb=MixFog(color.rgb,InitializeInputDataFog(float4(i.positionWS,1),i.fog));return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Terrain/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Terrain/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Terrain/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Terrain/Lit/SceneSelectionPass"
    }
    Dependency "BaseMapShader" = "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)"
    Dependency "BaseMapGenShader" = "Hidden/Universal Render Pipeline/Terrain/Lit (Basemap Gen)"
    Fallback "Universal Render Pipeline/Terrain/Lit"
}
