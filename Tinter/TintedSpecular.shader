Shader "KSP/Tinted Specular"
{
	Properties 
	{
		_MainTex("_MainTex (RGB spec(A))", 2D) = "white" {}
		_Color ("Main Color", Color) = (1,1,1,1)
		_SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
		_Shininess ("Shininess", Range (0.03, 1)) = 0.078125
		
		_Opacity("_Opacity", Range(0,1) ) = 1
		_RimFalloff("_RimFalloff", Range(0.01,5) ) = 0.1
		_RimColor("_RimColor", Color) = (0,0,0,0)

		_TemperatureColor("_TemperatureColor", Color) = (0,0,0,0)
		_BurnColor ("Burn Color", Color) = (1,1,1,1)

		_TintHue("_Tint Hue", Range(0,1)) = 0
		_TintSat("_Tint Saturation", Range(0,1)) = 0
		_TintVal("_Tint Value", Range(0,1)) = 0

		//_TintRGB("_Tint RGB", Color ) = (0,0,0,1)
		_TintPoint("_Tint Value Midpoint", Range(0,1)) = 0
		_TintBand("_Tint Band Width", Range(0,1)) = 0
		_TintFalloff("_Tint Falloff", Range(0,1)) = 0
		_TintSatThreshold("_Tint Saturation Threshold", Range(0,1)) = 0.1
	}
	
	SubShader 
	{
		Tags { "RenderType"="Opaque" }
		ZWrite On
		ZTest LEqual
//		Blend SrcAlpha OneMinusSrcAlpha 
		Blend Off

		CGPROGRAM

#pragma enable_d3d11_debug_symbols
		#pragma surface surf BlinnPhong
		#pragma target 3.0
		
		half _Shininess;

		sampler2D _MainTex;

		float _Opacity;
		float _RimFalloff;
		float4 _RimColor;
		float4 _TemperatureColor;
		float4 _BurnColor;

		float _TintHue;
		float _TintSat;
		float _TintVal;
		float _TintPoint;
		float _TintBand;
		float _TintFalloff;
		float _TintSatThreshold;
		float4 _TintRGB;
		
		struct Input
		{
			float2 uv_MainTex;
			float3 viewDir;
		};

		float Epsilon = 1e-10;

		float3 RGBtoHCV(in float3 RGB)
		{
			// Based on work by Sam Hocevar and Emil Persson
			float4 P = (RGB.g < RGB.b) ? float4(RGB.bg, -1.0, 2.0 / 3.0) : float4(RGB.gb, 0.0, -1.0 / 3.0);
			float4 Q = (RGB.r < P.x) ? float4(P.xyw, RGB.r) : float4(RGB.r, P.yzx);
			float C = Q.x - min(Q.w, Q.y);
			float H = abs((Q.w - Q.y) / (6 * C + Epsilon) + Q.z);
			return float3(H, C, Q.x);
		}

		float3 HUEtoRGB(in float H)
		{
			float R = abs(H * 6 - 3) - 1;
			float G = 2 - abs(H * 6 - 2);
			float B = 2 - abs(H * 6 - 4);
			return saturate(float3(R, G, B));
		}

		float3 HSVtoRGB(in float3 HSV)
		{
			float3 RGB = HUEtoRGB(HSV.x);
			return ((RGB - 1) * HSV.y + 1) * HSV.z;
		}

		float3 RGBtoHSV(in float3 RGB)
		{
			float3 HCV = RGBtoHCV(RGB);
			float S = HCV.y / (HCV.z + Epsilon);
			return float3(HCV.x, S, HCV.z);
		}

		void surf (Input IN, inout SurfaceOutput o)
		{
			float4 color = tex2D(_MainTex, (IN.uv_MainTex));// *_BurnColor;
			float3 normal = float3(0,0,1);

			half rim = 1.0 - saturate(dot (normalize(IN.viewDir), normal));

			float3 emission = (_RimColor.rgb * pow(rim, _RimFalloff)) * _RimColor.a;
			emission += _TemperatureColor.rgb * _TemperatureColor.a;

			float3 asHSV = RGBtoHSV(color.rgb);

			// move some saturation window calc to code
			float saturationFalloff = saturate(_TintSatThreshold * 0.75);
			float saturationWindow = saturate(_TintSatThreshold - saturationFalloff);
			float tintBlend = (1 - (saturate(abs(asHSV.z - _TintPoint) - _TintBand) / _TintFalloff)) *
				( 1- saturate((asHSV.y - saturationFalloff )/ saturationWindow )); // two divisions? do some better maths

	//		float nHue = (asHSV.x * _TintHue);
	//		asHSV.x = lerp(asHSV.x, nHue, tintBlend);
	//		// interpolate saturation
	//		asHSV.y = lerp(asHSV.y, _TintSat, tintBlend);
			// multiply value
	//		float nVal = (asHSV.z * _TintVal);
	//		asHSV.z = lerp(asHSV.z, nVal, tintBlend);

	//		color.rgb = HSVtoRGB(asHSV) * _BurnColor;
			color.rgb = lerp(color.rgb, color.rgb * HSVtoRGB(float3(_TintHue, _TintSat, _TintVal)), saturate(tintBlend)) *_BurnColor;
//			color.rgb = lerp(color.rgb, color.rgb* _TintRGB.rgb, saturate(tintBlend));
	//		color.rgb *= _TintRGB.rgb;

			o.Albedo = color.rgb;
			o.Emission = emission;
			o.Gloss = color.a;
			o.Specular = _Shininess;
			o.Normal = normal;

			o.Emission *= _Opacity;
			o.Alpha = _Opacity;
		}
		ENDCG
	}
	Fallback "Specular"
}