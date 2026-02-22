Shader "Hidden/BlackHoleLensing"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BHPosWS ("Black Hole World Pos", Vector) = (0,0,0,0)
        _Mass ("Mass", Float) = 1.0
        _Radius ("Event Horizon Radius", Float) = 0.5
        _LensingStrength ("Lensing Strength", Float) = 1.0
        _CameraPosWS ("Camera Position", Vector) = (0,0,0,0)
        _CameraDirWS ("Camera Direction", Vector) = (0,0,1,0)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float4 _BHPosWS;
            float _Mass;
            float _Radius;
            float _LensingStrength;
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
                // Project the black hole's world position into screen UV space
                float4 clipPos = mul(UNITY_MATRIX_VP, float4(_BHPosWS.xyz, 1.0));
                float2 bhScreenUV = (clipPos.xy / clipPos.w) * 0.5 + 0.5;
                // Flip Y if necessary (platform-dependent)
                #if UNITY_UV_STARTS_AT_TOP
                    bhScreenUV.y = 1.0 - bhScreenUV.y;
                #endif

                // If the black hole is behind the camera, skip lensing
                if (clipPos.w < 0)
                {
                    return tex2D(_MainTex, i.uv);
                }

                // Direction from pixel to BH in screen space (corrected for aspect ratio)
                float aspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
                float2 diff = bhScreenUV - i.uv;
                diff.x *= aspect;

                float dist = length(diff);
                float2 dir = normalize(diff);

                // Compute lensing offset: strength falls off as 1/r^2
                float schwarzschildScreenRadius = _Radius * 0.01; // approximate screen-space radius
                float lensOffset = _LensingStrength * schwarzschildScreenRadius * schwarzschildScreenRadius / (dist * dist + 0.0001);

                // Clamp offset to avoid extreme distortions
                lensOffset = min(lensOffset, 0.3);

                // Apply the offset: pull sampled UV toward the black hole
                float2 offsetUV = i.uv;
                offsetUV.x += dir.x * lensOffset / aspect;
                offsetUV.y += dir.y * lensOffset;

                // Clamp to valid UV range
                offsetUV = saturate(offsetUV);

                fixed4 col = tex2D(_MainTex, offsetUV);

                // Darken pixels inside the event horizon (in screen space)
                float eventHorizonScreenR = schwarzschildScreenRadius * 2.0;
                float darkenFactor = smoothstep(eventHorizonScreenR, eventHorizonScreenR * 0.3, dist);
                col.rgb *= (1.0 - darkenFactor);

                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}
