// NBodyMaterial.shader
Shader "Custom/NBodyMaterial"
{
    Properties
    {
        _MaxMass ("Max Mass", Float) = 10000000000000000000
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            // Structure for a body.
            struct Body
            {
                float3 position;
                float3 velocity;
                float mass;
                float isComet;
                float collided;
            };
            
            // The structured buffer (set by the manager script)
            StructuredBuffer<Body> bodies;
            float _MaxMass;
            
            // Our input: we only need the vertex ID.
            struct appdata
            {
                uint id : SV_VertexID;
            };
            
            // Data passed to the fragment shader.
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 color : COLOR;
                float collided : TEXCOORD0; // Pass the collision flag
            };

            float3 GetBodyColor(Body body)
            {
                // If it's a comet, keep it bright white or add a blueish hue
                if (body.isComet > 0.5)
                {
                    //return float3(1.0, 1.0, 1.0); // Bright white for visibility
                    float cometScalingFactor = 2.0;
                    return float3(0.8, 1, 1.5) * cometScalingFactor; // Light blue icy glow
                }
                if (body.mass > 10000000000000.0)
                {
                    return float3(1.0, 0.9, 0.2); // Bright yellow glow
                }
                if (body.mass > 10000000000000.0)
                {
                    return float3(1.5, 1.3, 0.0) * 2.0; // Bright yellow glow
                }

                // Normalize mass to a 0-1 scale (logarithmic scaling)
                float logMass = log10(body.mass);
                float normalizedMass = saturate((logMass - 10.0) / 1.0); // 10^10 maps to 0, 10^11 maps to 1

                // Base asteroid color: dark gray for small, yellow for large
                float3 baseColor = lerp(float3(0.15, 0.15, 0.15), float3(0.6, 0.6, 0.15), normalizedMass);

                // Boost brightness for large asteroids (fake glow effect)
                float brightnessFactor = (normalizedMass * 1); // Up to 100% brighter
                baseColor *= brightnessFactor;

                // Ensure colors remain within valid range (avoid overexposure)
                baseColor = saturate(baseColor);

                // Compute distance-based variation (inner vs. outer belt)
                float distanceFromCenter = length(body.position); // Approximate AU distance
                float beltFactor = saturate((distanceFromCenter - 2.0) / 3.0); // Inner belt (~2 AU) vs. outer belt (~5 AU+)

                // Inner belt: Rust-red, Outer belt: Bluish-gray
                float3 beltColor = lerp(float3(0.3, 0.2, 0.15), float3(0.5, 0.2, 0.2), beltFactor);

                // Blend asteroid color with belt color for natural variation
                return lerp(baseColor, beltColor, 0.4);
            }

            float3 GetAsteroidGlow(float mass)
            {
                float logMass = log10(mass);
                float normalizedMass = saturate((logMass - 10.0) / 1.0);

                // Larger asteroids emit more glow (scaled non-linearly)
                float glowStrength = pow(normalizedMass, 2.0); // Exponential glow effect

                // Yellow-orange glow for large asteroids
                return glowStrength * float3(1.0, 0.6, 0.2);
            }
            
            v2f vert(appdata v)
            {
                // Look up the body by its vertex ID.
                Body body = bodies[v.id];
                v2f o;
                // Convert simulation-space position to clip space.
                // (You may want to scale or translate body.position to fit your scene.)
                o.pos = UnityObjectToClipPos(float4(body.position, 1.0));
                // Color can be based on mass.
                // Determine color based on isComet
                o.color = GetBodyColor(body);
                o.collided = body.collided;  // Pass along the collision flag.

                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // Use clip() to discard the fragment when collided > -1.0.
                clip(-abs(i.collided + 1.0));

                float3 color = i.color;
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
} 