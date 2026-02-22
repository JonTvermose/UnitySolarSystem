Shader "Custom/AccretionDisk"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BHScreenPos ("BH Screen Pos (UV)", Vector) = (0.5,0.5,0,0)
        _ScreenRadius ("Screen Radius", Float) = 0.1
        _InnerRadius ("Inner Radius", Float) = 0.5
        _OuterRadius ("Outer Radius", Float) = 2.0
        _BaseColor ("Base Color", Color) = (1, 0.6, 0.1, 1)
        _Emission ("Emission", Float) = 1.5
        _SpinAngle ("Spin Angle (deg)", Float) = 0.0
        _Time2 ("Time", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float4 _BHScreenPos;
            float _ScreenRadius;
            float _InnerRadius;
            float _OuterRadius;
            float4 _BaseColor;
            float _Emission;
            float _SpinAngle;
            float _Time2;

            // ---- Procedural noise helpers ----
            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i + float2(0,0));
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Fractal Brownian Motion — 5 octaves
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                for (int i = 0; i < 5; i++)
                {
                    value += amplitude * valueNoise(p * frequency);
                    frequency *= 2.17;
                    amplitude *= 0.48;
                }
                return value;
            }

            // Blackbody-inspired color ramp (temperature 0..1 -> color)
            float3 temperatureColor(float t)
            {
                float3 cold   = float3(0.6, 0.08, 0.02);   // deep red
                float3 warm   = float3(1.0, 0.35, 0.05);    // orange
                float3 hot    = float3(1.0, 0.85, 0.55);    // yellow-white
                float3 vhot   = float3(0.75, 0.85, 1.0);    // blue-white

                float3 col = cold;
                col = lerp(col, warm,  smoothstep(0.0, 0.3, t));
                col = lerp(col, hot,   smoothstep(0.3, 0.65, t));
                col = lerp(col, vhot,  smoothstep(0.65, 1.0, t));
                return col;
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 bhUV = _BHScreenPos.xy;

                // Aspect-corrected vector from pixel to BH center
                float aspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
                float2 diff = i.uv - bhUV;
                diff.x *= aspect;
                float dist = length(diff);

                // Use ScreenRadius to define inner/outer bands
                float innerR = _ScreenRadius * 0.35;
                float outerR = _ScreenRadius * 2.5;
                float glowR  = _ScreenRadius * 3.5;

                // Early-out: outside glow range
                if (dist > glowR)
                {
                    return tex2D(_MainTex, i.uv);
                }

                float angle = atan2(diff.y, diff.x);
                float spinRad = _SpinAngle * 0.01745329;

                // Radial parameter: 0 at inner edge, 1 at outer edge
                float ringT = saturate((dist - innerR) / max(outerR - innerR, 0.0001));

                // ---- Turbulent noise in log-polar space ----
                float logR = log(max(dist, 0.0001));
                float2 noiseCoord = float2(angle * 1.5 + spinRad * 0.8, logR * 6.0 - _Time2 * 0.15);
                float turb = fbm(noiseCoord * 3.0);

                // Large-scale spiral structure
                float spiralAngle = angle + spinRad + ringT * 8.0 + turb * 1.2;
                float spiral = sin(spiralAngle * 2.0) * 0.5 + 0.5;
                spiral = pow(spiral, 0.6);

                // Fine-detail turbulence
                float2 fineCoord = float2(angle * 4.0 + spinRad * 1.5, logR * 12.0 - _Time2 * 0.3);
                float fine = fbm(fineCoord * 5.0);

                // Combine patterns
                float pattern = lerp(0.5, 1.0, spiral);
                pattern *= lerp(0.6, 1.0, turb);
                pattern *= lerp(0.8, 1.0, fine);

                // ---- Doppler beaming ----
                float doppler = 1.0 + 0.45 * sin(angle + spinRad);
                doppler = pow(max(doppler, 0.0), 3.0);

                // ---- Temperature / brightness ----
                float temperature = pow(1.0 - ringT, 0.75);
                temperature *= pattern;
                temperature *= doppler;

                // Edge fade
                float edgeFade = smoothstep(innerR * 0.8, innerR + (outerR - innerR) * 0.1, dist)
                               * smoothstep(outerR, outerR - (outerR - innerR) * 0.2, dist);

                float brightness = temperature * edgeFade * _Emission;

                // ---- Color from temperature ----
                float3 diskColor = temperatureColor(pow(1.0 - ringT, 0.5));
                diskColor = lerp(diskColor, _BaseColor.rgb, ringT * 0.4);
                diskColor = lerp(diskColor, diskColor * float3(0.8, 0.9, 1.3), saturate(doppler - 1.0) * 0.5);

                // ---- Soft outer glow ----
                float glow = 0.0;
                if (dist > outerR && dist < glowR)
                {
                    float glowT = (dist - outerR) / (glowR - outerR);
                    glow = (1.0 - glowT) * (1.0 - glowT) * 0.15 * _Emission;
                    glow *= doppler * 0.5;
                }

                // ---- Photon ring ----
                float photonRing = 0.0;
                if (dist < innerR && dist > innerR * 0.5)
                {
                    float prT = (dist - innerR * 0.5) / (innerR * 0.5);
                    photonRing = pow(prT, 2.0) * 2.0 * _Emission;
                }

                // ---- Composite ----
                fixed4 original = tex2D(_MainTex, i.uv);
                fixed4 result = original;

                result.rgb += diskColor * brightness * 0.7;
                result.rgb += float3(1.0, 0.4, 0.08) * glow;
                result.rgb += float3(0.9, 0.92, 1.0) * photonRing;

                return result;
            }
            ENDCG
        }
    }
    FallBack Off
}