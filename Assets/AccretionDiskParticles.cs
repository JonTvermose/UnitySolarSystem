using UnityEngine;

/// <summary>
/// Generates a procedural accretion disk around the black hole using a particle system.
/// Attach this to the black hole prefab root. It creates a ring of orbiting hot particles
/// (orange/white) visible from all angles, with Keplerian differential rotation.
/// </summary>
public class AccretionDiskParticles : MonoBehaviour
{
    [Header("Disk Settings")]
    public float innerRadius = 0.02f;
    public float outerRadius = 0.06f;
    public int particleCount = 500;
    public float orbitSpeed = 60f; // degrees per second base speed
    public float diskThickness = 0.004f;

    [Header("Colors")]
    public Color innerColor = new Color(0.75f, 0.85f, 1f, 1f);     // blue-white (hot)
    public Color midColor   = new Color(1f, 0.9f, 0.6f, 0.9f);     // yellow-white
    public Color outerColor = new Color(1f, 0.3f, 0.02f, 0.5f);    // deep orange-red

    private ParticleSystem ps;
    private NBodySimulation _simulation;

    void Start()
    {
        var manager = GameObject.FindGameObjectWithTag("NBodySimulationManager");
        if (manager != null)
        {
            _simulation = manager.GetComponent<NBodySimulation>();
        }
        SetupParticleSystem();
    }

    void SetupParticleSystem()
    {
        ps = gameObject.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.maxParticles = particleCount;
        main.startLifetime = Mathf.Infinity;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = true;
        main.loop = false;
        main.startSize = 0.003f;

        // Disable the default emission — we'll emit all at once
        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.enabled = false;

        // Renderer setup
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.SetColor("_Color", midColor);

        // Emit particles in a ring
        EmitRing();
    }

    Color ColorForRadius(float normalizedDist)
    {
        // Three-stop gradient: inner (blue-white) → mid (yellow) → outer (red-orange)
        if (normalizedDist < 0.4f)
            return Color.Lerp(innerColor, midColor, normalizedDist / 0.4f);
        else
            return Color.Lerp(midColor, outerColor, (normalizedDist - 0.4f) / 0.6f);
    }

    void EmitRing()
    {
        var emitParams = new ParticleSystem.EmitParams();
        for (int i = 0; i < particleCount; i++)
        {
            float t = (float)i / particleCount;
            float angle = t * 360f * Mathf.Deg2Rad;

            // Bias radius distribution toward the inner edge (more particles where it's hotter)
            float rnd = Random.Range(0f, 1f);
            rnd = rnd * rnd; // square bias → more inner particles
            float radius = Mathf.Lerp(innerRadius, outerRadius, rnd);

            // Slight thickness with Gaussian-like distribution
            float yOffset = Random.Range(-1f, 1f);
            yOffset = yOffset * Mathf.Abs(yOffset) * diskThickness; // shaped toward center plane

            Vector3 pos = new Vector3(
                Mathf.Cos(angle) * radius,
                yOffset,
                Mathf.Sin(angle) * radius
            );

            emitParams.position = pos;
            emitParams.velocity = Vector3.zero;
            emitParams.startLifetime = float.MaxValue;

            float normalizedDist = (radius - innerRadius) / (outerRadius - innerRadius);
            emitParams.startColor = ColorForRadius(normalizedDist);
            // Inner particles are brighter/larger, outer ones dimmer
            emitParams.startSize = Mathf.Lerp(0.005f, 0.0015f, normalizedDist);

            ps.Emit(emitParams, 1);
        }
    }

    void Update()
    {
        if (ps == null) return;

        float speedMultiplier = 1f;
        if (_simulation != null)
        {
            speedMultiplier = _simulation.timeStep / 100f;
        }

        // Rotate particles around the local Y axis to simulate orbital motion
        var particles = new ParticleSystem.Particle[ps.particleCount];
        int count = ps.GetParticles(particles);

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = particles[i].position;
            float dist = new Vector2(pos.x, pos.z).magnitude;

            // Keplerian rotation: speed ∝ 1/√r
            float speed = orbitSpeed * speedMultiplier / Mathf.Max(Mathf.Sqrt(dist / innerRadius), 0.5f);
            float angleStep = speed * Time.deltaTime * Mathf.Deg2Rad;

            float cosA = Mathf.Cos(angleStep);
            float sinA = Mathf.Sin(angleStep);
            float newX = pos.x * cosA - pos.z * sinA;
            float newZ = pos.x * sinA + pos.z * cosA;

            particles[i].position = new Vector3(newX, pos.y, newZ);
        }

        ps.SetParticles(particles, count);
    }
}
