Shader "KSP/Tinted Specular Masked"
{
	Properties 
	{
		_MainTex("_MainTex (RGB spec(A))", 2D) = "white" {}
		_BlendMask("_BlendMask (RGBA)", 2D) = "white" {}
		_Colour ("Main Colour", Color) = (1,1,1,1)
		_SecondColour("Secondary Colour", Color ) = (1,1,1,1)
		_SpecColour ("Specular Colour", Color) = (0.5, 0.5, 0.5, 1)
		_Shininess ("Shininess", Range (0.03, 1)) = 0.078125
		
		_Opacity("_Opacity", Range(0,1) ) = 1
		_RimFalloff("_RimFalloff", Range(0.01,5) ) = 0.1
		_RimColour("_RimColour", Color) = (0,0,0,0)

		_TemperatureColour("_TemperatureColour", Color) = (0,0,0,0)
		_BurnColour ("_Burn Colour", Color) = (1,1,1,1)

//#include "TintProperties.cginc"

	}
	
	SubShader 
	{
		Tags { "RenderType"="Opaque" }
		ZWrite On
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha 

		CGPROGRAM

		#pragma surface surf NormalizedBlinnPhong keepalpha
		#pragma target 3.0
		
		half _Shininess;

		sampler2D _MainTex;
		sampler2D _BlendMask;

		float _Opacity;
		float _RimFalloff;
		float4 _RimColour;
		float4 _TemperatureColour;
		float4 _BurnColour;

#include "Tint.cginc"
		
		struct Input
		{
			float2 uv_MainTex;
			float3 viewDir;
		};

		void surf (Input IN, inout SurfaceOutput o)
		{
			float4 color = tex2D(_MainTex, (IN.uv_MainTex));// *_BurnColour;
			float4 blend = tex2D(_BlendMask, (IN.uv_MainTex));
			float3 normal = float3(0,0,1);

			half rim = 1.0 - saturate(dot (normalize(IN.viewDir), normal));

			float3 emission = (_RimColour.rgb * pow(rim, _RimFalloff)) * _RimColour.a;
			emission += _TemperatureColour.rgb * _TemperatureColour.a;

			color.rgb = lerp(color.rgb, color.rgb * HSVtoRGB(float3(_TintHue, _TintSat, _TintVal)), blend.a ) *_BurnColour;

			o.Albedo = color.rgb;
			o.Emission = emission;
			o.Gloss = color.a *_GlossMult;
			o.Specular = _Shininess;
			o.Normal = normal;

			o.Emission *= _Opacity;
			o.Alpha = _Opacity;
		}
		ENDCG
	}
	Fallback "Specular"
}