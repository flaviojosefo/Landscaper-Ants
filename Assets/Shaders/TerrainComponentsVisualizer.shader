Shader "Custom/TerrainComponentsVisualizer" 
{
	Properties
	{
		// used in fallback on old cards & base map
		[HideInInspector] _MainTex("BaseMap (RGB)", 2D) = "white" {}
		[HideInInspector] _Color("Main Color", Color) = (1,1,1,1)
		[HideInInspector] _TerrainHolesTexture("Holes Map (RGB)", 2D) = "white" {}

		[HideInInspector] _PheromoneTex("Pheromone Texture", 2D) = "white" {}
	}

	SubShader
	{
		Tags 
		{
			"Queue" = "Geometry-100"
			"RenderType" = "Opaque"
			"TerrainCompatible" = "True"
		}

		CGPROGRAM
		#pragma surface surf Standard vertex:SplatmapVert finalcolor:SplatmapFinalColor finalgbuffer:SplatmapFinalGBuffer addshadow fullforwardshadows
		#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd
		#pragma multi_compile_fog // needed because finalcolor oppresses fog code generation.
		#pragma target 3.0
		#include "UnityPBSLighting.cginc"
		
		#pragma multi_compile_local __ _ALPHATEST_ON
		#pragma multi_compile_local __ _NORMALMAP
		
		#define TERRAIN_STANDARD_SHADER
		#define TERRAIN_INSTANCED_PERPIXEL_NORMAL
		#define TERRAIN_SURFACE_OUTPUT SurfaceOutputStandard
		#include "TerrainSplatmapCommon.cginc"

		sampler2D _PheromoneTex;
		half4 _PheromoneTex_TexelSize;

		struct PhInput
		{
			float2 uv_PheromoneTex;
		};

		#define CREATE_PHINPUT PhInput phIN; \
		    phIN.uv_PheromoneTex = (IN.tc.xy * (_PheromoneTex_TexelSize.zw - 1.0f) + 0.5f) * _PheromoneTex_TexelSize.xy

		half _Metallic0;
		half _Metallic1;
		half _Metallic2;
		half _Metallic3;

		half _Smoothness0;
		half _Smoothness1;
		half _Smoothness2;
		half _Smoothness3;

		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			half4 splat_control;
			half weight;
			fixed4 mixedDiffuse;
			half4 defaultSmoothness = half4(_Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3);
			SplatmapMix(IN, defaultSmoothness, splat_control, weight, mixedDiffuse, o.Normal);
			CREATE_PHINPUT;
			o.Albedo = tex2D(_PheromoneTex, phIN.uv_PheromoneTex) * mixedDiffuse.rgb;
			o.Alpha = weight;
			o.Smoothness = mixedDiffuse.a;
			o.Metallic = dot(splat_control, half4(_Metallic0, _Metallic1, _Metallic2, _Metallic3));
		}
		ENDCG

		UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
		UsePass "Hidden/Nature/Terrain/Utilities/SELECTION"
	}

	Dependency "AddPassShader" = "Hidden/TerrainEngine/Splatmap/Standard-AddPass"
	Dependency "BaseMapShader" = "Hidden/TerrainEngine/Splatmap/Standard-Base"
	Dependency "BaseMapGenShader" = "Hidden/TerrainEngine/Splatmap/Standard-BaseGen"

	Fallback "Nature/Terrain/Standard"
}
