Shader "Environment/BikeRoute Terrain Distance V2"
{
    Properties
    {
        _CanyonColor("Canyon broad color", 2D) = "white" {}
        _CanyonRegion("Canyon normalized X/Z/size", Vector) = (0,.0625,.25,.25)

        _BaseColor("Rock palette", Color) = (.78,.80,.83,1)
        _TerrainWorldSize("Terrain width / depth", Vector) = (2048,2048,0,0)
        [Toggle(_CLIFF_PROJECTION)] _CliffProjection("Project rock onto steep faces", Float) = 1
        _CliffScale("Cliff detail scale", Range(.25,2)) = .7
        _CliffVariation("Cliff mineral variation", Range(0,1)) = .65
        _CliffNormalStrength("Cliff normal strength", Range(0,2)) = 1.25
        [Toggle(_FOREST_MOSS)] _ForestMoss("Localized forest cliff moss", Float) = 0
        [NoScaleOffset] _MossRegion("Showcase coverage / shelter / aspect", 2D) = "black" {}
        [NoScaleOffset] _MossAlbedo("Mossy rock color", 2D) = "grey" {}
        [NoScaleOffset] _MossNormal("Mossy rock relief", 2D) = "bump" {}
        [NoScaleOffset] _MossMask("Mossy rock AO / smoothness", 2D) = "white" {}
        _MossField("Moss region origin / size", Vector) = (0,0,1,1)
        _MossCoverage("Moss patch coverage", Range(0,1)) = .88
        _MossRelief("Additional moss relief", Range(0,2)) = 1.8
        _GroundStrength("Ground treatment", Range(0,1)) = 0
        _GroundNormalStrength("Ground relief strength", Range(0,3)) = 1.8
        [NoScaleOffset] _GroundColors("Ground families: color / AO", 2DArray) = "" {}
        [NoScaleOffset] _GroundRelief("Ground families: normal XY / smoothness", 2DArray) = "" {}
        [NoScaleOffset] _GroundRoadDistance("Baked asphalt distance (32m)", 2D) = "white" {}
        [NoScaleOffset] _GroundCorrectionRegion("Local wash / ridge gravel coverage", 2D) = "black" {}
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
SubShader {
Tags {"Queue"="Geometry-100" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "TerrainCompatible"="True"}
UsePass "Environment/BikeRoute Terrain Medium/ForwardLit"
UsePass "Universal Render Pipeline/Terrain/Lit/ShadowCaster"
UsePass "Universal Render Pipeline/Terrain/Lit/DepthOnly"
UsePass "Universal Render Pipeline/Terrain/Lit/DepthNormals"
}
Dependency "BaseMapShader" = "Hidden/Environment/BikeRoute Terrain Distance Base"
Dependency "BaseMapGenShader" = "Hidden/Environment/BikeRoute Canyon Basemap Gen"
Fallback "Universal Render Pipeline/Terrain/Lit"
}