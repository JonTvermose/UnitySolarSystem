Shader "Custom/AccretionDisk"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 0.5, 0.1, 1)
        _Emission ("Emission", Float) = 1.5
        _InnerRadius ("Inner Radius", Float) = 0.5
        _OuterRadius ("Outer Radius", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZTest Always
            Cull Off
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _BaseColor;
            float _Emission;
            float _InnerRadius;
            float _OuterRadius;
            float _SpinAngle;         // degrees

            float4 _BHScreenPos;      // xy = screen UV of BH
            float _ScreenRadius;      // outer radius in screen-space

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Pass through the source image first
                fixed4 original = tex2D(_MainTex, i.uv);

                float2 bhUV = _BHScreenPos.xy;

                // Aspect-corrected distance from pixel to BH center
                float aspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
                float2 diff = i.uv - bhUV;
                diff.x *= aspect;
                float dist = length(diff);

                // Screen-space inner/outer radii
                float outerR = max(_ScreenRadius, 0.001);
                float innerR = outerR * (_InnerRadius / max(_OuterRadius, 0.001));

                // Outside the disk? Return original
                if (dist < innerR || dist > outerR)
                {
                    return original;
                }

                // Radial factor: 0 at inner edge, 1 at outer
                float ringT = (dist - innerR) / max(outerR - innerR, 0.0001);

                // Angle for spin
                float angle = atan2(diff.y, diff.x);
                float spinRad = _SpinAngle * 0.01745329; // deg to rad
                angle += spinRad;

                // Procedural spiral pattern
                float pattern = sin(angle * 3.0 + ringT * 12.0) * 0.5 + 0.5;
                pattern *= sin(angle * 7.0 - ringT * 8.0) * 0.3 + 0.7;

                // Brighter near inner edge
                float brightness = (1.0 - ringT) * _Emission * pattern;

                // Smooth fade at edges
                float edgeFade = smoothstep(innerR, innerR + (outerR - innerR) * 0.15, dist)
                               * smoothstep(outerR, outerR - (outerR - innerR) * 0.15, dist);
                brightness *= edgeFade;

                // Color: white-hot inner, base color at outer
                float3 diskCol = lerp(float3(1, 0.95, 0.85), _BaseColor.rgb, ringT);

                // Additive blend on top of original
                fixed4 result = original;
                result.rgb += diskCol * brightness * 0.5;
                return result;
            }
            ENDCG
        }
    }
}
