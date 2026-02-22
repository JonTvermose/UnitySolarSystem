Shader "Custom/BlackHoleLensing"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _LensingStrength ("Lensing Strength", Float) = 1
        _Mass ("Mass (normalized)", Float) = 1
        _Radius ("Radius", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
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
            float _LensingStrength;
            float _Mass;              // normalized 0-1+ (set from C#)
            float _Radius;

            float4 _BHScreenPos;      // xy = screen UV of BH, zw unused

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
                float2 bhUV = _BHScreenPos.xy;

                // Correct for aspect ratio so the distortion is circular
                float aspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
                float2 diff = bhUV - i.uv;
                diff.x *= aspect;

                float dist = length(diff);
                float2 dir = diff / max(dist, 0.00001);

                // Lensing offset: 1/r^2 fall-off, _Mass controls strength
                float effectRadius = _Radius * 0.015;
                float offset = _LensingStrength * _Mass * effectRadius * effectRadius
                             / (dist * dist + 0.0001);
                offset = min(offset, 0.25); // clamp to stop extreme warping

                // Pull sampled UV toward the black hole
                float2 distortedUV = i.uv;
                distortedUV.x += dir.x * offset / aspect;
                distortedUV.y += dir.y * offset;
                distortedUV = saturate(distortedUV);

                fixed4 col = tex2D(_MainTex, distortedUV);

                // Darken the event-horizon region
                float eventR = effectRadius * 1.5;
                float darken = smoothstep(eventR, eventR * 0.2, dist);
                col.rgb *= (1.0 - darken);

                return col;
            }
            ENDCG
        }
    }
}
