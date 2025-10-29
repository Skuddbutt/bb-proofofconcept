Shader "Custom/EnhancedSurfaceShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        
        // New features from the second shader
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _AmbientColor ("Ambient Color", Color) = (1,1,1,1)
        _Hue ("Hue", Range(0, 1)) = 0.0
        _Saturation ("Saturation", Range(0, 1)) = 1.0
        _Brightness ("Brightness", Range(0, 1)) = 1.0
        _GlowEnabled ("Enable Glow", Float) = 0
        _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 0.0
        _Emission ("Emission", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off

        CGPROGRAM
        // Standard surface shader with enhanced customization
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        // New properties
        half _AlphaCutoff;
        fixed4 _AmbientColor;
        half _Hue;
        half _Saturation;
        half _Brightness;
        float _GlowEnabled;
        fixed4 _GlowColor;
        half _GlowIntensity;
        sampler2D _Emission;

        struct Input
        {
            float2 uv_MainTex;
        };

        // Function to apply hue, saturation, and brightness adjustments
        half3 AdjustHSB(half3 color, half hue, half saturation, half brightness)
        {
            // Adjust brightness
            color *= brightness;

            // Convert color to grayscale using luminance (luminosity)
            half gray = dot(color, half3(0.299, 0.587, 0.114)); // Luminance calculation
            half3 grayscale = half3(gray, gray, gray);

            // Interpolate between grayscale and original color based on saturation
            color = lerp(grayscale, color, saturation);

            // Adjust hue (using a simple hue rotation method)
            half angle = hue * 6.2831853; // 2 * PI
            half3x3 rotMatrix = half3x3(
                cos(angle), sin(angle), 0.0,
                -sin(angle), cos(angle), 0.0,
                0.0, 0.0, 1.0
            );

            color = mul(rotMatrix, color);
            return saturate(color);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Sample the base texture (albedo)
            fixed4 texColor = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            // Apply hue, saturation, brightness adjustments to albedo
            texColor.rgb = AdjustHSB(texColor.rgb, _Hue, _Saturation, _Brightness);

            // Alpha cutoff for transparency
            if (texColor.a < _AlphaCutoff)
                discard; // Discard fragment if alpha is below threshold

            // Apply ambient color to the albedo
            o.Albedo = texColor.rgb * _AmbientColor.rgb;
            o.Alpha = texColor.a;

            // Metallic and smoothness from properties
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;

            // Handle glow effect (if enabled)
            if (_GlowEnabled > 0.5)
            {
                // Add glow color with intensity
                o.Emission = _GlowColor.rgb * _GlowIntensity;
            }
            else
            {
                // If no glow, use emission texture if defined
                o.Emission = tex2D(_Emission, IN.uv_MainTex).rgb;
            }
        }
        ENDCG
    }
    FallBack "Diffuse"
}
