Shader "Custom/CharacterShader"
{
    Properties
    {
        // 1. Base Texture
        _MainTex ("Base Texture", 2D) = "white" { }

        // 2. Alpha Cutoff (Default to 0.5)
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        // 3. Ambient Color
        _AmbientColor ("Ambient Color", Color) = (1,1,1,1)

        // 4. Hue (Default to 0)
        _Hue ("Hue", Range(0, 1)) = 0.0

        // 5. Saturation (Default to 1)
        _Saturation ("Saturation", Range(0, 2)) = 1.0

        // 6. Brightness (Default to 1)
        _Brightness ("Brightness", Range(0, 2)) = 1.0

        // 7. Shading Threshold (Range from 0 to 1)
        _Threshold ("Shading Threshold", Range(0, 1)) = 0.5

        // 8. Shadow Smoothness (Range from 0 to 1)
        _Smoothness ("Shadow Smoothness", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            // Disable backface culling
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // Material properties
            sampler2D _MainTex;
            float4 _AmbientColor;
            float _Hue;
            float _Saturation;
            float _Brightness;
            float _Threshold;
            float _Smoothness;
            float _AlphaCutoff;

            // Function to convert RGB to HSV
            float4 RGBToHSV(float4 rgb)
            {
                float cMax = max(rgb.r, max(rgb.g, rgb.b));
                float cMin = min(rgb.r, min(rgb.g, rgb.b));
                float delta = cMax - cMin;

                float h = 0.0;
                if (delta != 0.0)
                {
                    if (cMax == rgb.r)
                        h = (rgb.g - rgb.b) / delta;
                    else if (cMax == rgb.g)
                        h = (rgb.b - rgb.r) / delta + 2.0;
                    else
                        h = (rgb.r - rgb.g) / delta + 4.0;
                }

                float s = (cMax == 0.0) ? 0.0 : (delta / cMax);
                float v = cMax;

                h /= 6.0; // Normalize hue to [0, 1]
                if (h < 0.0) h += 1.0;

                return float4(h, s, v, 1.0);
            }

            // Function to convert HSV to RGB
            float3 HSVToRGB(float4 hsv)
            {
                float3 rgb;
                float h = hsv.x;
                float s = hsv.y;
                float v = hsv.z;

                int i = int(h * 6.0);
                float f = h * 6.0 - i;
                float p = v * (1.0 - s);
                float q = v * (1.0 - f * s);
                float t = v * (1.0 - (1.0 - f) * s);

                if (i == 0) rgb = float3(v, t, p);
                else if (i == 1) rgb = float3(q, v, p);
                else if (i == 2) rgb = float3(p, v, t);
                else if (i == 3) rgb = float3(p, q, v);
                else if (i == 4) rgb = float3(t, p, v);
                else rgb = float3(v, p, q);

                return rgb;
            }

            // Function to adjust hue, saturation, and brightness
            float3 AdjustHSB(float3 color, float hue, float saturation, float brightness)
            {
                // Convert to HSV
                float4 hsv = RGBToHSV(float4(color, 1.0));

                // Adjust the hue by adding a value to it
                hsv.x += hue; // Wrap hue to stay within [0, 1]
                if (hsv.x > 1.0)
                    hsv.x -= 1.0;
                if (hsv.x < 0.0)
                    hsv.x += 1.0;

                // Adjust the saturation
                hsv.y *= saturation;
                if (hsv.y > 1.0) hsv.y = 1.0;

                // Adjust the brightness (value)
                hsv.z *= brightness;
                if (hsv.z > 1.0) hsv.z = 1.0;

                // Convert back to RGB
                return HSVToRGB(hsv);
            }

            // Vertex Shader
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                o.uv = v.uv;
                return o;
            }

            // Fragment Shader
            half4 frag(v2f i) : SV_Target
            {
                // Sample the base texture (albedo)
                half4 texColor = tex2D(_MainTex, i.uv);

                // Apply hue, saturation, and brightness adjustments
                texColor.rgb = AdjustHSB(texColor.rgb, _Hue, _Saturation, _Brightness);

                // Handle transparency (cutout)
                if (texColor.a < _AlphaCutoff)
                    discard; // Discards the fragment if alpha is below threshold

                // Final color, considering the ambient color
                float4 finalColor = texColor * _AmbientColor;

                // Calculate lighting (directional light)
                float3 normal = normalize(i.normal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz); // Directional light direction
                float diff = max(0.0, dot(normal, lightDir));

                // Apply shadow smoothness and shading threshold
                diff = smoothstep(_Threshold - _Smoothness, _Threshold + _Smoothness, diff);

                // Final lighting and shading
                finalColor.rgb += diff * finalColor.rgb;

                // Final color output
                return finalColor;
            }

            ENDCG
        }
    }

    Fallback "Diffuse"
}