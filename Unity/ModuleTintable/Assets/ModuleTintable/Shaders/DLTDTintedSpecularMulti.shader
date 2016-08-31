Shader "DLTD/Tinted Specular Multi"
{
	// go through & replace some of these floats with fixed
	Properties 
	{
		_MainTex("_MainTex (RGB spec(A)", 2D) = "white" {}

		[Toggle(BLENDMASK)]_EnableBlend("Blendmask?", Int ) = 0
		_BlendMask("_BlendMask (Greyscale)", 2D) = "white" {}

		[Toggle(BUMPMAP)] _EnableBump("Bump?", Int ) = 0
		[Normal]_BumpMap("_BumpMap", 2D) = "bump" {}

		_Color("Main Colour", Color) = (1,1,1,1)
		_SpecColor("Specular Colour", Color) = (0.5, 0.5, 0.5, 1)
		_Shininess("Shininess", Range(0.03, 1)) = 0.078125
		_GlossMult("Gloss multiplier", Range(0,1)) = 1


		[Toggle(EMISSIVE)] _EnableEmissive("Emissive?", Int ) = 0
		_EmissiveColor("_EmissiveColor", Color) = (0,0,0,1)
		_Emissive("_Emissive", 2D) = "white" {}

		[Toggle(DECAL)] _EnableDecal("Decal?", Int) = 0
		_Decal("_Decal (RGB trans(A))", 2D) = "white" {}

		_Opacity("_Opacity", Range(0,1) ) = 1
		_RimFalloff("_RimFalloff", Range(0.01,5) ) = 0.1
		_RimColour("_RimColour", Color) = (0,0,0,0)

		[HideInInspector]_TemperatureColor("_TemperatureColour", Color) = (0,0,0,0)
		[HideInInspector]_BurnColor ("_Burn Colour", Color) = (1,1,1,1)

	}
	
	SubShader 
	{
		Tags { "RenderType"="Opaque" }
		ZWrite On
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha 

		CGPROGRAM

		#pragma multi_compile __ BLENDMASK 
		#pragma multi_compile __ EMISSIVE BUMPMAP
		#pragma multi_compile __ DECAL

		#pragma surface surf NormalizedBlinnPhong keepalpha
		#pragma target 3.0
		
		half _Shininess;

		sampler2D _MainTex;
		float3 _Color;

#if defined (BLENDMASK)
		sampler2D _BlendMask;
#endif
#if defined (EMISSIVE)
		float4 _EmissiveColor;
		sampler2D _Emissive;
#endif
#if defined (BUMPMAP)
		sampler2D _BumpMap;
#endif

		float _Opacity;
		float _RimFalloff;
		float4 _RimColour;
		float4 _TemperatureColor;
		float4 _BurnColor;

#if defined (DECAL)
		sampler2D _Decal;
		float2 _DecalTL; // top left corner
		float2 _DecalBR; // bottom right corner
		float2 _DecalXY; // scale factor: 1/size relative to main tex
#endif

#include "Tint.cginc"
		
		struct Input
		{
			float2 uv_MainTex;
#if defined (BUMPMAP)
			float2 uv_BumpMap;
#endif
#if defined (EMISSIVE)
			float2 uv_Emissive;
#endif
			float3 viewDir;
		};

		void surf (Input IN, inout SurfaceOutput o)
		{
			float4 color = tex2D(_MainTex, (IN.uv_MainTex)); // *_BurnColour;

#if defined (BUMPMAP)
			float3 normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
#else
			float3 normal = float3(0,0,1);
#endif

#if defined (DECAL)
			float decalBlend = saturate(step(IN.uv_MainTex, _DecalTL) + step(_DecalBR, IN.uv_MainTex) - 1);
			float4 decal = tex2D(_Decal, (IN.uv_MainTex * _DecalXY));
			color.rgb = lerp(color.rgb, decal.rgb, decalBlend * decal.a );
#endif

			half rim = 1.0 - saturate(dot (normalize(IN.viewDir), normal));

			float3 emission = (_RimColour.rgb * pow(rim, _RimFalloff)) * _RimColour.a;
			emission += _TemperatureColor.rgb * _TemperatureColor.a;
#if defined (EMISSIVE)
			emission += (tex2D(_Emissive, IN.uv_Emissive).rgb * _EmissiveColor.rgb) * _EmissiveColor.a;
#endif

#if defined (BLENDMASK)
			float3 blend = tex2D(_BlendMask, (IN.uv_MainTex));
#else
			float blend = BlendFactor(color);
#endif

			// code must write _Color in RGB now
			o.Albedo = lerp(color.rgb, color.rgb * _Color, blend) *_BurnColor;
			o.Emission = emission;
			o.Gloss = color.a *_GlossMult;
			o.Specular = _Shininess;
			o.Normal = normal;

			o.Emission *= _Opacity;
			o.Alpha = _Opacity;
		}
		ENDCG
	}
	Fallback "KSP/Specular"
}