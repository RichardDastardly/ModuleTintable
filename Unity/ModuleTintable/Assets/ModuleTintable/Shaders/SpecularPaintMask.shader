Shader "DLTD/SpecularPaintMask" {
	Properties{
		[Header(Temporary Colour pickers)]
		_Color1("Color1", Color) = (1,1,1,1)
		_Color2("Color2", Color) = (1,1,1,1)
		_Color3("Color3", Color) = (1,1,1,1)
		_Color4("Color4", Color) = (1,1,1,1)
		[Space]
		[Header(Maps)]
		_MainTex("Overlay Greyscale 3 channel, spec A", 2D) = "white" {} // AO texture, use MainTex for compatibility
		_PaintMask("Paint Mask Greyscale 3 channel/RGB, blendmask A", 2D) = "white" {}
		[Space]

		[Header(Temporary Selectors)]
		_PaintMaskSelector("Paint Mask channel", Color ) = (1,0,0,0)
		_OverlaySelector("Overlay channel", Color) = (0,0,0,0)
			 
		[Space]
		[Header(Specular)]
		_SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
		_OverlaySpec("Blend Overlay level with Spec", Range(0,1)) = 0.1
		_Shininess("Phong tightness", Range(0.03, 1)) = 0.078125

		_RimFalloff("_RimFalloff", Range(0.01,5)) = 0.1
		_RimColor("_RimColor", Color) = (0,0,0,0)
		[HideInInspector]_TemperatureColor("_TemperatureColor", Color) = (0,0,0,0)
		[HideInInspector]_BurnColor("_Burn Color", Color) = (1,1,1,1)
	}
		SubShader{
			Tags { "RenderType" = "Opaque" }
			ZWrite On
				ZTest LEqual
				Blend SrcAlpha OneMinusSrcAlpha

				CGPROGRAM

	#pragma surface surf NormalizedBlinnPhong keepalpha
	#pragma target 3.0


			struct Input {
				float2 uv_MainTex;
				float3 viewDir;
			};

			half _Glossiness;
			half _Metallic;
			fixed4 _Color1;
			fixed4 _Color2;
			fixed4 _Color3;
			fixed4 _Color4;
			fixed4 Colour[17];
			fixed usableColours = 4;
			half _Shininess;

			sampler2D _MainTex;
			sampler2D _PaintMask;

			float _Opacity;
			float _RimFalloff;
			float4 _RimColor;
			float4 _TemperatureColor;
			float4 _BurnColor;
			float4 _OverlaySelector;
			float4 _PaintMaskSelector;
			float _OverlaySpec;
			float _OverlayChan;

			float3 overlayMasked;
			float4 paintMasked;

			float paintLower;
			float paintSelected;
			float3 paintFinal;

			float3 mult;
			float3 screen;

#include "Tint.cginc"

			void surf(Input IN, inout SurfaceOutput o)
			{
				float4 overlay = tex2D(_MainTex, (IN.uv_MainTex));
				float4 paint = tex2D(_PaintMask, (IN.uv_MainTex));
				float3 normal = float3(0, 0, 1);


				half rim = 1.0 - saturate(dot(normalize(IN.viewDir), normal));
				float3 emission = (_RimColor.rgb * pow(rim, _RimFalloff)) * _RimColor.a;
				emission += _TemperatureColor.rgb * _TemperatureColor.a;

				// temporary - will be written in from C#, this is just to make the inspector usable
				usableColours = 4;
				Colour[0] = float4(0.5, 0.5, 0.5, 1.0);
				Colour[1] = _Color1;
				Colour[2] = _Color2;
				Colour[3] = _Color3;
				Colour[4] = _Color4;

				// Select paint mask channel
				// _PaintMaskSelector contains a RGB value corresponding to how much of each channel of the paint mask to blend
				paintMasked = paint * _PaintMaskSelector;

				// somehow despite using a singular value as a selector here, the shader is blending channels
				// it's actually useful but I'm not sure *why* it's doing it at the moment - I suspect it's in the paint selector swizzle
				// paintSelected is the index to Colours[] - it's the 0-1 8 bit greyscale value from the selected channel
				// multiplied by the number of used elements of Colours[]
				paintSelected = max(max(paintMasked.r, paintMasked.b), paintMasked.g) * usableColours;

				// upper value is only used once, but cache lower
				// lerping between lower and upper will avoid jagged boundaries between paint areas ( also gradients )
				paintLower = floor(paintSelected);

				// Alpha channel of the paint mask is a blending value between absolute RGB value of the map and the blended
				// colours picked up from Colours[]
				// this lets the shader pass through common pre-painted parts like labels which shouldn't be recoloured ever, and 
				// also parts of the map which simply aren't to be painted on.
				// The Overlay map will still be blended over this
				paintFinal = lerp( paint.rgb, lerp(Colour[paintLower], Colour[ceil(paintSelected)], paintSelected - paintLower), paint.a );


				// _OverlaySelector is again a RGB value to pick up the right channel
				// the mechanism works exactly the same
				overlayMasked = overlay * _OverlaySelector;
				overlayMasked.rgb = max(max(overlayMasked.r, overlayMasked.g), overlayMasked.b);

				// premultiplied output for Overlay blending - pick one of multiply if Overlay lumi is < 0.5, otherwise screen
				mult = 2 * overlayMasked.rgb * paintFinal.rgb;
				screen = 1 - 2 * (1 - overlayMasked.rgb)*(1 - paintFinal.rgb);
				o.Albedo = lerp(mult, screen, round(overlayMasked.rgb)) * _BurnColor;
		
				o.Emission = emission;

				// option to multiply the specular map with the Overlay map to turn down gloss in darker areas
				// independent of paint colour
				o.Gloss = lerp(overlay.a, 2 * overlayMasked.r * overlay.a, _OverlaySpec);
				o.Specular = _Shininess;
				o.Normal = normal;

				o.Emission *= _Opacity;
				o.Alpha = 1;
			}
			ENDCG
		}
			FallBack "Diffuse"
}
