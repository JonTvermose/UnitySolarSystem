Shader "Custom/Comet"
{
    Properties
    {
        _MaxMass ("Max Mass", Float) = 10000000000000000000
        _CometSize ("Comet Size", Float) = 0.05
        _NonCometSize ("Non-Comet Size", Float) = 0.001
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Body
            {
                float3 position;
                float3 velocity;
                float mass;
                float isComet;
                float collided;
            };

            StructuredBuffer<Body> bodies;
            float _MaxMass;
            float _CometSize;
            float _NonCometSize;
            sampler2D _MainTex;

            struct appdata
            {
                uint id : SV_VertexID;
            };

            struct v2g
            {
                float4 pos : SV_POSITION;
                float3 color : COLOR;
                float isComet : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float collided : TEXCOORD2;  // Pass collision flag
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                float3 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2g vert(appdata v)
            {
                Body body = bodies[v.id];
                v2g o;
                o.pos = UnityObjectToClipPos(float4(body.position, 1.0));
                o.isComet = body.isComet;
                o.worldPos = body.position;
                o.collided = body.collided;  // Pass collision flag

                if (body.isComet > 0.5)
                {
                    o.color = float3(1, 1, 1);
                }
                else
                {
                    o.color = float3(1, 1, 0);
                }
                return o;
            }

            [maxvertexcount(4)]
            void geom(triangle v2g i[3], inout TriangleStream<g2f> o)
            {
                // If the body is marked as collided, exit early.
                if (i[0].collided > 0.5)
                {
                    return;
                }

                float size = i[0].isComet > 0.5 ? _CometSize : _NonCometSize;
                float3 center = i[0].worldPos;

                float3 up = float3(0, size, 0);
                float3 right = float3(size, 0, 0);

                // Calculate billboard up and right vectors in world space
                float3 cameraForward = normalize(UnityWorldSpaceViewDir(center)); // Direction from center to camera
                float3 billboardRight = normalize(cross(cameraForward, up)); // Right vector
                float3 billboardUp = normalize(cross(billboardRight, cameraForward)); // Up vector

                g2f vertex;
                vertex.color = i[0].color;


                vertex.uv = float2(0, 0);
                vertex.pos = UnityWorldToClipPos(center - billboardRight * 0.5 * size - billboardUp * 0.5 * size);
                o.Append(vertex);

                vertex.uv = float2(1, 0);
                vertex.pos = UnityWorldToClipPos(center + billboardRight * 0.5 * size - billboardUp * 0.5 * size);
                o.Append(vertex);

                vertex.uv = float2(1, 1);
                vertex.pos = UnityWorldToClipPos(center + billboardRight * 0.5 * size + billboardUp * 0.5 * size);
                o.Append(vertex);

                vertex.uv = float2(0, 1);
                vertex.pos = UnityWorldToClipPos(center - billboardRight * 0.5 * size + billboardUp * 0.5 * size);
                o.Append(vertex);

                o.RestartStrip();
            }

            fixed4 frag(g2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                col *= fixed4(i.color, 1.0);
                return col;
            }
            ENDCG
        }
    }
}