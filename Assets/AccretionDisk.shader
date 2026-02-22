Shader "Hidden/AccretionDisk"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _InnerRadius ("Inner Radius", Float) = 0.5
        _OuterRadius ("Outer Radius", Float) = 2.0
        _BaseColor ("Base Color", Color) = (1, 0.6, 0.1, 1)
        _Emission ("Emission", Float) = 1.0
        _SpinAngle ("Spin Angle", Float) = 0.0
        _BHPosWS ("Black Hole World Pos", Vector) = (0,0,0,0)
        _CameraPosWS ("Camera Position", Vector) = (0,0,0,0)
        _CameraDirWS ("Camera Direction", Vector) = (0,0,1,0)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Blend One One // Additive blending for glow

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float _InnerRadius;
            float _OuterRadius;
            float4 _BaseColor;
            float _Emission;
            float _SpinAngle;
            float4 _BHPosWS;
            float4 _CameraPosWS;
            float4 _CameraDirWS;

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
                // Project BH world position to screen UV
                float4 clipPos = mul(UNITY_MATRIX_VP, float4(_BHPosWS.xyz, 1.0));

                // If behind camera, pass through
                if (clipPos.w < 0)
                {
                    return tex2D(_MainTex, i.uv);
                }

                float2 bhScreenUV = (clipPos.xy / clipPos.w) * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                    bhScreenUV.y = 1.0 - bhScreenUV.y;
                #endif

                // Compute pixel position relative to BH in screen space
                float aspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
                float2 diff = i.uv - bhScreenUV;
                diff.x *= aspect;
                float dist = length(diff);

                // Compute screen-space radii (approximate based on distance to camera)
                float distToCamera = length(_BHPosWS.xyz - _CameraPosWS.xyz);
                float screenScaleFactor = 1.0 / max(distToCamera, 0.01);
                float innerR = _InnerRadius * screenScaleFactor * 0.1;
                float outerR = _OuterRadius * screenScaleFactor * 0.1;

                // If outside the disk range, pass through the original image
                if (dist < innerR || dist > outerR)
                {
                    return tex2D(_MainTex, i.uv);
                }

                // Compute angle for spinning effect
                float angle = atan2(diff.y, diff.x);
                float spinRad = _SpinAngle * 3.14159265 / 180.0;
                angle += spinRad;

                // Procedural ring pattern — spiraling hot gas
                float ringT = (dist - innerR) / (outerR - innerR); // 0 at inner, 1 at outer
                float ringPattern = sin(angle * 3.0 + ringT * 12.0) * 0.5 + 0.5;
                ringPattern *= sin(angle * 7.0 - ringT * 8.0) * 0.3 + 0.7;

                // Brightness — hotter near the inner edge
                float brightness = (1.0 - ringT) * _Emission;
                brightness *= ringPattern;

                // Smooth fade at inner and outer edges
                float edgeFade = smoothstep(innerR, innerR + (outerR - innerR) * 0.15, dist)
                               * smoothstep(outerR, outerR - (outerR - innerR) * 0.15, dist);
                brightness *= edgeFade;

                // Color: white-hot near inner, orange/red at outer
                float3 diskColor = lerp(
                    float3(1.0, 0.95, 0.85),  // inner: white-yellow
                    _BaseColor.rgb,             // outer: orange
                    ringT
                );

                // Pass through the original image and ADD the disk glow
                fixed4 original = tex2D(_MainTex, i.uv);
                fixed4 result = original;
                result.rgb += diskColor * brightness * 0.5;

                return result;
            }
            ENDCG
        }
    }
    FallBack Off
}
