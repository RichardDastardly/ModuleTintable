Shader "DLTD/Tinted Specular Multi"
{
	Properties 
	{
		_MainTex("_MainTex (RGB spec(A)", 2D) = "white" {}
		_Mask("_Mask (Greyscale)", 2D) = "white" {}
		_BumpMap("_BumpMap", 2D) = "bump" {}
		_Colour ("Main Colour", Color) = (1,1,1,1)
		_SpecColour ("Specular Colour", Color) = (0.5, 0.5, 0.5, 1)
		_Shininess ("Shininess", Range (0.03, 1)) = 0.078125
		_EmissiveColor("_EmissiveColor", Color) = (0,0,0,1)
		_Emissive("_Emissive", 2D) = "white" {}

		
		_Opacity("_Opacity", Range(0,1) ) = 1
		_RimFalloff("_RimFalloff", Range(0.01,5) ) = 0.1
		_RimColour("_RimColour", Color) = (0,0,0,0)

			[HideInInspector]_TemperatureColour("_TemperatureColour", Color) = (0,0,0,0)
			[HideInInspector]_BurnColour ("_Burn Colour", Color) = (1,1,1,1)

	}
	
	SubShader 
	{
		Tags { "RenderType"="Opaque" }
		ZWrite On
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha 

		CGPROGRAM

#pragma multi_compile BLEND BLENDMASK 
#pragma multi_compile __ EMISSIVE BUMP

		#pragma surface surf NormalizedBlinnPhong keepalpha
		#pragma target 3.0
		
		half _Shininess;

		sampler2D _MainTex;
		float4 _Colour;

#if defined (BLENDMASK)
		sampler2D _Mask;
#endif
#if defined (EMISSIVE)
		float4 _EmissiveColor;
		sampler2D _Emissive;
#endif
#if defined (BUMP)
		sampler2D _BumpMap;
#endif

		float _Opacity;
		float _RimFalloff;
		float4 _RimColour;
		float4 _TemperatureColour;
		float4 _BurnColour;

#include "Tint.cginc"
		
		struct Input
		{
			float2 uv_MainTex;
#if defined (BUMP)
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
#if defined (BLENDMASK)
			float3 blend = tex2D(_Mask, (IN.uv_MainTex));
#endif
#if defined (BUMP)
			float3 normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
#else
			float3 normal = float3(0,0,1);
#endif

			half rim = 1.0 - saturate(dot (normalize(IN.viewDir), normal));

			float3 emission = (_RimColour.rgb * pow(rim, _RimFalloff)) * _RimColour.a;
			emission += _TemperatureColour.rgb * _TemperatureColour.a;
#if defined (EMISSIVE)
			emission += (tex2D(_Emissive, IN.uv_Emissive).rgb * _EmissiveColor.rgb) * _EmissiveColor.a;
#endif

#if defined (BLEND)
			float blend = BlendFactor(color);
#endif

			// code must write _Colour in RGB now
			o.Albedo = lerp(color.rgb, color.rgb * _Colour, blend) *_BurnColour;
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